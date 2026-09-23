using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Unity.XR.CoreUtils;

namespace LOG8704.Locomotion
{
    /// <summary>
    /// In-world physical VR gateway portal connecting scenes (e.g. Demo <-> TP1_TestArena).
    /// Supports dual activation:
    /// 1. Ray-Teleportation: Aiming and releasing your teleport ray on the portal pad/gateway.
    /// 2. Walk-through: Physical walk-in trigger (smooth move or dash) via OnTriggerEnter.
    /// Coordinates haptics and screen blackout fade before calling SceneManager.LoadSceneAsync.
    /// </summary>
    [AddComponentMenu("LOG8704/Locomotion/Scene Teleport Portal")]
    [SelectionBase]
    public class SceneTeleportPortal : BaseTeleportationInteractable
    {
        [Header("Scene Transition")]
        [Tooltip("The name of the scene to load (must be added in EditorBuildSettings).")]
        [SerializeField] private string m_TargetSceneName = "Demo";

        [Tooltip("Blackout fade duration in seconds.")]
        [SerializeField] private float m_FadeDuration = 0.25f;

        [Tooltip("Enable haptic feedback on the controllers when entering or selecting the portal.")]
        [SerializeField] private bool m_EnableHaptics = true;

        [Header("Visual Elements")]
        [Tooltip("Optional Renderer for the energy curtain to pulse emission.")]
        [SerializeField] private Renderer m_CurtainRenderer;

        [Tooltip("Base emission color for the energy curtain.")]
        [ColorUsage(true, true)]
        [SerializeField] private Color m_CurtainColor = new Color(0f, 0.7f, 1f, 1f);

        [Tooltip("Pulsing speed for the portal energy curtain.")]
        [SerializeField] private float m_PulseSpeed = 2f;

        private bool m_IsTransitioning = false;
        private MaterialPropertyBlock m_PropertyBlock;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public string targetSceneName
        {
            get => m_TargetSceneName;
            set => m_TargetSceneName = value;
        }

        public Renderer curtainRenderer
        {
            get => m_CurtainRenderer;
            set => m_CurtainRenderer = value;
        }

        public Color curtainColor
        {
            get => m_CurtainColor;
            set => m_CurtainColor = value;
        }

        protected override void Reset()
        {
            base.Reset();
            teleportTrigger = TeleportTrigger.OnSelectExited;
            interactionLayers = unchecked((int)2147483648) | 1 | InteractionLayerMask.GetMask("Teleport");
        }

        protected override void Awake()
        {
            base.Awake();
            teleportTrigger = TeleportTrigger.OnSelectExited;
            interactionLayers = unchecked((int)2147483648) | 1 | InteractionLayerMask.GetMask("Teleport");
            m_IsTransitioning = false;

            if (m_CurtainRenderer != null)
            {
                m_PropertyBlock = new MaterialPropertyBlock();
            }
        }

        protected void Update()
        {
            // Subtle idle energy pulse on the portal curtain
            if (m_CurtainRenderer != null)
            {
                if (m_PropertyBlock == null)
                    m_PropertyBlock = new MaterialPropertyBlock();

                float pulse = 0.75f + Mathf.PingPong(Time.time * m_PulseSpeed, 0.5f);
                Color currentEmission = m_CurtainColor * pulse;
                m_CurtainRenderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(EmissionColorId, currentEmission);
                m_CurtainRenderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        /// <summary>
        /// When selected via Teleport ray release, trigger the cross-scene transition.
        /// Return false so local TeleportationProvider does not reposition rig within the active scene.
        /// </summary>
        protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit raycastHit, ref TeleportRequest teleportRequest)
        {
            if (interactor is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(0.5f, 0.15f);
            }

            TriggerTransition();
            return false;
        }

        /// <summary>
        /// Walk-through / dash-through trigger.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (m_IsTransitioning)
                return;

            // Check if player rig, CharacterController, or Camera entered the portal
            if (other.GetComponentInParent<XROrigin>() != null ||
                other.GetComponentInParent<CharacterController>() != null ||
                other.CompareTag("MainCamera"))
            {
                TriggerTransition();
            }
        }

        /// <summary>
        /// Initiates the smooth comfort fade and async scene load.
        /// </summary>
        public void TriggerTransition()
        {
            if (m_IsTransitioning || string.IsNullOrEmpty(m_TargetSceneName))
                return;

            m_IsTransitioning = true;
            Debug.Log($"[SceneTeleportPortal] Teleporting player to scene: '{m_TargetSceneName}'...");

            if (m_EnableHaptics)
            {
                TriggerControllerHaptics(0.6f, 0.2f);
            }

            var fadeCanvas = ScreenFadeCanvas.Instance;
            if (fadeCanvas != null)
            {
                fadeCanvas.BlinkFade(() =>
                {
                    ExecuteSceneLoad();
                }, m_FadeDuration);
            }
            else
            {
                ExecuteSceneLoad();
            }
        }

        private void ExecuteSceneLoad()
        {
            try
            {
                // 1. Direct load if Unity directly recognizes the scene name
                if (Application.CanStreamedLevelBeLoaded(m_TargetSceneName))
                {
                    SceneManager.LoadSceneAsync(m_TargetSceneName);
                    return;
                }

                // 2. Search through build settings scenes by normalized name to handle Unicode accents (e.g. Début)
                int sceneCount = SceneManager.sceneCountInBuildSettings;
                for (int i = 0; i < sceneCount; i++)
                {
                    string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                    if (string.Equals(sceneName, m_TargetSceneName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(sceneName.Normalize(System.Text.NormalizationForm.FormC), m_TargetSceneName.Normalize(System.Text.NormalizationForm.FormC), StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(sceneName.Normalize(System.Text.NormalizationForm.FormD), m_TargetSceneName.Normalize(System.Text.NormalizationForm.FormD), StringComparison.OrdinalIgnoreCase))
                    {
                        SceneManager.LoadSceneAsync(i);
                        return;
                    }
                }

                // Fallback attempt
                SceneManager.LoadSceneAsync(m_TargetSceneName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneTeleportPortal] Failed to load scene '{m_TargetSceneName}': {ex}");
                m_IsTransitioning = false;
            }
        }

        private void TriggerControllerHaptics(float amplitude, float duration)
        {
            try
            {
                var inputDevices = new List<UnityEngine.XR.InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand, inputDevices);
                foreach (var dev in inputDevices)
                {
                    if (dev.isValid)
                    {
                        dev.SendHapticImpulse(0u, amplitude, duration);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneTeleportPortal] Haptic pulse failed: {ex.Message}");
            }
        }
    }
}
