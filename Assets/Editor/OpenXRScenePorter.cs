using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Unity.XR.CoreUtils;
using LOG8704.Locomotion;

namespace LOG8704.Editor
{
    public static class OpenXRScenePorter
    {
        private const string BaselineScenePath = "Assets/Settings/Project Configuration/LocomotionExamples.unity";
        private const string TargetSceneDir = "Assets/Scenes";
        private const string TargetScenePath = "Assets/Scenes/LocomotionOpenXR.unity";
        private const string XrOriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string TunnelingVignettePrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/TunnelingVignette/TunnelingVignette.prefab";

        [MenuItem("LOG8704/Port Locomotion Scene to OpenXR")]
        public static void PortLocomotionScene()
        {
            Debug.Log("[OpenXRScenePorter] Starting locomotion scene porting process...");

            // 1. Ensure target directory exists
            if (!AssetDatabase.IsValidFolder(TargetSceneDir))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            // 2. Duplicate baseline scene to target scene
            if (!File.Exists(BaselineScenePath))
            {
                Debug.LogError($"[OpenXRScenePorter] Baseline scene not found at {BaselineScenePath}!");
                return;
            }

            if (File.Exists(TargetScenePath))
            {
                Debug.Log($"[OpenXRScenePorter] Removing existing target scene at {TargetScenePath}...");
                AssetDatabase.DeleteAsset(TargetScenePath);
            }

            Debug.Log($"[OpenXRScenePorter] Copying {BaselineScenePath} to {TargetScenePath}...");
            if (!AssetDatabase.CopyAsset(BaselineScenePath, TargetScenePath))
            {
                Debug.LogError($"[OpenXRScenePorter] Failed to copy scene to {TargetScenePath}!");
                return;
            }

            AssetDatabase.Refresh();

            // 3. Open target scene
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[OpenXRScenePorter] Could not open scene at {TargetScenePath}!");
                return;
            }

            // 4. Strip Meta proprietary rigs and components
            CleanMetaComponents(scene);

            // 5. Instantiate OpenXR XR Origin Rig & Comfort Vignette
            SetupOpenXRRig(scene);

            // 6. Configure Walkable Surfaces (TeleportationArea)
            ConfigureWalkableSurfaces(scene);

            // 7. Update EditorBuildSettings
            RegisterSceneInBuildSettings(TargetScenePath);

            // 8. Save target scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[OpenXRScenePorter] Successfully ported locomotion scene to OpenXR at " + TargetScenePath);
        }

        private static void CleanMetaComponents(Scene scene)
        {
            var rootObjects = scene.GetRootGameObjects();

            // First pass: Destroy Meta camera rigs
            foreach (var root in rootObjects)
            {
                if (root == null) continue;
                string rootName = root.name;

                if (rootName.Contains("OVRCameraRig") ||
                    rootName.Contains("OVRComprehensiveInteractionRig") ||
                    rootName.Contains("OVRInteraction"))
                {
                    Debug.Log($"[OpenXRScenePorter] Destroying Meta rig GameObject: {rootName}");
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            // Second pass: Remove Meta-specific scripts attached to remaining environment GameObjects
            var allMonoBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int removedCount = 0;

            foreach (var mb in allMonoBehaviours)
            {
                if (mb == null) continue;

                var type = mb.GetType();
                var typeName = type.Name;
                var ns = type.Namespace ?? "";

                bool isMeta = ns.StartsWith("Oculus", StringComparison.OrdinalIgnoreCase) ||
                              ns.StartsWith("Meta", StringComparison.OrdinalIgnoreCase) ||
                              typeName.StartsWith("OVR", StringComparison.OrdinalIgnoreCase) ||
                              typeName.Contains("LocomotionEnvironment") ||
                              typeName.Contains("TeleportHotspot") ||
                              typeName.Contains("TeleportBlocker") ||
                              typeName.Contains("TeleportDestination") ||
                              typeName.Contains("TeleportAimVisual");

                if (isMeta)
                {
                    UnityEngine.Object.DestroyImmediate(mb);
                    removedCount++;
                }
            }

            Debug.Log($"[OpenXRScenePorter] Cleaned {removedCount} Meta-specific environment script components.");
        }

        private static void SetupOpenXRRig(Scene scene)
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
            if (rigPrefab == null)
            {
                Debug.LogError($"[OpenXRScenePorter] Failed to load XR Origin prefab at {XrOriginPrefabPath}!");
                return;
            }

            // Ensure XRInteractionManager exists
            var existingManager = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
            if (existingManager == null)
            {
                var mgrObj = new GameObject("XR Interaction Manager");
                mgrObj.AddComponent<XRInteractionManager>();
                Debug.Log("[OpenXRScenePorter] Created XRInteractionManager in scene.");
            }

            var rigInstance = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            rigInstance.name = "XR Origin (XR Rig)";
            rigInstance.transform.position = Vector3.zero;
            rigInstance.transform.rotation = Quaternion.identity;

            // Configure Floor tracking
            var xrOrigin = rigInstance.GetComponent<XROrigin>();
            if (xrOrigin != null)
            {
                xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            }

            // Locate camera
            var cam = rigInstance.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.tag = "MainCamera";
                cam.gameObject.tag = "MainCamera";
            }

            // Locate or add LocomotionMediator
            var mediator = rigInstance.GetComponentInChildren<LocomotionMediator>(true);
            if (mediator == null)
            {
                mediator = rigInstance.AddComponent<LocomotionMediator>();
            }

            // Setup Comfort Tunneling Vignette
            TunnelingVignetteController vignetteController = null;
            var vignettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TunnelingVignettePrefabPath);
            if (vignettePrefab != null && cam != null)
            {
                var vignetteObj = (GameObject)PrefabUtility.InstantiatePrefab(vignettePrefab, scene);
                vignetteObj.name = "TunnelingVignette";
                vignetteObj.transform.SetParent(cam.transform, false);
                vignetteObj.transform.localPosition = Vector3.zero;
                vignetteObj.transform.localRotation = Quaternion.identity;
                vignetteController = vignetteObj.GetComponent<TunnelingVignetteController>();
            }

