using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using LOG8704.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LOG8704.UI
{
    /// <summary>
    /// Forearm-mounted holographic VR interface displaying icon-driven controls
    /// for locomotion (Walk, Teleport, Dash), comfort options (Blink, Vignette),
    /// and rotation mode (Snap vs Smooth).
    /// Features glance-based visibility: hidden by default and smoothly reveals itself
    /// when the user turns their left wrist towards their eyes (smartwatch / Pip-Boy style).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class WristUIController : MonoBehaviour
    {
        [Header("Canvas Group for Glance Visibility")]
        [SerializeField] private CanvasGroup m_CanvasGroup;
        [SerializeField] private bool m_ForceVisibleForDesktop = false;

        [Header("Transform Placement (Smartwatch Offset)")]
        [Tooltip("Local position relative to Left Controller (X: Left[-]/Right[+], Y: Up[+]/Down[-], Z: Forward[+]/Back[-] along forearm). Defaults to dorsal smartwatch position.")]
        [SerializeField] private Vector3 m_UiLocalPosition = new Vector3(-0.08f, 0.03f, -0.07f);

        [Tooltip("Local Euler angles relative to Left Controller (X: Pitch, Y: Yaw, Z: Roll). Quarter-turn to dorsal watch face is near Yaw -80° to -90°.")]
        [SerializeField] private Vector3 m_UiLocalEuler = new Vector3(15f, -80f, -25f);

        [Tooltip("When enabled, changes made to position/rotation in the Inspector take effect immediately in real time, even during Play Mode!")]
        [SerializeField] private bool m_LiveSyncInEditor = true;

        [Header("Glance Detection (Controller-Based)")]
        [Tooltip("Local direction on the Left Controller pointing towards the watch face. For the Left Controller, -X is the outer/dorsal wrist, slightly +Y for natural grip angle.")]
        [SerializeField] private Vector3 m_WatchFacingAxis = new Vector3(-1f, 0.2f, 0f);

        [Tooltip("Minimum dot product between the watch facing axis and vector to HMD (0.35 = ~70° cone).")]
        [SerializeField] private float m_FacingThreshold = 0.35f;

        [Tooltip("Maximum distance in meters between controller and HMD.")]
        [SerializeField] private float m_MaxViewingDistance = 0.85f;

        [Tooltip("Minimum distance in meters between controller and HMD.")]
        [SerializeField] private float m_MinViewingDistance = 0.18f;

        [Tooltip("Maximum vertical drop below HMD in meters (rejects hands resting near hips/legs).")]
        [SerializeField] private float m_MaxHeightBelowHmd = 0.65f;

        [Header("Sprites for Icons")]
        [SerializeField] private Sprite m_WalkSprite;
        [SerializeField] private Sprite m_TeleportSprite;
        [SerializeField] private Sprite m_DashSprite;
        [SerializeField] private Sprite m_BlinkSprite;
        [SerializeField] private Sprite m_VignetteSprite;
        [SerializeField] private Sprite m_TurnSprite;

        [Header("Locomotion Mode Icon Cards")]
        [SerializeField] private Button m_WalkButton;
        [SerializeField] private Image m_WalkCardBg;
        [SerializeField] private Image m_WalkIconImg;
        [SerializeField] private Image m_WalkLedDot;
        [SerializeField] private TMP_Text m_WalkStatusText;

        [SerializeField] private Button m_TeleportButton;
        [SerializeField] private Image m_TeleportCardBg;
        [SerializeField] private Image m_TeleportIconImg;
        [SerializeField] private Image m_TeleportLedDot;
        [SerializeField] private TMP_Text m_TeleportStatusText;

        [SerializeField] private Button m_DashButton;
        [SerializeField] private Image m_DashCardBg;
        [SerializeField] private Image m_DashIconImg;
        [SerializeField] private Image m_DashLedDot;
        [SerializeField] private TMP_Text m_DashStatusText;

        [Header("Option Icon Cards")]
        [SerializeField] private Button m_BlinkButton;
        [SerializeField] private Image m_BlinkCardBg;
        [SerializeField] private Image m_BlinkIconImg;
        [SerializeField] private Image m_BlinkLedDot;
        [SerializeField] private TMP_Text m_BlinkStatusText;

        [SerializeField] private Button m_VignetteButton;
        [SerializeField] private Image m_VignetteCardBg;
        [SerializeField] private Image m_VignetteIconImg;
        [SerializeField] private Image m_VignetteLedDot;
        [SerializeField] private TMP_Text m_VignetteStatusText;

        [SerializeField] private Button m_TurnButton;
        [SerializeField] private Image m_TurnCardBg;
        [SerializeField] private Image m_TurnIconImg;
        [SerializeField] private Image m_TurnLedDot;
        [SerializeField] private TMP_Text m_TurnStatusText;

        [Header("HUD Status Strip")]
        [SerializeField] private TMP_Text m_HudSummaryText;
        [SerializeField] private TMP_Text m_GlanceHintText;

        // Visual Palette (Sci-Fi Holographic Glassmorphism)
        private readonly Color k_ActiveCardBg = new Color(0.06f, 0.22f, 0.14f, 0.95f);    // Deep emerald glass
        private readonly Color k_InactiveCardBg = new Color(0.09f, 0.12f, 0.18f, 0.90f);  // Dark slate glass
        private readonly Color k_ActiveLedColor = new Color(0.22f, 0.94f, 0.49f, 1.0f);   // Neon Green LED
        private readonly Color k_InactiveLedColor = new Color(0.25f, 0.30f, 0.38f, 0.8f); // Dim charcoal LED
        private readonly Color k_ActiveIconColor = Color.white;
        private readonly Color k_InactiveIconColor = new Color(0.60f, 0.68f, 0.76f, 0.85f);
        private readonly Color k_SnapTurnLedColor = new Color(0.21f, 0.74f, 0.98f, 1.0f); // Neon Cyan for Snap

        private Camera m_MainCamera;
        private bool m_Subscribed = false;
        private float m_NextPollTime;

        public void SetSprites(Sprite walk, Sprite tele, Sprite dash, Sprite blink, Sprite vig, Sprite turn)
        {
            m_WalkSprite = walk;
            m_TeleportSprite = tele;
            m_DashSprite = dash;
            m_BlinkSprite = blink;
            m_VignetteSprite = vig;
            m_TurnSprite = turn;

            if (m_WalkIconImg != null && walk != null) m_WalkIconImg.sprite = walk;
            if (m_TeleportIconImg != null && tele != null) m_TeleportIconImg.sprite = tele;
            if (m_DashIconImg != null && dash != null) m_DashIconImg.sprite = dash;
            if (m_BlinkIconImg != null && blink != null) m_BlinkIconImg.sprite = blink;
            if (m_VignetteIconImg != null && vig != null) m_VignetteIconImg.sprite = vig;
            if (m_TurnIconImg != null && turn != null) m_TurnIconImg.sprite = turn;
        }

        private void Start()
        {
            ApplyTransformOffset();

            if (m_CanvasGroup == null)
                m_CanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            // Hidden by default until wrist is turned
            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.interactable = false;
            m_CanvasGroup.blocksRaycasts = false;

            // Bind click listeners
            if (m_WalkButton != null) m_WalkButton.onClick.AddListener(OnWalkClicked);
            if (m_TeleportButton != null) m_TeleportButton.onClick.AddListener(OnTeleportClicked);
            if (m_DashButton != null) m_DashButton.onClick.AddListener(OnDashClicked);
            if (m_BlinkButton != null) m_BlinkButton.onClick.AddListener(OnBlinkClicked);
            if (m_VignetteButton != null) m_VignetteButton.onClick.AddListener(OnVignetteClicked);
            if (m_TurnButton != null) m_TurnButton.onClick.AddListener(OnTurnClicked);

            TrySubscribe();
        }

        private void Update()
        {
#if UNITY_EDITOR
            UpdateEditorTransformSync();
#endif

            UpdateGlanceVisibility();

            if (!m_Subscribed)
            {
                TrySubscribe();
            }

            if (Time.time >= m_NextPollTime)
            {
                m_NextPollTime = Time.time + 0.1f;
                RefreshUI();
            }
        }

        public Vector3 uiLocalPosition => m_UiLocalPosition;
        public Vector3 uiLocalEuler => m_UiLocalEuler;

        public void ApplyTransformOffset()
        {
            transform.localPosition = m_UiLocalPosition;
            transform.localRotation = Quaternion.Euler(m_UiLocalEuler);
        }

        public void SetTransformOffset(Vector3 pos, Vector3 rotEuler)
        {
            m_UiLocalPosition = pos;
            m_UiLocalEuler = rotEuler;
            ApplyTransformOffset();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyTransformOffset();
        }

        private void UpdateEditorTransformSync()
        {
            if (!m_LiveSyncInEditor) return;
            if (Application.isPlaying) return; // Prevent VR controller tracking updates from trampling inspector edits during play mode

            // Keep Inspector fields and Scene Transform synchronized
            // If the user drags the Scene Gizmo, update the inspector values
            if (transform.hasChanged)
            {
                m_UiLocalPosition = transform.localPosition;
                m_UiLocalEuler = transform.localRotation.eulerAngles;
                transform.hasChanged = false;
            }
            else if (transform.localPosition != m_UiLocalPosition || transform.localRotation != Quaternion.Euler(m_UiLocalEuler))
            {
                // If the user typed new numbers into the Inspector, update the transform
                ApplyTransformOffset();
            }
        }
#endif

        private void UpdateGlanceVisibility()
        {
            // Keyboard shortcut [M] allows desktop testers in Unity Editor to force-show the menu
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            {
                m_ForceVisibleForDesktop = !m_ForceVisibleForDesktop;
                Debug.Log($"[WristUIController] Desktop force-visibility toggled: {m_ForceVisibleForDesktop}");
            }

            if (m_MainCamera == null)
            {
                m_MainCamera = Camera.main;
                if (m_MainCamera == null)
                {
                    var camObj = GameObject.FindWithTag("MainCamera");
                    if (camObj != null) m_MainCamera = camObj.GetComponent<Camera>();
                }
            }

            bool isLookingAtWrist = false;
            if (m_MainCamera != null)
            {
                Transform camT = m_MainCamera.transform;
                // Anchor to the Left Controller transform (parent), completely decoupled from UI offsets/rotations
                Transform controllerT = transform.parent != null ? transform.parent : transform;
                Vector3 controllerPos = controllerT.position;

                // 1. Check watch facing direction (Supination):
                // -X on the Left Controller points towards the outer/dorsal wrist where the watch sits.
                Vector3 watchWorldNormal = controllerT.TransformDirection(m_WatchFacingAxis.normalized);
                Vector3 toCamera = (camT.position - controllerPos).normalized;
                float facingDot = Vector3.Dot(watchWorldNormal, toCamera);
                bool isFacingCamera = facingDot > m_FacingThreshold;

                // 2. Check viewing distance (Hand held in front of chest/face, not resting at side or stretched out):
                float distanceToHmd = Vector3.Distance(controllerPos, camT.position);
                bool inViewingDistance = distanceToHmd >= m_MinViewingDistance && distanceToHmd <= m_MaxViewingDistance;

                // 3. Check height (Hand raised to chest/chin level, not down at hips):
                float heightBelowHmd = camT.position.y - controllerPos.y;
                bool atViewingHeight = heightBelowHmd > -0.30f && heightBelowHmd < m_MaxHeightBelowHmd;

                // 4. Relaxed gaze constraint (Player is looking generally in the wrist's direction):
                Vector3 toWrist = (controllerPos - camT.position).normalized;
                float gazeDot = Vector3.Dot(camT.forward, toWrist);
                bool isLookingTowardWrist = gazeDot > 0.25f;

                isLookingAtWrist = isFacingCamera && inViewingDistance && atViewingHeight && isLookingTowardWrist;
            }

            bool shouldBeVisible = isLookingAtWrist || m_ForceVisibleForDesktop;
            float targetAlpha = shouldBeVisible ? 1f : 0f;

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = Mathf.MoveTowards(m_CanvasGroup.alpha, targetAlpha, Time.deltaTime * 6f);
                bool interactive = m_CanvasGroup.alpha > 0.1f;
                m_CanvasGroup.interactable = interactive;
                m_CanvasGroup.blocksRaycasts = interactive;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform controllerT = transform.parent != null ? transform.parent : transform;
            Gizmos.color = Color.cyan;
            Vector3 normal = controllerT.TransformDirection(m_WatchFacingAxis.normalized);
            Gizmos.DrawRay(controllerT.position, normal * 0.15f);
        }
#endif

        private void TrySubscribe()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null)
            {
                mgr.onLocomotionModeChanged += (mode) => RefreshUI();
                mgr.onTeleportBlinkToggled += (enabled) => RefreshUI();
                mgr.onVignetteToggled += (enabled) => RefreshUI();
                mgr.onTurnModeChanged += (mode) => RefreshUI();
                m_Subscribed = true;
                RefreshUI();
            }
        }

        // ==========================================
        // BUTTON HANDLERS
        // ==========================================

        private void OnWalkClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null) mgr.SetLocomotionMode(TP1ComfortManager.LocomotionMode.SmoothMove);
            DeselectUI();
        }

        private void OnTeleportClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null) mgr.SetLocomotionMode(TP1ComfortManager.LocomotionMode.Teleport);
            DeselectUI();
        }

        private void OnDashClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null) mgr.SetLocomotionMode(TP1ComfortManager.LocomotionMode.Dash);
            DeselectUI();
        }

        private void OnBlinkClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null) mgr.ToggleTeleportBlink();
            DeselectUI();
        }

        private void OnVignetteClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null) mgr.ToggleVignette();
            DeselectUI();
        }

        private void OnTurnClicked()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null) mgr.ToggleTurnMode();
            DeselectUI();
        }

        private void DeselectUI()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

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

            // Update Mode Cards
            UpdateCardState(m_WalkCardBg, m_WalkIconImg, m_WalkLedDot, m_WalkStatusText, isWalk, "CONTINU");
            UpdateCardState(m_TeleportCardBg, m_TeleportIconImg, m_TeleportLedDot, m_TeleportStatusText, isTeleport, "TÉLÉPORT");
            UpdateCardState(m_DashCardBg, m_DashIconImg, m_DashLedDot, m_DashStatusText, isDash, "DASH (0.2s)");

            // Update Option Cards
            UpdateCardState(m_BlinkCardBg, m_BlinkIconImg, m_BlinkLedDot, m_BlinkStatusText, blink, blink ? "BLINK: ON" : "BLINK: OFF");
            UpdateCardState(m_VignetteCardBg, m_VignetteIconImg, m_VignetteLedDot, m_VignetteStatusText, vignette, vignette ? "OEILLÈRE: ON" : "OEILLÈRE: OFF");

            // Turn Card
            if (m_TurnCardBg != null) m_TurnCardBg.color = isSnap ? k_InactiveCardBg : k_ActiveCardBg;
            if (m_TurnIconImg != null) m_TurnIconImg.color = isSnap ? k_ActiveIconColor : k_ActiveIconColor;
            if (m_TurnLedDot != null) m_TurnLedDot.color = isSnap ? k_SnapTurnLedColor : k_ActiveLedColor;
            if (m_TurnStatusText != null)
            {
                m_TurnStatusText.text = isSnap ? "<color=#35baf6>SNAP 45°</color>" : "<color=#38ef7d>SMOOTH</color>";
            }

            // HUD Summary Strip
            if (m_HudSummaryText != null)
            {
                string modeStr = isWalk ? "<color=#38ef7d>Walk</color>" : (isDash ? "<color=#38ef7d>Dash</color>" : "<color=#38ef7d>Teleport</color>");
                string blinkStr = blink ? "<color=#38ef7d>Blink</color>" : "<color=#94a3b8>Instant</color>";
                string vigStr = vignette ? "<color=#38ef7d>ON</color>" : "<color=#f87171>OFF</color>";
                string turnStr = isSnap ? "<color=#35baf6>Snap 45°</color>" : "<color=#38ef7d>Smooth</color>";

                m_HudSummaryText.text = $"MODE: <b>{modeStr}</b> • BLINK: <b>{blinkStr}</b> • VIGNETTE: <b>{vigStr}</b> • ROTATION: <b>{turnStr}</b>";
            }
        }

        private void UpdateCardState(Image bg, Image icon, Image led, TMP_Text label, bool active, string text)
        {
            if (bg != null) bg.color = active ? k_ActiveCardBg : k_InactiveCardBg;
            if (icon != null) icon.color = active ? k_ActiveIconColor : k_InactiveIconColor;
            if (led != null) led.color = active ? k_ActiveLedColor : k_InactiveLedColor;
            if (label != null)
            {
                string colorHex = active ? "#38ef7d" : "#94a3b8";
                label.text = $"<color={colorHex}><b>{text}</b></color>";
            }
        }

        /// <summary>
        /// Procedural generator to construct the Forearm Holographic Gauntlet UI hierarchy.
        /// </summary>
        public static WristUIController CreateWristUI(Transform wristAnchor, Vector3? initialPos = null, Vector3? initialRotEuler = null)
        {
            var root = new GameObject("Wrist_Comfort_UI");
            Vector3 pos = initialPos ?? new Vector3(-0.08f, 0.03f, -0.07f);
            Vector3 rotEuler = initialRotEuler ?? new Vector3(15f, -80f, -25f);

            if (wristAnchor != null)
            {
                root.transform.SetParent(wristAnchor, false);
                // Positioned on the dorsal side of the left wrist (smartwatch location)
                // Rotated a quarter-turn so the screen faces directly towards the user's eyes when turning wrist inward to check watch
                root.transform.localPosition = pos;
                root.transform.localRotation = Quaternion.Euler(rotEuler);
            }
            root.transform.localScale = Vector3.one * 0.00045f;

            // Canvas setup
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(460f, 400f);

            var canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f; // Starts hidden until wrist glance

            // Graphic raycasters for controller interaction
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            root.AddComponent<GraphicRaycaster>();

            // Dark semi-transparent backdrop panel with border
            var panelObj = new GameObject("BackgroundPanel");
            panelObj.transform.SetParent(root.transform, false);
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            var panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.07f, 0.11f, 0.96f); // Deep dark obsidian

            var controller = root.AddComponent<WristUIController>();
            controller.m_CanvasGroup = canvasGroup;
            controller.m_UiLocalPosition = pos;
            controller.m_UiLocalEuler = rotEuler;

            // 1. Futuristic Header Title
            var title = CreateTMPText(panelObj.transform, "HeaderTitle", "GAUNTLET OS // LOG8704", 17, FontStyles.Bold, new Vector2(0f, 180f), new Vector2(460f, 26f));
            title.color = new Color(0.22f, 0.74f, 0.98f); // Neon Cyan
            title.alignment = TextAlignmentOptions.Left;

            var subTitle = CreateTMPText(panelObj.transform, "SubTitle", "CONTRÔLE LOCOMOTION & CONFORT VR", 10, FontStyles.Normal, new Vector2(0f, 158f), new Vector2(460f, 18f));
            subTitle.color = new Color(0.55f, 0.65f, 0.75f);
            subTitle.alignment = TextAlignmentOptions.Left;

            // 2. Section 1: Déplacement Header
            var moveHeader = CreateTMPText(panelObj.transform, "MoveHeader", "MODE DE DÉPLACEMENT", 12, FontStyles.Bold, new Vector2(0f, 130f), new Vector2(460f, 20f));
            moveHeader.color = new Color(0.96f, 0.62f, 0.04f); // Warm Amber
            moveHeader.alignment = TextAlignmentOptions.Left;

            // Row 1: 3 Icon Cards (Walk, Teleport, Dash)
            float cardY1 = 65f;
            var (walkBtn, walkBg, walkIcon, walkLed, walkTxt) = CreateIconCard(panelObj.transform, "Card_Walk", new Vector2(-150f, cardY1), new Vector2(140f, 95f), "CONTINU");
            var (teleBtn, teleBg, teleIcon, teleLed, teleTxt) = CreateIconCard(panelObj.transform, "Card_Teleport", new Vector2(0f, cardY1), new Vector2(140f, 95f), "TÉLÉPORT");
            var (dashBtn, dashBg, dashIcon, dashLed, dashTxt) = CreateIconCard(panelObj.transform, "Card_Dash", new Vector2(150f, cardY1), new Vector2(140f, 95f), "DASH (0.2s)");

            controller.m_WalkButton = walkBtn;
            controller.m_WalkCardBg = walkBg;
            controller.m_WalkIconImg = walkIcon;
            controller.m_WalkLedDot = walkLed;
            controller.m_WalkStatusText = walkTxt;

            controller.m_TeleportButton = teleBtn;
            controller.m_TeleportCardBg = teleBg;
            controller.m_TeleportIconImg = teleIcon;
            controller.m_TeleportLedDot = teleLed;
            controller.m_TeleportStatusText = teleTxt;

            controller.m_DashButton = dashBtn;
            controller.m_DashCardBg = dashBg;
            controller.m_DashIconImg = dashIcon;
            controller.m_DashLedDot = dashLed;
            controller.m_DashStatusText = dashTxt;

            // 3. Section 2: Confort & Rotation Header
            var optHeader = CreateTMPText(panelObj.transform, "OptHeader", "OPTIONS DE CONFORT & ROTATION", 12, FontStyles.Bold, new Vector2(0f, 0f), new Vector2(460f, 20f));
            optHeader.color = new Color(0.96f, 0.62f, 0.04f);
            optHeader.alignment = TextAlignmentOptions.Left;

            // Row 2: 3 Icon Cards (Blink, Vignette, Turn)
            float cardY2 = -65f;
            var (blinkBtn, blinkBg, blinkIcon, blinkLed, blinkTxt) = CreateIconCard(panelObj.transform, "Card_Blink", new Vector2(-150f, cardY2), new Vector2(140f, 95f), "BLINK: ON");
            var (vigBtn, vigBg, vigIcon, vigLed, vigTxt) = CreateIconCard(panelObj.transform, "Card_Vignette", new Vector2(0f, cardY2), new Vector2(140f, 95f), "OEILLÈRE: ON");
            var (turnBtn, turnBg, turnIcon, turnLed, turnTxt) = CreateIconCard(panelObj.transform, "Card_Turn", new Vector2(150f, cardY2), new Vector2(140f, 95f), "SNAP 45°");

            controller.m_BlinkButton = blinkBtn;
            controller.m_BlinkCardBg = blinkBg;
            controller.m_BlinkIconImg = blinkIcon;
            controller.m_BlinkLedDot = blinkLed;
            controller.m_BlinkStatusText = blinkTxt;

            controller.m_VignetteButton = vigBtn;
            controller.m_VignetteCardBg = vigBg;
            controller.m_VignetteIconImg = vigIcon;
            controller.m_VignetteLedDot = vigLed;
            controller.m_VignetteStatusText = vigTxt;

            controller.m_TurnButton = turnBtn;
            controller.m_TurnCardBg = turnBg;
            controller.m_TurnIconImg = turnIcon;
            controller.m_TurnLedDot = turnLed;
            controller.m_TurnStatusText = turnTxt;

            // 4. Bottom HUD Summary Strip
            var hud = CreateTMPText(panelObj.transform, "HUD_Summary", "MODE: Teleport • BLINK: ON • VIGNETTE: ON • ROTATION: Snap", 11, FontStyles.Normal, new Vector2(0f, -140f), new Vector2(460f, 24f));
            hud.color = Color.white;
            controller.m_HudSummaryText = hud;

            // 5. Glance Hint
            var hint = CreateTMPText(panelObj.transform, "GlanceHint", "Tourner le poignet vers soi pour ouvrir • [M] Touche Desktop", 10, FontStyles.Italic, new Vector2(0f, -170f), new Vector2(460f, 20f));
            hint.color = new Color(0.45f, 0.55f, 0.65f);
            controller.m_GlanceHintText = hint;

            return controller;
        }

        private static (Button, Image, Image, Image, TMP_Text) CreateIconCard(Transform parent, string name, Vector2 pos, Vector2 size, string initialLabel)
        {
            var cardObj = new GameObject(name);
            cardObj.transform.SetParent(parent, false);
            var rect = cardObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            // Background Card
            var bgImg = cardObj.AddComponent<Image>();
            bgImg.color = new Color(0.09f, 0.12f, 0.18f, 0.90f);

            var btn = cardObj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            // Center Icon Image
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(cardObj.transform, false);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(0f, 8f);
            iconRect.sizeDelta = new Vector2(48f, 48f);

            var iconImg = iconObj.AddComponent<Image>();
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;

            // Glowing LED Indicator Dot (Upper Right)
            var ledObj = new GameObject("LED");
            ledObj.transform.SetParent(cardObj.transform, false);
            var ledRect = ledObj.AddComponent<RectTransform>();
            ledRect.anchorMin = new Vector2(1f, 1f);
            ledRect.anchorMax = new Vector2(1f, 1f);
            ledRect.anchoredPosition = new Vector2(-10f, -10f);
            ledRect.sizeDelta = new Vector2(8f, 8f);

            var ledImg = ledObj.AddComponent<Image>();
            ledImg.color = new Color(0.25f, 0.30f, 0.38f, 0.8f);
            ledImg.raycastTarget = false;

            // Bottom Sub-Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(cardObj.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchoredPosition = new Vector2(0f, -28f);
            labelRect.sizeDelta = new Vector2(130f, 22f);

            var tmp = labelObj.AddComponent<TextMeshProUGUI>();
            tmp.text = initialLabel;
            tmp.fontSize = 11;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.60f, 0.68f, 0.76f, 0.85f);
            tmp.raycastTarget = false;

            return (btn, bgImg, iconImg, ledImg, tmp);
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
    }
}
