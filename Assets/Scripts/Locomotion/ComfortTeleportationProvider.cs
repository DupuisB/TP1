using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Unity.XR.CoreUtils;

namespace LOG8704.Locomotion
{
    /// <summary>
    /// Comfort-enhanced TeleportationProvider coordinating Teleport and Dash locomotion.
    /// Integrates seamlessly with XRI TeleportationArea, ScreenFadeCanvas (Blink), and DashProvider.
    /// </summary>
    [AddComponentMenu("LOG8704/Locomotion/Comfort Teleportation Provider")]
    public class ComfortTeleportationProvider : TeleportationProvider
    {
        [Header("Dependencies")]
        [SerializeField] private DashProvider m_DashProvider;
        [SerializeField] private ScreenFadeCanvas m_ScreenFade;

        public DashProvider dashProvider
        {
            get => m_DashProvider;
            set => m_DashProvider = value;
        }

        public ScreenFadeCanvas screenFade
        {
            get => m_ScreenFade;
            set => m_ScreenFade = value;
        }

        protected override void Awake()
        {
            base.Awake();

            if (mediator == null)
                mediator = GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionMediator>() ?? FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionMediator>();

            if (m_DashProvider == null)
                m_DashProvider = GetComponent<DashProvider>() ?? FindFirstObjectByType<DashProvider>();

            if (m_ScreenFade == null)
                m_ScreenFade = ScreenFadeCanvas.Instance ?? FindFirstObjectByType<ScreenFadeCanvas>();
        }

        /// <summary>
        /// Intercepts teleport requests to route through the active Locomotion Mode (Dash, Teleport with Blink, Instant).
        /// </summary>
        public override bool QueueTeleportRequest(TeleportRequest teleportRequest)
        {
            // Reject teleport if destination penetrates inside geometry/walls or lacks player body clearance
            if (!IsValidTeleportDestination(teleportRequest.destinationPosition))
            {
                Debug.LogWarning($"[ComfortTeleportationProvider] Teleport destination {teleportRequest.destinationPosition} rejected: collision/clearance obstacle detected.");
                return false;
            }

            // Reject teleport if destination lands on a red or blocked surface
            if (IsDestinationRedOrBlocked(teleportRequest.destinationPosition))
            {
                Debug.LogWarning($"[ComfortTeleportationProvider] Teleport destination {teleportRequest.destinationPosition} rejected: surface is red (red.mat) or blocked.");
                return false;
            }

            var mgr = TP1ComfortManager.Instance;
            if (mgr != null)
            {
                // In SmoothMove mode, ignore teleport requests from ray
                if (mgr.isSmoothMoveActive)
                    return false;

                // In Dash mode, perform rapid 0.20s lerp to target destination
                if (mgr.isDashActive)
                {
                    return ExecuteDashTeleport(teleportRequest);
                }

                // In Teleport mode, check if Blink option is enabled
                if (mgr.isTeleportActive)
                {
                    if (mgr.isTeleportBlinkEnabled)
                    {
                        return ExecuteBlinkTeleport(teleportRequest);
                    }
                    else
                    {
                        return base.QueueTeleportRequest(teleportRequest);
                    }
                }
            }

            // Fallback default: Blink teleport
            return ExecuteBlinkTeleport(teleportRequest);
        }

        /// <summary>
        /// Validates that the player's body and head clearance volume at the destination does not overlap solid wall/obstacle geometry.
        /// </summary>
        public bool IsValidTeleportDestination(Vector3 destination)
        {
            // Check player torso and head clearance capsule (from 0.35m above ground to 1.60m)
            Vector3 bottom = destination + Vector3.up * 0.35f;
            Vector3 top = destination + Vector3.up * 1.60f;
            float radius = 0.22f;

            var colliders = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in colliders)
            {
                // Ignore player's own colliders (e.g. CharacterController on rig)
                if (col.transform.IsChildOf(transform.root) || col.transform.root == transform.root)
                    continue;

                // Hit a solid obstacle/wall inside the destination volume
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that the surface under the teleport destination is not red (red.mat) or blocked.
        /// </summary>
        public bool IsDestinationRedOrBlocked(Vector3 destination)
        {
            if (Physics.Raycast(destination + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (TP1ComfortManager.IsRedOrBlocked(hit.collider))
                    return true;
            }
            return false;
        }

        private bool ExecuteBlinkTeleport(TeleportRequest request)
        {
            if (m_ScreenFade == null)
                m_ScreenFade = ScreenFadeCanvas.Instance ?? FindFirstObjectByType<ScreenFadeCanvas>();

            if (m_ScreenFade != null)
            {
                // Rapid fade-to-black canvas transition (0.08s fade out, jump while black, 0.08s fade in)
                m_ScreenFade.BlinkFade(() =>
                {
                    base.QueueTeleportRequest(request);
                }, 0.08f);

                return true;
            }

            // Fallback to instant teleport if no ScreenFadeCanvas in scene
            return base.QueueTeleportRequest(request);
        }

        private bool ExecuteDashTeleport(TeleportRequest request)
        {
            if (m_DashProvider == null)
                m_DashProvider = GetComponent<DashProvider>() ?? FindFirstObjectByType<DashProvider>();

            if (m_DashProvider != null)
            {
                // Only pass targetRotation if the teleport request explicitly requested matching forward direction (e.g. TargetUpAndForward on an Anchor)
                // For standard WorldSpaceUp, TargetUp, or None (e.g. TeleportationArea on floor/platforms), preserve player heading
                Quaternion? targetRot = null;
                if (request.matchOrientation == MatchOrientation.TargetUpAndForward)
                {
                    targetRot = request.destinationRotation;
                }

                return m_DashProvider.DashTo(request.destinationPosition, targetRot);
            }

            // Fallback to instant if no DashProvider configured
            return base.QueueTeleportRequest(request);
        }
    }
}
