using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

namespace LOG8704.Locomotion
{
    /// <summary>
    /// Custom LocomotionProvider for rapid directional translation (Dashing).
    /// Inherits from LocomotionProvider and implements ITunnelingVignetteProvider for VR comfort.
    /// Supports obstacle collision stopping, elevation clearance arcs for platforms, and haptic feedback.
    /// </summary>
    [AddComponentMenu("LOG8704/Locomotion/Dash Provider")]
    public class DashProvider : LocomotionProvider, ITunnelingVignetteProvider
    {
        [Header("Dash Settings")]
        [Tooltip("Duration of the dash in seconds (typically 0.20s)")]
        [SerializeField] private float m_DashDuration = 0.20f;

        [Tooltip("Cooldown period between consecutive dashes in seconds")]
        [SerializeField] private float m_Cooldown = 0.25f;

        [Tooltip("Easing curve for the dash translation")]
        [SerializeField] private AnimationCurve m_DashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Collision & Clearance")]
        [Tooltip("Layers to check for collision during obstacle sweep")]
        [SerializeField] private LayerMask m_ObstacleLayers = ~0;

        [Tooltip("Radius of the player body capsule used for obstacle detection")]
        [SerializeField] private float m_PlayerRadius = 0.30f;

        [Tooltip("Assumed human player height used for clearance sweep if camera is unmeasured")]
        [SerializeField] private float m_PlayerHeight = 1.8f;

        [Tooltip("Safety margin to maintain between player and hit obstacle")]
        [SerializeField] private float m_SkinWidth = 0.12f;

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

        [Tooltip("Enable controller haptic pulses at dash launch and impact/landing")]
        [SerializeField] private bool m_EnableHaptics = true;

        private bool m_IsDashing;
        private float m_LastDashTime;
        private Coroutine m_DashCoroutine;

        public VignetteParameters vignetteParameters => m_VignetteParameters;
        public bool isDashing => m_IsDashing;
        public float dashDuration { get => m_DashDuration; set => m_DashDuration = value; }
        public float cooldown { get => m_Cooldown; set => m_Cooldown = value; }
        public TunnelingVignetteController vignetteController { get => m_VignetteController; set => m_VignetteController = value; }
        public bool enableHaptics { get => m_EnableHaptics; set => m_EnableHaptics = value; }

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
        /// Dash directly to a target destination (e.g. from teleport ray) over 0.20s.
        /// Evaluates obstacle collisions along the path, smoothly navigates platforms and height changes,
        /// and provides haptic and visual comfort feedback.
        /// </summary>
        public bool DashTo(Vector3 targetPosition, Quaternion? targetRotation = null, System.Action onComplete = null)
        {
            if (m_IsDashing)
                return false;

            if (Time.time < m_LastDashTime + m_Cooldown)
                return false;

            XROrigin origin = GetXROrigin();
            if (origin == null || origin.Origin == null)
                return false;

            if (!TryStartLocomotionImmediately())
                return false;

            // 1. Calculate destination origin position compensating for room-scale head offset
            Vector3 originPos = origin.Origin.transform.position;
            Vector3 headPos = origin.Camera != null ? origin.Camera.transform.position : originPos + Vector3.up * m_PlayerHeight;
            float actualPlayerHeight = Mathf.Max(1.0f, headPos.y - originPos.y);
            Vector3 playerGroundPos = new Vector3(headPos.x, originPos.y, headPos.z);
            Vector3 targetOriginPos = targetPosition + originPos - playerGroundPos;

            // 2. Identify destination & start surface colliders & platform roots
            Collider destCollider = null;
            Transform destPlatform = null;
            if (Physics.Raycast(targetPosition + Vector3.up * 0.3f, Vector3.down, out RaycastHit destHit, 0.8f, m_ObstacleLayers, QueryTriggerInteraction.Ignore))
            {
                destCollider = destHit.collider;
                destPlatform = destHit.collider.transform;
                if (destPlatform.parent != null && destPlatform.parent.GetComponentInParent<XROrigin>() == null)
                {
                    destPlatform = destPlatform.parent;
                }
            }

            Collider startCollider = null;
            Transform startPlatform = null;
            if (Physics.Raycast(playerGroundPos + Vector3.up * 0.3f, Vector3.down, out RaycastHit startHit, 0.8f, m_ObstacleLayers, QueryTriggerInteraction.Ignore))
            {
                startCollider = startHit.collider;
                startPlatform = startHit.collider.transform;
                if (startPlatform.parent != null && startPlatform.parent.GetComponentInParent<XROrigin>() == null)
                {
                    startPlatform = startPlatform.parent;
                }
            }

            // 3. Obstacle collision sweep along dash trajectory
            bool wasBlocked = false;
            Vector3 dashVec = targetOriginPos - originPos;
            float dashDist = dashVec.magnitude;

            if (dashDist > 0.05f)
            {
                Vector3 dashDir = dashVec / dashDist;

                // CapsuleCast along the path using player's actual room-scale position
                Vector3 capsuleBottom = playerGroundPos + Vector3.up * (m_PlayerRadius + 0.05f);
                Vector3 capsuleTop = playerGroundPos + Vector3.up * (actualPlayerHeight - m_PlayerRadius);
                if (capsuleTop.y <= capsuleBottom.y)
                    capsuleTop = capsuleBottom + Vector3.up * 0.2f;

                var hits = Physics.CapsuleCastAll(capsuleBottom, capsuleTop, m_PlayerRadius, dashDir, dashDist, m_ObstacleLayers, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var hit in hits)
                {
                    if (!IsObstacle(hit.collider, hit.normal, destCollider, destPlatform, startCollider, startPlatform, origin.transform))
                        continue;

                    // Obstacle (wall, pillar, barrier) directly in the path
                    float safeDist = Mathf.Max(0f, hit.distance - m_SkinWidth);
                    if (safeDist <= 0.05f)
                    {
                        TryEndLocomotion();
                        TriggerHaptics(0.6f, 0.1f);
                        return false;
                    }

                    targetOriginPos = originPos + dashDir * safeDist;
                    wasBlocked = true;
                    break;
                }
            }

            m_DashCoroutine = StartCoroutine(PerformDash(origin, targetOriginPos, targetRotation, wasBlocked, onComplete));
            return true;
        }

