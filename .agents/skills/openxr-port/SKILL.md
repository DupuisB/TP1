---
name: openxr-port
description: Step-by-step workflow to port the VR locomotion environment from Meta XR to OpenXR (Unity XR Interaction Toolkit) and implement the Dash mechanic.
---

# OpenXR Locomotion Port & Dash Implementation Guide

This skill guides an Antigravity agent through porting the existing Meta XR locomotion scene (`LocomotionExamples.unity`) to the industry-standard **OpenXR** framework using **Unity XR Interaction Toolkit (XRI 3.5.1)**, and implementing the custom **Dash** mechanic.

---

## Phase 1: Scene Preparation

1. **Duplicate the Baseline Scene**:
   - Duplicate `Assets/Settings/Project Configuration/LocomotionExamples.unity` to `Assets/Scenes/LocomotionOpenXR.unity`.
   - Ensure the original `LocomotionExamples.unity` remains untouched as a reference and backup.

2. **Clean Meta Proprietary Components**:
   - In `LocomotionOpenXR.unity`, delete or disable the `OVRCameraRig` (and any child `OVRComprehensiveInteractionRig` objects).
   - Remove Meta-specific script components attached to the environment (e.g. `LocomotionEnvironment`, `TeleportHotspot`, `TeleportBlocker` scripts), but **keep all MeshFilters, MeshRenderers, and Colliders intact**.

---

## Phase 2: OpenXR Rig & Interaction Setup

1. **Add the XR Origin Setup**:
   - Add the standard XRI complete rig prefab or instantiate `XR Origin (VR)`.
   - Required components on the rig root:
     - `XR Origin` (configured with Camera Offset and Main Camera tagged as `MainCamera`).
     - `LocomotionMediator` (coordinates all locomotion providers).
     - `TeleportationProvider` (handles teleportation jumps).
     - `SnapTurnProvider` (handles 45-degree snap turning).
     - `TunnelingVignetteController` (comfort mode visual occlusion).
   - Controllers:
     - Left & Right Controller objects equipped with `ActionBasedController` or `XRController` reading standard XR Input Actions.
     - Add `XRRayInteractor` and `LineRenderer` to controllers for teleport ray projection.

2. **Configure Walkable Surfaces**:
   - For all ground colliders (`Meta_Locomotion_groundKitchen`, `Meta_Locomotion_groundStart`, carpets, stairs):
     - Add the `TeleportationArea` component.
     - Set the teleportation trigger to `OnDeactivated` (teleport occurs when releasing the thumbstick / trigger).

---

## Phase 3: Custom Dash Mechanic (`DashProvider`)

Create `Assets/Scripts/DashProvider.cs` following this architecture:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace LOG8704.Locomotion
{
    /// <summary>
    /// Custom LocomotionProvider for rapid directional translation (Dashing).
    /// </summary>
    public class DashProvider : LocomotionProvider
    {
        [Header("Dash Settings")]
        [Tooltip("Maximum distance of a single dash in meters")]
        [SerializeField] private float m_DashDistance = 5.0f;

        [Tooltip("Duration of the dash in seconds")]
        [SerializeField] private float m_DashDuration = 0.18f;

        [Tooltip("Cooldown period between consecutive dashes")]
        [SerializeField] private float m_Cooldown = 0.5f;

        [Tooltip("Easing curve for the dash translation")]
        [SerializeField] private AnimationCurve m_DashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Collision & Safety")]
        [SerializeField] private LayerMask m_ObstacleLayers = ~0;
        [SerializeField] private float m_PlayerRadius = 0.3f;
        [SerializeField] private float m_SkinWidth = 0.1f;

        [Header("Comfort & Feedback")]
        [SerializeField] private TunnelingVignetteController m_VignetteController;
        [SerializeField] private InputActionProperty m_DashAction;

        private bool m_IsDashing;
        private float m_LastDashTime;

        protected void OnEnable()
        {
            if (m_DashAction.action != null)
            {
                m_DashAction.action.Enable();
                m_DashAction.action.performed += OnDashInput;
            }
        }

        protected void OnDisable()
        {
            if (m_DashAction.action != null)
            {
                m_DashAction.action.performed -= OnDashInput;
                m_DashAction.action.Disable();
            }
        }

        private void OnDashInput(InputAction.CallbackContext context)
        {
            if (m_IsDashing || Time.time < m_LastDashTime + m_Cooldown)
                return;

            TryStartDash();
        }

        public bool TryStartDash()
        {
            if (!CanBeginLocomotion())
                return false;

            // Compute direction based on forward gaze or hand direction
            Transform head = xrOrigin.Camera.transform;
            Vector3 dashDirection = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (dashDirection.sqrMagnitude < 0.01f)
                return false;

            // Collision sweep check
            float targetDistance = m_DashDistance;
            Vector3 startPos = xrOrigin.Origin.transform.position + Vector3.up * 0.5f;
            if (Physics.SphereCast(startPos, m_PlayerRadius, dashDirection, out RaycastHit hit, m_DashDistance, m_ObstacleLayers))
            {
                targetDistance = Mathf.Max(0f, hit.distance - m_SkinWidth);
            }

            if (targetDistance <= 0.1f)
                return false;

            Vector3 destination = xrOrigin.Origin.transform.position + dashDirection * targetDistance;
            StartCoroutine(PerformDash(destination));
            return true;
        }

        private IEnumerator PerformDash(Vector3 targetPosition)
        {
            m_IsDashing = true;
            BeginLocomotion();

            Vector3 originStart = xrOrigin.Origin.transform.position;
            float elapsed = 0f;

            while (elapsed < m_DashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / m_DashDuration);
                float curvedT = m_DashCurve.Evaluate(t);
                xrOrigin.Origin.transform.position = Vector3.Lerp(originStart, targetPosition, curvedT);
                yield return null;
            }

            xrOrigin.Origin.transform.position = targetPosition;
            m_LastDashTime = Time.time;
            m_IsDashing = false;
            EndLocomotion();
        }
    }
}
```

---

## Phase 4: Build & Verification

1. **Update Build Settings**:
   - Register `Assets/Scenes/LocomotionOpenXR.unity` in `EditorBuildSettings.asset`.
2. **Desktop Testing with XR Device Simulator**:
   - Enter Play Mode in Unity Editor.
   - Use WASD + Mouse to verify Teleportation, Snap Turn, and Dashing.
3. **Quest 3 Testing**:
   - Export APK to `builds/` and deploy to Quest 3 via `metavr`:
     ```bash
     npx metavr app install --device <device_id> builds/test_build.apk
     ```
