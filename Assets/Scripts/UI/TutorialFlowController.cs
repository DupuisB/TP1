using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LOG8704.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LOG8704.UI
{
    public class TutorialFlowController : MonoBehaviour
    {
        public enum Step
        {
            OpenWristMenu = 0,
            TeleportWithBlink = 1,
            PerformDash = 2,
            SmoothMoveWithVignette = 3,
            PortalUnlocked = 4
        }

        [Header("UI Display Board")]
        [SerializeField] private TMP_Text m_StepNumberText;
        [SerializeField] private TMP_Text m_StepTitleText;
        [SerializeField] private TMP_Text m_StepInstructionText;
        [SerializeField] private TMP_Text m_StepProgressText;
        [SerializeField] private Button m_SkipButton;

        [Header("Checklist Indicators")]
        [SerializeField] private TMP_Text m_CheckWristText;
        [SerializeField] private TMP_Text m_CheckTeleportText;
        [SerializeField] private TMP_Text m_CheckDashText;
        [SerializeField] private TMP_Text m_CheckWalkText;

        [Header("Scene References")]
        [SerializeField] private GameObject m_TeleportTargetZone;
        [SerializeField] private SceneTeleportPortal m_ExitPortal;
        [SerializeField] private TMP_Text m_PortalStatusBillboard;

        public GameObject teleportTargetZone { get => m_TeleportTargetZone; set => m_TeleportTargetZone = value; }
        public SceneTeleportPortal exitPortal { get => m_ExitPortal; set => m_ExitPortal = value; }
        public TMP_Text portalStatusBillboard { get => m_PortalStatusBillboard; set => m_PortalStatusBillboard = value; }

        private Step m_CurrentStep = Step.OpenWristMenu;
        private float m_SmoothMoveAccumulatedDistance = 0f;
        private Vector3 m_LastPlayerPosition;
        private bool m_WasDashing = false;
        private bool m_WristButtonsHooked = false;
        private float m_StepStartTime = 0f;
        private float m_WristGazeDwellTime = 0f;
        private const float k_RequiredWristGazeDwell = 1.2f;
        private const float k_StartupGracePeriod = 1.5f;

        public Step currentStep => m_CurrentStep;

        private void Start()
        {
            FindSceneReferencesIfNeeded();
            WireSkipButton();
            GatePortal(true);
            m_StepStartTime = Time.time;
            m_WristGazeDwellTime = 0f;
            SetStep(Step.OpenWristMenu);
        }

        private void Update()
        {
            if (!m_WristButtonsHooked)
                TryWireWristButtons();

            TrackLocomotionStepProgress();
        }

        private void OnDestroy()
        {
            var mgr = TP1ComfortManager.Instance;
            if (mgr != null)
                mgr.onLocomotionModeChanged -= OnLocomotionModeChanged;
        }

        public void FindSceneReferencesIfNeeded()
        {
            if (m_ExitPortal == null)
                m_ExitPortal = FindFirstObjectByType<SceneTeleportPortal>();

            if (m_TeleportTargetZone == null)
                m_TeleportTargetZone = GameObject.Find("Tutorial_Target_Zone") ?? GameObject.Find("TutorialTeleportZone");

            if (m_TeleportTargetZone != null)
            {
                var trigger = m_TeleportTargetZone.GetComponent<TutorialTeleportZoneTrigger>() ?? m_TeleportTargetZone.AddComponent<TutorialTeleportZoneTrigger>();
                trigger.SetController(this);
            }

            var mgr = TP1ComfortManager.Instance;
            if (mgr != null)
            {
                mgr.onLocomotionModeChanged -= OnLocomotionModeChanged;
                mgr.onLocomotionModeChanged += OnLocomotionModeChanged;
            }
        }

        public void SkipTutorial() => SetStep(Step.PortalUnlocked);

        public void RestartTutorial()
        {
            m_SmoothMoveAccumulatedDistance = 0f;
            m_StepStartTime = Time.time;
            m_WristGazeDwellTime = 0f;
            SetStep(Step.OpenWristMenu);
        }

        public void OnAnyWristButtonPressed()
        {
            if (m_CurrentStep == Step.OpenWristMenu && Time.time >= m_StepStartTime + 0.5f)
                SetStep(Step.TeleportWithBlink);
        }

        public void OnTeleportZoneEntered()
        {
            if (m_CurrentStep == Step.TeleportWithBlink)
                SetStep(Step.PerformDash);
        }

        private void OnLocomotionModeChanged(TP1ComfortManager.LocomotionMode mode)
        {
            // Only advance if past startup grace period AND wrist menu is visibly open
            if (m_CurrentStep == Step.OpenWristMenu && Time.time >= m_StepStartTime + k_StartupGracePeriod)
            {
                var wrist = FindFirstObjectByType<WristUIController>();
                if (wrist != null && wrist.isMenuVisible)
                    SetStep(Step.TeleportWithBlink);
            }
        }

        private void TrackLocomotionStepProgress()
        {
            switch (m_CurrentStep)
            {
                case Step.OpenWristMenu:
                    EvaluateWristStep();
                    break;
                case Step.TeleportWithBlink:
                    EvaluateTeleportStep();
                    break;
                case Step.PerformDash:
                    EvaluateDashStep();
                    break;
                case Step.SmoothMoveWithVignette:
                    EvaluateWalkStep();
                    break;
            }
        }

        private void EvaluateWristStep()
        {
            // 1. Enforce initial startup grace period so startup events/frames never auto-complete
            if (Time.time < m_StepStartTime + k_StartupGracePeriod)
            {
                if (m_StepProgressText != null)
                    m_StepProgressText.text = "Initialisation...";
                return;
            }

            var wrist = FindFirstObjectByType<WristUIController>();
            if (wrist == null)
            {
                if (m_StepProgressText != null)
                    m_StepProgressText.text = "En attente du contrôleur...";
                return;
            }

            // 2. Check if the smartwatch menu is open and clearly visible
            bool isFullyVisible = wrist.isMenuVisible && wrist.menuAlpha >= 0.7f;

            if (isFullyVisible)
            {
                m_WristGazeDwellTime += Time.deltaTime;
                float progressFraction = Mathf.Clamp01(m_WristGazeDwellTime / k_RequiredWristGazeDwell);

                if (m_StepProgressText != null)
                {
                    int pct = Mathf.RoundToInt(progressFraction * 100f);
                    m_StepProgressText.text = $"<color=#38ef7d>Menu ouvert !</color> Maintenez le regard ({pct}%) ou appuyez sur un bouton";
                }

                if (m_WristGazeDwellTime >= k_RequiredWristGazeDwell)
                {
                    SetStep(Step.TeleportWithBlink);
                }
            }
            else
            {
                // Smoothly decay dwell time if looking away before reaching threshold
                m_WristGazeDwellTime = Mathf.Max(0f, m_WristGazeDwellTime - Time.deltaTime * 2.5f);

                if (m_StepProgressText != null)
                {
                    m_StepProgressText.text = "Tournez votre poignet gauche vers vos yeux (ou touche [M])";
                }
            }
        }

        private void EvaluateTeleportStep()
        {
            // Transition evaluated reactively via OnTeleportZoneEntered callback
        }

        private void EvaluateDashStep()
        {
            var mgr = TP1ComfortManager.Instance;
            if (mgr == null || mgr.dashProvider == null) return;

            bool isDashingNow = mgr.dashProvider.isDashing;
            if (isDashingNow && !m_WasDashing)
            {
                m_WasDashing = true;
            }
            else if (!isDashingNow && m_WasDashing)
            {
                m_WasDashing = false;
                SetStep(Step.SmoothMoveWithVignette);
            }
        }

        private void EvaluateWalkStep()
        {
            var mgr = TP1ComfortManager.Instance;
            if (mgr == null || !mgr.isSmoothMoveActive) return;

            var cam = Camera.main;
            if (cam == null) return;

            if (m_LastPlayerPosition == Vector3.zero)
            {
                m_LastPlayerPosition = cam.transform.position;
                return;
            }

            Vector3 currentPos = cam.transform.position;
            float frameDist = Vector2.Distance(new Vector2(currentPos.x, currentPos.z), new Vector2(m_LastPlayerPosition.x, m_LastPlayerPosition.z));
            m_LastPlayerPosition = currentPos;

            if (frameDist > 0.005f)
            {
                m_SmoothMoveAccumulatedDistance += frameDist;
                if (m_StepProgressText != null)
                    m_StepProgressText.text = $"Distance: {Mathf.Min(3.0f, m_SmoothMoveAccumulatedDistance):F1} / 3.0 m";

                if (m_SmoothMoveAccumulatedDistance >= 3.0f)
                    SetStep(Step.PortalUnlocked);
            }
        }

        public void SetStep(Step step)
        {
            m_CurrentStep = step;

            switch (step)
            {
                case Step.OpenWristMenu:
                    m_StepStartTime = Time.time;
                    m_WristGazeDwellTime = 0f;
                    UpdateDisplay(
                        "ÉTAPE 1 / 4",
                        "MENU DE POIGNET (SMARTWATCH)",
                        "Tournez votre poignet gauche vers vos yeux (ou appuyez sur la touche [M] sur Desktop) pour ouvrir l'interface. Appuyez sur n'importe quel bouton pour continuer.",
                        "Initialisation..."
                    );
                    if (m_TeleportTargetZone != null) m_TeleportTargetZone.SetActive(false);
                    GatePortal(true);
                    break;

                case Step.TeleportWithBlink:
                    UpdateDisplay(
                        "ÉTAPE 2 / 4",
                        "TÉLÉPORTATION & BLINK",
                        "Sur le menu du poignet, activez TÉLÉPORT (et assurez-vous que BLINK est actif). Poussez le joystick gauche vers l'avant pour viser la cible bleu au sol, puis relâchez pour vous y téléporter.",
                        "Visez la cible lumineuse"
                    );
                    if (m_TeleportTargetZone != null) m_TeleportTargetZone.SetActive(true);
                    GatePortal(true);
                    break;

                case Step.PerformDash:
                    UpdateDisplay(
                        "ÉTAPE 3 / 4",
                        "DASH",
                        "Activez le mode DASH sur le menu du poignet (ou appuyez sur [Espace] sur Desktop / gâchette) pour vous téléporter en mode dash.",
                        "Effectuez un Dash"
                    );
                    if (m_TeleportTargetZone != null) m_TeleportTargetZone.SetActive(false);
                    GatePortal(true);
                    break;

                case Step.SmoothMoveWithVignette:
                    UpdateDisplay(
                        "ÉTAPE 4 / 4",
                        "DÉPLACEMENT CONTINU & VIGNETTE",
                        "Activez CONTINU sur le menu. Déplacez-vous avec le joystick gauche (ou ZQSD sur Desktop) sur 3 mètres. Observez la vignette qui réduit le champ de vision pour prévenir la cinétose.",
                        "Distance: 0.0 / 3.0 m"
                    );
                    m_SmoothMoveAccumulatedDistance = 0f;
                    GatePortal(true);
                    break;

                case Step.PortalUnlocked:
                    UpdateDisplay(
                        "COMPLÉTÉ !",
                        "TUTORIEL TERMINÉ",
                        "Vous pouvez maintenant explorer la ville en passant dans le portail.",
                        "Prêt pour l'exploration !"
                    );
                    GatePortal(false);
                    break;
            }

            UpdateChecklist(step);
        }

        private void UpdateDisplay(string stepNum, string title, string instruction, string progress)
        {
            if (m_StepNumberText != null) m_StepNumberText.text = stepNum;
            if (m_StepTitleText != null) m_StepTitleText.text = title;
            if (m_StepInstructionText != null) m_StepInstructionText.text = instruction;
            if (m_StepProgressText != null) m_StepProgressText.text = progress;
        }

        private void UpdateChecklist(Step step)
        {
            int s = (int)step;
            SetCheckItem(m_CheckWristText, s > 0, "1. Menu de Poignet");
            SetCheckItem(m_CheckTeleportText, s > 1, "2. Téléportation & Blink");
            SetCheckItem(m_CheckDashText, s > 2, "3. Translation Dash (0.2s)");
            SetCheckItem(m_CheckWalkText, s > 3, "4. Déplacement Continu & Œillère");
        }

        private void SetCheckItem(TMP_Text item, bool done, string label)
        {
            if (item == null) return;
            item.text = done ? $"<color=#38ef7d>✔ <b>{label}</b></color>" : $"<color=#94a3b8>○ {label}</color>";
        }

        public void GatePortal(bool locked)
        {
            if (m_ExitPortal == null)
                m_ExitPortal = FindFirstObjectByType<SceneTeleportPortal>();

            if (m_ExitPortal != null)
            {
                m_ExitPortal.SetGated(locked);

                if (m_ExitPortal.curtainRenderer != null)
                {
                    Color lockedColor = new Color(0.95f, 0.25f, 0.1f, 1.0f);
                    Color unlockedColor = new Color(0.2f, 0.95f, 0.5f, 1.0f);
                    m_ExitPortal.curtainColor = locked ? lockedColor : unlockedColor;
                }

                if (m_PortalStatusBillboard != null)
                {
                    m_PortalStatusBillboard.text = locked
                        ? "<color=#f87171><b>PORTAIL VERROUILLÉ</b></color>\n<size=70%>Complétez le tutoriel pour continuer</size>"
                        : "<color=#38ef7d><b>PORTAIL DÉVERROUILLÉ</b></color>\n<size=70%>Entrez pour explorer la ville !</size>";
                }
            }
        }

        private void WireSkipButton()
        {
            if (m_SkipButton != null)
            {
                m_SkipButton.onClick.RemoveListener(SkipTutorial);
                m_SkipButton.onClick.AddListener(SkipTutorial);
            }
        }

        private void TryWireWristButtons()
        {
            var wrist = FindFirstObjectByType<WristUIController>();
            Transform wristRoot = wrist != null ? wrist.transform : GameObject.Find("Wrist_Comfort_UI")?.transform;
            if (wristRoot == null) return;

            bool anyHooked = false;
            string[] buttons = { "Card_Walk", "Card_Teleport", "Card_Dash", "Card_Blink", "Card_Vignette", "Card_Turn" };

            foreach (var bName in buttons)
            {
                var t = wristRoot.Find($"BackgroundPanel/{bName}") ?? wristRoot.Find(bName);
                if (t != null)
                {
                    var btn = t.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveListener(OnAnyWristButtonPressed);
                        btn.onClick.AddListener(OnAnyWristButtonPressed);
                        anyHooked = true;
                    }
                }
            }

            if (anyHooked) m_WristButtonsHooked = true;
        }

        public static TutorialFlowController CreateTutorialBoard(Transform parent, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("Tutorial_Board_Station");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = rotation;
            root.transform.localScale = Vector3.one * 0.0022f;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1000f, 650f);

            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<TrackedDeviceGraphicRaycaster>();

            var panelObj = new GameObject("BackgroundBoard");
            panelObj.transform.SetParent(root.transform, false);
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            var panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);

            var controller = root.AddComponent<TutorialFlowController>();

            var banner = CreateText(panelObj.transform, "HeaderBanner", "Tutoreil d'utilisation", 24f, FontStyles.Bold, new Vector2(0f, 280f), new Vector2(920f, 40f));
            banner.color = new Color(0.22f, 0.94f, 0.49f);

            var stepBadge = CreateText(panelObj.transform, "StepBadge", "ÉTAPE 1 / 4", 20f, FontStyles.Bold, new Vector2(-330f, 230f), new Vector2(260f, 34f));
            stepBadge.color = new Color(0.96f, 0.62f, 0.04f);
            controller.m_StepNumberText = stepBadge;

            var stepTitle = CreateText(panelObj.transform, "StepTitle", "MENU DE POIGNET (SMARTWATCH)", 28f, FontStyles.Bold, new Vector2(0f, 185f), new Vector2(920f, 45f));
            stepTitle.color = Color.white;
            controller.m_StepTitleText = stepTitle;

            var instruction = CreateText(panelObj.transform, "StepInstruction", "Instructions du tutoriel...", 22f, FontStyles.Normal, new Vector2(0f, 90f), new Vector2(900f, 120f));
            instruction.color = new Color(0.85f, 0.90f, 0.95f);
            instruction.textWrappingMode = TextWrappingModes.Normal;
            controller.m_StepInstructionText = instruction;

            var progress = CreateText(panelObj.transform, "StepProgress", "En attente...", 20f, FontStyles.Italic, new Vector2(0f, 5f), new Vector2(900f, 32f));
            progress.color = new Color(0.45f, 0.85f, 1.0f);
            controller.m_StepProgressText = progress;

            var div = new GameObject("Divider");
            div.transform.SetParent(panelObj.transform, false);
            var divRect = div.AddComponent<RectTransform>();
            divRect.anchoredPosition = new Vector2(0f, -25f);
            divRect.sizeDelta = new Vector2(920f, 2f);
            var divImg = div.AddComponent<Image>();
            divImg.color = new Color(0.20f, 0.25f, 0.35f, 0.8f);

            var checkHeader = CreateText(panelObj.transform, "ChecklistHeader", "PROGRESSION DE L'ENTRAÎNEMENT :", 18f, FontStyles.Bold, new Vector2(-260f, -55f), new Vector2(400f, 30f));
            checkHeader.color = new Color(0.60f, 0.70f, 0.85f);

            controller.m_CheckWristText = CreateText(panelObj.transform, "Check1", "○ 1. Menu de Poignet", 18f, FontStyles.Normal, new Vector2(-260f, -90f), new Vector2(400f, 26f));
            controller.m_CheckTeleportText = CreateText(panelObj.transform, "Check2", "○ 2. Téléportation & Blink", 18f, FontStyles.Normal, new Vector2(-260f, -120f), new Vector2(400f, 26f));
            controller.m_CheckDashText = CreateText(panelObj.transform, "Check3", "○ 3. Translation Dash (0.2s)", 18f, FontStyles.Normal, new Vector2(-260f, -150f), new Vector2(400f, 26f));
            controller.m_CheckWalkText = CreateText(panelObj.transform, "Check4", "○ 4. Déplacement Continu & Œillère", 18f, FontStyles.Normal, new Vector2(-260f, -180f), new Vector2(400f, 26f));

            var tipTitle = CreateText(panelObj.transform, "TipTitle", "RACCOURCIS CLAVIER / DESKTOP :", 18f, FontStyles.Bold, new Vector2(230f, -55f), new Vector2(420f, 30f));
            tipTitle.color = new Color(0.60f, 0.70f, 0.85f);

            var tipContent = CreateText(panelObj.transform, "TipContent",
                "[M] : Ouvrir / Fermer Menu Poignet\n" +
                "[1] Continu  •  [2] Téléport  •  [3] Dash\n" +
                "[4] Blink On/Off  •  [5] Œillère On/Off\n" +
                "[Space] : Dash  •  [T] : Téléport Test",
                16f, FontStyles.Normal, new Vector2(230f, -135f), new Vector2(420f, 110f));
            tipContent.color = new Color(0.70f, 0.78f, 0.88f);
            tipContent.textWrappingMode = TextWrappingModes.Normal;

            var skipObj = new GameObject("SkipButton");
            skipObj.transform.SetParent(panelObj.transform, false);
            var skipRect = skipObj.AddComponent<RectTransform>();
            skipRect.anchoredPosition = new Vector2(330f, 230f);
            skipRect.sizeDelta = new Vector2(180f, 38f);
            var skipImg = skipObj.AddComponent<Image>();
            skipImg.color = new Color(0.20f, 0.25f, 0.35f, 0.9f);
            var skipBtn = skipObj.AddComponent<Button>();
            controller.m_SkipButton = skipBtn;

            var skipTxt = CreateText(skipObj.transform, "Text", "PASSER LE TUTO >>", 15f, FontStyles.Bold, Vector2.zero, new Vector2(180f, 38f));
            skipTxt.color = Color.white;

            return controller;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, float fontSize, FontStyles style, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
