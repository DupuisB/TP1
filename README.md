# LOG8704 - TP1 (VR Locomotion on Meta Quest 3)

## Project Overview
VR locomotion laboratory for Meta Quest 3 built with **Unity 6 (6000.3.22f1)**, **Universal Render Pipeline (URP 17.3.0)**, and **Unity XR Interaction Toolkit 3.5.1 (OpenXR)**.

---

## Developer Testing Guide

### 1. Desktop Fast Iteration (Unity XR Device Simulator)
Test locomotion and UI ergonomics directly in the Unity Editor Game view without putting on the headset:
* **WASD**: Move the simulated player rig.
* **Mouse Look**: Aim the headset camera / view direction.
* **Left Control**: Hold to take control of the **Left Hand** controller.
* **Left Shift**: Hold to take control of the **Right Hand** controller.
* **G / Space**: Trigger grips or secondary actions.
* **[M] Key**: Toggle force-visibility of the **Forearm Holographic Gauntlet Menu** in the Editor.

---

### 2. On-Device Validation (Meta Quest 3)
1. **Build Android APK**:
   * Open `File ▸ Build Settings`.
   * Ensure `TP1_TestArena.unity` is at Index 0.
   * Target Platform: **Android**.
   * Click **Build** to generate `builds/TP1_Build.apk`.

2. **Deploy & View Logs via Meta CLI (`metavr`)**:
   ```bash
   # Install APK to connected Quest 3
   npx metavr app install --device <device_id> builds/TP1_Build.apk

   # Tail device logcat output
   npx metavr log --device <device_id>
   ```

---

## Controls & Locomotion Architecture

| Hand / Stick | Role | Mechanics |
| :--- | :--- | :--- |
| **Left Joystick** | **Locomotion Only** | Continuous Smooth Move (Walk), Parabolic Teleport (Blink / Instant), or Directional Dash (0.2s). Rotation disabled. |
| **Right Joystick** | **View Only** | Snap Turn (45°) or Continuous Smooth Turn (60°/s). Translation disabled. |
| **Left Forearm** | **Gauntlet UI** | Glance-based reveal when wrist is turned towards eyes. Contains Walk, Teleport, Dash, Blink, Vignette, and Turn mode icon buttons. |
| **Right Hand** | **Direct / Ray Interaction** | Near grab, far-ray grab, and socket docking on interactive props and UI buttons. |
