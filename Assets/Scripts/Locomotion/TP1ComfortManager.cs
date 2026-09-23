using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;
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
        [SerializeField] private ClimbProvider m_ClimbProvider;
        [SerializeField] private JumpProvider m_JumpProvider;
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

            if (m_ClimbProvider == null)
                m_ClimbProvider = FindFirstObjectByType<ClimbProvider>(FindObjectsInactive.Include);

            if (m_JumpProvider == null)
                m_JumpProvider = FindFirstObjectByType<JumpProvider>(FindObjectsInactive.Include);

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
        /// Ensures walkable ground, floors, platforms, ramps, stairs, modular blocks, and crates
        /// have active TeleportationArea components, while red surfaces (red.mat) and vertical walls/props are rejected.
        /// </summary>
        public void EnsureAllSurfacesTeleportable()
        {
            var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                    continue;

                // 1. Red surfaces and marked blocked objects must NEVER be teleportable
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

                // 2. Only surfaces where teleportation makes sense receive TeleportationArea
                if (IsWalkableSurface(col))
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
                    area.upNormalToleranceDegrees = 60f; // Accepts up to 60° slopes/ramps (including 45° ramps and stairs), rejects vertical walls/sides (90°)
                    if (m_TeleportProvider != null)
                        area.teleportationProvider = m_TeleportProvider;
                    count++;
                }
                else
                {
                    // Non-walkable objects (walls, columns, foliage, props) must not have TeleportationArea
                    var existingArea = col.GetComponent<TeleportationArea>();
                    if (existingArea != null)
                    {
                        if (Application.isPlaying) Destroy(existingArea);
                        else DestroyImmediate(existingArea);
                    }
                }
            }

            // Also cleanup any stray or misconfigured TeleportationArea components in the scene
            var allAreas = FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None);
            foreach (var area in allAreas)
            {
                var col = area.GetComponent<Collider>();
                if (col == null || IsRedOrBlocked(col) || !IsWalkableSurface(col))
                {
                    if (Application.isPlaying) Destroy(area);
                    else DestroyImmediate(area);
                    continue;
                }

                area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
                area.matchOrientation = MatchOrientation.WorldSpaceUp;
                area.interactionLayers = unchecked((int)2147483648) | 1 | UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask.GetMask("Teleport");
                area.filterSelectionByHitNormal = true;
                area.upNormalToleranceDegrees = 60f;
                if (m_TeleportProvider != null)
                    area.teleportationProvider = m_TeleportProvider;
            }

            Debug.Log($"[TP1ComfortManager] Configured {count} teleportable surfaces (ground, floors, platforms, blocks, crates, ramps). Red surfaces strictly excluded.");
        }

        /// <summary>
        /// Returns true if the collider represents a walkable/standable surface where teleportation makes sense
        /// (ground, terrain, floors, platforms, stairs, ramps, modular blocks, crates, roofs, decks),
        /// excluding vertical obstacles (walls, fences, columns, doors, ladders) and small props/foliage.
        /// </summary>
        public static bool IsWalkableSurface(Collider col)
        {
            if (col == null || col.isTrigger) return false;

            string n = col.gameObject.name.ToLowerInvariant();
            string p = col.transform.parent != null ? col.transform.parent.name.ToLowerInvariant() : "";
            string fullName = n + " " + p;

            // Exclude vertical structures, frames, ladders, barriers
            if (fullName.Contains("wall") || fullName.Contains("fence") || fullName.Contains("door") || 
                fullName.Contains("window") || fullName.Contains("boundary") || fullName.Contains("column") ||
                fullName.Contains("pillar") || fullName.Contains("frame") || fullName.Contains("ladder"))
            {
                return false;
            }

            // Exclude small props, weapons, coins, cones, targets, arrows, foliage, vehicles, rig, and safety catch floors
            if (fullName.Contains("tree") || fullName.Contains("sword") || fullName.Contains("coin") || fullName.Contains("cone") ||
                fullName.Contains("arrow") || fullName.Contains("target") || fullName.Contains("controller") || fullName.Contains("hand") ||
                fullName.Contains("shield") || fullName.Contains("watergun") || fullName.Contains("waterpistol") || fullName.Contains("refill") ||
                fullName.Contains("ring") || fullName.Contains("question") || fullName.Contains("car") || fullName.Contains("plane_stunt") ||
                fullName.Contains("wheel") || fullName.Contains("sphere") || fullName.Contains("tube") || fullName.Contains("xr origin") ||
                fullName.Contains("camera") || fullName.Contains("canvas") || fullName.Contains("pedestal") || fullName.Contains("socket") ||
                fullName.Contains("safety"))
            {
                return false;
            }

            // Match walkable/standable geometry (Safety_Teleport_Floor excluded to prevent teleporting into the void)
            return fullName.Contains("ground") || fullName.Contains("floor") || fullName.Contains("road") || fullName.Contains("path") || 
                   fullName.Contains("dirt") || fullName.Contains("grass") || fullName.Contains("concrete") || 
                   fullName.Contains("platform") || fullName.Contains("ramp") || fullName.Contains("stairs") || fullName.Contains("roof") || 
                   fullName.Contains("bench") || fullName.Contains("table") || fullName.Contains("step") || fullName.Contains("deck") || 
                   fullName.Contains("rock") || fullName.Contains("mountain") || fullName.Contains("bld") || fullName.Contains("house") ||
                   fullName.Contains("block") || fullName.Contains("crate") || fullName.Contains("box");
        }

        /// <summary>
        /// Returns true if a collider belongs to an object that rejects teleportation
        /// (e.g. red colored objects using red.mat, NoTeleportZone component, or marked obstacles).
        /// </summary>
        public static bool IsRedOrBlocked(Collider col)
        {
            if (col == null) return false;
            if (col.GetComponent<NoTeleportZone>() != null || col.GetComponentInParent<NoTeleportZone>() != null)
                return true;

            string n = col.gameObject.name.ToLowerInvariant();
            string p = col.transform.parent != null ? col.transform.parent.name.ToLowerInvariant() : "";
            string fullName = n + " " + p;

            // Explicitly blocked by name (obstacles, marked no-teleport zones, or explicitly named red)
            if (fullName.Contains("obstacle") || fullName.Contains("red") || fullName.Contains("no_teleport") ||
                fullName.Contains("noteleport") || fullName.Contains("m_arenaobstacle"))
            {
                return true;
            }

            // Inspect all renderers attached to the collider, its children, or its parent
            var renderers = col.GetComponentsInChildren<Renderer>();
            var parentRenderer = col.GetComponentInParent<Renderer>();

            bool CheckRenderer(Renderer r)
            {
                if (r == null || r.sharedMaterials == null) return false;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    string matName = mat.name.ToLowerInvariant();
                    // Matches "red", "red.mat", "polygonstarter_02" (legacy red name), "arenaobstacle"
                    if (matName.Contains("red") || matName.Contains("polygonstarter_02") || matName.Contains("arenaobstacle"))
                        return true;

                    // Check albedo texture name if available
                    if (mat.HasProperty("_Albedo_Map"))
                    {
                        var tex = mat.GetTexture("_Albedo_Map");
                        if (tex != null && (tex.name.ToLowerInvariant().Contains("red") || tex.name.ToLowerInvariant().Contains("polygonstarter_02")))
                            return true;
                    }
                    if (mat.mainTexture != null)
                    {
                        if (mat.mainTexture.name.ToLowerInvariant().Contains("red") || mat.mainTexture.name.ToLowerInvariant().Contains("polygonstarter_02"))
                            return true;
                    }

                    // Check color properties
                    Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.black);
                    if (c.r > 0.6f && c.g < 0.35f && c.b < 0.35f && c.a > 0.1f)
                        return true;
                }
                return false;
            }

            if (parentRenderer != null && CheckRenderer(parentRenderer))
                return true;

            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (CheckRenderer(r))
                        return true;
                }
            }

            return false;
        }

        private void SetupVignetteProviders()
        {
            if (m_VignetteController == null)
                return;

            EnsureVignetteProvider(m_ContinuousMoveProvider);
            EnsureVignetteProvider(m_ContinuousTurnProvider);

            // Smaller aperture values create a stronger vignette. Keep jump protection
            // modestly stronger than the default and climbing protection barely visible.
            EnsureVignetteProvider(m_JumpProvider, CreateVignetteParameters(0.62f, 0.08f, 0.2f));
            EnsureVignetteProvider(m_ClimbProvider, CreateVignetteParameters(0.92f, 0.12f, 0.2f));
        }

        private static VignetteParameters CreateVignetteParameters(float apertureSize, float easeInTime, float easeOutTime)
        {
            return new VignetteParameters
            {
                apertureSize = apertureSize,
                featheringEffect = 0.2f,
                easeInTime = easeInTime,
                easeOutTime = easeOutTime
            };
        }

        private void EnsureVignetteProvider(UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider locomotionProvider, VignetteParameters overrideParameters = null)
        {
            if (locomotionProvider == null)
                return;

            var providers = m_VignetteController.locomotionVignetteProviders;
            LocomotionVignetteProvider registration = null;
            foreach (var provider in providers)
            {
                if (provider.locomotionProvider == locomotionProvider)
                {
                    registration = provider;
                    break;
                }
            }

            if (registration == null)
            {
                registration = new LocomotionVignetteProvider
                {
                    locomotionProvider = locomotionProvider
                };
                providers.Add(registration);
            }

            registration.enabled = m_IsVignetteActive;
            if (overrideParameters != null)
            {
                registration.overrideDefaultParameters = true;
                registration.overrideParameters = overrideParameters;
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

                if (!enabled)
                {
                    foreach (var provider in m_VignetteController.locomotionVignetteProviders)
                    {
                        m_VignetteController.EndTunnelingVignette(provider);
                    }
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
