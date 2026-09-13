---
name: openxr-port
description: Step-by-step workflow to port the VR locomotion environment from Meta XR to OpenXR (Unity XR Interaction Toolkit).
---

# OpenXR Locomotion Port Guide

This skill guides an Antigravity agent through porting the existing Meta XR locomotion scene (`LocomotionExamples.unity`) to the industry-standard **OpenXR** framework using **Unity XR Interaction Toolkit (XRI 3.5.1)**.

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
     - `ContinuousTurnProvider` (optional, for smooth turning mode).
     - `TunnelingVignetteController` (comfort mode visual occlusion).
   - Controllers:
     - Left & Right Controller objects equipped with `ActionBasedController` or `XRController` reading standard XR Input Actions.
     - Add `XRRayInteractor` and `LineRenderer` to controllers for teleport ray projection.

2. **Configure Walkable Surfaces**:
   - For all ground colliders (`Meta_Locomotion_groundKitchen`, `Meta_Locomotion_groundStart`, carpets, stairs):
     - Add the `TeleportationArea` component.
     - Set the teleportation trigger to `OnDeactivated` (teleport occurs when releasing the thumbstick / trigger).

---

## Phase 3: Build & Verification

1. **Update Build Settings**:
   - Register `Assets/Scenes/LocomotionOpenXR.unity` in `EditorBuildSettings.asset`.
2. **Desktop Testing with XR Device Simulator**:
   - Enter Play Mode in Unity Editor.
   - Use WASD + Mouse to verify Teleportation and Snap/Smooth Turning.
3. **Quest 3 Testing**:
   - Export APK to `builds/` and deploy to Quest 3 via `metavr`:
     ```bash
     npx metavr app install --device <device_id> builds/test_build.apk
     ```
