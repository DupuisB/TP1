using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LOG8704.UI
{
    public class TutorialFlowController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<TutorialFlowController>() != null)
                return;

            var go = new GameObject("TutorialFlowController");
            go.AddComponent<TutorialFlowController>();
        }

        private enum Step
        {
            OpenWristMenu = 0,
            SelectTeleport = 1,
            TeleportToZone = 2,
            Complete = 3
        }

        [Header("UI References")]
        [SerializeField] private GameObject m_TutorialPanel;
        [SerializeField] private TMP_Text m_StepTitle;
        [SerializeField] private TMP_Text m_StepInstruction;
        [SerializeField] private TMP_Text m_StepProgress;
        [SerializeField] private Button m_SkipButton;

        [Header("Scene References")]
        [SerializeField] private string m_WristUiRootName = "Wrist_Comfort_UI";
        [SerializeField] private string m_TeleportZoneName = "TutorialTeleportZone";

        [Header("Tutorial Options")]
        [SerializeField] private bool m_AutoFindReferences = true;
        [SerializeField] private bool m_AutoWireWristButtons = true;

        private Step m_CurrentStep;
        private bool m_WristButtonsHooked;

        private void Awake()
        {
            if (m_AutoFindReferences)
                AutoFindReferences();

            ApplyPanelTextLayout();
            WireSkipButton();
            SetStep(Step.OpenWristMenu);
        }

        private void Update()
        {
            if (m_AutoWireWristButtons && !m_WristButtonsHooked)
                TryWireWristButtons();
        }

        private void OnValidate()
        {
            if (m_AutoFindReferences)
                AutoFindReferences();

            ApplyPanelTextLayout();
            WireSkipButton();
        }

        public void SkipTutorial()
        {
            SetStep(Step.Complete);
        }

        public void RestartTutorial()
        {
            SetStep(Step.OpenWristMenu);
        }

        public void OnAnyWristButtonPressed()
        {
            if (m_CurrentStep == Step.OpenWristMenu)
                SetStep(Step.SelectTeleport);
        }

        public void OnTeleportModeSelected()
        {
            if (m_CurrentStep == Step.OpenWristMenu)
                SetStep(Step.SelectTeleport);

            if (m_CurrentStep == Step.SelectTeleport)
                SetStep(Step.TeleportToZone);
        }

        public void OnTeleportZoneEntered()
        {
            if (m_CurrentStep == Step.TeleportToZone)
                SetStep(Step.Complete);
        }

        private void SetStep(Step step)
        {
            m_CurrentStep = step;

            if (m_TutorialPanel != null)
                m_TutorialPanel.SetActive(step != Step.Complete);

            switch (step)
            {
                case Step.OpenWristMenu:
                    UpdateText("Step 1: Wrist Menu", "Raise your left wrist and look at it. Press any wrist menu button.", "1/3");
                    break;
                case Step.SelectTeleport:
                    UpdateText("Step 2: Select Teleport", "On the wrist menu, press TELEPORT mode.", "2/3");
                    break;
                case Step.TeleportToZone:
                    UpdateText("Step 3: Teleport", "Aim at the highlighted zone and teleport inside it.", "3/3");
                    EnsureTeleportZoneTrigger();
                    break;
                case Step.Complete:
                    UpdateText("Tutorial Complete", "Great! You can now use the wrist menu and teleport.", "Done");
                    break;
            }
        }

        private void UpdateText(string title, string instruction, string progress)
        {
            if (m_StepTitle != null) m_StepTitle.text = title;
            if (m_StepInstruction != null) m_StepInstruction.text = instruction;
            if (m_StepProgress != null) m_StepProgress.text = progress;
        }

        private void AutoFindReferences()
        {
            if (m_TutorialPanel == null)
            {
                var panelObj = GameObject.Find("TutorialPanel");
                if (panelObj != null)
                    m_TutorialPanel = panelObj;
            }

            if (m_TutorialPanel != null)
            {
                if (m_StepTitle == null)
                {
                    var t = m_TutorialPanel.transform.Find("StepTitle");
                    if (t != null) m_StepTitle = t.GetComponent<TMP_Text>();
                }

                if (m_StepInstruction == null)
                {
                    var t = m_TutorialPanel.transform.Find("StepInstruction");
                    if (t != null) m_StepInstruction = t.GetComponent<TMP_Text>();
                }

                if (m_StepProgress == null)
                {
                    var t = m_TutorialPanel.transform.Find("StepProgress");
                    if (t != null) m_StepProgress = t.GetComponent<TMP_Text>();
                }

                if (m_SkipButton == null)
                {
                    var b = m_TutorialPanel.transform.Find("SkipButton");
                    if (b != null) m_SkipButton = b.GetComponent<Button>();
                }
            }
        }

        private void ApplyPanelTextLayout()
        {
            if (m_TutorialPanel == null)
                return;

            var panelRect = m_TutorialPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.localScale = Vector3.one;
                panelRect.anchorMin = new Vector2(0.5f, 1f);
                panelRect.anchorMax = new Vector2(0.5f, 1f);
                panelRect.pivot = new Vector2(0.5f, 1f);
                panelRect.anchoredPosition = new Vector2(0f, -40f);
                panelRect.sizeDelta = new Vector2(900f, 220f);
            }

            var canvas = m_TutorialPanel.GetComponentInParent<Canvas>();
            if (canvas != null && (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.ScreenSpaceCamera))
            {
                var canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    canvasRect.localScale = Vector3.one;
                    canvasRect.anchoredPosition = Vector2.zero;
                }
            }

            SetupTextRect(m_StepTitle, new Vector2(0f, -34f), new Vector2(840f, 42f), 34f, TextAlignmentOptions.Center);
            SetupTextRect(m_StepInstruction, new Vector2(0f, -102f), new Vector2(850f, 86f), 27f, TextAlignmentOptions.Center);
            SetupProgressRect(m_StepProgress);
            SetupSkipButtonRect();
        }

        private void SetupTextRect(TMP_Text text, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            if (text == null)
                return;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            text.alignment = alignment;
            text.fontSize = fontSize;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void SetupProgressRect(TMP_Text text)
        {
            if (text == null)
                return;

            var rect = text.rectTransform;
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-18f, 10f);
            rect.sizeDelta = new Vector2(140f, 28f);

            text.alignment = TextAlignmentOptions.Right;
            text.fontSize = 20f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        private void SetupSkipButtonRect()
        {
            if (m_SkipButton == null)
                return;

            var rect = m_SkipButton.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-18f, -16f);
            rect.sizeDelta = new Vector2(120f, 34f);
        }

        private void WireSkipButton()
        {
            if (m_SkipButton == null)
                return;

            m_SkipButton.onClick.RemoveListener(SkipTutorial);
            m_SkipButton.onClick.AddListener(SkipTutorial);
        }

        private void TryWireWristButtons()
        {
            var wristRoot = GameObject.Find(m_WristUiRootName);
            if (wristRoot == null)
                return;

            bool anyFound = false;

            anyFound |= WireButton(wristRoot.transform, "BackgroundPanel/Card_Walk", OnAnyWristButtonPressed);
            anyFound |= WireButton(wristRoot.transform, "BackgroundPanel/Card_Dash", OnAnyWristButtonPressed);
            anyFound |= WireButton(wristRoot.transform, "BackgroundPanel/Card_Blink", OnAnyWristButtonPressed);
            anyFound |= WireButton(wristRoot.transform, "BackgroundPanel/Card_Vignette", OnAnyWristButtonPressed);
            anyFound |= WireButton(wristRoot.transform, "BackgroundPanel/Card_Turn", OnAnyWristButtonPressed);
            anyFound |= WireButton(wristRoot.transform, "BackgroundPanel/Card_Teleport", OnTeleportModeSelected);

            if (anyFound)
                m_WristButtonsHooked = true;
        }

        private bool WireButton(Transform root, string relativePath, UnityEngine.Events.UnityAction action)
        {
            var t = root.Find(relativePath);
            if (t == null)
                return false;

            var button = t.GetComponent<Button>();
            if (button == null)
                return false;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
            return true;
        }

        private void EnsureTeleportZoneTrigger()
        {
            var zoneObj = GameObject.Find(m_TeleportZoneName);
            if (zoneObj == null)
                return;

            var trigger = zoneObj.GetComponent<TutorialTeleportZoneTrigger>();
            if (trigger == null)
                trigger = zoneObj.AddComponent<TutorialTeleportZoneTrigger>();

            trigger.SetController(this);
        }
    }
}
