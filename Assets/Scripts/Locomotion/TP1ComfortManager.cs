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

namespace LOG8704.Locomotion
{
    /// <summary>
    /// Central manager for LOG8704 TP1 locomotion and comfort systems:
    /// 1. Locomotion Mode: Smooth Déplacement (Walk), Teleport, or Dash
    /// 2. Teleport Blink Option: Blackout fade vs Instant jump
    /// 3. Global Tunneling Vignette (Oeillère de protection) for all locomotion
    /// 4. Turn Mode: Snap (45°) vs Smooth (Continuous) rotation
    /// Provides high-level APIs for the Wrist UI and keyboard shortcuts for desktop testing.
    /// </summary>
    public class TP1ComfortManager : MonoBehaviour
    {
        public enum LocomotionMode
        {
            SmoothMove = 0,
            Teleport = 1,
            Dash = 2
        }

        public enum TurnMode
        {
            Snap = 0,
            Smooth = 1
        }

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
        [Tooltip("Enable keyboard shortcuts (1: Smooth, 2: Teleport, 3: Dash, 4: Blink, 5: Vignette, 6: Turn, Space: Dash, T: Teleport)")]
        [SerializeField] private bool m_EnableKeyboardShortcuts = true;

        // Current runtime states
        private LocomotionMode m_LocomotionMode = LocomotionMode.Teleport;
        private bool m_IsTeleportBlinkEnabled = true;
        private bool m_IsVignetteActive = true;
        private TurnMode m_TurnMode = TurnMode.Snap;

        // Events for UI synchronization
        public event Action<LocomotionMode> onLocomotionModeChanged;
        public event Action<bool> onTeleportBlinkToggled;
        public event Action<bool> onVignetteToggled;
        public event Action<TurnMode> onTurnModeChanged;

        // Public getters
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
            if (s_Instance == null)
            {
                s_Instance = this;
            }

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
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        public void FindDependencies()
        {
            // Verify / configure CharacterController on XR Origin
            var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xrOrigin != null)
            {
                var originTarget = xrOrigin.Origin != null ? xrOrigin.Origin : xrOrigin.gameObject;
                var cc = originTarget.GetComponent<CharacterController>();
                if (cc == null && xrOrigin.gameObject != originTarget)
                    cc = xrOrigin.gameObject.GetComponent<CharacterController>();

                if (cc == null)
                {
                    cc = originTarget.AddComponent<CharacterController>();
                }

                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.05f;
                cc.minMoveDistance = 0f;

                var transformer = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.XRBodyTransformer>();
                if (transformer != null)
                {
                    transformer.useCharacterControllerIfExists = true;
                }
            }

            if (m_TeleportProvider == null)
                m_TeleportProvider = FindFirstObjectByType<ComfortTeleportationProvider>(FindObjectsInactive.Include);

            if (m_DashProvider == null)
                m_DashProvider = FindFirstObjectByType<DashProvider>(FindObjectsInactive.Include);

            if (m_ContinuousMoveProvider == null)
                m_ContinuousMoveProvider = FindFirstObjectByType<ContinuousMoveProvider>(FindObjectsInactive.Include);

            if (m_SnapTurnProvider == null)
                m_SnapTurnProvider = FindFirstObjectByType<SnapTurnProvider>(FindObjectsInactive.Include);

            if (m_ContinuousTurnProvider == null)
                m_ContinuousTurnProvider = FindFirstObjectByType<ContinuousTurnProvider>(FindObjectsInactive.Include);

            if (m_VignetteController == null)
                m_VignetteController = FindFirstObjectByType<TunnelingVignetteController>(FindObjectsInactive.Include);

            // Setup Vignette providers
            SetupVignetteProviders();

            // Configure teleport line visuals to Orange for impossible/blocked teleport
            ConfigureTeleportRayVisuals();

            // Ensure all walkable & elevated surfaces (ground, platforms, ramps, stairs) are teleportable
            EnsureAllSurfacesTeleportable();
        }

