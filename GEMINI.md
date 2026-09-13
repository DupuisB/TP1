# LOG8704 - Travail Pratique 1 (VR Locomotion on Meta Quest 3)

## Project Overview
This project is an academic Virtual Reality (VR) application targeting the **Meta Quest 3**, developed in **Unity 6 (6000.3.22f1)** with the **Universal Render Pipeline (URP 17.3.0)**.
The primary academic focus is the design and implementation of VR locomotion techniques (teleportation, turning, and comfort/anti-motion-sickness systems) following clean software engineering principles.

## Core Technology Stack & Architecture
- **Target Platform**: Meta Quest 3 standalone (Android / Horizon OS) via OpenXR.
- **Backend Loader**: `OpenXRLoader` (`com.unity.xr.openxr`, `com.unity.xr.meta-openxr`).
- **Interaction Framework**: **Unity XR Interaction Toolkit (XRI 3.5.1)** (`com.unity.xr.interaction.toolkit`).
- **Input System**: Unity New Input System (`com.unity.inputsystem` 1.20.0).
- **Desktop Testing**: Unity **XR Device Simulator** (`com.unity.xr.interaction.toolkit`).

> [!IMPORTANT]
> **OpenXR / XRI Architecture vs. Legacy Meta OVR**:
> - All new locomotion mechanics and scene setups **must** use Unity XRI (`XR Origin`, `LocomotionMediator`, `LocomotionProvider`, `XRRayInteractor`).
> - Do **not** mix `OVRCameraRig` or `OVRInput` into OpenXR scenes.
> - The original Meta XR scene (`Assets/Settings/Project Configuration/LocomotionExamples.unity`) is preserved as a reference baseline.

## Locomotion Systems & Components
1. **Teleportation**:
   - Uses `TeleportationProvider` coordinated by `LocomotionMediator`.
   - Walkable surfaces (floors, stairs, carpets) are tagged with `TeleportationArea`.
   - Controller rays use `XRRayInteractor` with line renderers configured for parabolic/projectile curve.
2. **Turning**:
   - Uses `SnapTurnProvider` (default 45° snap) or `ContinuousTurnProvider`.
3. **Comfort & Motion Sickness Mitigation**:
   - Integrated with `TunnelingVignetteController` to restrict FOV during turning and locomotion.

## Testing Guidelines
- **Fast Iteration (Desktop)**: Always test locomotion in the Unity Editor using the **XR Device Simulator** (`Left Control / Shift` to manipulate hands, `WASD` to move, `Mouse` to look/aim).
- **On-Device Validation (Quest 3)**:
  - Build Android APK via Unity Build Settings (`File ▸ Build Settings`).
  - Deploy and inspect logs using Meta's `metavr` CLI / MCP:
    ```bash
    npx metavr app install --device <device_id> builds/test_build.apk
    npx metavr log --device <device_id>
    ```
