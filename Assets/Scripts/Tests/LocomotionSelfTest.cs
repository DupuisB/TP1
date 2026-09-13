using System.Collections;
using UnityEngine;
using LOG8704.Locomotion;
using Unity.XR.CoreUtils;

namespace LOG8704.Tests
{
    /// <summary>
    /// Runtime automated self-test validating all 5 requirements for LOG8704 TP1:
    /// 1. Locomotion Mode Selection (SmoothMove, Teleport, Dash)
    /// 2. Dedicated Teleport Blink Option (Blink vs Instant)
    /// 3. Global Tunneling Vignette Toggle for all modes
    /// 4. Turn Mode Toggle (Snap 45° vs Smooth)
    /// 5. Dash Position Persistence (Verify No Position Reset Bug)
    /// </summary>
    [AddComponentMenu("LOG8704/Tests/Locomotion Self Test")]
    public class LocomotionSelfTest : MonoBehaviour
    {
        [Tooltip("Automatically execute test routine on Start in Play Mode")]
        [SerializeField] private bool m_RunOnStart = false;

        private void Start()
        {
            if (m_RunOnStart)
            {
                StartCoroutine(RunValidationSuite());
            }
        }

        public IEnumerator RunValidationSuite()
        {
            Debug.Log("[LocomotionSelfTest] >>> STARTING LOG8704 TP1 AUTOMATED VALIDATION SUITE <<<");
            yield return new WaitForSeconds(0.5f);

            var mgr = TP1ComfortManager.Instance ?? FindFirstObjectByType<TP1ComfortManager>();
            if (mgr == null)
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: TP1ComfortManager not found in scene!");
                yield break;
            }

            var origin = FindFirstObjectByType<XROrigin>();
            if (origin == null)
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: XROrigin not found in scene!");
                yield break;
            }

            // ==========================================
            // TEST 1: Locomotion Mode Selection
            // ==========================================
            Debug.Log("[LocomotionSelfTest] [1/5] Testing Locomotion Modes (SmoothMove, Teleport, Dash)...");

            mgr.SetLocomotionMode(TP1ComfortManager.LocomotionMode.SmoothMove);
            yield return null;
            if (!mgr.isSmoothMoveActive || (mgr.continuousMoveProvider != null && !mgr.continuousMoveProvider.enabled))
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: SmoothMove mode activation mismatch!");
                yield break;
            }

            mgr.SetLocomotionMode(TP1ComfortManager.LocomotionMode.Teleport);
            yield return null;
            if (!mgr.isTeleportActive || (mgr.continuousMoveProvider != null && mgr.continuousMoveProvider.enabled))
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: Teleport mode activation mismatch!");
                yield break;
            }

            mgr.SetLocomotionMode(TP1ComfortManager.LocomotionMode.Dash);
            yield return null;
            if (!mgr.isDashActive)
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: Dash mode activation mismatch!");
                yield break;
            }
            Debug.Log("[LocomotionSelfTest] PASS [1/5]: Locomotion Mode Selection working correctly.");

            // ==========================================
            // TEST 2: Dedicated Teleport Blink Option
            // ==========================================
            Debug.Log("[LocomotionSelfTest] [2/5] Testing Dedicated Teleport Blink Option...");

            mgr.SetTeleportBlinkEnabled(false);
            yield return null;
            if (mgr.isTeleportBlinkEnabled)
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: Teleport Blink option did not toggle OFF!");
                yield break;
            }

            mgr.SetTeleportBlinkEnabled(true);
            yield return null;
            if (!mgr.isTeleportBlinkEnabled)
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: Teleport Blink option did not toggle ON!");
                yield break;
            }
            Debug.Log("[LocomotionSelfTest] PASS [2/5]: Dedicated Teleport Blink Option working correctly.");

            // ==========================================
            // TEST 3: Global Vignette (Oeillère) Toggle
            // ==========================================
            Debug.Log("[LocomotionSelfTest] [3/5] Testing Global Vignette Toggle for all modes...");

            mgr.SetVignetteEnabled(false);
            yield return null;
            if (mgr.isVignetteActive)
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: Vignette did not toggle OFF!");
                yield break;
            }

            mgr.SetVignetteEnabled(true);
            yield return null;
            if (!mgr.isVignetteActive)
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: Vignette did not toggle ON!");
                yield break;
            }
            Debug.Log("[LocomotionSelfTest] PASS [3/5]: Global Vignette Toggle working correctly.");

            // ==========================================
            // TEST 4: Turn Mode Toggle (Snap 45° vs Smooth)
            // ==========================================
            Debug.Log("[LocomotionSelfTest] [4/5] Testing Turn Mode Toggle (Snap vs Smooth)...");

            mgr.SetTurnMode(TP1ComfortManager.TurnMode.Snap);
            yield return null;
            if (!mgr.isSnapTurnActive || (mgr.snapTurnProvider != null && !mgr.snapTurnProvider.enabled) || (mgr.continuousTurnProvider != null && mgr.continuousTurnProvider.enabled))
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: Snap Turn mode provider mismatch!");
                yield break;
            }

            mgr.SetTurnMode(TP1ComfortManager.TurnMode.Smooth);
            yield return null;
            if (!mgr.isSmoothTurnActive || (mgr.snapTurnProvider != null && mgr.snapTurnProvider.enabled) || (mgr.continuousTurnProvider != null && !mgr.continuousTurnProvider.enabled))
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: Smooth Turn mode provider mismatch!");
                yield break;
            }

            // Restore Snap mode default
            mgr.SetTurnMode(TP1ComfortManager.TurnMode.Snap);
            Debug.Log("[LocomotionSelfTest] PASS [4/5]: Turn Mode Toggle working correctly.");

            // ==========================================
            // TEST 5: Dash Position Persistence (NO RESET)
            // ==========================================
            Debug.Log("[LocomotionSelfTest] [5/5] Testing Dash Position Persistence (Verify No Reset Bug)...");

            Vector3 startPos = origin.Origin.transform.position;
            Vector3 targetDestination = startPos + Vector3.forward * 4.0f;

            if (mgr.dashProvider == null)
            {
                Debug.LogError("[LocomotionSelfTest] FAIL: DashProvider is null!");
                yield break;
            }

            mgr.dashProvider.DashTo(targetDestination);

            // Wait for dash duration (0.2s) + buffer
            yield return new WaitForSeconds(0.40f);

            Vector3 finalPos = origin.Origin.transform.position;
            float distFromStart = Vector3.Distance(finalPos, startPos);
            float distToTarget = Vector3.Distance(finalPos, targetDestination);

            Debug.Log($"[LocomotionSelfTest] Dash motion result: Start={startPos} -> End={finalPos} (Moved={distFromStart:F2}m, OffsetFromTarget={distToTarget:F2}m)");

            if (distFromStart < 1.0f)
            {
                Debug.LogError($"[LocomotionSelfTest] FAIL: Dash RESET back to start position! FinalPos={finalPos}");
                yield break;
            }

            if (distToTarget > 0.5f)
            {
                Debug.LogError($"[LocomotionSelfTest] FAIL: Dash did not reach expected target! Dist={distToTarget:F2}m");
                yield break;
            }

            Debug.Log("[LocomotionSelfTest] PASS [5/5]: Dash Position Persistence! Body remained at destination without snapping back.");

            Debug.Log("[LocomotionSelfTest] ========================================================");
            Debug.Log("[LocomotionSelfTest] ALL 5 TP1 REQUIREMENTS SUCCESSFULLY VERIFIED AND PASSED!");
            Debug.Log("[LocomotionSelfTest] ========================================================");
        }
    }
}
