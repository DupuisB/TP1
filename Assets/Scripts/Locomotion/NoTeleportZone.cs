using UnityEngine;

namespace LOG8704.Locomotion
{
    /// <summary>
    /// Add this component to any GameObject (or prefab) that should reject teleportation.
    /// The teleport ray can still aim at and hit its collider (showing the invalid/blocked visual),
    /// but teleportation will not occur. Physical walking and continuous locomotion remain fully functional.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("LOG8704/Locomotion/No Teleport Zone")]
    public class NoTeleportZone : MonoBehaviour
    {
    }
}
