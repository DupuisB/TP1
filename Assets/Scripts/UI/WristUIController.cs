using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using LOG8704.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LOG8704.UI
{
    [RequireComponent(typeof(Canvas))]
    public class WristUIController : MonoBehaviour
    {
        [Header("Canvas Group for Glance Visibility")]
        [SerializeField] private CanvasGroup m_CanvasGroup;
        [SerializeField] private bool m_ForceVisibleForDesktop = false;

        [Header("Debug Visibility")]
        [Tooltip("For debugging: keeps the wrist UI always visible and bypasses glance detection.")]
        [SerializeField] private bool m_DebugAlwaysVisible = false;

        [Tooltip("Base world scale of the wrist UI root.")]
        [SerializeField] private float m_BaseWorldScale = 0.000585f;

#pragma warning disable CS0414
        [Tooltip("For debugging: multiplies wrist UI scale to make it easier to inspect.")]
        [SerializeField] private float m_DebugScaleMultiplier = 1f;

        [Tooltip("For debugging: boosts panel background alpha for readability.")]
        [SerializeField] private bool m_DebugBoostPanelOpacity = false;

        [Tooltip("Panel alpha used when Debug Boost Panel Opacity is enabled.")]
        [SerializeField] private float m_DebugPanelAlpha = 1f;

        [Tooltip("For debugging: detaches UI from the left controller so you can move it freely.")]
        [SerializeField] private bool m_DebugDetachFromWrist = false;

        [Tooltip("World position applied once when enabling Debug Detach From Wrist.")]
        [SerializeField] private Vector3 m_DebugDetachedWorldPosition = new Vector3(0f, 1.35f, 0.55f);

        [Tooltip("World rotation applied once when enabling Debug Detach From Wrist.")]
        [SerializeField] private Vector3 m_DebugDetachedWorldEuler = new Vector3(0f, 180f, 0f);

        [Tooltip("For debugging: keep detached UI pinned in front of the camera.")]
        [SerializeField] private bool m_DebugPinInFrontOfCamera = false;

        [Tooltip("Distance from camera when Pin In Front Of Camera is enabled.")]
        [SerializeField] private float m_DebugCameraDistance = 0.55f;

        [Tooltip("Extra camera-space offset (x=right, y=up, z=forward) when pinned in front of camera.")]
        [SerializeField] private Vector3 m_DebugCameraOffset = new Vector3(0f, -0.04f, 0f);

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
        [SerializeField] private bool m_LogIconBinding = true;
#pragma warning restore CS0414
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

        [Header("Menu Hint")]
        [SerializeField] private TMP_Text m_GlanceHintText;

        // Visual Palette (Sci-Fi Holographic Glassmorphism)
        private readonly Color k_ActiveCardBg = new Color(0.06f, 0.22f, 0.14f, 0.95f);
        private readonly Color k_InactiveCardBg = new Color(0.09f, 0.12f, 0.18f, 0.90f);
        private readonly Color k_ActiveLedColor = new Color(0.22f, 0.94f, 0.49f, 1.0f);
        private readonly Color k_InactiveLedColor = new Color(0.25f, 0.30f, 0.38f, 0.8f);
        private readonly Color k_ActiveIconColor = Color.white;
        private readonly Color k_InactiveIconColor = new Color(0.60f, 0.68f, 0.76f, 0.85f);
        private readonly Color k_SnapTurnLedColor = new Color(0.21f, 0.74f, 0.98f, 1.0f);

        private Camera m_MainCamera;
        private bool m_Subscribed = false;
        private float m_NextPollTime;
        private static Sprite s_RoundedPanelSprite;

        public bool isMenuVisible => m_CanvasGroup != null && m_CanvasGroup.alpha > 0.4f;
        public float menuAlpha => m_CanvasGroup != null ? m_CanvasGroup.alpha : 0f;
        public Vector3 uiLocalPosition => m_UiLocalPosition;
        public Vector3 uiLocalEuler => m_UiLocalEuler;

        public void SetSprites(Sprite walk, Sprite tele, Sprite dash, Sprite blink, Sprite vig, Sprite turn)
        {
            m_WalkSprite = walk;
            m_TeleportSprite = tele;
            m_DashSprite = dash;
            m_BlinkSprite = blink;
            m_VignetteSprite = vig;
            m_TurnSprite = turn;
            ApplySerializedSpritesToIcons();
        }

        private void Start()
        {
            ApplyTransformOffset();
            ApplySerializedSpritesToIcons();

            if (m_CanvasGroup == null)
                m_CanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.interactable = false;
            m_CanvasGroup.blocksRaycasts = false;

            InitializeModeButtons();
            InitializeOptionToggles();
            TrySubscribe();
        }

        private void Update()
        {
            UpdateGlanceVisibility();

            if (!m_Subscribed)
                TrySubscribe();

            if (Time.time >= m_NextPollTime)
            {
                m_NextPollTime = Time.time + 0.1f;
                RefreshUI();
            }
        }

        public void ApplyTransformOffset()
        {
            transform.localPosition = m_UiLocalPosition;
            transform.localRotation = Quaternion.Euler(m_UiLocalEuler);
            transform.localScale = Vector3.one * Mathf.Max(0.00001f, m_BaseWorldScale);
        }

        public void SetTransformOffset(Vector3 pos, Vector3 rotEuler)
        {
            m_UiLocalPosition = pos;
            m_UiLocalEuler = rotEuler;
            ApplyTransformOffset();
        }

        private void InitializeModeButtons()
        {
            if (m_WalkButton != null) m_WalkButton.onClick.AddListener(() => SetLocomotion(TP1ComfortManager.LocomotionMode.SmoothMove));
            if (m_TeleportButton != null) m_TeleportButton.onClick.AddListener(() => SetLocomotion(TP1ComfortManager.LocomotionMode.Teleport));
            if (m_DashButton != null) m_DashButton.onClick.AddListener(() => SetLocomotion(TP1ComfortManager.LocomotionMode.Dash));
        }

        private void InitializeOptionToggles()
        {
            if (m_BlinkButton != null) m_BlinkButton.onClick.AddListener(() => ToggleOption(m => m.ToggleTeleportBlink()));
            if (m_VignetteButton != null) m_VignetteButton.onClick.AddListener(() => ToggleOption(m => m.ToggleVignette()));
            if (m_TurnButton != null) m_TurnButton.onClick.AddListener(() => ToggleOption(m => m.ToggleTurnMode()));
        }

        private void SetLocomotion(TP1ComfortManager.LocomotionMode mode)
        {
            var mgr = TP1ComfortManager.Instance;
            if (mgr != null) mgr.SetLocomotionMode(mode);
            DeselectUI();
        }

        private void ToggleOption(Action<TP1ComfortManager> toggle)
        {
            var mgr = TP1ComfortManager.Instance;
            if (mgr != null) toggle(mgr);
            DeselectUI();
        }

        private void ApplySerializedSpritesToIcons()
        {
            if (m_WalkIconImg != null && m_WalkSprite != null) m_WalkIconImg.sprite = m_WalkSprite;
            if (m_TeleportIconImg != null && m_TeleportSprite != null) m_TeleportIconImg.sprite = m_TeleportSprite;
            if (m_DashIconImg != null && m_DashSprite != null) m_DashIconImg.sprite = m_DashSprite;
            if (m_BlinkIconImg != null && m_BlinkSprite != null) m_BlinkIconImg.sprite = m_BlinkSprite;
            if (m_VignetteIconImg != null && m_VignetteSprite != null) m_VignetteIconImg.sprite = m_VignetteSprite;
            if (m_TurnIconImg != null && m_TurnSprite != null) m_TurnIconImg.sprite = m_TurnSprite;
        }

        private void UpdateGlanceVisibility()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
                m_ForceVisibleForDesktop = !m_ForceVisibleForDesktop;

            bool shouldShow = m_ForceVisibleForDesktop || m_DebugAlwaysVisible || IsLookingAtWatch();
            float targetAlpha = shouldShow ? 1f : 0f;

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = Mathf.MoveTowards(m_CanvasGroup.alpha, targetAlpha, Time.deltaTime * 6f);
                bool interactive = m_CanvasGroup.alpha > 0.1f;
                m_CanvasGroup.interactable = interactive;
                m_CanvasGroup.blocksRaycasts = interactive;
            }
        }

        private bool IsLookingAtWatch()
        {
            if (m_MainCamera == null)
            {
                m_MainCamera = Camera.main;
                if (m_MainCamera == null)
                {
                    var camObj = GameObject.FindWithTag("MainCamera");
                    if (camObj != null) m_MainCamera = camObj.GetComponent<Camera>();
                }
            }
            if (m_MainCamera == null) return false;

            Transform camT = m_MainCamera.transform;
            Transform controllerT = transform.parent != null ? transform.parent : transform;
            Vector3 controllerPos = controllerT.position;

            // 1. Supination / facing check: watch normal vector faces toward HMD
            Vector3 watchNormal = controllerT.TransformDirection(m_WatchFacingAxis.normalized);
            Vector3 toCamera = (camT.position - controllerPos).normalized;
            if (Vector3.Dot(watchNormal, toCamera) <= m_FacingThreshold)
                return false;

            // 2. Distance check: hand is within reading distance
            float dist = Vector3.Distance(controllerPos, camT.position);
            if (dist < m_MinViewingDistance || dist > m_MaxViewingDistance)
                return false;

            // 3. Elevation check: hand is raised to chest/chin level
            float heightBelowHmd = camT.position.y - controllerPos.y;
            if (heightBelowHmd <= -0.30f || heightBelowHmd >= m_MaxHeightBelowHmd)
                return false;

            // 4. Gaze check: player head direction is oriented toward the wrist
            Vector3 toWrist = (controllerPos - camT.position).normalized;
            return Vector3.Dot(camT.forward, toWrist) > 0.25f;
        }

        private void TrySubscribe()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr != null)
            {
                mgr.onLocomotionModeChanged += _ => RefreshUI();
                mgr.onTeleportBlinkToggled += _ => RefreshUI();
                mgr.onVignetteToggled += _ => RefreshUI();
                mgr.onTurnModeChanged += _ => RefreshUI();
                m_Subscribed = true;
                RefreshUI();
            }
        }

        private void DeselectUI()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        public void RefreshUI()
        {
            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr == null) return;

            bool isWalk = mgr.isSmoothMoveActive;
            bool isTeleport = mgr.isTeleportActive;
            bool isDash = mgr.isDashActive;
            bool blink = mgr.isTeleportBlinkEnabled;
            bool vignette = mgr.isVignetteActive;
            bool isSnap = mgr.isSnapTurnActive;

            UpdateCardState(m_WalkCardBg, m_WalkIconImg, m_WalkLedDot, m_WalkStatusText, isWalk, "CONTINU");
            UpdateCardState(m_TeleportCardBg, m_TeleportIconImg, m_TeleportLedDot, m_TeleportStatusText, isTeleport, "TÉLÉPORT");
            UpdateCardState(m_DashCardBg, m_DashIconImg, m_DashLedDot, m_DashStatusText, isDash, "DASH (0.2s)");
            UpdateCardState(m_BlinkCardBg, m_BlinkIconImg, m_BlinkLedDot, m_BlinkStatusText, blink, blink ? "BLINK: ON" : "BLINK: OFF");
            UpdateCardState(m_VignetteCardBg, m_VignetteIconImg, m_VignetteLedDot, m_VignetteStatusText, vignette, vignette ? "OEILLÈRE: ON" : "OEILLÈRE: OFF");

            if (m_TurnCardBg != null) m_TurnCardBg.color = isSnap ? k_InactiveCardBg : k_ActiveCardBg;
            if (m_TurnIconImg != null) m_TurnIconImg.color = k_ActiveIconColor;
            if (m_TurnLedDot != null) m_TurnLedDot.color = isSnap ? k_SnapTurnLedColor : k_ActiveLedColor;
            if (m_TurnStatusText != null)
                m_TurnStatusText.text = isSnap ? "<color=#35baf6>SNAP 45°</color>" : "<color=#38ef7d>SMOOTH</color>";
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

        public static WristUIController CreateWristUI(Transform wristAnchor, Vector3? initialPos = null, Vector3? initialRotEuler = null)
        {
            var root = new GameObject("Wrist_Comfort_UI");
            Vector3 pos = initialPos ?? new Vector3(-0.08f, 0.03f, -0.07f);
            Vector3 rotEuler = initialRotEuler ?? new Vector3(15f, -80f, -25f);

            if (wristAnchor != null)
            {
                root.transform.SetParent(wristAnchor, false);
                root.transform.localPosition = pos;
                root.transform.localRotation = Quaternion.Euler(rotEuler);
            }
            root.transform.localScale = Vector3.one * 0.000585f;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(430f, 380f);

            var canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            root.AddComponent<GraphicRaycaster>();

            var panelObj = new GameObject("BackgroundPanel");
            panelObj.transform.SetParent(root.transform, false);
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            var panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.07f, 0.11f, 0.96f);
            panelImg.sprite = GetOrCreateRoundedPanelSprite();
            panelImg.type = Image.Type.Sliced;

            var controller = root.AddComponent<WristUIController>();
            controller.m_CanvasGroup = canvasGroup;
            controller.m_UiLocalPosition = pos;
            controller.m_UiLocalEuler = rotEuler;
            controller.m_BaseWorldScale = 0.000585f;

            var moveHeader = CreateTMPText(panelObj.transform, "MoveHeader", "MODE DE DÉPLACEMENT", 13f, FontStyles.Bold, new Vector2(0f, 152f), new Vector2(430f, 20f));
            moveHeader.color = new Color(0.96f, 0.62f, 0.04f);

            Vector2 cardSize = new Vector2(128f, 90f);
            var (wBtn, wBg, wIcon, wLed, wTxt) = CreateIconCard(panelObj.transform, "Card_Walk", new Vector2(-138f, 90f), cardSize, "CONTINU");
            var (tBtn, tBg, tIcon, tLed, tTxt) = CreateIconCard(panelObj.transform, "Card_Teleport", new Vector2(0f, 90f), cardSize, "TÉLÉPORT");
            var (dBtn, dBg, dIcon, dLed, dTxt) = CreateIconCard(panelObj.transform, "Card_Dash", new Vector2(138f, 90f), cardSize, "DASH (0.2s)");

            controller.m_WalkButton = wBtn; controller.m_WalkCardBg = wBg; controller.m_WalkIconImg = wIcon; controller.m_WalkLedDot = wLed; controller.m_WalkStatusText = wTxt;
            controller.m_TeleportButton = tBtn; controller.m_TeleportCardBg = tBg; controller.m_TeleportIconImg = tIcon; controller.m_TeleportLedDot = tLed; controller.m_TeleportStatusText = tTxt;
            controller.m_DashButton = dBtn; controller.m_DashCardBg = dBg; controller.m_DashIconImg = dIcon; controller.m_DashLedDot = dLed; controller.m_DashStatusText = dTxt;

            var optHeader = CreateTMPText(panelObj.transform, "OptHeader", "OPTIONS DE CONFORT & ROTATION", 13f, FontStyles.Bold, new Vector2(0f, 28f), new Vector2(430f, 20f));
            optHeader.color = new Color(0.96f, 0.62f, 0.04f);

            var (bBtn, bBg, bIcon, bLed, bTxt) = CreateIconCard(panelObj.transform, "Card_Blink", new Vector2(-138f, -40f), cardSize, "BLINK: ON");
            var (vBtn, vBg, vIcon, vLed, vTxt) = CreateIconCard(panelObj.transform, "Card_Vignette", new Vector2(0f, -40f), cardSize, "OEILLÈRE: ON");
            var (rBtn, rBg, rIcon, rLed, rTxt) = CreateIconCard(panelObj.transform, "Card_Turn", new Vector2(138f, -40f), cardSize, "SNAP 45°");

            controller.m_BlinkButton = bBtn; controller.m_BlinkCardBg = bBg; controller.m_BlinkIconImg = bIcon; controller.m_BlinkLedDot = bLed; controller.m_BlinkStatusText = bTxt;
            controller.m_VignetteButton = vBtn; controller.m_VignetteCardBg = vBg; controller.m_VignetteIconImg = vIcon; controller.m_VignetteLedDot = vLed; controller.m_VignetteStatusText = vTxt;
            controller.m_TurnButton = rBtn; controller.m_TurnCardBg = rBg; controller.m_TurnIconImg = rIcon; controller.m_TurnLedDot = rLed; controller.m_TurnStatusText = rTxt;

            controller.m_GlanceHintText = CreateTMPText(panelObj.transform, "GlanceHint", "Tourner le poignet vers soi pour ouvrir • [M] Touche Desktop", 11f, FontStyles.Italic, new Vector2(0f, -128f), new Vector2(430f, 20f));
            controller.m_GlanceHintText.color = new Color(0.45f, 0.55f, 0.65f);

            return controller;
        }

        private static (Button, Image, Image, Image, TMP_Text) CreateIconCard(Transform parent, string name, Vector2 pos, Vector2 size, string initialLabel)
        {
            var cardObj = new GameObject(name);
            cardObj.transform.SetParent(parent, false);
            var rect = cardObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var bgImg = cardObj.AddComponent<Image>();
            bgImg.color = new Color(0.09f, 0.12f, 0.18f, 0.90f);

            var btn = cardObj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(cardObj.transform, false);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(0f, 8f);
            iconRect.sizeDelta = new Vector2(50f, 50f);

            var iconImg = iconObj.AddComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var ledObj = new GameObject("LED");
            ledObj.transform.SetParent(cardObj.transform, false);
            var ledRect = ledObj.AddComponent<RectTransform>();
            ledRect.anchorMin = Vector2.one;
            ledRect.anchorMax = Vector2.one;
            ledRect.anchoredPosition = new Vector2(-10f, -10f);
            ledRect.sizeDelta = new Vector2(8f, 8f);

            var ledImg = ledObj.AddComponent<Image>();
            ledImg.color = new Color(0.25f, 0.30f, 0.38f, 0.8f);
            ledImg.raycastTarget = false;

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(cardObj.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchoredPosition = new Vector2(0f, -28f);
            labelRect.sizeDelta = new Vector2(136f, 24f);

            var label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = initialLabel;
            label.fontSize = 12f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.60f, 0.68f, 0.76f, 0.85f);
            label.raycastTarget = false;

            return (btn, bgImg, iconImg, ledImg, label);
        }

        private static TMP_Text CreateTMPText(Transform parent, string name, string text, float fontSize, FontStyles style, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
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

        private static Sprite GetOrCreateRoundedPanelSprite()
        {
            if (s_RoundedPanelSprite != null) return s_RoundedPanelSprite;

            const int texSize = 64, radius = 10;
            var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color clear = new Color(1f, 1f, 1f, 0f), solid = Color.white;
            float innerMin = radius, innerMax = texSize - 1 - radius;

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    if ((x >= innerMin && x <= innerMax) || (y >= innerMin && y <= innerMax))
                    {
                        tex.SetPixel(x, y, solid);
                        continue;
                    }
                    float cx = x < innerMin ? innerMin : innerMax;
                    float cy = y < innerMin ? innerMin : innerMax;
                    float dx = x - cx, dy = y - cy;
                    tex.SetPixel(x, y, (dx * dx + dy * dy) <= radius * radius ? solid : clear);
                }
            }
            tex.Apply();
            s_RoundedPanelSprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return s_RoundedPanelSprite;
        }
    }
}