            // Add and configure DashProvider
            var locomotionObj = rigInstance.transform.Find("Locomotion");
            GameObject targetHost = locomotionObj != null ? locomotionObj.gameObject : rigInstance;

            var dashProvider = targetHost.GetComponent<DashProvider>();
            if (dashProvider == null)
            {
                dashProvider = targetHost.AddComponent<DashProvider>();
            }

            dashProvider.mediator = mediator;
            dashProvider.vignetteController = vignetteController;

            Debug.Log("[OpenXRScenePorter] XR Origin Rig, Comfort Vignette, and DashProvider successfully installed.");
        }

        private static void ConfigureWalkableSurfaces(Scene scene)
        {
            int configuredCount = 0;
            var allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var renderer in allRenderers)
            {
                if (renderer == null) continue;
                var go = renderer.gameObject;
                string name = go.name;

                bool isWalkable = name.Equals("Meta_Locomotion_groundKitchen", StringComparison.OrdinalIgnoreCase) ||
                                 name.Equals("Meta_Locomotion_groundStart", StringComparison.OrdinalIgnoreCase) ||
                                 name.Equals("RoundCarpet", StringComparison.OrdinalIgnoreCase) ||
                                 name.Equals("MainCarpet", StringComparison.OrdinalIgnoreCase) ||
                                 name.Equals("GroundCollider", StringComparison.OrdinalIgnoreCase) ||
                                 name.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 name.IndexOf("carpet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 name.IndexOf("stairs", StringComparison.OrdinalIgnoreCase) >= 0;

                // Do not mark walls, ceilings, UI, or small props as walkable
                if (name.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("ceiling", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isWalkable = false;
                }

                if (isWalkable)
                {
                    // Ensure collider exists
                    var col = go.GetComponent<Collider>();
                    if (col == null)
                    {
                        col = go.AddComponent<MeshCollider>();
                    }

                    // Add or configure TeleportationArea
                    var teleportArea = go.GetComponent<TeleportationArea>();
                    if (teleportArea == null)
                    {
                        teleportArea = go.AddComponent<TeleportationArea>();
                    }

                    teleportArea.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnDeactivated;
                    teleportArea.matchOrientation = MatchOrientation.WorldSpaceUp;

                    configuredCount++;
                }
            }

            Debug.Log($"[OpenXRScenePorter] Configured {configuredCount} walkable surfaces with TeleportationArea (OnDeactivated trigger).");
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            bool alreadyRegistered = false;
            foreach (var s in scenes)
            {
                if (s.path.Equals(scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    s.enabled = true;
                    alreadyRegistered = true;
                    break;
                }
            }

            if (!alreadyRegistered)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"[OpenXRScenePorter] Registered scene {scenePath} at index 0 in EditorBuildSettings.");
            }
        }
    }
}