        /// <summary>
        /// Configures teleport ray visuals so that impossible/blocked/invalid teleport destinations
        /// display an orange line instead of yellow or red.
        /// </summary>
        public void ConfigureTeleportRayVisuals()
        {
            var lineVisuals = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(FindObjectsSortMode.None);
            var orange = new Color(1.0f, 0.5f, 0.0f, 1.0f);
            var orangeSemi = new Color(1.0f, 0.5f, 0.0f, 0.65f);

            foreach (var lv in lineVisuals)
            {
                if (lv.name.Contains("Teleport") || (lv.transform.parent != null && lv.transform.parent.name.Contains("Teleport")))
                {
                    // Blocked gradient: Solid Orange
                    var blockedGrad = new Gradient();
                    blockedGrad.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(orange, 0f), new GradientColorKey(orange, 1f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                    );
                    lv.blockedColorGradient = blockedGrad;

                    // Invalid gradient: Semi-transparent Orange
                    var invalidGrad = new Gradient();
                    invalidGrad.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(orange, 0f), new GradientColorKey(orange, 1f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(0.65f, 0f), new GradientAlphaKey(0.65f, 1f) }
                    );
                    lv.invalidColorGradient = invalidGrad;
                }
            }
        }

        /// <summary>
        /// Ensures walkable ground, floors, platforms, ramps, and stairs
        /// have active TeleportationArea components, while boxes, crates, and vertical walls are rejected.
        /// </summary>
        public void EnsureAllSurfacesTeleportable()
        {
            var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                    continue;

                string n = col.gameObject.name.ToLowerInvariant();
                string p = col.transform.parent != null ? col.transform.parent.name.ToLowerInvariant() : "";
                string fullName = n + " " + p;

                // Explicit exclusions: walls, columns, pillars, doors, fences, hand-held items, boundary colliders
                if (fullName.Contains("wall") || fullName.Contains("fence") || fullName.Contains("door") || 
                    fullName.Contains("window") || fullName.Contains("boundary") || fullName.Contains("column") ||
                    fullName.Contains("pillar") || fullName.Contains("frame") ||
                    fullName.Contains("tree") || fullName.Contains("sword") || fullName.Contains("coin") || fullName.Contains("cone") ||
                    fullName.Contains("arrow") || fullName.Contains("target") || fullName.Contains("controller") || fullName.Contains("hand"))
                {
                    continue;
                }

                // If object is a box, crate, block, red object, or marked to block teleportation:
                // Keep collider for walking and raycast aiming, but ensure TeleportationArea is removed.
                if (IsRedOrBlocked(col))
                {
                    var existingArea = col.GetComponent<TeleportationArea>();
                    if (existingArea != null)
                    {
                        if (Application.isPlaying) Destroy(existingArea);
                        else DestroyImmediate(existingArea);
                    }
                    continue;
                }

                // Match walkable ground, floors, platforms, ramps, stairs, roofs, decks (boxes/crates excluded)
                bool isSurface = 
                    fullName.Contains("ground") || fullName.Contains("floor") || fullName.Contains("road") || fullName.Contains("path") || 
                    fullName.Contains("dirt") || fullName.Contains("grass") || fullName.Contains("concrete") || fullName.Contains("plane") ||
                    fullName.Contains("platform") || fullName.Contains("ramp") || fullName.Contains("stairs") || fullName.Contains("roof") ||
                    fullName.Contains("bench") || fullName.Contains("table") || fullName.Contains("step") || fullName.Contains("deck") ||
                    fullName.Contains("rock") || fullName.Contains("mountain") || fullName.Contains("bld") || fullName.Contains("house") || fullName.Contains("veh");

                if (isSurface)
                {
                    var area = col.GetComponent<TeleportationArea>();
                    if (area == null)
                    {
                        area = col.gameObject.AddComponent<TeleportationArea>();
                    }
                    area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
                    area.matchOrientation = MatchOrientation.WorldSpaceUp;
                    area.interactionLayers = unchecked((int)2147483648) | 1 | UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask.GetMask("Teleport");
                    area.filterSelectionByHitNormal = true;
                    area.upNormalToleranceDegrees = 75f; // Rejects only very vertical walls (angle > 75°), accepts slopes/ramps up to 75°
                    if (m_TeleportProvider != null)
                        area.teleportationProvider = m_TeleportProvider;
                    count++;
                }
            }

            // Also link any other existing TeleportationArea components in the scene,
            // ensuring no boxes, crates, or blocked surfaces are linked, and all have hit normal filtering enabled
            var allAreas = FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None);
            foreach (var area in allAreas)
            {
                var col = area.GetComponent<Collider>();
                if (col != null && IsRedOrBlocked(col))
                {
                    if (Application.isPlaying) Destroy(area);
                    else DestroyImmediate(area);
                    continue;
                }

                area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
                area.matchOrientation = MatchOrientation.WorldSpaceUp;
                area.interactionLayers = unchecked((int)2147483648) | 1 | UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask.GetMask("Teleport");
                area.filterSelectionByHitNormal = true;
                area.upNormalToleranceDegrees = 75f; // Rejects only very vertical walls (angle > 75°), accepts slopes/ramps up to 75°
                if (m_TeleportProvider != null)
                    area.teleportationProvider = m_TeleportProvider;
            }

            Debug.Log($"[TP1ComfortManager] Configured {count} teleportable surfaces (ground, platforms, ramps).");
        }

        /// <summary>
        /// Returns true if a collider belongs to an object that rejects teleportation
        /// (e.g. boxes, crates, blocks, red colored objects, NoTeleportZone component, or rejected obstacles).
        /// </summary>
        public static bool IsRedOrBlocked(Collider col)
        {
            if (col == null) return false;
            if (col.GetComponent<NoTeleportZone>() != null || col.GetComponentInParent<NoTeleportZone>() != null)
                return true;

            string n = col.gameObject.name.ToLowerInvariant();
            string p = col.transform.parent != null ? col.transform.parent.name.ToLowerInvariant() : "";
            string fullName = n + " " + p;

            // Reject teleportation on boxes, crates, modular blocks, obstacles, and marked objects
            if (fullName.Contains("crate") || fullName.Contains("box") || fullName.Contains("block") ||
                fullName.Contains("obstacle") || fullName.Contains("red") || fullName.Contains("no_teleport") ||
                fullName.Contains("noteleport") || fullName.Contains("m_arenaobstacle"))
            {
                return true;
            }

            var renderer = col.GetComponent<Renderer>() ?? col.GetComponentInParent<Renderer>();
            if (renderer != null && renderer.sharedMaterials != null)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null) continue;
                    string matName = mat.name.ToLowerInvariant();
                    if (matName.Contains("red") || matName.Contains("arenaobstacle"))
                        return true;

                    Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.black);
                    if (c.r > 0.6f && c.g < 0.35f && c.b < 0.35f && c.a > 0.1f)
                        return true;
                }
            }

            return false;
        }

        private void SetupVignetteProviders()
        {
            if (m_VignetteController == null)
                return;

            var providers = m_VignetteController.locomotionVignetteProviders;

            // Ensure ContinuousMoveProvider is registered
            if (m_ContinuousMoveProvider != null)
            {
                bool moveFound = false;
                foreach (var p in providers)
                {
                    if (p.locomotionProvider == m_ContinuousMoveProvider)
                    {
                        moveFound = true;
                        p.enabled = m_IsVignetteActive;
                        break;
                    }
                }
                if (!moveFound)
                {
                    providers.Add(new LocomotionVignetteProvider
                    {
                        locomotionProvider = m_ContinuousMoveProvider,
                        enabled = m_IsVignetteActive
                    });
                }
            }

            // Ensure ContinuousTurnProvider is registered
            if (m_ContinuousTurnProvider != null)
            {
                bool turnFound = false;
                foreach (var p in providers)
                {
                    if (p.locomotionProvider == m_ContinuousTurnProvider)
                    {
                        turnFound = true;
                        p.enabled = m_IsVignetteActive;
                        break;
                    }
                }
                if (!turnFound)
                {
                    providers.Add(new LocomotionVignetteProvider
                    {
                        locomotionProvider = m_ContinuousTurnProvider,
                        enabled = m_IsVignetteActive
                    });
                }
            }
        }

        private void Update()
        {
            if (!m_EnableKeyboardShortcuts || Keyboard.current == null)
                return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                SetLocomotionMode(LocomotionMode.SmoothMove);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                SetLocomotionMode(LocomotionMode.Teleport);
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                SetLocomotionMode(LocomotionMode.Dash);
            }
            else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
            {
                ToggleTeleportBlink();
            }
            else if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame)
            {
                ToggleVignette();
            }
            else if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame)
            {
                ToggleTurnMode();
            }
            else if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (m_DashProvider != null)
                    m_DashProvider.TryStartDash();
            }
            else if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                TryTestTeleportForward();
            }
        }

        // ==========================================
        // 1. LOCOMOTION MODE SELECTION
        // ==========================================

        public void SetLocomotionMode(LocomotionMode mode)
        {
            m_LocomotionMode = mode;
            if (m_ContinuousMoveProvider == null)
                FindDependencies();

            // Coordinate ContinuousMoveProvider
            if (m_ContinuousMoveProvider != null)
            {
                m_ContinuousMoveProvider.enabled = (mode == LocomotionMode.SmoothMove);
            }

            // Coordinate ControllerInputActionManagers in scene
            var actionManagers = FindObjectsByType<ControllerInputActionManager>(FindObjectsSortMode.None);
            foreach (var mgr in actionManagers)
            {
                // Left controller manages locomotion (smooth move vs teleport)
                // Right controller is strictly for view/turning and must keep smoothMotionEnabled false
                if (mgr.name.Contains("Left") || (mgr.transform.parent != null && mgr.transform.parent.name.Contains("Left")))
                {
                    mgr.smoothMotionEnabled = (mode == LocomotionMode.SmoothMove);
                }
                else
                {
                    mgr.smoothMotionEnabled = false;
                }
            }

            Debug.Log($"[TP1ComfortManager] Locomotion mode changed to: {mode}");
            onLocomotionModeChanged?.Invoke(mode);
        }

        // ==========================================
        // 2. TELEPORT BLINK OPTION
        // ==========================================

        public void ToggleTeleportBlink()
        {
            SetTeleportBlinkEnabled(!m_IsTeleportBlinkEnabled);
        }

        public void SetTeleportBlinkEnabled(bool enabled)
        {
            m_IsTeleportBlinkEnabled = enabled;
            Debug.Log($"[TP1ComfortManager] Teleport Blink transition enabled: {enabled}");
            onTeleportBlinkToggled?.Invoke(enabled);
        }

        // ==========================================
        // 3. GLOBAL TUNNELING VIGNETTE TOGGLE
        // ==========================================

        public void ToggleVignette()
        {
            SetVignetteEnabled(!m_IsVignetteActive);
        }

        public void SetVignetteEnabled(bool enabled)
        {
            m_IsVignetteActive = enabled;

            if (m_VignetteController == null)
                FindDependencies();

            if (m_VignetteController != null)
            {
                foreach (var provider in m_VignetteController.locomotionVignetteProviders)
                {
                    provider.enabled = enabled;
                }

                if (!enabled && m_DashProvider != null)
                {
                    m_VignetteController.EndTunnelingVignette(m_DashProvider);
                }
            }

            Debug.Log($"[TP1ComfortManager] Global Tunneling Vignette (Oeillère) enabled: {enabled}");
            onVignetteToggled?.Invoke(enabled);
        }

        // ==========================================
        // 4. TURN MODE TOGGLE (SNAP vs SMOOTH)
        // ==========================================

        public void ToggleTurnMode()
        {
            SetTurnMode(m_TurnMode == TurnMode.Snap ? TurnMode.Smooth : TurnMode.Snap);
        }

        public void SetTurnMode(TurnMode mode)
        {
            m_TurnMode = mode;

            if (m_SnapTurnProvider == null || m_ContinuousTurnProvider == null)
                FindDependencies();

            bool isSmooth = (mode == TurnMode.Smooth);

            if (m_SnapTurnProvider != null)
                m_SnapTurnProvider.enabled = !isSmooth;

            if (m_ContinuousTurnProvider != null)
                m_ContinuousTurnProvider.enabled = isSmooth;

            // Ensure Input Actions are enabled
            if (!isSmooth && m_RightSnapTurnAction != null && m_RightSnapTurnAction.action != null && !m_RightSnapTurnAction.action.enabled)
            {
                m_RightSnapTurnAction.action.Enable();
            }
            if (isSmooth && m_RightTurnAction != null && m_RightTurnAction.action != null && !m_RightTurnAction.action.enabled)
            {
                m_RightTurnAction.action.Enable();
            }

            // Coordinate ControllerInputActionManagers
            var actionManagers = FindObjectsByType<ControllerInputActionManager>(FindObjectsSortMode.None);
            foreach (var mgr in actionManagers)
            {
                mgr.smoothTurnEnabled = isSmooth;
            }

            Debug.Log($"[TP1ComfortManager] Turn mode changed to: {mode}");
            onTurnModeChanged?.Invoke(mode);
        }

        // ==========================================
        // TESTING UTILITIES
        // ==========================================

        public void TryTestTeleportForward()
        {
            var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null && m_TeleportProvider != null)
            {
                Vector3 fwd = origin.Camera != null ? origin.Camera.transform.forward : origin.transform.forward;
                fwd.y = 0f;
                fwd.Normalize();
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;

                Vector3 dest = origin.transform.position + fwd * 3f;
                var req = new TeleportRequest
                {
                    destinationPosition = dest,
                    destinationRotation = origin.transform.rotation,
                    matchOrientation = MatchOrientation.WorldSpaceUp
                };
                m_TeleportProvider.QueueTeleportRequest(req);
                Debug.Log($"[TP1ComfortManager] Desktop test teleport to {dest}");
            }
        }
    }
}
