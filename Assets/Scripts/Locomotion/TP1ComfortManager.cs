using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

namespace LOG8704.Locomotion
{
    public class TP1ComfortManager : MonoBehaviour
    {
        public enum LocomotionMode { SmoothMove = 0, Teleport = 1, Dash = 2 }
        public enum TurnMode { Snap = 0, Smooth = 1 }

        private static TP1ComfortManager s_Instance;
        public static TP1ComfortManager Instance => s_Instance;

        [Header("Locomotion Components")]
        [SerializeField] private ComfortTeleportationProvider m_TeleportProvider;
        [SerializeField] private DashProvider m_DashProvider;
        [SerializeField] private ContinuousMoveProvider m_ContinuousMoveProvider;
        [SerializeField] private SnapTurnProvider m_SnapTurnProvider;
        [SerializeField] private ContinuousTurnProvider m_ContinuousTurnProvider;
        [SerializeField] private TunnelingVignetteController m_VignetteController;

        [Header("Turn Input Action References")]
        [FormerlySerializedAs("m_LeftSnapTurnAction")]
        [SerializeField] private InputActionReference m_RightSnapTurnAction;
        [FormerlySerializedAs("m_LeftTurnAction")]
        [SerializeField] private InputActionReference m_RightTurnAction;

        [Header("Initial Configuration")]
        [SerializeField] private LocomotionMode m_InitialLocomotionMode = LocomotionMode.Teleport;
        [SerializeField] private bool m_TeleportBlinkDefault = true;
        [SerializeField] private bool m_VignetteDefault = true;
        [SerializeField] private TurnMode m_InitialTurnMode = TurnMode.Snap;

        [Header("Desktop Testing Shortcuts")]
        [Tooltip("Enable keyboard shortcuts (1: Smooth, 2: Teleport, 3: Dash, 4: Blink, 5: Vignette, 6: Turn, T: Teleport/Dash)")]
        [SerializeField] private bool m_EnableKeyboardShortcuts = true;

        private LocomotionMode m_LocomotionMode = LocomotionMode.Teleport;
        private bool m_IsTeleportBlinkEnabled = true;
        private bool m_IsVignetteActive = true;
        private TurnMode m_TurnMode = TurnMode.Snap;

        public event Action<LocomotionMode> onLocomotionModeChanged;
        public event Action<bool> onTeleportBlinkToggled;
        public event Action<bool> onVignetteToggled;
        public event Action<TurnMode> onTurnModeChanged;

        public LocomotionMode locomotionMode => m_LocomotionMode;
        public bool isTeleportBlinkEnabled => m_IsTeleportBlinkEnabled;
        public bool isVignetteActive => m_IsVignetteActive;
        public TurnMode turnMode => m_TurnMode;

        public bool isSmoothMoveActive => m_LocomotionMode == LocomotionMode.SmoothMove;
        public bool isTeleportActive => m_LocomotionMode == LocomotionMode.Teleport;
        public bool isDashActive => m_LocomotionMode == LocomotionMode.Dash;
        public bool isSnapTurnActive => m_TurnMode == TurnMode.Snap;
        public bool isSmoothTurnActive => m_TurnMode == TurnMode.Smooth;

        public ComfortTeleportationProvider teleportProvider => m_TeleportProvider;
        public DashProvider dashProvider => m_DashProvider;
        public ContinuousMoveProvider continuousMoveProvider => m_ContinuousMoveProvider;
        public SnapTurnProvider snapTurnProvider => m_SnapTurnProvider;
        public ContinuousTurnProvider continuousTurnProvider => m_ContinuousTurnProvider;
        public TunnelingVignetteController vignetteController => m_VignetteController;

        private void Awake()
        {
            if (s_Instance == null) s_Instance = this;
            FindDependencies();
        }

