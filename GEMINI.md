# LOG8704 - Travail Pratique 1 (VR Locomotion on Meta Quest 3)

## Core Technology Stack & Architecture
- **Target Platform**: Meta Quest 3 standalone (Android / Horizon OS) via OpenXR.
- **Backend Loader**: `OpenXRLoader` (`com.unity.xr.openxr`, `com.unity.xr.meta-openxr`).
- **Interaction Framework**: **Unity XR Interaction Toolkit (XRI 3.5.1)** (`com.unity.xr.interaction.toolkit`).
- **Input System**: Unity New Input System (`com.unity.inputsystem` 1.20.0).

> [!IMPORTANT]
> **OpenXR / XRI Architecture vs. Legacy Meta OVR**:
> - All locomotion mechanics and scene setups **must** use Unity XRI (`XR Origin`, `LocomotionMediator`, `LocomotionProvider`, `XRRayInteractor`).
> - Do **not** mix `OVRCameraRig` or `OVRInput` into OpenXR scenes.

## Locomotion & Controls Mapping Rules
1. **Joystick Isolation**:
   - **Left Joystick**: Exclusively for **View / Rotation** (`SnapTurnProvider` default 45° or `ContinuousTurnProvider`). No translation.
   - **Right Joystick**: Exclusively for **Locomotion** (`ContinuousMoveProvider` in Smooth mode, `TeleportationProvider` / `DashProvider` in Teleport/Dash modes). No rotation.
2. **Comfort Systems**:
   - Field-of-view reduction via `TunnelingVignetteController`.
   - Rapid blink blackout transition via `ScreenFadeCanvas`.
3. **Forearm Wrist Menu**:
   - Attached to dorsal left forearm (`WristUIController`), glance-based visibility.

## Agent Guidelines & Verification Rules
- **Zero Runtime Test Bloat**: Do NOT generate automated Play-Mode coroutine test scripts or script-driven remote Play/Stop toggling.
- **Code & Setup Verification**:
  - Verify C# compilation cleanly with 0 errors via `read_console`.
  - Maintain reproducible scene construction via `TP1ArenaBuilder.cs`.
