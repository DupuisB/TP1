using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LOG8704.Locomotion
{
    /// <summary>
    /// Teleportation interactable attached to surfaces where teleportation is forbidden
    /// (e.g. red objects, obstacles, props, boundary walls).
    /// By participating in the Teleport interaction layer and allowing hover while strictly rejecting selection,
    /// XRInteractorLineVisual automatically renders the blocked visual state:
    /// an orange line terminating at the hit point with the cross reticle (matching the behavior on the sides of walls).
    /// </summary>
    [AddComponentMenu("LOG8704/Blocked Teleport Area")]
    [SelectionBase]
    public class BlockedTeleportArea : BaseTeleportationInteractable
    {
        protected override void Reset()
        {
            base.Reset();
            teleportTrigger = TeleportTrigger.OnSelectExited;
            interactionLayers = unchecked((int)2147483648) | 1 | InteractionLayerMask.GetMask("Teleport");
        }

        protected override void Awake()
        {
            base.Awake();
            teleportTrigger = TeleportTrigger.OnSelectExited;
            interactionLayers = unchecked((int)2147483648) | 1 | InteractionLayerMask.GetMask("Teleport");
        }

        /// <summary>
        /// Always returns false so this area can never be selected for teleportation.
        /// XRInteractorLineVisual interprets hover + non-selectable as the Blocked state,
        /// rendering the orange line with the cross reticle.
        /// </summary>
        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            return false;
        }

        /// <summary>
        /// Safety override: never generate a teleport request.
        /// </summary>
        protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit raycastHit, ref TeleportRequest teleportRequest)
        {
            return false;
        }
    }
}
