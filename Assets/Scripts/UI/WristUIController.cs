using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LOG8704.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LOG8704.UI
{
    /// <summary>
    /// Left wrist-mounted VR interface displaying the controls guide and interactive buttons
    /// to switch locomotion modes (Smooth Walk, Teleport, Dash), toggle Teleport Blink,
    /// toggle global Vignette, and toggle Turn mode (Snap 45° vs Smooth).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class WristUIController : MonoBehaviour
    {
        [Header("Locomotion Mode Buttons")]
        [SerializeField] private Button m_SmoothMoveButton;
        [SerializeField] private Button m_TeleportButton;
        [SerializeField] private Button m_DashButton;

        [Header("Locomotion Mode Backgrounds")]
        [SerializeField] private Image m_SmoothMoveButtonBg;
        [SerializeField] private Image m_TeleportButtonBg;
        [SerializeField] private Image m_DashButtonBg;

        [Header("Locomotion Mode Labels")]
        [SerializeField] private TMP_Text m_SmoothMoveStatusText;
        [SerializeField] private TMP_Text m_TeleportStatusText;
        [SerializeField] private TMP_Text m_DashStatusText;

        [Header("Option Buttons")]
        [SerializeField] private Button m_BlinkButton;
        [SerializeField] private Image m_BlinkButtonBg;
        [SerializeField] private TMP_Text m_BlinkStatusText;

        [SerializeField] private Button m_VignetteButton;
        [SerializeField] private Image m_VignetteButtonBg;
        [SerializeField] private TMP_Text m_VignetteStatusText;

        [SerializeField] private Button m_TurnButton;
        [SerializeField] private Image m_TurnButtonBg;
        [SerializeField] private TMP_Text m_TurnStatusText;

        [Header("Summary & Guide")]
        [SerializeField] private TMP_Text m_ModeSummaryText;

        private readonly Color k_ActiveColor = new Color(0.12f, 0.65f, 0.30f, 0.95f);   // Clean Green
        private readonly Color k_InactiveColor = new Color(0.20f, 0.22f, 0.25f, 0.90f); // Dark Charcoal
        private readonly Color k_ActiveTextColor = Color.white;
        private readonly Color k_InactiveTextColor = new Color(0.75f, 0.75f, 0.75f, 1f);

        private bool m_Subscribed = false;
        private float m_NextPollTime;

        private void Start()
        {
            // Bind button click events
            if (m_SmoothMoveButton != null)
                m_SmoothMoveButton.onClick.AddListener(OnSmoothMoveClicked);

            if (m_TeleportButton != null)
                m_TeleportButton.onClick.AddListener(OnTeleportClicked);

            if (m_DashButton != null)
                m_DashButton.onClick.AddListener(OnDashClicked);

            if (m_BlinkButton != null)
                m_BlinkButton.onClick.AddListener(OnBlinkClicked);

            if (m_VignetteButton != null)
                m_VignetteButton.onClick.AddListener(OnVignetteClicked);

            if (m_TurnButton != null)
                m_TurnButton.onClick.AddListener(OnTurnClicked);

            TrySubscribe();
        }

        private void Update()
        {
            if (!m_Subscribed)
            {
                TrySubscribe();
            }

            // Continuously poll to ensure wrist UI stays 100% in sync with keyboard/ray changes
            if (Time.time >= m_NextPollTime)
            {
                m_NextPollTime = Time.time + 0.1f;
                RefreshUI();
            }
        }

        private void TrySubscribe()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null)
            {
                mgr.onLocomotionModeChanged += OnLocomotionModeChanged;
                mgr.onTeleportBlinkToggled += OnTeleportBlinkToggled;
                mgr.onVignetteToggled += OnVignetteToggled;
                mgr.onTurnModeChanged += OnTurnModeChanged;
                m_Subscribed = true;
                RefreshUI();
            }
        }

        private void OnDestroy()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null && m_Subscribed)
            {
                mgr.onLocomotionModeChanged -= OnLocomotionModeChanged;
                mgr.onTeleportBlinkToggled -= OnTeleportBlinkToggled;
                mgr.onVignetteToggled -= OnVignetteToggled;
                mgr.onTurnModeChanged -= OnTurnModeChanged;
            }
        }

        // ==========================================
        // BUTTON HANDLERS
        // ==========================================

        private void OnSmoothMoveClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null)
            {
                mgr.SetLocomotionMode(TP1ComfortManager.LocomotionMode.SmoothMove);
                RefreshUI();
            }
            DeselectUI();
        }

        private void OnTeleportClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null)
            {
                mgr.SetLocomotionMode(TP1ComfortManager.LocomotionMode.Teleport);
                RefreshUI();
            }
            DeselectUI();
        }

        private void OnDashClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null)
            {
                mgr.SetLocomotionMode(TP1ComfortManager.LocomotionMode.Dash);
                RefreshUI();
            }
            DeselectUI();
        }

        private void OnBlinkClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null)
            {
                mgr.ToggleTeleportBlink();
                RefreshUI();
            }
            DeselectUI();
        }

        private void OnVignetteClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null)
            {
                mgr.ToggleVignette();
                RefreshUI();
            }
            DeselectUI();
        }

        private void OnTurnClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null)
            {
                mgr.ToggleTurnMode();
                RefreshUI();
            }
            DeselectUI();
        }

        private void DeselectUI()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        // ==========================================
        // EVENT CALLBACKS
        // ==========================================

        private void OnLocomotionModeChanged(TP1ComfortManager.LocomotionMode mode) => RefreshUI();
        private void OnTeleportBlinkToggled(bool enabled) => RefreshUI();
        private void OnVignetteToggled(bool enabled) => RefreshUI();
        private void OnTurnModeChanged(TP1ComfortManager.TurnMode mode) => RefreshUI();

        // ==========================================
        // UI REFRESH
        // ==========================================

        public void RefreshUI()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr == null)
                return;

            bool isWalk = mgr.isSmoothMoveActive;
            bool isTeleport = mgr.isTeleportActive;
            bool isDash = mgr.isDashActive;

            bool blink = mgr.isTeleportBlinkEnabled;
            bool vignette = mgr.isVignetteActive;
            bool isSnap = mgr.isSnapTurnActive;

            // 1. Locomotion Mode Buttons
            UpdateButtonVisual(m_SmoothMoveButton, m_SmoothMoveButtonBg, m_SmoothMoveStatusText, isWalk, "1. Continu (Walk)");
            UpdateButtonVisual(m_TeleportButton, m_TeleportButtonBg, m_TeleportStatusText, isTeleport, "2. Téléportation");
            UpdateButtonVisual(m_DashButton, m_DashButtonBg, m_DashStatusText, isDash, "3. Dash (0.2s)");

            // 2. Teleport Option: Blink
            UpdateButtonVisual(m_BlinkButton, m_BlinkButtonBg, m_BlinkStatusText, blink, "Transition Blink (Téléport)");

            // 3. Global Comfort: Vignette
            UpdateButtonVisual(m_VignetteButton, m_VignetteButtonBg, m_VignetteStatusText, vignette, "Oeillère Vignette (Global)");

            // 4. Turn Mode: Snap vs Smooth
            if (m_TurnButton != null)
            {
                m_TurnButton.transition = Selectable.Transition.None;
                m_TurnButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            if (m_TurnButtonBg != null)
            {
                m_TurnButtonBg.color = isSnap ? k_InactiveColor : k_ActiveColor;
            }
            if (m_TurnStatusText != null)
            {
                string turnBadge = isSnap ? "<color=#35baf6>[SNAP 45°]</color>" : "<color=#38ef7d>[SMOOTH]</color>";
                m_TurnStatusText.text = $"Rotation : <b>{turnBadge}</b>";
                m_TurnStatusText.color = Color.white;
            }

            // 5. Summary Text
            if (m_ModeSummaryText != null)
            {
                string modeStr = isWalk ? "<color=#38ef7d>Walk</color>" : (isDash ? "<color=#38ef7d>Dash</color>" : "<color=#38ef7d>Teleport</color>");
                string blinkStr = blink ? "<color=#38ef7d>Blink</color>" : "<color=#aaaaaa>Instant</color>";
                string vigStr = vignette ? "<color=#38ef7d>ON</color>" : "<color=#ff5555>OFF</color>";
                string turnStr = isSnap ? "<color=#35baf6>Snap</color>" : "<color=#38ef7d>Smooth</color>";

                m_ModeSummaryText.text = $"Mode: <b>{modeStr}</b> | Blink: <b>{blinkStr}</b> | Oeillere: <b>{vigStr}</b> | Tourner: <b>{turnStr}</b>";
            }
        }

        private void UpdateButtonVisual(Button btn, Image bg, TMP_Text label, bool active, string title)
        {
            Color targetBg = active ? k_ActiveColor : k_InactiveColor;
            Color targetText = active ? k_ActiveTextColor : k_InactiveTextColor;

            if (bg != null)
                bg.color = targetBg;

            if (btn != null)
            {
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            if (label != null)
            {
                string badge = active ? "<color=#38ef7d>[ACTIF]</color>" : "<color=#888888>[OFF]</color>";
                label.text = $"{title} : <b>{badge}</b>";
                label.color = targetText;
            }
        }

        /// <summary>
        /// Procedural generator to construct the Wrist UI hierarchy cleanly at runtime or in editor.
        /// </summary>
        public static GameObject CreateWristUI(Transform wristAnchor)
        {
            var root = new GameObject("Wrist_Comfort_UI");
            if (wristAnchor != null)
            {
                root.transform.SetParent(wristAnchor, false);
                // Position slightly above the wrist, angled towards user's eyes
                root.transform.localPosition = new Vector3(0.02f, 0.08f, -0.06f);
                root.transform.localRotation = Quaternion.Euler(40f, 0f, 0f);
            }
            root.transform.localScale = Vector3.one * 0.00075f;

            // Canvas setup
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(540f, 740f);

            // XRI Tracked Device Raycaster for VR controller ray interaction
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            root.AddComponent<GraphicRaycaster>();

            // Dark semi-transparent backdrop panel
            var panelObj = new GameObject("BackgroundPanel");
            panelObj.transform.SetParent(root.transform, false);
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            var panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.07f, 0.08f, 0.11f, 0.95f); // Rich dark slate

            var controller = root.AddComponent<WristUIController>();

            // 1. Header Title
            var titleObj = CreateTMPText(panelObj.transform, "Title", "LOG8704 - VR LOCOMOTION", 24, FontStyles.Bold, new Vector2(0f, 325f), new Vector2(500f, 40f));
            titleObj.color = new Color(0.35f, 0.75f, 1f); // Neon Cyan

            // 2. Controls Guide Box
            var guideHeader = CreateTMPText(panelObj.transform, "GuideHeader", "GUIDE DES CONTRÔLES", 16, FontStyles.Bold, new Vector2(0f, 285f), new Vector2(500f, 25f));
            guideHeader.color = new Color(0.85f, 0.85f, 0.85f);

            string guideContent =
                "<b>Stick Gauche :</b> Déplacement continu (si activé)\n" +
                "<b>Stick Droit (Ray) :</b> Viser sol + relâcher (Téléport / Dash)\n" +
                "<b>Stick Droit X :</b> Rotation (Snap 45° ou Smooth)\n" +
                "<b>Casque HMD :</b> Mouvement physique 6DOF roomscale";

            var guideBody = CreateTMPText(panelObj.transform, "GuideBody", guideContent, 13, FontStyles.Normal, new Vector2(0f, 220f), new Vector2(500f, 85f));
            guideBody.alignment = TextAlignmentOptions.TopLeft;
            guideBody.color = new Color(0.8f, 0.85f, 0.9f);

            // 3. Section: Locomotion Modes (Exclusive: Walk vs Teleport vs Dash)
            var modeHeader = CreateTMPText(panelObj.transform, "ModeHeader", "MODE DE DÉPLACEMENT (CHOIX)", 16, FontStyles.Bold, new Vector2(0f, 150f), new Vector2(500f, 25f));
            modeHeader.color = new Color(0.95f, 0.8f, 0.3f); // Warm amber

            var (walkBtn, walkBg, walkTxt) = CreateButton(panelObj.transform, "WalkButton", "1. Continu (Walk)", new Vector2(0f, 105f), new Vector2(480f, 44f));
            var (teleBtn, teleBg, teleTxt) = CreateButton(panelObj.transform, "TeleportButton", "2. Téléportation", new Vector2(0f, 55f), new Vector2(480f, 44f));
            var (dashBtn, dashBg, dashTxt) = CreateButton(panelObj.transform, "DashButton", "3. Dash (0.2s)", new Vector2(0f, 5f), new Vector2(480f, 44f));

            controller.m_SmoothMoveButton = walkBtn;
            controller.m_SmoothMoveButtonBg = walkBg;
            controller.m_SmoothMoveStatusText = walkTxt;

            controller.m_TeleportButton = teleBtn;
            controller.m_TeleportButtonBg = teleBg;
            controller.m_TeleportStatusText = teleTxt;

            controller.m_DashButton = dashBtn;
            controller.m_DashButtonBg = dashBg;
            controller.m_DashStatusText = dashTxt;

            // 4. Section: Teleport Option (Blink)
            var teleOptHeader = CreateTMPText(panelObj.transform, "TeleOptHeader", "OPTION TÉLÉPORTATION", 15, FontStyles.Bold, new Vector2(0f, -40f), new Vector2(500f, 25f));
            teleOptHeader.color = new Color(0.95f, 0.8f, 0.3f);

            var (blinkBtn, blinkBg, blinkTxt) = CreateButton(panelObj.transform, "BlinkButton", "Transition Blink (Téléport)", new Vector2(0f, -80f), new Vector2(480f, 44f));
            controller.m_BlinkButton = blinkBtn;
            controller.m_BlinkButtonBg = blinkBg;
            controller.m_BlinkStatusText = blinkTxt;

            // 5. Section: Global Comfort (Oeillère Vignette)
            var comfortHeader = CreateTMPText(panelObj.transform, "ComfortHeader", "CONFORT GLOBAL (POUR TOUS LES MODES)", 15, FontStyles.Bold, new Vector2(0f, -125f), new Vector2(500f, 25f));
            comfortHeader.color = new Color(0.95f, 0.8f, 0.3f);

            var (vigBtn, vigBg, vigTxt) = CreateButton(panelObj.transform, "VignetteButton", "Oeillère Vignette (Global)", new Vector2(0f, -165f), new Vector2(480f, 44f));
            controller.m_VignetteButton = vigBtn;
            controller.m_VignetteButtonBg = vigBg;
            controller.m_VignetteStatusText = vigTxt;

            // 6. Section: Rotation Mode
            var turnHeader = CreateTMPText(panelObj.transform, "TurnHeader", "MODE DE ROTATION", 15, FontStyles.Bold, new Vector2(0f, -210f), new Vector2(500f, 25f));
            turnHeader.color = new Color(0.95f, 0.8f, 0.3f);

            var (turnBtn, turnBg, turnTxt) = CreateButton(panelObj.transform, "TurnButton", "Rotation : Snap 45° / Smooth", new Vector2(0f, -250f), new Vector2(480f, 44f));
            controller.m_TurnButton = turnBtn;
            controller.m_TurnButtonBg = turnBg;
            controller.m_TurnStatusText = turnTxt;

            // 7. Live Summary Status
            var summaryTxt = CreateTMPText(panelObj.transform, "SummaryStatus", "Mode: Teleport | Blink: ON | Oeillere: ON | Tourner: Snap", 14, FontStyles.Normal, new Vector2(0f, -300f), new Vector2(500f, 35f));
            summaryTxt.color = Color.white;
            controller.m_ModeSummaryText = summaryTxt;

            // 8. Desktop shortcuts hint
            var shortcutTxt = CreateTMPText(panelObj.transform, "ShortcutHint", "Clavier: [1]=Walk [2]=Teleport [3]=Dash [4]=Blink [5]=Oeillère [6]=Turn [Space]=Dash", 11, FontStyles.Italic, new Vector2(0f, -335f), new Vector2(500f, 25f));
            shortcutTxt.color = new Color(0.6f, 0.65f, 0.7f);

            return root;
        }

        private static TMP_Text CreateTMPText(Transform parent, string name, string text, float fontSize, FontStyles style, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static (Button, Image, TMP_Text) CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.22f, 0.25f, 0.9f);

            var btn = btnObj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            var textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            return (btn, img, tmp);
        }
    }
}
