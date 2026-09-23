using UnityEngine;

namespace LOG8704.UI
{
    public class TutorialTeleportZoneTrigger : MonoBehaviour
    {
        private TutorialFlowController m_Controller;

        public void SetController(TutorialFlowController controller)
        {
            m_Controller = controller;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_Controller == null)
                return;

            if (other == null)
                return;

            if (other.CompareTag("MainCamera") || other.GetComponentInChildren<Camera>() != null || other.GetComponentInParent<Camera>() != null)
            {
                m_Controller.OnTeleportZoneEntered();
            }
        }
    }
}
