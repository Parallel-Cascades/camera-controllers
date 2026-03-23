using UnityEngine;

namespace CameraControllers.Runtime
{
    public class FlyCameraController : MonoBehaviour
    {
        [SerializeField] private float m_baseMovementSpeed = 1;
        [SerializeField] private float m_lookSensitivity = .3f;
        
        [SerializeField] private float m_speedUpMultiplier = 5;
        
        [SerializeField] private float m_maxPitchAngle = 89;
        [SerializeField] private float m_minPitchAngle = -89;
    
        [Header("General")]
        [SerializeField] private float m_movementSharpness = 10f;
        [SerializeField] private float m_rotationSharpness = 100f;

        private float m_movementSpeed;

        private Vector3 m_movementVelocity;
        private float m_pitchAngle;
        /// <summary>
        /// Yaw-only forward direction on the XZ plane.
        /// We rotate this with yaw input, then combine with pitch when building final camera rotation.
        /// </summary>
        private Vector3 m_yawForward = Vector3.forward;

        private Quaternion m_targetPitchRotation;
        
        private FlyCameraControls m_cameraControls;
        private Camera m_mainCamera;
        private Transform m_mainCameraTransform;

        private bool m_ignoreInput;
        
        private struct CameraInputs
        {
            public Vector2 LateralMovement;
            public float VerticalMovement;
            public Vector2 LookDelta;
            public bool SpeedUpModifierPressed;
            public bool ToggleMovement;
        }

        private CameraInputs GetCameraInputs()
        {
            CameraInputs cameraInputs = new CameraInputs
            {
                LateralMovement = m_cameraControls.Camera.LateralMovement.ReadValue<Vector2>(),
                VerticalMovement = m_cameraControls.Camera.VerticalMovement.ReadValue<float>(),
                LookDelta = m_cameraControls.Camera.LookDelta.ReadValue<Vector2>(),
                SpeedUpModifierPressed = m_cameraControls.Camera.SpeedUpModifier.IsPressed(),
                ToggleMovement = m_cameraControls.Camera.ToggleMovement.WasPressedThisFrame()
            };
            return cameraInputs;
        }
        
        private void OnEnable()
        {
            m_cameraControls.Camera.Enable();
        }

        private void OnDisable()
        {
            m_cameraControls.Camera.Disable();
        }

        private void Awake()
        {
            m_cameraControls = new FlyCameraControls();
            m_mainCamera = Camera.main;
            m_mainCameraTransform = m_mainCamera.transform;
            
            Vector3 eulerAngles = m_mainCameraTransform.rotation.eulerAngles;
            m_pitchAngle = eulerAngles.x;
            // Make sure we retain whatever rotation camera had on start.
            m_yawForward = Quaternion.Euler(0f, eulerAngles.y, 0f) * Vector3.forward;
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }



        private void Update()
        {
            var cameraInputs = GetCameraInputs();

            HandleMovementToggle(cameraInputs);
            
            if (m_ignoreInput)
            {
                return;
            }
            
            // Rotate before move
            ApplyYawInput(cameraInputs.LookDelta.x);
            ApplyPitchInput(-cameraInputs.LookDelta.y);
            UpdateCameraRotation();
            
            ApplySpeedUpModifier(cameraInputs.SpeedUpModifierPressed);
            CalculateCurrentMoveVelocity(new Vector3(cameraInputs.LateralMovement.x, cameraInputs.VerticalMovement, cameraInputs.LateralMovement.y));
            UpdateCameraPosition();
        }

        private void HandleMovementToggle(CameraInputs cameraInputs)
        {
            if (cameraInputs.ToggleMovement)
            {
                m_ignoreInput = !m_ignoreInput;
                Cursor.lockState = m_ignoreInput ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = m_ignoreInput;
            }
        }

        private void ApplyYawInput(float lookYawAmount)
        {
            float yawAngleChange = lookYawAmount * m_lookSensitivity;
            Quaternion yawRotation = Quaternion.Euler(Vector3.up *  yawAngleChange);

            m_yawForward = yawRotation * m_yawForward;
        }

        private void ApplyPitchInput(float lookPitchAmount)
        {
            m_pitchAngle += lookPitchAmount * m_lookSensitivity;
            m_pitchAngle = Mathf.Clamp(m_pitchAngle, m_minPitchAngle, m_maxPitchAngle);
            m_targetPitchRotation = Quaternion.Euler(Vector3.right * m_pitchAngle);
        }

        private void UpdateCameraRotation()
        {
            Quaternion targetRotation = Quaternion.LookRotation(m_yawForward, Vector3.up) * m_targetPitchRotation;

            m_mainCameraTransform.rotation = Quaternion.Slerp(m_mainCameraTransform.rotation, targetRotation,
                SharpnessUtility.GetSharpnessInterpolant(m_rotationSharpness, Time.deltaTime));
        }
        
        private void ApplySpeedUpModifier(bool speedUpModifierPressed)
        {
            m_movementSpeed = m_baseMovementSpeed;
            if (speedUpModifierPressed)
            {
                m_movementSpeed *= m_speedUpMultiplier;
            }
        }

        private void CalculateCurrentMoveVelocity(Vector3 moveInput)
        {
            Vector3 worldMoveInputs = m_mainCameraTransform.rotation * moveInput;

            m_movementVelocity = Vector3.Lerp(m_movementVelocity,
                worldMoveInputs * m_movementSpeed,
                SharpnessUtility.GetSharpnessInterpolant(m_movementSharpness, Time.deltaTime));
        }

        private void UpdateCameraPosition()
        {
            m_mainCameraTransform.position += m_movementVelocity * Time.deltaTime;
        }
    }
}