using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

namespace LOG8704.Locomotion
{
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
                m_VignetteController = FindFirstObjectByType<TunnelingVignetteController>();
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

        public bool DashTo(Vector3 targetPosition, Quaternion? targetRotation = null, Action onComplete = null)
        {
            if (m_IsDashing || Time.time < m_LastDashTime + m_Cooldown)
                return false;

            XROrigin origin = GetXROrigin();
            if (origin == null || origin.Origin == null || !TryStartLocomotionImmediately())
                return false;

            // Compensate for room-scale head offset from tracking origin
            Vector3 originPos = origin.Origin.transform.position;
            Vector3 headPos = origin.Camera != null ? origin.Camera.transform.position : originPos + Vector3.up * m_PlayerHeight;
            float actualPlayerHeight = Mathf.Max(1.0f, headPos.y - originPos.y);
            Vector3 playerGroundPos = new Vector3(headPos.x, originPos.y, headPos.z);
            Vector3 targetOriginPos = targetPosition + originPos - playerGroundPos;

            // Identify surfaces to exclude lips/edges of departure and arrival platforms
            GetSurfaceInfo(targetPosition, out Collider destCol, out Transform destPlat);
            GetSurfaceInfo(playerGroundPos, out Collider startCol, out Transform startPlat);

            bool wasBlocked = false;
            Vector3 dashVec = targetOriginPos - originPos;
            float dashDist = dashVec.magnitude;

            if (dashDist > 0.05f)
            {
                Vector3 dashDir = dashVec / dashDist;
                if (CheckObstacleAlongPath(playerGroundPos, dashDir, dashDist, actualPlayerHeight, destCol, destPlat, startCol, startPlat, origin.transform, out RaycastHit hit))
                {
                    float safeDist = Mathf.Max(0f, hit.distance - m_SkinWidth);
                    if (safeDist <= 0.05f)
                    {
                        TryEndLocomotion();
                        TriggerDashHaptics(0.6f, 0.1f);
                        return false;
                    }

                    targetOriginPos = originPos + dashDir * safeDist;
                    wasBlocked = true;
                }
            }

            m_DashCoroutine = StartCoroutine(PerformDash(origin, targetOriginPos, targetRotation, wasBlocked, onComplete));
            return true;
        }

        public bool CheckObstacleAlongPath(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            return CheckObstacleAlongPath(origin, direction, distance, m_PlayerHeight, null, null, null, null, null, out hit);
        }

        private bool CheckObstacleAlongPath(Vector3 groundOrigin, Vector3 direction, float distance, float playerHeight,
            Collider destCol, Transform destPlatform, Collider startCol, Transform startPlatform, Transform rigTransform,
            out RaycastHit blockingHit)
        {
            blockingHit = default;
            Vector3 capsuleBottom = groundOrigin + Vector3.up * (m_PlayerRadius + 0.05f);
            Vector3 capsuleTop = groundOrigin + Vector3.up * Mathf.Max(m_PlayerRadius + 0.25f, playerHeight - m_PlayerRadius);

            var hits = Physics.CapsuleCastAll(capsuleBottom, capsuleTop, m_PlayerRadius, direction, distance, m_ObstacleLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (IsObstacle(h.collider, h.normal, destCol, destPlatform, startCol, startPlatform, rigTransform))
                {
                    blockingHit = h;
                    return true;
                }
            }
            return false;
        }

        public Vector3 CalculateElevationArc(Vector3 start, Vector3 end, float progress)
        {
            float heightDiff = end.y - start.y;
            Vector3 pos = Vector3.Lerp(start, end, progress);

            if (heightDiff > 0.15f)
            {
                // Climbing: add vertical parabola (sin arc) to cleanly clear platform ledges
                float arcHeight = Mathf.Min(0.35f, heightDiff * 0.5f);
                pos.y = Mathf.Lerp(start.y, end.y, Mathf.SmoothStep(0f, 1f, progress)) + Mathf.Sin(progress * Mathf.PI) * arcHeight;
            }
            else if (heightDiff < -0.15f)
            {
                // Descending: delay vertical fall until clearing horizontal step edge
                float dropProgress = Mathf.Clamp01((progress - 0.15f) / 0.85f);
                pos.y = Mathf.Lerp(start.y, end.y, Mathf.SmoothStep(0f, 1f, dropProgress));
            }

            return pos;
        }

        public void TriggerHaptics(float amplitude, float duration) => TriggerDashHaptics(amplitude, duration);

