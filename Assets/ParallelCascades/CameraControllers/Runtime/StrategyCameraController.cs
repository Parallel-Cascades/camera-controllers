using System;
using ParallelCascades.CameraControllers.Runtime.CursorManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace ParallelCascades.CameraControllers.Runtime
{
    public class StrategyCameraController : MonoBehaviour
    {
        [Header("Pan Movement")]
        [SerializeField] private float m_baseMovementSpeed = .1f;
        [SerializeField] private float m_mouseDragSensitivity = 0.02f;
        
        [Header("Screen edge pan")]
        [SerializeField] private float m_screenEdgePanSpeed = .1f;

        [Header("Rotational Movement")]
        [SerializeField][Tooltip("Yaw is left/right rotation")] private float m_baseYawSpeed = 0.5f;
        
        [Header("Elevation Movement")]
        [SerializeField][Tooltip("Pitch is up/down tilt")] private float m_pitchSpeed = .5f;
        [SerializeField] private Vector2 m_pitchAngleMinMax = new Vector2(-25f, 35f);
        
        [Header("Zoom")]
        [SerializeField] private float m_zoomSpeed = 0.5f;
        [SerializeField] private Vector2 m_zoomMinMax = new Vector2(-3f, 6f);
        [SerializeField][Tooltip("As you zoom in the camera, the movement speed decreases to maintain smooth movement at close range.")]
        private float m_maxZoomSpeedMultiplier = 0.25f; // m_maxZoomSpeedMultiplier * m_baseMovementSpeed = speed at max zoom-in
        
        [Header("General")]
        [SerializeField] private float m_speedUpMultiplier = 3f;

        [Tooltip("How quickly the camera interpolates toward its target state. Higher values are snappier; lower values are smoother and more floaty.")]
        [SerializeField] private float m_sharpness = 10f;
        
        [Tooltip("The camera cannot move outside of these bounds.")]
        [SerializeField] private CameraMovementBounds m_movementBounds = new CameraMovementBounds(new Vector2(10f,10f), Vector3.zero);

        [Serializable]
        private struct CameraMovementBounds
        {
            public Vector2 Extents;
            public Vector3 Position;
            
            public CameraMovementBounds(Vector2 extents, Vector3 position)
            {
                Extents = extents;
                Position = position;
            }
        }

        [Header("Cursors")] 
        [SerializeField] private ActiveCursorStateSO m_activeCursorState;
        [SerializeField] private SingleCursorStateSO m_defaultSingleCursorState;
        [SerializeField] private SingleCursorStateSO m_mousePanSingleCursorState;
        [SerializeField] private SingleCursorStateSO m_mouseYawSingleCursorState;
        [SerializeField] private SingleCursorStateSO m_mousePitchSingleCursorState;
        [SerializeField] private DirectionalCursorStateSO m_mouseScreenEdgePanCursorState;
        
        private RTSCameraControls m_cameraControls;
        private Camera m_mainCamera;
        private Transform m_cameraTransform;
        
        // We modify speed based on a speed up key being pressed, and as we zoom in and out to maintain consistent
        // camera movement speed at very close and very far zoom.
        private float m_movementSpeed;
        private float m_rotationSpeed;
        
        // This camera controller works on the principle of calculating the desired 'target' position and rotation
        // of the camera each frame according to the user inputs, and then smoothly lerping the actual camera position
        // and rotation towards those targets each render frame. This allows for smooth movement that still feels responsive.
        private Vector3 m_targetPanPosition;
        private float m_targetYaw;
        private float m_targetPitch;
        
        private Vector3 m_defaultZoomCameraLocalPosition;
        private Vector3 m_defaultEulerAngles;
        private float m_targetZoom;

        private bool m_ignoreInput;

        private struct CameraInputs
        {
            public bool ResetRotation;
            public Vector2 PanMovement;
            public float YawMovement;
            public bool SpeedUpModifierPressed;
            public Vector2 MouseDelta;
            public Vector2 MousePosition;
            public bool MousePan;
            public bool MouseYaw;
            public bool MousePitch;
            /// <summary>
            /// Scroll wheel zoom is stored in y component
            /// </summary>
            public Vector2 Zoom;
        }
        
        private CameraInputs GetCameraInputs()
        {
            Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            
#if UNITY_EDITOR
            mousePosition = ClampAndWarpCursorToScreenBounds(mousePosition);
#endif
            var cameraInputs = new CameraInputs
            {
                ResetRotation = m_cameraControls.Camera.ResetRotationZoom.WasPressedThisFrame(),
                PanMovement = m_cameraControls.Camera.PanMovement.ReadValue<Vector2>(),
                YawMovement = m_cameraControls.Camera.YawMovement.ReadValue<float>(),
                SpeedUpModifierPressed = m_cameraControls.Camera.SpeedUpModifier.IsPressed(),
                Zoom = m_cameraControls.Camera.Zoom.ReadValue<Vector2>(), // note that in the new Input system scroll is Vector2, y is the actual scroll value for mice
                MousePan = m_cameraControls.Camera.MousePan.IsPressed(),
                MouseYaw = m_cameraControls.Camera.MouseYaw.IsPressed(),
                MousePitch = m_cameraControls.Camera.MousePitch.IsPressed(),
                MouseDelta = m_cameraControls.Camera.MouseDelta.ReadValue<Vector2>(),
                MousePosition = mousePosition
            };
            return cameraInputs;
        }
        
        private void Awake()
        {
            m_cameraControls = new RTSCameraControls();
            m_mainCamera = Camera.main;
            m_cameraTransform = m_mainCamera.transform;
            
            SetDefaultTargets();
            
#if UNITY_EDITOR
            m_focused = true;
            Application.focusChanged += OnApplicationFocusChanged;
#endif
        }

        private void SetDefaultTargets()
        {
            m_targetPanPosition = transform.position;
            m_defaultEulerAngles = transform.eulerAngles;
            m_targetYaw = m_defaultEulerAngles.y;
            m_targetPitch = m_defaultEulerAngles.x;
            m_defaultZoomCameraLocalPosition = m_cameraTransform.localPosition;
            m_targetZoom = 0;
        }

        private void OnEnable()
        {
            m_cameraControls.Camera.Enable();
        }

        private void OnDisable()
        {
            m_cameraControls.Camera.Disable();
        }

        private void OnDestroy()
        {
            m_cameraControls?.Dispose();
        }

        private void Update()
        {
            if (m_ignoreInput)
            {
                return;
            }
            
#if UNITY_EDITOR
            if (EditorNotFocused()) return;

            CacheGameViewProperties();
#endif
            
            CameraInputs cameraInputs = GetCameraInputs();
            
            if(cameraInputs.ResetRotation)
            {
                ResetCameraRotationAndZoom();
            }
            
            ApplySpeedUpModifier(cameraInputs.SpeedUpModifierPressed);
            
            // Mouse drags are mutually exclusive and lock out keyboard movement - this is a personal preference,
            // it just seems weird to be able to hold down all mouse buttons and move the camera in multiple axes
            if (cameraInputs.MousePan)
            {
                m_activeCursorState.SetCursorState(m_mousePanSingleCursorState);
                ApplyMousePan(cameraInputs.MouseDelta);
            }
            else if (cameraInputs.MouseYaw)
            {
                m_activeCursorState.SetCursorState(m_mouseYawSingleCursorState);
                ApplyMouseYaw(cameraInputs.MouseDelta.x);
            }
            else if (cameraInputs.MousePitch)
            {
                m_activeCursorState.SetCursorState(m_mousePitchSingleCursorState);
                ApplyMousePitch(cameraInputs.MouseDelta.y);
            }
            else if (TryApplyMouseScreenEdgePan(cameraInputs.MousePosition, out DirectionalCursorStateSO.EdgeScrollCursorDirection edgeScrollDirection))
            {
                m_mouseScreenEdgePanCursorState.SetDirection(edgeScrollDirection);
                m_activeCursorState.SetCursorState(m_mouseScreenEdgePanCursorState);
            }
            else
            {
                m_activeCursorState.SetCursorState(m_defaultSingleCursorState);
                ApplyKeyboardPan(cameraInputs.PanMovement);
                ApplyKeyboardYaw(cameraInputs.YawMovement);
            }
            
            ApplyZoom(cameraInputs.Zoom.y);
            
            ClampPanPositionToCameraBounds();
            
            UpdatePanPosition();
            UpdateRotation();
            UpdateZoomPosition();
        }



        // A speed up 'modifier' is the key you hold down to speed up movement
        // The speed up 'multiplier' is how much the speed increases when the modifier is held down
        private void ApplySpeedUpModifier(bool speedUpModifierPressed)
        {
            float baseSpeed = speedUpModifierPressed 
                ? m_baseMovementSpeed * m_speedUpMultiplier 
                : m_baseMovementSpeed;
            
            float zoomT = Mathf.InverseLerp(m_zoomMinMax.y, m_zoomMinMax.x, m_targetZoom);
            float zoomSpeedMultiplier = Mathf.Lerp(m_maxZoomSpeedMultiplier, 1f, zoomT);

            m_movementSpeed = baseSpeed * zoomSpeedMultiplier;
            m_rotationSpeed = speedUpModifierPressed ? m_baseYawSpeed * m_speedUpMultiplier : m_baseYawSpeed;
        }
        
        private void ResetCameraRotationAndZoom()
        {
            m_targetYaw = m_defaultEulerAngles.y;
            m_targetPitch = m_defaultEulerAngles.x;
            m_targetZoom = 0;
        }
        
        private void ApplyMousePan(Vector2 mouseDelta)
        {
            if(mouseDelta.sqrMagnitude < 0.01f) return; // don't apply very small mouse movements, helps with mouse jitter when trying to click but not move the camera
            
            m_targetPanPosition -= mouseDelta.x * m_mouseDragSensitivity * GetCameraScreenRight();
            m_targetPanPosition -= mouseDelta.y * m_mouseDragSensitivity * GetCameraScreenForward();
        }

        private void ApplyMouseYaw(float dragAmount)
        {
            if (Mathf.Abs(dragAmount) > 0.1)
            {
                m_targetYaw += dragAmount * m_rotationSpeed;
            }
        }

        private void ApplyMousePitch(float dragAmount)
        {
            if (Mathf.Abs(dragAmount) > 0.1)
            {
                m_targetPitch -= dragAmount * m_pitchSpeed;
                m_targetPitch = Mathf.Clamp(m_targetPitch, m_pitchAngleMinMax.x, m_pitchAngleMinMax.y);
            }
        }

        private bool TryApplyMouseScreenEdgePan(Vector2 mousePosition, out DirectionalCursorStateSO.EdgeScrollCursorDirection edgeScrollDirection)
        {
            bool isPanning = false;
            
            Vector3 screenEdgePanDirection = Vector3.zero;
            edgeScrollDirection = 0;
            
#if UNITY_EDITOR
            int hEdgeThreshold = m_editorHorizontalEdgeThreshold;
            int vEdgeThreshold = m_editorVerticalEdgeThreshold;
            int bottomEdgeThreshold = m_editorVerticalEdgeThreshold + 1;
#else
            const int hEdgeThreshold = 0;
            const int vEdgeThreshold = 0;
            const int bottomEdgeThreshold = 0;
#endif
            
            if (mousePosition.x <= hEdgeThreshold)
            {
                screenEdgePanDirection -= GetCameraScreenRight() * m_screenEdgePanSpeed;
                isPanning = true;
                edgeScrollDirection |= DirectionalCursorStateSO.EdgeScrollCursorDirection.W;
            }
            else if (mousePosition.x >= Screen.width - 1 - hEdgeThreshold)
            {
                screenEdgePanDirection += GetCameraScreenRight() * m_screenEdgePanSpeed;
                isPanning = true;
                edgeScrollDirection |= DirectionalCursorStateSO.EdgeScrollCursorDirection.E;
            }

            if (mousePosition.y <= bottomEdgeThreshold)
            {
                screenEdgePanDirection -= GetCameraScreenForward() * m_screenEdgePanSpeed;
                isPanning = true;
                edgeScrollDirection |= DirectionalCursorStateSO.EdgeScrollCursorDirection.S;
            }
            else if (mousePosition.y >= Screen.height - 1 - vEdgeThreshold)
            {
                screenEdgePanDirection += GetCameraScreenForward() * m_screenEdgePanSpeed;
                isPanning = true;
                edgeScrollDirection |= DirectionalCursorStateSO.EdgeScrollCursorDirection.N;
            }
            
            m_targetPanPosition += screenEdgePanDirection;
            return isPanning;
        }
        
        private void ApplyKeyboardPan(Vector2 panMovementInput)
        {
            Vector3 cameraMovement = panMovementInput.x * GetCameraScreenRight() + panMovementInput.y * GetCameraScreenForward();
            cameraMovement = cameraMovement.normalized;
            if (cameraMovement.sqrMagnitude > 0.1f)
            {
                m_targetPanPosition += cameraMovement * m_movementSpeed;
            }
        }

        private Vector3 GetCameraScreenForward() => new(transform.forward.x, 0f, transform.forward.z);

        private Vector3 GetCameraScreenRight() => new(transform.right.x, 0f, transform.right.z);
        
        private void ApplyKeyboardYaw(float rotationalMovementInput)
        {
            var yawMovement = rotationalMovementInput;
            if (Mathf.Abs(yawMovement) > 0.1)
            {
                m_targetYaw += yawMovement * m_rotationSpeed;
            }
        }
        
        private void ApplyZoom(float zoomInput)
        {
            if (Mathf.Abs(zoomInput) > 0.1f)
            {
                m_targetZoom += zoomInput * m_zoomSpeed;
                m_targetZoom = Mathf.Clamp(m_targetZoom, m_zoomMinMax.x, m_zoomMinMax.y);
            }
        }

        private void ClampPanPositionToCameraBounds()
        {
            m_targetPanPosition.x = Mathf.Clamp(m_targetPanPosition.x,
                m_movementBounds.Position.x - m_movementBounds.Extents.x,
                m_movementBounds.Position.x + m_movementBounds.Extents.x);
            m_targetPanPosition.z = Mathf.Clamp(m_targetPanPosition.z,
                m_movementBounds.Position.z - m_movementBounds.Extents.y,
                m_movementBounds.Position.z + m_movementBounds.Extents.y);
        }
        
        private void UpdatePanPosition()
        {
            transform.position = Vector3.Lerp(transform.position, m_targetPanPosition, SharpnessUtility.GetSharpnessInterpolant(m_sharpness, Time.deltaTime));
        }
        
        private void UpdateRotation()
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(m_targetPitch, m_targetYaw, 0f), SharpnessUtility.GetSharpnessInterpolant(m_sharpness, Time.deltaTime));
        }

        private void UpdateZoomPosition()
        {
            Vector3 zoomDirection = m_cameraTransform.localRotation * Vector3.forward;

            Vector3 targetZoomPosition = m_defaultZoomCameraLocalPosition + zoomDirection * m_targetZoom;
            m_cameraTransform.localPosition = Vector3.Lerp(m_cameraTransform.localPosition, targetZoomPosition, SharpnessUtility.GetSharpnessInterpolant(m_sharpness, Time.deltaTime));
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;

            // Extents are half-size in X/Z, so multiply by 2 to get full wire cube size.
            const float gizmoThickness = 0.1f;
            Vector3 size = new Vector3(m_movementBounds.Extents.x * 2f, gizmoThickness, m_movementBounds.Extents.y * 2f);
            Gizmos.DrawWireCube(m_movementBounds.Position, size);
        }
        