        private bool IsObstacle(Collider col, Vector3 normal, Collider destCol, Transform destPlatform, Collider startCol, Transform startPlatform, Transform rigTransform)
        {
            if (col == null || col.isTrigger)
                return false;

            // Ignore player rig and self-colliders
            if (col.transform == rigTransform || col.transform.IsChildOf(rigTransform))
                return false;

            // Ignore destination platform / surface colliders
            if (destCol != null)
            {
                if (col == destCol)
                    return false;
                if (destPlatform != null && (col.transform.IsChildOf(destPlatform) || destPlatform.IsChildOf(col.transform)))
                    return false;
            }

            // Ignore start platform / surface colliders (so stepping off an elevated platform/box doesn't hit its front lip)
            if (startCol != null)
            {
                if (col == startCol)
                    return false;
                if (startPlatform != null && (col.transform.IsChildOf(startPlatform) || startPlatform.IsChildOf(col.transform)))
                    return false;
            }

            // Ignore flat or gentle walkable floors under the trajectory (slope < 45 deg, normal.y > 0.7)
            if (normal.y > 0.7f)
                return false;

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

        private IEnumerator PerformDash(XROrigin origin, Vector3 targetPosition, Quaternion? targetRotation, bool wasBlocked, System.Action onComplete)
        {
            m_IsDashing = true;

            try
            {
                // 1. Launch feedback: trigger crisp tactile impulse
                TriggerHaptics(0.45f, 0.08f);

                // 2. Comfort: respect global vignette toggle from TP1ComfortManager
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

                // Retrieve CharacterController
                CharacterController cc = origin.Origin != null ? origin.Origin.GetComponent<CharacterController>() : null;
                if (cc == null && origin.GetComponent<CharacterController>() != null)
                    cc = origin.GetComponent<CharacterController>();

                // Elevation analysis: calculate clearance arc for elevated platforms and steps
                float heightDiff = targetPosition.y - originStart.y;
                bool isClimbing = heightDiff > 0.15f;
                bool isDescending = heightDiff < -0.15f;
                float arcHeight = isClimbing ? Mathf.Min(0.35f, heightDiff * 0.5f) : 0f;

                float elapsed = 0f;

                while (elapsed < m_DashDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / m_DashDuration);
                    float curvedT = m_DashCurve.Evaluate(t);

                    Vector3 desiredPos = Vector3.Lerp(originStart, targetPosition, curvedT);

                    // Smooth elevation profiling to clear platform edges cleanly
                    if (isClimbing)
                    {
                        float verticalArc = Mathf.Sin(t * Mathf.PI) * arcHeight;
                        desiredPos.y = Mathf.Lerp(originStart.y, targetPosition.y, Mathf.SmoothStep(0f, 1f, t)) + verticalArc;
                    }
                    else if (isDescending)
                    {
                        // Step out horizontally before dropping cleanly to lower ground
                        float dropT = Mathf.Clamp01((t - 0.15f) / 0.85f);
                        desiredPos.y = Mathf.Lerp(originStart.y, targetPosition.y, Mathf.SmoothStep(0f, 1f, dropT));
                    }

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

                // Final step to destination
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

                // Ground snap on arrival if not blocked by a vertical wall
                if (!wasBlocked && Physics.Raycast(originTransform.position + Vector3.up * 0.4f, Vector3.down, out RaycastHit landHit, 1.0f, m_ObstacleLayers, QueryTriggerInteraction.Ignore))
                {
                    Vector3 groundOffset = (landHit.point - originTransform.position);
                    groundOffset.x = 0f;
                    groundOffset.z = 0f;
                    if (cc != null && cc.enabled)
                        cc.Move(groundOffset);
                    else
                        originTransform.position += groundOffset;
                }

                // Landing / impact tactile feedback
                if (wasBlocked)
                {
                    TriggerHaptics(0.65f, 0.12f); // Obstacle collision thud
                }
                else
                {
                    TriggerHaptics(0.25f, 0.05f); // Soft landing settle
                }
            }
            finally
            {
                // Guaranteed state cleanup even if coroutine is interrupted or stopped
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

        /// <summary>
        /// Triggers tactile vibration on both active VR controllers using OpenXR and XRI pipelines.
        /// </summary>
        public void TriggerHaptics(float amplitude, float duration)
        {
            if (!m_EnableHaptics)
                return;

            try
            {
                // 1. Send haptics via XRI Interactors
                var interactors = FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None);
                foreach (var interactor in interactors)
                {
                    interactor.SendHapticImpulse(amplitude, duration);
                }

                // 2. Send haptics via UnityEngine.XR.InputDevices for hardware controllers
                var devices = new List<UnityEngine.XR.InputDevice>();
                UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                    UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.HeldInHand,
                    devices);
                foreach (var dev in devices)
                {
                    dev.SendHapticImpulse(0u, amplitude, duration);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DashProvider] Haptic impulse failed: {ex.Message}");
            }
        }
    }
}