        public void TriggerDashHaptics(float intensity, float duration)
        {
            if (!m_EnableHaptics) return;

            try
            {
                var interactors = FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None);
                foreach (var interactor in interactors)
                    interactor.SendHapticImpulse(intensity, duration);

                var devices = new List<UnityEngine.XR.InputDevice>();
                UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                    UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.HeldInHand,
                    devices);
                foreach (var dev in devices)
                    dev.SendHapticImpulse(0u, intensity, duration);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DashProvider] Haptic impulse failed: {ex.Message}");
            }
        }

        private IEnumerator PerformDash(XROrigin origin, Vector3 targetPosition, Quaternion? targetRotation, bool wasBlocked, Action onComplete)
        {
            m_IsDashing = true;

            try
            {
                TriggerDashHaptics(0.45f, 0.08f);

                bool showVignette = m_VignetteController != null &&
                    (TP1ComfortManager.Instance == null || TP1ComfortManager.Instance.isVignetteActive);

                if (showVignette)
                    m_VignetteController.BeginTunnelingVignette(this);

                Transform originTransform = origin.Origin.transform;
                Vector3 originStart = originTransform.position;
                Quaternion rotStart = originTransform.rotation;

                CharacterController cc = origin.Origin.GetComponent<CharacterController>() ?? origin.GetComponent<CharacterController>();

                float elapsed = 0f;
                while (elapsed < m_DashDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / m_DashDuration);
                    float curvedT = m_DashCurve.Evaluate(t);

                    Vector3 desiredPos = CalculateElevationArc(originStart, targetPosition, curvedT);
                    Vector3 frameStep = desiredPos - originTransform.position;

                    if (cc != null && cc.enabled)
                        cc.Move(frameStep);
                    else
                        originTransform.position += frameStep;

                    if (targetRotation.HasValue)
                        originTransform.rotation = Quaternion.Slerp(rotStart, targetRotation.Value, curvedT);

                    yield return null;
                }

                // Snap to final arrival position
                Vector3 finalStep = targetPosition - originTransform.position;
                if (cc != null && cc.enabled)
                    cc.Move(finalStep);
                else
                    originTransform.position = targetPosition;

                if (targetRotation.HasValue)
                    originTransform.rotation = targetRotation.Value;

                // Ground snap on arrival if not blocked by a vertical wall
                if (!wasBlocked && Physics.Raycast(originTransform.position + Vector3.up * 0.4f, Vector3.down, out RaycastHit landHit, 1.0f, m_ObstacleLayers, QueryTriggerInteraction.Ignore))
                {
                    Vector3 groundOffset = new Vector3(0f, landHit.point.y - originTransform.position.y, 0f);
                    if (cc != null && cc.enabled)
                        cc.Move(groundOffset);
                    else
                        originTransform.position += groundOffset;
                }

                TriggerDashHaptics(wasBlocked ? 0.65f : 0.25f, wasBlocked ? 0.12f : 0.05f);
            }
            finally
            {
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

        private void GetSurfaceInfo(Vector3 pos, out Collider col, out Transform platform)
        {
            col = null;
            platform = null;
            if (Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 0.8f, m_ObstacleLayers, QueryTriggerInteraction.Ignore))
            {
                col = hit.collider;
                platform = hit.collider.transform;
                if (platform.parent != null && platform.parent.GetComponentInParent<XROrigin>() == null)
                    platform = platform.parent;
            }
        }

        private bool IsObstacle(Collider col, Vector3 normal, Collider destCol, Transform destPlatform, Collider startCol, Transform startPlatform, Transform rigTransform)
        {
            if (col == null || col.isTrigger) return false;
            if (rigTransform != null && (col.transform == rigTransform || col.transform.IsChildOf(rigTransform))) return false;

            if (destCol != null && (col == destCol || (destPlatform != null && (col.transform.IsChildOf(destPlatform) || destPlatform.IsChildOf(col.transform)))))
                return false;

            if (startCol != null && (col == startCol || (startPlatform != null && (col.transform.IsChildOf(startPlatform) || startPlatform.IsChildOf(col.transform)))))
                return false;

            // Reject horizontal walkable floors (slope < 45 degrees)
            return normal.y <= 0.7f;
        }

        private XROrigin GetXROrigin()
        {
            return (mediator != null ? mediator.xrOrigin : null)
                ?? GetComponentInParent<XROrigin>()
                ?? FindFirstObjectByType<XROrigin>();
        }
    }
}
