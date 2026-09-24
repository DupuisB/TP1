using UnityEngine;

namespace LOG8704.Locomotion
{
    /// <summary>
    /// Forwards trigger events from the child Walk_Trigger volume to its portal.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class SceneTeleportPortalTriggerBridge : MonoBehaviour
    {
        public SceneTeleportPortal portal;

        private void OnTriggerEnter(Collider other)
        {
            if (portal != null)
                portal.OnWalkTriggerEnter(other);
        }
    }
}
