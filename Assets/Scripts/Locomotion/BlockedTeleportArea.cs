using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LOG8704.Locomotion
{
    /// <summary>
    /// Teleportation interactable attached to surfaces where teleportation is forbidden (red objects, obstacles).
    /// Allowing hover while rejecting selection causes XRInteractorLineVisual to render the blocked orange reticle.
    /// </summary>
    [AddComponentMenu("LOG8704/Blocked Teleport Area")]
    [SelectionBase]
    public class BlockedTeleportArea : BaseTeleportationInteractable
    {
        protected override void Reset()
        {
            base.Reset();
            teleportTrigger = TeleportTrigger.OnSelectExited;
            interactionLayers = InteractionLayerMask.GetMask("Default", "Teleport");
        }

        protected override void Awake()
        {
            base.Awake();
            teleportTrigger = TeleportTrigger.OnSelectExited;
            interactionLayers = InteractionLayerMask.GetMask("Default", "Teleport");
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor) => false;

        protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit raycastHit, ref TeleportRequest teleportRequest) => false;
    }
}
