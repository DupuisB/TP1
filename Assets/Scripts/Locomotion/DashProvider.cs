using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using Unity.XR.CoreUtils;

namespace LOG8704.Locomotion
{
    /// <summary>
    /// Custom LocomotionProvider for rapid directional translation (Dashing).
    /// Inherits from LocomotionProvider and implements ITunnelingVignetteProvider for VR comfort.
    /// </summary>
    [AddComponentMenu("LOG8704/Locomotion/Dash Provider")]
    public class DashProvider : LocomotionProvider, ITunnelingVignetteProvider
    {
        [Header("Dash Settings")]
        [Tooltip("Duration of the dash in seconds (typically 0.2s)")]
        [SerializeField] private float m_DashDuration = 0.20f;

        [Tooltip("Cooldown period between consecutive dashes in seconds")]
        [SerializeField] private float m_Cooldown = 0.5f;

        [Tooltip("Easing curve for the dash translation")]
        [SerializeField] private AnimationCurve m_DashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Collision & Safety")]
        [Tooltip("Layers to check for collision during dash sweep")]
        [SerializeField] private LayerMask m_ObstacleLayers = ~0;

        [Tooltip("Radius of the sphere cast used for obstacle detection")]
        [SerializeField] private float m_PlayerRadius = 0.3f;

        [Tooltip("Safety margin to maintain between player and hit obstacle")]
        [SerializeField] private float m_SkinWidth = 0.1f;

        [Header("Comfort & Feedback")]
        [Tooltip("Tunneling vignette controller to trigger comfort occlusion during dash")]
        [SerializeField] private TunnelingVignetteController m_VignetteController;

        [Tooltip("Custom vignette parameters for dash motion")]
        [SerializeField] private VignetteParameters m_VignetteParameters = new VignetteParameters
        {
            apertureSize = 0.5f,
            featheringEffect = 0.2f,
            easeInTime = 0.05f,
            easeOutTime = 0.15f
        };

        private bool m_IsDashing;
        private float m_LastDashTime;
        private Coroutine m_DashCoroutine;

        public VignetteParameters vignetteParameters => m_VignetteParameters;
        public bool isDashing => m_IsDashing;
        public float dashDuration { get => m_DashDuration; set => m_DashDuration = value; }
        public float cooldown { get => m_Cooldown; set => m_Cooldown = value; }
        public TunnelingVignetteController vignetteController { get => m_VignetteController; set => m_VignetteController = value; }

        protected override void Awake()
        {
            base.Awake();
            if (m_VignetteController == null)
            {
                m_VignetteController = FindFirstObjectByType<TunnelingVignetteController>();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (m_DashCoroutine != null)
            {
                StopCoroutine(m_DashCoroutine);
                m_DashCoroutine = null;
                m_IsDashing = false;
                if (m_VignetteController != null)
                    m_VignetteController.EndTunnelingVignette(this);
                TryEndLocomotion();
            }
        }

        /// <summary>
        /// Dash directly to a target destination (e.g. from teleport ray) over 0.2s.
        /// </summary>
        public bool DashTo(Vector3 targetPosition, Quaternion? targetRotation = null, System.Action onComplete = null)
        {
            if (m_IsDashing)
            {
                if (m_DashCoroutine != null)
                    StopCoroutine(m_DashCoroutine);
            }

            XROrigin origin = GetXROrigin();
            if (origin == null || origin.Origin == null)
                return false;

            if (!TryStartLocomotionImmediately())
                return false;

            // Calculate destination for origin taking into account user's head/body ground offset
            Vector3 originPos = origin.Origin.transform.position;
            Vector3 targetOriginPos = targetPosition;
            if (origin.Camera != null)
            {
                Vector3 bodyGroundPos = new Vector3(origin.Camera.transform.position.x, originPos.y, origin.Camera.transform.position.z);
                targetOriginPos = targetPosition + originPos - bodyGroundPos;
            }

            // Obstacle safety sweep along dash trajectory
            Vector3 dashVec = targetOriginPos - originPos;
            float dashDist = dashVec.magnitude;
            if (dashDist > 0.05f)
            {
                Vector3 dashDir = dashVec / dashDist;
                Vector3 sweepStart = originPos + Vector3.up * 0.5f;
                if (Physics.SphereCast(sweepStart, m_PlayerRadius, dashDir, out RaycastHit hit, dashDist, m_ObstacleLayers, QueryTriggerInteraction.Ignore))
                {
                    float safeDist = Mathf.Max(0f, hit.distance - m_SkinWidth);
                    if (safeDist <= 0.1f)
                    {
                        TryEndLocomotion();
                        return false;
                    }
                    targetOriginPos = originPos + dashDir * safeDist;
                }
            }

            m_DashCoroutine = StartCoroutine(PerformDash(origin, targetOriginPos, targetRotation, onComplete));
            return true;
        }

        private XROrigin GetXROrigin()
        {
            XROrigin origin = mediator != null ? mediator.xrOrigin : null;
            if (origin == null)
            {
                origin = GetComponentInParent<XROrigin>();
                if (origin == null)
                    origin = FindFirstObjectByType<XROrigin>();
            }
            return origin;
        }

        private IEnumerator PerformDash(XROrigin origin, Vector3 targetPosition, Quaternion? targetRotation, System.Action onComplete)
        {
            m_IsDashing = true;

            // Respect global vignette toggle from TP1ComfortManager
            bool shouldShowVignette = m_VignetteController != null;
            if (TP1ComfortManager.Instance != null && !TP1ComfortManager.Instance.isVignetteActive)
            {
                shouldShowVignette = false;
            }

            if (shouldShowVignette && m_VignetteController != null)
                m_VignetteController.BeginTunnelingVignette(this);

            var originTransform = origin.Origin.transform;
            Vector3 originStart = originTransform.position;
            Quaternion rotStart = originTransform.rotation;

            // Retrieve CharacterController; keep it ENABLED throughout dash to prevent PhysX position snapback
            CharacterController cc = origin.Origin != null ? origin.Origin.GetComponent<CharacterController>() : null;
            if (cc == null && origin.GetComponent<CharacterController>() != null)
                cc = origin.GetComponent<CharacterController>();

            float elapsed = 0f;

            while (elapsed < m_DashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / m_DashDuration);
                float curvedT = m_DashCurve.Evaluate(t);
                Vector3 desiredPos = Vector3.Lerp(originStart, targetPosition, curvedT);
                Vector3 frameStep = desiredPos - originTransform.position;

                if (cc != null && cc.enabled)
                {
                    cc.Move(frameStep);
                }
                else
                {
                    originTransform.position += frameStep;
                }

                if (targetRotation.HasValue)
                {
                    originTransform.rotation = Quaternion.Slerp(rotStart, targetRotation.Value, curvedT);
                }

                yield return null;
            }

            // Ensure final step to destination respecting obstacles
            Vector3 finalStep = targetPosition - originTransform.position;
            if (cc != null && cc.enabled)
            {
                cc.Move(finalStep);
            }
            else
            {
                originTransform.position = targetPosition;
            }

            if (targetRotation.HasValue)
            {
                originTransform.rotation = targetRotation.Value;
            }

            // Synchronize active colliders with PhysX
            Physics.SyncTransforms();

            m_LastDashTime = Time.time;
            m_IsDashing = false;

            if (m_VignetteController != null)
                m_VignetteController.EndTunnelingVignette(this);

            TryEndLocomotion();
            m_DashCoroutine = null;

            onComplete?.Invoke();
        }
    }
}