// All editor code is related to constraining the cursor within the game view so that edge scrolling works the same
// way it does in builds
#if UNITY_EDITOR
        private bool m_focused;

        private int m_editorVerticalEdgeThreshold;
        private int m_editorHorizontalEdgeThreshold;
        private int m_lastScreenHeight = -1;
        private int m_lastScreenWidth = -1;
        private float m_lastGameViewHeight = -1;
        private float m_lastGameViewWidth = -1;
        private UnityEditor.EditorWindow m_gameView;
        
        private void OnApplicationFocusChanged(bool focused)
        {
            m_focused = focused;
        }
        
        /// <summary>
        /// This is only useful when the cursor is locked to the screen and we're trying to move it to the top or right edge of the screen
        /// </summary>
        /// <param name="cursorPos"></param>
        /// <returns></returns>
        private Vector2 ClampAndWarpCursorToScreenBounds(Vector2 cursorPos)
        {
            float clampedX = Mathf.Clamp(cursorPos.x, 0f, Screen.width);
            float clampedY = Mathf.Clamp(cursorPos.y, 0f, Screen.height);
            if (!Mathf.Approximately(clampedX, cursorPos.x) || !Mathf.Approximately(clampedY, cursorPos.y))
            {
                Vector2 clampedPos = new Vector2(clampedX, clampedY);
                Mouse.current.WarpCursorPosition(clampedPos);
                return clampedPos;
            }
            return cursorPos;
        }
        
        private void CacheGameViewProperties()
        {
            // Recalculate if the resolution dropdown changed (Screen dimensions change when it does),
            // or if the game window itself was resized.
            if (m_gameView == null
                || Screen.height != m_lastScreenHeight
                || Screen.width != m_lastScreenWidth
                || !Mathf.Approximately(m_gameView.position.height, m_lastGameViewHeight)
                || !Mathf.Approximately(m_gameView.position.width, m_lastGameViewWidth))
            {
                CalculateEditorEdgeThresholds();
                m_lastScreenHeight = Screen.height;
                m_lastScreenWidth = Screen.width;
                m_lastGameViewHeight =  m_gameView.position.height;
                m_lastGameViewWidth = m_gameView.position.width;
            }
        }
        
        /// <summary>
        /// Calculates the edge threshold in screen-space pixels, accounting for the scale
        /// factor between the Game View's logical resolution (Screen.width/height) and the
        /// actual pixel dimensions of the Game View window inside the Editor.
        /// 
        /// <para>
        /// The game content always fills either the full window content height (pillarboxing:
        /// grey bars on left/right) or width (letterboxing: grey bars on top/bottom).
        /// In both cases the scale is driven by the constraining dimension, so
        /// both thresholds resolve to the same value.
        /// </para>
        /// </summary>
        private void CalculateEditorEdgeThresholds()
        {
            const int fallback = 1;

            // Use reflection to fetch the game view EditorWindow, which is internal
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null)
            {
                m_editorVerticalEdgeThreshold = m_editorHorizontalEdgeThreshold = fallback; 
                return;
            }

            var windows = Resources.FindObjectsOfTypeAll(gameViewType);
            if (windows.Length == 0)
            {
                m_editorVerticalEdgeThreshold = m_editorHorizontalEdgeThreshold = fallback; 
                return;
            }
            
            if (windows[0] is not UnityEditor.EditorWindow gameView)
            {
                m_editorVerticalEdgeThreshold = m_editorHorizontalEdgeThreshold = fallback; 
                return;
            }
            m_gameView = gameView;

            var targetSizeProp = gameViewType.GetProperty(
                "targetSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (targetSizeProp == null)
            {
                m_editorVerticalEdgeThreshold = m_editorHorizontalEdgeThreshold = fallback; 
                return;
            }

            var targetSize = (Vector2)targetSizeProp.GetValue(gameView);
            if (targetSize.y <= 0 || targetSize.x <= 0)
            {
                m_editorVerticalEdgeThreshold = m_editorHorizontalEdgeThreshold = fallback; 
                return;
            }

            float windowContentWidth = gameView.position.width;
            // TODO: Don't want to hardcode the 23 px toolbar
            float windowContentHeight = gameView.position.height - 23; // subtract 23px toolbar
            float windowContentAspect = windowContentWidth / windowContentHeight;
            float gameAspect = (float)Screen.width / Screen.height;

            // The scale represents how many logical screen pixels correspond to each editor-unit
            // of game content. We always use the constraining dimension so grey bar area is excluded.
            float scale;
            if (windowContentAspect > gameAspect)
            {
                // Window wider than game -> pillarboxing (grey bars left/right).
                // Game content fills the full height; scale is driven by height.
                scale = targetSize.y / windowContentHeight;
            }
            else
            {
                // Window narrower than or equal to game -> letterboxing or exact match (grey bars top/bottom, or none).
                // Game content fills the full width; scale is driven by width.
                scale = targetSize.x / windowContentWidth;
            }

            m_editorVerticalEdgeThreshold = Mathf.Max(fallback, Mathf.CeilToInt(scale));
            m_editorHorizontalEdgeThreshold = Mathf.Max(fallback, Mathf.CeilToInt(scale));
        }

        private bool EditorNotFocused()
        {
            if(Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                m_focused = !m_focused;
            }

            if (!m_focused)
            {
                return true;
            }

            return false;
        }
#endif
    }
}