        private void Start()
        {
            SetLocomotionMode(m_InitialLocomotionMode);
            SetTeleportBlinkEnabled(m_TeleportBlinkDefault);
            SetVignetteEnabled(m_VignetteDefault);
            SetTurnMode(m_InitialTurnMode);
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        public void FindDependencies()
        {
            var xrOrigin = FindFirstObjectByType<XROrigin>();
            if (xrOrigin != null)
            {
                var originTarget = xrOrigin.Origin != null ? xrOrigin.Origin : xrOrigin.gameObject;
                var cc = originTarget.GetComponent<CharacterController>() ?? xrOrigin.gameObject.GetComponent<CharacterController>() ?? originTarget.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.05f;
                cc.minMoveDistance = 0f;

                var transformer = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.XRBodyTransformer>();
                if (transformer != null) transformer.useCharacterControllerIfExists = true;
            }

            if (m_TeleportProvider == null) m_TeleportProvider = FindFirstObjectByType<ComfortTeleportationProvider>(FindObjectsInactive.Include);
            if (m_DashProvider == null) m_DashProvider = FindFirstObjectByType<DashProvider>(FindObjectsInactive.Include);
            if (m_ContinuousMoveProvider == null) m_ContinuousMoveProvider = FindFirstObjectByType<ContinuousMoveProvider>(FindObjectsInactive.Include);
            if (m_SnapTurnProvider == null) m_SnapTurnProvider = FindFirstObjectByType<SnapTurnProvider>(FindObjectsInactive.Include);
            if (m_ContinuousTurnProvider == null) m_ContinuousTurnProvider = FindFirstObjectByType<ContinuousTurnProvider>(FindObjectsInactive.Include);
            if (m_VignetteController == null) m_VignetteController = FindFirstObjectByType<TunnelingVignetteController>(FindObjectsInactive.Include);

            SetupVignetteProviders();
            ConfigureTeleportRayVisuals();
            EnsureAllSurfacesTeleportable();
        }

        private void Update()
        {
            if (!m_EnableKeyboardShortcuts || Keyboard.current == null) return;
            var kb = Keyboard.current;

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SetLocomotionMode(LocomotionMode.SmoothMove);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) SetLocomotionMode(LocomotionMode.Teleport);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) SetLocomotionMode(LocomotionMode.Dash);
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) ToggleTeleportBlink();
            else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) ToggleVignette();
            else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) ToggleTurnMode();
            else if (kb.tKey.wasPressedThisFrame) TryTestTeleportForward();
        }

        public void SetLocomotionMode(LocomotionMode mode) => ApplyLocomotionMode(mode);

        private void ApplyLocomotionMode(LocomotionMode mode)
        {
            m_LocomotionMode = mode;
            if (m_ContinuousMoveProvider == null) FindDependencies();

            if (m_ContinuousMoveProvider != null)
                m_ContinuousMoveProvider.enabled = (mode == LocomotionMode.SmoothMove);

            foreach (var mgr in FindObjectsByType<ControllerInputActionManager>(FindObjectsSortMode.None))
            {
                bool isLeft = mgr.name.Contains("Left") || (mgr.transform.parent != null && mgr.transform.parent.name.Contains("Left"));
                mgr.smoothMotionEnabled = isLeft && (mode == LocomotionMode.SmoothMove);
            }

            onLocomotionModeChanged?.Invoke(mode);
        }

        public void ToggleTeleportBlink() => SetTeleportBlinkEnabled(!m_IsTeleportBlinkEnabled);

        public void SetTeleportBlinkEnabled(bool enabled)
        {
            m_IsTeleportBlinkEnabled = enabled;
            onTeleportBlinkToggled?.Invoke(enabled);
        }

        public void ToggleVignette() => SetVignetteEnabled(!m_IsVignetteActive);

        public void SetVignetteEnabled(bool enabled)
        {
            m_IsVignetteActive = enabled;
            if (m_VignetteController == null) FindDependencies();

            if (m_VignetteController != null)
            {
                foreach (var provider in m_VignetteController.locomotionVignetteProviders)
                    provider.enabled = enabled;

                if (!enabled && m_DashProvider != null)
                    m_VignetteController.EndTunnelingVignette(m_DashProvider);
            }

            onVignetteToggled?.Invoke(enabled);
        }

        public void ToggleTurnMode() => SetTurnMode(m_TurnMode == TurnMode.Snap ? TurnMode.Smooth : TurnMode.Snap);

        public void SetTurnMode(TurnMode mode) => ApplyTurnMode(mode);

        private void ApplyTurnMode(TurnMode mode)
        {
            m_TurnMode = mode;
            if (m_SnapTurnProvider == null || m_ContinuousTurnProvider == null) FindDependencies();

            bool isSmooth = mode == TurnMode.Smooth;
            if (m_SnapTurnProvider != null) m_SnapTurnProvider.enabled = !isSmooth;
            if (m_ContinuousTurnProvider != null) m_ContinuousTurnProvider.enabled = isSmooth;

            var action = isSmooth ? m_RightTurnAction?.action : m_RightSnapTurnAction?.action;
            if (action != null && !action.enabled) action.Enable();

            foreach (var mgr in FindObjectsByType<ControllerInputActionManager>(FindObjectsSortMode.None))
                mgr.smoothTurnEnabled = isSmooth;

            onTurnModeChanged?.Invoke(mode);
        }

        private void SetupVignetteProviders()
        {
            if (m_VignetteController == null) return;
            var providers = m_VignetteController.locomotionVignetteProviders;

            RegisterVignetteProvider(m_ContinuousMoveProvider, providers);
            RegisterVignetteProvider(m_ContinuousTurnProvider, providers);
        }

        private void RegisterVignetteProvider(UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider provider, List<LocomotionVignetteProvider> list)
        {
            if (provider == null) return;
            foreach (var p in list)
            {
                if (p.locomotionProvider == provider)
                {
                    p.enabled = m_IsVignetteActive;
                    return;
                }
            }
            list.Add(new LocomotionVignetteProvider { locomotionProvider = provider, enabled = m_IsVignetteActive });
        }

        public void ConfigureTeleportRayVisuals()
        {
            var orange = new Color(1.0f, 0.5f, 0.0f, 1.0f);
            var blockedGrad = new Gradient();
            blockedGrad.SetKeys(new[] { new GradientColorKey(orange, 0f), new GradientColorKey(orange, 1f) },
                                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

            var invalidGrad = new Gradient();
            invalidGrad.SetKeys(new[] { new GradientColorKey(orange, 0f), new GradientColorKey(orange, 1f) },
                                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.8f, 1f) });

            foreach (var lv in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(FindObjectsSortMode.None))
            {
                if (lv.name.Contains("Teleport") || (lv.transform.parent != null && lv.transform.parent.name.Contains("Teleport")))
                {
                    lv.blockedColorGradient = blockedGrad;
                    lv.invalidColorGradient = invalidGrad;
                    lv.stopLineAtFirstRaycastHit = true;
                }
            }

            foreach (var ray in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(FindObjectsSortMode.None))
            {
                if (ray.name.Contains("Teleport") || (ray.transform.parent != null && ray.transform.parent.name.Contains("Teleport")))
                {
                    ray.additionalGroundHeight = 100f;
                    ray.additionalFlightTime = 5f;
                    ray.maxRaycastDistance = 60f;
                    ray.endPointDistance = 60f;
                    ray.endPointHeight = -50f;
                }
            }
        }

        public void EnsureAllSurfacesTeleportable()
        {
            var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            var teleportMask = InteractionLayerMask.GetMask("Default", "Teleport");

            foreach (var col in colliders)
            {
                if (col == null || col.isTrigger || col.GetComponentInParent<XROrigin>() != null || col.GetComponentInParent<SceneTeleportPortal>() != null)
                    continue;

                var existingNonTeleport = col.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable>();
                if (existingNonTeleport != null && !(existingNonTeleport is BaseTeleportationInteractable))
                    continue;

                bool isBlocked = IsRedOrBlocked(col) || !IsWalkableSurface(col);
                if (isBlocked)
                {
                    RemoveComponent<TeleportationArea>(col);
                    var blockedArea = col.GetComponent<BlockedTeleportArea>() ?? col.gameObject.AddComponent<BlockedTeleportArea>();
                    blockedArea.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
                    blockedArea.interactionLayers = teleportMask;
                    if (m_TeleportProvider != null) blockedArea.teleportationProvider = m_TeleportProvider;
                }
                else
                {
                    RemoveComponent<BlockedTeleportArea>(col);
                    var area = col.GetComponent<TeleportationArea>() ?? col.gameObject.AddComponent<TeleportationArea>();
                    area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
                    area.matchOrientation = MatchOrientation.WorldSpaceUp;
                    area.interactionLayers = teleportMask;
                    area.filterSelectionByHitNormal = true;
                    area.upNormalToleranceDegrees = 60f;
                    if (m_TeleportProvider != null) area.teleportationProvider = m_TeleportProvider;
                }
            }
        }

        private static void RemoveComponent<T>(Collider col) where T : Component
        {
            var comp = col.GetComponent<T>();
            if (comp != null)
            {
                if (Application.isPlaying) Destroy(comp);
                else DestroyImmediate(comp);
            }
        }

        public static bool IsWalkableSurface(Collider col)
        {
            if (col == null || col.isTrigger) return false;
            string fullName = (col.gameObject.name + " " + (col.transform.parent != null ? col.transform.parent.name : "")).ToLowerInvariant();

            if (fullName.Contains("wall") || fullName.Contains("fence") || fullName.Contains("door") ||
                fullName.Contains("window") || fullName.Contains("boundary") || fullName.Contains("column") ||
                fullName.Contains("pillar") || fullName.Contains("frame") || fullName.Contains("ladder") ||
                fullName.Contains("tree") || fullName.Contains("sword") || fullName.Contains("coin") || fullName.Contains("cone") ||
                fullName.Contains("arrow") || fullName.Contains("target") || fullName.Contains("controller") || fullName.Contains("hand") ||
                fullName.Contains("shield") || fullName.Contains("watergun") || fullName.Contains("waterpistol") || fullName.Contains("refill") ||
                fullName.Contains("ring") || fullName.Contains("question") || fullName.Contains("car") || fullName.Contains("plane_stunt") ||
                fullName.Contains("wheel") || fullName.Contains("sphere") || fullName.Contains("tube") || fullName.Contains("xr origin") ||
                fullName.Contains("camera") || fullName.Contains("canvas") || fullName.Contains("pedestal") || fullName.Contains("socket") ||
                fullName.Contains("safety"))
            {
                return false;
            }

            return fullName.Contains("ground") || fullName.Contains("floor") || fullName.Contains("road") || fullName.Contains("path") ||
                   fullName.Contains("dirt") || fullName.Contains("grass") || fullName.Contains("concrete") ||
                   fullName.Contains("platform") || fullName.Contains("ramp") || fullName.Contains("stairs") || fullName.Contains("roof") ||
                   fullName.Contains("bench") || fullName.Contains("table") || fullName.Contains("step") || fullName.Contains("deck") ||
                   fullName.Contains("rock") || fullName.Contains("mountain") || fullName.Contains("bld") || fullName.Contains("house") ||
                   fullName.Contains("block") || fullName.Contains("crate") || fullName.Contains("box");
        }

        public static bool IsRedOrBlocked(Collider col)
        {
            if (col == null) return false;
            if (col.GetComponent<NoTeleportZone>() != null || col.GetComponentInParent<NoTeleportZone>() != null) return true;

            string fullName = (col.gameObject.name + " " + (col.transform.parent != null ? col.transform.parent.name : "")).ToLowerInvariant();
            if (fullName.Contains("obstacle") || fullName.Contains("red") || fullName.Contains("no_teleport") ||
                fullName.Contains("noteleport") || fullName.Contains("m_arenaobstacle"))
            {
                return true;
            }

            foreach (var r in col.GetComponentsInChildren<Renderer>())
            {
                if (CheckRendererForRed(r)) return true;
            }
            return CheckRendererForRed(col.GetComponentInParent<Renderer>());
        }

        private static bool CheckRendererForRed(Renderer r)
        {
            if (r == null || r.sharedMaterials == null) return false;
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                string matName = mat.name.ToLowerInvariant();
                if (matName.Contains("red") || matName.Contains("polygonstarter_02") || matName.Contains("arenaobstacle"))
                    return true;

                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.black);
                if (c.r > 0.6f && c.g < 0.35f && c.b < 0.35f && c.a > 0.1f) return true;
            }
            return false;
        }

        public void TryTestTeleportForward()
        {
            var origin = FindFirstObjectByType<XROrigin>();
            if (origin != null && m_TeleportProvider != null)
            {
                Vector3 fwd = origin.Camera != null ? origin.Camera.transform.forward : origin.transform.forward;
                fwd.y = 0f;
                fwd.Normalize();
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;

                m_TeleportProvider.QueueTeleportRequest(new TeleportRequest
                {
                    destinationPosition = origin.transform.position + fwd * 3f,
                    destinationRotation = origin.transform.rotation,
                    matchOrientation = MatchOrientation.WorldSpaceUp
                });
            }
        }
    }
}
