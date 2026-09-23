using UnityEngine;
using Unity.XR.CoreUtils;

namespace LOG8704.UI
{
    /// <summary>
    /// Trigger attached to the target landing zone in the tutorial room.
    /// Uses dual detection: PhysX OnTriggerEnter and continuous proximity checking
    /// to ensure 100% reliability even when instantaneous teleportation repositions the player.
    /// </summary>
    public class TutorialTeleportZoneTrigger : MonoBehaviour
    {
        [SerializeField] private TutorialFlowController m_Controller;
        [SerializeField] private float m_ProximityRadius = 1.8f;
        [SerializeField] private bool m_Active = true;

        public bool isActive
        {
            get => m_Active;
            set => m_Active = value;
        }

        public void SetController(TutorialFlowController controller)
        {
            m_Controller = controller;
        }

        private void Update()
        {
            if (!m_Active || m_Controller == null)
                return;

            // Proximity fallback check (ideal for VR teleport jumps)
            var cam = Camera.main;
            if (cam == null)
            {
                var camObj = GameObject.FindWithTag("MainCamera");
                if (camObj != null) cam = camObj.GetComponent<Camera>();
            }

            if (cam != null)
            {
                Vector3 playerPos = cam.transform.position;
                Vector3 zonePos = transform.position;
                float horizontalDist = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(zonePos.x, zonePos.z));

                if (horizontalDist <= m_ProximityRadius && Mathf.Abs(playerPos.y - zonePos.y) < 3.0f)
                {
                    m_Controller.OnTeleportZoneEntered();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!m_Active || m_Controller == null || other == null)
                return;

            if (other.GetComponentInParent<XROrigin>() != null ||
                other.GetComponentInParent<CharacterController>() != null ||
                other.CompareTag("MainCamera") ||
                other.GetComponentInChildren<Camera>() != null)
            {
                m_Controller.OnTeleportZoneEntered();
            }
        }
    }
}
