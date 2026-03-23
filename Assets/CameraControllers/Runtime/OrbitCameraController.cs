using UnityEngine;

namespace CameraControllers.Runtime
{
    public class OrbitCameraController : MonoBehaviour
    {
        [Header("Orbit Target")]
        [SerializeField] private Transform m_orbitTarget;
        
        [Header("Rotation Settings")]
        [SerializeField] private float m_maxVAngle = 89f;
        [SerializeField] private float m_minVAngle = -89f;
        [SerializeField] private float m_rotationSpeed = 0.5f;
        [SerializeField] private float m_rotationSharpness = 10f;
        
        [Header("Zoom Settings")]
        [SerializeField] private float m_minZoom = 1f;
        [SerializeField] private float m_maxZoom = 20f;
        [SerializeField] private float m_zoomSpeed = 1f;
        [SerializeField] private float m_zoomSharpness = 10f;
        [SerializeField] private float m_defaultZoom = 1.5f;
        
        private float m_pitchAngle;
        private float m_yawAngle;
        private float m_currentZoom;
        private float m_targetZoom;
        
        private Vector3 m_defaultEulerAngles;
        
        private Transform m_cameraTransform;
        
        private OrbitCameraControls  m_cameraControls;
        
        private struct CameraInputs
        {
            public bool ResetRotation;
            public bool MouseDrag;
            public Vector2 MouseDelta;
            public float Zoom;
        }

        private CameraInputs GetCameraInputs()
        {
            var cameraInputs = new CameraInputs
            {
                ResetRotation = m_cameraControls.Camera.ResetRotationZoom.WasPressedThisFrame(),
                MouseDrag = m_cameraControls.Camera.RotateCamera.IsPressed(),
                MouseDelta = m_cameraControls.Camera.MouseDelta.ReadValue<Vector2>(),
                Zoom = m_cameraControls.Camera.Zoom.ReadValue<Vector2>().y // note that in the new Input system scroll is Vector2, y is the actual scroll value
            };
        
            return cameraInputs;
        }
        
        private void Awake()
        {           
            m_cameraControls = new OrbitCameraControls();
            m_cameraTransform = Camera.main.transform;

            SetDefaultTargets();
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
        
        /// <summary>
        /// Seeds pitch/yaw from the camera's current world rotation and resets zoom to
        /// <see cref="m_defaultZoom"/>. Call this before enabling the component whenever the
        /// orbit target has changed or on first use, so the subsequent transition lands
        /// exactly where the orbit camera expects to start.
        /// </summary>
        public void SetDefaultTargets()
        {
            m_defaultEulerAngles = m_cameraTransform.rotation.eulerAngles;

            m_pitchAngle = NormalizePitchAngle(m_defaultEulerAngles.x);

            m_yawAngle = m_defaultEulerAngles.y;

            m_currentZoom = m_defaultZoom;
            m_targetZoom  = m_defaultZoom;
        }
        
        private static float NormalizePitchAngle(float pitchAngle)
        {
            return pitchAngle > 180f ? pitchAngle - 360f : pitchAngle;
        }

        private void Update()
        {
            if (m_orbitTarget == null)
            {
                return;
            }
            
            CameraInputs cameraInputs = GetCameraInputs();

            ApplyResetIfRequested(cameraInputs.ResetRotation);
            
            if (cameraInputs.MouseDrag)
            {
                ApplyOrbitRotation(cameraInputs.MouseDelta);
            }
            
            ApplyZoom(cameraInputs.Zoom);
            ClampPitch();
            ClampZoom();
            
            UpdateZoom();
            UpdateRotation();
            UpdatePosition();
        }

        private void ApplyResetIfRequested(bool resetPressed)
        {
            if (!resetPressed)
            {
                return;
            }

            m_yawAngle = m_defaultEulerAngles.y;
            m_pitchAngle = NormalizePitchAngle(m_defaultEulerAngles.x);
            m_targetZoom = m_defaultZoom;
        }

        private void ApplyOrbitRotation(Vector2 mouseDelta)
        {
            m_yawAngle += mouseDelta.x * m_rotationSpeed;

            m_pitchAngle += -mouseDelta.y * m_rotationSpeed;
        }

        private void ApplyZoom(float zoomInput)
        {
            m_targetZoom -= zoomInput * m_zoomSpeed;
        }

        private void ClampPitch()
        {
            m_pitchAngle = Mathf.Clamp(m_pitchAngle, m_minVAngle, m_maxVAngle);
        }

        private void ClampZoom()
        {
            m_targetZoom = Mathf.Clamp(m_targetZoom, m_minZoom, m_maxZoom);
        }

        private void UpdateZoom()
        {
            m_currentZoom = Mathf.Lerp(m_currentZoom, m_targetZoom,
                SharpnessUtility.GetSharpnessInterpolant(m_zoomSharpness, Time.deltaTime));
        }

        public bool UseSharpnessInterpolant;
        
        private void UpdateRotation()
        {
            Quaternion targetRotation = Quaternion.Euler(m_pitchAngle, m_yawAngle, 0f);
            
            if (Time.deltaTime > 0f)
            {
                m_cameraTransform.rotation = Quaternion.Slerp(m_cameraTransform.rotation, targetRotation, UseSharpnessInterpolant ?
                    SharpnessUtility.GetSharpnessInterpolant(m_rotationSharpness, Time.deltaTime) : m_rotationSharpness * Time.deltaTime);
            }
            else
            {
                m_cameraTransform.rotation = targetRotation;
            }
        }

        private void UpdatePosition()
        {
            Vector3 offset = m_cameraTransform.rotation * new Vector3(0f, 0f, -m_currentZoom);
            m_cameraTransform.position = m_orbitTarget.position + offset;
        }



    }
}