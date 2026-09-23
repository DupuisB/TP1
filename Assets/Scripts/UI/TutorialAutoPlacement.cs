using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOG8704.UI
{
    /// <summary>
    /// Runtime helper that auto-positions beginner tutorial objects so they are immediately visible.
    /// Looks for scene objects by name and places the tutorial panel in front of the camera,
    /// and the teleport zone at the player's starting position.
    /// </summary>
    public class TutorialAutoPlacement : MonoBehaviour
    {
        [SerializeField] private string m_TutorialPanelName = "TutorialPanel";
        [SerializeField] private string m_TutorialTeleportZoneName = "TutorialTeleportZone";

        [Header("Panel Placement")]
        [SerializeField] private bool m_PlacePanelInFrontOfCamera = true;
        [SerializeField] private bool m_FollowCamera = true;
        [SerializeField] private float m_PanelDistance = 0.75f;
        [SerializeField] private Vector3 m_PanelCameraOffset = new Vector3(0f, -0.06f, 0f);

        [Header("Teleport Zone Placement")]
        [SerializeField] private bool m_PlaceTeleportZoneAtPlayerStart = true;
        [SerializeField] private Vector3 m_TeleportZoneWorldOffset = Vector3.zero;

        private Transform m_MainCamera;
        private Transform m_TutorialPanel;
        private Transform m_TutorialTeleportZone;
        private bool m_ZonePlaced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("TutorialAutoPlacement");
            DontDestroyOnLoad(go);
            go.AddComponent<TutorialAutoPlacement>();
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ResolveReferences();
            ApplyInitialPlacement();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void LateUpdate()
        {
            if (m_PlacePanelInFrontOfCamera && m_FollowCamera)
                PlacePanelInFrontOfCamera();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            m_ZonePlaced = false;
            ResolveReferences();
            ApplyInitialPlacement();
        }

        private void ResolveReferences()
        {
            if (m_MainCamera == null)
            {
                var cam = Camera.main;
                if (cam == null)
                {
                    var camObj = GameObject.FindWithTag("MainCamera");
                    if (camObj != null)
                        cam = camObj.GetComponent<Camera>();
                }

                if (cam != null)
                    m_MainCamera = cam.transform;
            }

            var panelObj = GameObject.Find(m_TutorialPanelName);
            m_TutorialPanel = panelObj != null ? panelObj.transform : null;

            var zoneObj = GameObject.Find(m_TutorialTeleportZoneName);
            m_TutorialTeleportZone = zoneObj != null ? zoneObj.transform : null;
        }

        private void ApplyInitialPlacement()
        {
            if (m_PlacePanelInFrontOfCamera)
                PlacePanelInFrontOfCamera();

            if (m_PlaceTeleportZoneAtPlayerStart)
                PlaceTeleportZoneAtPlayerStart();
        }

        private void PlacePanelInFrontOfCamera()
        {
            if (m_MainCamera == null || m_TutorialPanel == null)
                return;

            var canvas = m_TutorialPanel.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                return;

            Vector3 camPos = m_MainCamera.position;
            Vector3 camFwd = m_MainCamera.forward;
            Vector3 camRight = m_MainCamera.right;
            Vector3 camUp = m_MainCamera.up;

            float distance = Mathf.Max(0.2f, m_PanelDistance);
            Vector3 targetPos = camPos + camFwd * distance;
            targetPos += camRight * m_PanelCameraOffset.x;
            targetPos += camUp * m_PanelCameraOffset.y;
            targetPos += camFwd * m_PanelCameraOffset.z;

            m_TutorialPanel.position = targetPos;
            m_TutorialPanel.rotation = m_MainCamera.rotation;
        }

        private void PlaceTeleportZoneAtPlayerStart()
        {
            if (m_ZonePlaced || m_MainCamera == null || m_TutorialTeleportZone == null)
                return;

            Vector3 playerPos = m_MainCamera.position;
            Vector3 zonePos = m_TutorialTeleportZone.position;

            zonePos.x = playerPos.x + m_TeleportZoneWorldOffset.x;
            zonePos.y = zonePos.y + m_TeleportZoneWorldOffset.y;
            zonePos.z = playerPos.z + m_TeleportZoneWorldOffset.z;

            m_TutorialTeleportZone.position = zonePos;
            m_ZonePlaced = true;
        }
    }
}
