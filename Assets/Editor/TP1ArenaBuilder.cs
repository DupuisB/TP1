using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using TMPro;
using LOG8704.Locomotion;
using LOG8704.UI;

namespace LOG8704.Editor
{
    public static class TP1ArenaBuilder
    {
        private const string TargetSceneDir = "Assets/Scenes";
        public const string TargetScenePath = "Assets/Scenes/TP1_TestArena.unity";
        public const string SyntyScenePath = "Assets/Synty/PolygonStarter/Scenes/Demo.unity";
        private const string MaterialsDir = "Assets/Materials";
        private const string XrOriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string TunnelingVignettePrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/TunnelingVignette/TunnelingVignette.prefab";

        [InitializeOnLoadMethod]
        private static void OnInitialize()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlaying && EditorPrefs.GetBool("TP1_Rebuild_V6", false))
                {
                    EditorPrefs.SetBool("TP1_Rebuild_V6", false);
                    Debug.Log("[TP1ArenaBuilder] Generating pristine TP1 Test Arena scene with unpacked rig (V6)...");
                    BuildArenaScene();
                }

                if (!EditorApplication.isPlaying && !EditorPrefs.GetBool("TP1_Boxes_Teleportable_V1", false))
                {
                    EditorPrefs.SetBool("TP1_Boxes_Teleportable_V1", true);
                    Debug.Log("[TP1ArenaBuilder] Automatically configuring Synty Demo scene with elevated surfaces (boxes, crates, platforms, ramps)...");
                    SetupSyntyDemoScene();
                }

                if (!EditorApplication.isPlaying && !EditorPrefs.GetBool("TP1_Pistols_V1", false))
                {
                    EditorPrefs.SetBool("TP1_Pistols_V1", true);
                    Debug.Log("[TP1ArenaBuilder] Automatically configuring Grabbable Water Pistols in scenes...");
                    WaterPistolSetupUtility.CreateOrUpdateWaterPistolPrefab();
                    SetupSyntyDemoScene();
                }
            };
        }

        [MenuItem("LOG8704/Build TP1 Test Arena Scene")]
        public static void BuildArenaScene()
        {
            Debug.Log("[TP1ArenaBuilder] Starting automated creation of TP1 Test Arena scene...");

            // 0. Preserve existing customized Wrist UI settings before wiping the scene
            Vector3? preservedWristPos = null;
            Vector3? preservedWristRot = null;
            var existingWristInActiveScene = UnityEngine.Object.FindFirstObjectByType<WristUIController>();
            if (existingWristInActiveScene != null)
            {
                preservedWristPos = existingWristInActiveScene.uiLocalPosition;
                preservedWristRot = existingWristInActiveScene.uiLocalEuler;
            }
            else
            {
                // Fallback to user-customized dorsal watch position
                preservedWristPos = new Vector3(-0.04f, -0.2f, -0.2f);
                preservedWristRot = new Vector3(0f, 100f, 10f);
            }

            // 1. Ensure directories exist
            EnsureDirectory(TargetSceneDir);
            EnsureDirectory(MaterialsDir);

            // 2. Create / load materials
            var floorMat = GetOrCreateMaterial("Assets/Materials/M_ArenaFloor.mat", new Color(0.18f, 0.21f, 0.26f), 0.3f);
            var wallMat = GetOrCreateMaterial("Assets/Materials/M_ArenaWall.mat", new Color(0.11f, 0.12f, 0.15f), 0.1f);
            var obstacleMat = GetOrCreateMaterial("Assets/Materials/M_ArenaObstacle.mat", new Color(0.85f, 0.15f, 0.15f), 0.3f);
            var obstacleTopMat = GetOrCreateMaterial("Assets/Materials/M_ArenaObstacleTop.mat", new Color(0.75f, 0.12f, 0.12f), 0.2f);
            var platformMat = GetOrCreateMaterial("Assets/Materials/M_ArenaPlatform.mat", new Color(0.15f, 0.65f, 0.25f), 0.3f);
            var platformTopMat = GetOrCreateMaterial("Assets/Materials/M_ArenaPlatformTop.mat", new Color(0.20f, 0.78f, 0.30f), 0.4f);

            // 3. Create a fresh scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 4. Setup Lighting
            SetupLighting();

            // 5. Ensure XRInteractionManager
            EnsureInteractionManager();

            // 6. Build Environment Hierarchy
            var envRoot = new GameObject("Environment");

            // A. Walkable Floor with TeleportationArea
            BuildFloor(envRoot.transform, floorMat);

            // B. Boundary Walls with Colliders (blocking joystick move & teleportation)
            BuildBoundaryWalls(envRoot.transform, wallMat);

            // C. Obstacle Box (blocking joystick move, top ray aimable but rejects teleportation)
            BuildObstacleBox(envRoot.transform, obstacleMat, obstacleTopMat);

            // D. Teleportable Platform Box (elevated platform with valid TeleportationArea)
            BuildTeleportPlatformBox(envRoot.transform, platformMat, platformTopMat);

            // E. Interaction Test Station (Pedestal with dynamic XRGrabInteractable cube and XRSocketInteractor)
            var pedestalMat = GetOrCreateMaterial("Assets/Materials/M_ArenaPedestal.mat", new Color(0.14f, 0.16f, 0.20f), 0.2f);
            var grabMat = GetOrCreateMaterial("Assets/Materials/M_GrabbableCube.mat", new Color(0.18f, 0.55f, 0.92f), 0.4f);
            var socketRingMat = GetOrCreateMaterial("Assets/Materials/M_SocketRing.mat", new Color(0.92f, 0.65f, 0.15f), 0.5f);
            BuildInteractionTestStation(envRoot.transform, pedestalMat, grabMat, socketRingMat);

            // 7. Setup XR Rig and Locomotion
            var rigInstance = SetupXRRig(scene, preservedWristPos, preservedWristRot);

            // 7. Setup EventSystem
            SetupEventSystem();

            // 8. Save scene to Assets/Scenes/TP1_TestArena.unity
            EditorSceneManager.SaveScene(scene, TargetScenePath);
            Debug.Log($"[TP1ArenaBuilder] Scene successfully saved to {TargetScenePath}");

            // 9. Register as Scene 0 in EditorBuildSettings
            RegisterSceneAsScene0(TargetScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TP1ArenaBuilder] Successfully built and configured LOG8704 TP1 Test Arena!");
        }

        [MenuItem("LOG8704/Setup Synty Demo Scene for VR")]
        public static void SetupSyntyDemoScene()
        {
            Debug.Log($"[TP1ArenaBuilder] Starting VR setup for Synty Demo scene ({SyntyScenePath})...");

            if (!File.Exists(SyntyScenePath))
            {
                Debug.LogError($"[TP1ArenaBuilder] Synty Demo scene not found at {SyntyScenePath}!");
                return;
            }

            // 0. Preserve wrist position if any in current active scene
            Vector3? preservedWristPos = null;
            Vector3? preservedWristRot = null;
            var existingWristInActiveScene = UnityEngine.Object.FindFirstObjectByType<WristUIController>();
            if (existingWristInActiveScene != null)
            {
                preservedWristPos = existingWristInActiveScene.uiLocalPosition;
                preservedWristRot = existingWristInActiveScene.uiLocalEuler;
            }
            else
            {
                preservedWristPos = new Vector3(-0.04f, -0.2f, -0.2f);
                preservedWristRot = new Vector3(0f, 100f, 10f);
            }

            // 1. Open the Synty Demo scene
            var scene = EditorSceneManager.OpenScene(SyntyScenePath, OpenSceneMode.Single);

            // 2. Remove non-XR desktop Camera(s) and AudioListener(s)
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.GetComponentInParent<XROrigin>() == null)
                {
                    Debug.Log($"[TP1ArenaBuilder] Removing non-XR camera: {cam.gameObject.name}");
                    UnityEngine.Object.DestroyImmediate(cam.gameObject);
                }
            }

            // Remove any existing XR Origin to prevent duplicates
            var existingRigs = UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None);
            foreach (var r in existingRigs)
            {
                UnityEngine.Object.DestroyImmediate(r.gameObject);
            }

            // 3. Ensure XRInteractionManager & Setup EventSystem with XRUIInputModule
            EnsureInteractionManager();
            SetupEventSystem();

            // Strip pre-existing TeleportationAreas from props to avoid teleport traps
            var allExistingTeleportAreas = UnityEngine.Object.FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None);
            foreach (var area in allExistingTeleportAreas)
            {
                UnityEngine.Object.DestroyImmediate(area);
            }

            // 4. Configure TeleportationArea on all walkable ground and elevated platforms
            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            int surfaceCount = 0;
            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;

                // If object is red (red.mat), has NoTeleportZone, or is marked to block teleportation:
                // Ensure TeleportationArea is removed.
                if (TP1ComfortManager.IsRedOrBlocked(col))
                {
                    var existingArea = col.GetComponent<TeleportationArea>();
                    if (existingArea != null)
                    {
                        UnityEngine.Object.DestroyImmediate(existingArea);
                    }
                    continue;
                }

                // Match walkable ground, floors, platforms, ramps, stairs, modular blocks, crates, roofs, decks
                if (TP1ComfortManager.IsWalkableSurface(col))
                {
                    var teleArea = col.GetComponent<TeleportationArea>();
                    if (teleArea == null)
                    {
                        teleArea = col.gameObject.AddComponent<TeleportationArea>();
                    }
                    teleArea.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
                    teleArea.matchOrientation = MatchOrientation.WorldSpaceUp;
                    teleArea.interactionLayers = unchecked((int)2147483648) | 1 | InteractionLayerMask.GetMask("Teleport");
                    teleArea.filterSelectionByHitNormal = true;
                    teleArea.upNormalToleranceDegrees = 60f;
                    surfaceCount++;
                }
                else
                {
                    var existingArea = col.GetComponent<TeleportationArea>();
                    if (existingArea != null)
                    {
                        UnityEngine.Object.DestroyImmediate(existingArea);
                    }
                }
            }

            // Cleanup any stray TeleportationAreas on red or non-walkable objects
            var allAreas = UnityEngine.Object.FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None);
            foreach (var area in allAreas)
            {
                var col = area.GetComponent<Collider>();
                if (col == null || TP1ComfortManager.IsRedOrBlocked(col) || !TP1ComfortManager.IsWalkableSurface(col))
                {
                    UnityEngine.Object.DestroyImmediate(area);
                }
            }

            Debug.Log($"[TP1ArenaBuilder] Configured TeleportationArea on {surfaceCount} valid surfaces (ground, floors, platforms, blocks, crates, ramps). Red surfaces strictly excluded.");

            // Safety floor underneath to prevent falling into void (strictly NO TeleportationArea)
            var safetyFloor = GameObject.Find("Safety_Teleport_Floor");
            if (safetyFloor == null)
            {
                safetyFloor = new GameObject("Safety_Teleport_Floor");
                safetyFloor.transform.position = new Vector3(0f, -0.05f, 0f);
                var boxCol = safetyFloor.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(300f, 0.1f, 300f);
                boxCol.center = Vector3.zero;
            }
            else
            {
                var tele = safetyFloor.GetComponent<TeleportationArea>();
                if (tele != null) UnityEngine.Object.DestroyImmediate(tele);
            }

            // 5. Setup XR Rig and Locomotion
            var rigInstance = SetupXRRig(scene, preservedWristPos, preservedWristRot);
            if (rigInstance != null)
            {
                rigInstance.transform.position = new Vector3(0f, 0.05f, 10f);
                rigInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            // 5b. Setup Water Pistol Pickup Station near player spawn (0, 0.05, 10)
            SetupDemoWaterPistolStation(scene);

            // 6. Save scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SyntyScenePath);
            Debug.Log($"[TP1ArenaBuilder] Scene successfully saved to {SyntyScenePath}");

            // 7. Register as Scene 0 in EditorBuildSettings
            RegisterSceneAsScene0(SyntyScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TP1ArenaBuilder] Synty Demo scene successfully configured as active VR locomotion scene!");
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static Material GetOrCreateMaterial(string path, Color color, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader)
                {
                    color = color
                };
                if (mat.HasProperty("_Smoothness"))
                    mat.SetFloat("_Smoothness", smoothness);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                if (mat.HasProperty("_Smoothness"))
                    mat.SetFloat("_Smoothness", smoothness);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static void SetupLighting()
        {
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.92f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.28f, 0.35f);
        }

        private static void BuildFloor(Transform parent, Material mat)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Walkable_Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(24f, 0.1f, 24f);

            if (mat != null)
                floor.GetComponent<Renderer>().sharedMaterial = mat;

            // Ensure TeleportationArea is configured with SerializedObject for persistent serialization
            var teleportArea = floor.AddComponent<TeleportationArea>();
            var col = floor.GetComponent<Collider>();
            var so = new SerializedObject(teleportArea);
            so.Update();
            var triggerProp = so.FindProperty("m_TeleportTrigger");
            if (triggerProp != null) triggerProp.intValue = 0; // OnSelectExited
            var matchProp = so.FindProperty("m_MatchOrientation");
            if (matchProp != null) matchProp.intValue = 0; // WorldSpaceUp
            var layersProp = so.FindProperty("m_InteractionLayers.m_Bits");
            if (layersProp != null) layersProp.longValue = 2147483649L;
            var filterProp = so.FindProperty("m_FilterSelectionByHitNormal");
            if (filterProp != null) filterProp.boolValue = true;
            var tolProp = so.FindProperty("m_UpNormalToleranceDegrees");
            if (tolProp != null) tolProp.floatValue = 75f;
            var colProp = so.FindProperty("m_Colliders");
            if (colProp != null && col != null)
            {
                colProp.arraySize = 1;
                colProp.GetArrayElementAtIndex(0).objectReferenceValue = col;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(teleportArea);

            Debug.Log("[TP1ArenaBuilder] Walkable floor created with TeleportationArea on layer Teleport (OnSelectExited).");
        }

        private static void BuildBoundaryWalls(Transform parent, Material mat)
        {
            var wallsRoot = new GameObject("BoundaryWalls");
            wallsRoot.transform.SetParent(parent, false);

            float arenaSize = 24f;
            float halfSize = arenaSize * 0.5f;
            float wallHeight = 4f;
            float wallThickness = 0.8f;
            float yPos = wallHeight * 0.5f;

            CreateWall(wallsRoot.transform, "Wall_North", new Vector3(0f, yPos, halfSize), new Vector3(arenaSize + wallThickness, wallHeight, wallThickness), mat);
            CreateWall(wallsRoot.transform, "Wall_South", new Vector3(0f, yPos, -halfSize), new Vector3(arenaSize + wallThickness, wallHeight, wallThickness), mat);
            CreateWall(wallsRoot.transform, "Wall_East", new Vector3(halfSize, yPos, 0f), new Vector3(wallThickness, wallHeight, arenaSize + wallThickness), mat);
            CreateWall(wallsRoot.transform, "Wall_West", new Vector3(-halfSize, yPos, 0f), new Vector3(wallThickness, wallHeight, arenaSize + wallThickness), mat);

            Debug.Log("[TP1ArenaBuilder] 4 boundary walls created with colliders (blocking joystick & teleportation).");
        }

        private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = pos;
            wall.transform.localScale = scale;

            if (mat != null)
                wall.GetComponent<Renderer>().sharedMaterial = mat;

            // Has BoxCollider by default from CreatePrimitive.
            // Explicitly NO TeleportationArea component!
        }

        private static void BuildObstacleBox(Transform parent, Material sideMat, Material topMat)
        {
            var obstacleRoot = new GameObject("Obstacle_Box");
            obstacleRoot.transform.SetParent(parent, false);
            obstacleRoot.transform.localPosition = new Vector3(0f, 0f, 4f);

            float width = 3.6f;
            float height = 1.4f;
            float depth = 3.6f;

            // Main box collider + mesh (blocks joystick movement)
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Obstacle_Body";
            box.transform.SetParent(obstacleRoot.transform, false);
            box.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            box.transform.localScale = new Vector3(width, height, depth);

            if (sideMat != null)
                box.GetComponent<Renderer>().sharedMaterial = sideMat;

            // Top horizontal surface: Can be aimed at by teleport ray, but REJECTS teleportation (NO TeleportationArea)
            var topSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topSurface.name = "Obstacle_Top_RejectedSurface";
            topSurface.transform.SetParent(obstacleRoot.transform, false);
            topSurface.transform.localPosition = new Vector3(0f, height + 0.005f, 0f);
            topSurface.transform.localScale = new Vector3(width, 0.01f, depth);

            if (topMat != null)
                topSurface.GetComponent<Renderer>().sharedMaterial = topMat;

            Debug.Log("[TP1ArenaBuilder] Obstacle Box created with vertical colliders and non-teleportable top surface.");
        }

        private static void BuildTeleportPlatformBox(Transform parent, Material sideMat, Material topMat)
        {
            var platformRoot = new GameObject("Teleport_Platform_Box");
            platformRoot.transform.SetParent(parent, false);
            platformRoot.transform.localPosition = new Vector3(5.5f, 0f, 2f);

            float width = 3.2f;
            float height = 1.2f;
            float depth = 3.2f;

            // Main platform body collider + mesh (blocks walking through, must teleport/dash on top)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Platform_Body";
            body.transform.SetParent(platformRoot.transform, false);
            body.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            body.transform.localScale = new Vector3(width, height, depth);

            if (sideMat != null)
                body.GetComponent<Renderer>().sharedMaterial = sideMat;

            // Top surface with TeleportationArea (allows valid teleport and dash)
            var topSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topSurface.name = "Platform_Top_TeleportSurface";
            topSurface.transform.SetParent(platformRoot.transform, false);
            topSurface.transform.localPosition = new Vector3(0f, height + 0.005f, 0f);
            topSurface.transform.localScale = new Vector3(width, 0.01f, depth);

            if (topMat != null)
                topSurface.GetComponent<Renderer>().sharedMaterial = topMat;

            // Add TeleportationArea to top surface
            var teleportArea = topSurface.AddComponent<TeleportationArea>();
            teleportArea.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            teleportArea.matchOrientation = MatchOrientation.WorldSpaceUp;
            teleportArea.interactionLayers = unchecked((int)2147483648) | 1 | InteractionLayerMask.GetMask("Teleport");
            teleportArea.filterSelectionByHitNormal = true;
            teleportArea.upNormalToleranceDegrees = 75f;

            Debug.Log("[TP1ArenaBuilder] Teleport Platform Box created with TeleportationArea on top surface.");
        }

        private static void BuildInteractionTestStation(Transform parent, Material tableMat, Material cubeMat, Material socketMat)
        {
            var stationRoot = new GameObject("Interaction_Test_Station");
            stationRoot.transform.SetParent(parent, false);
            stationRoot.transform.localPosition = new Vector3(-4.0f, 0f, 2.0f);

            // 1. Pedestal Table (Blocks movement and provides surface for items)
            float tableHeight = 0.8f;
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Station_Pedestal";
            table.transform.SetParent(stationRoot.transform, false);
            table.transform.localPosition = new Vector3(0f, tableHeight * 0.5f, 0f);
            table.transform.localScale = new Vector3(2.2f, tableHeight, 0.9f);
            if (tableMat != null)
                table.GetComponent<Renderer>().sharedMaterial = tableMat;

            // 2. Grabbable Dynamic Cube (XRGrabInteractable)
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Grabbable_Cube";
            cube.transform.SetParent(stationRoot.transform, false);
            cube.transform.localPosition = new Vector3(-0.65f, tableHeight + 0.12f, 0f);
            cube.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            if (cubeMat != null)
                cube.GetComponent<Renderer>().sharedMaterial = cubeMat;

            var rb = cube.AddComponent<Rigidbody>();
            rb.mass = 1.0f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var grab = cube.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.throwVelocityScale = 1.2f;
            grab.interactionLayers = unchecked((int)2147483648) | 1;

            // 3. Socket Interactor (XRSocketInteractor)
            var socketPad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socketPad.name = "Socket_Pedestal_Pad";
            socketPad.transform.SetParent(stationRoot.transform, false);
            socketPad.transform.localPosition = new Vector3(0.65f, tableHeight + 0.02f, 0f);
            socketPad.transform.localScale = new Vector3(0.32f, 0.02f, 0.32f);
            if (socketMat != null)
                socketPad.GetComponent<Renderer>().sharedMaterial = socketMat;

            // Remove cylinder collider so it doesn't collide with the socket trigger
            var cylCol = socketPad.GetComponent<Collider>();
            if (cylCol != null) UnityEngine.Object.DestroyImmediate(cylCol);

            var socketObj = new GameObject("Socket_Receptacle");
            socketObj.transform.SetParent(socketPad.transform, false);
            socketObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);

            var socketCol = socketObj.AddComponent<SphereCollider>();
            socketCol.isTrigger = true;
            socketCol.radius = 0.25f;

            var socket = socketObj.AddComponent<XRSocketInteractor>();
            socket.socketActive = true;
            socket.showInteractableHoverMeshes = true;
            socket.interactionLayers = unchecked((int)2147483648) | 1;

            // 4. Grabbable Water Pistols on Pedestal Table
            var pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPistolSetupUtility.TargetPrefabPath);
            if (pistolPrefab == null)
            {
                pistolPrefab = WaterPistolSetupUtility.CreateOrUpdateWaterPistolPrefab();
            }

            if (pistolPrefab != null)
            {
                // Primary pistol on center of table facing forward
                var pistol1 = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab, stationRoot.transform);
                pistol1.name = "WaterPistol_Pedestal_Main";
                pistol1.transform.localPosition = new Vector3(0.05f, tableHeight + 0.04f, 0f);
                pistol1.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);

                // Secondary pistol alongside
                var pistol2 = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab, stationRoot.transform);
                pistol2.name = "WaterPistol_Pedestal_Secondary";
                pistol2.transform.localPosition = new Vector3(-0.25f, tableHeight + 0.04f, 0f);
                pistol2.transform.localRotation = Quaternion.Euler(0f, -90f, -90f);
            }

            Debug.Log("[TP1ArenaBuilder] Interaction Test Station created with Pedestal, Dynamic Cube, XRSocketInteractor, and 2 Grabbable Water Pistols.");
        }

        private static void SetupDemoWaterPistolStation(Scene scene)
        {
            var existingStation = GameObject.Find("Water_Pistol_Pickup_Station");
            if (existingStation != null)
            {
                UnityEngine.Object.DestroyImmediate(existingStation);
            }

            var stationRoot = new GameObject("Water_Pistol_Pickup_Station");
            // Placed ~1.6m in front of player spawn at (0, 0.05, 10) facing 180°
            stationRoot.transform.position = new Vector3(0.4f, 0.05f, 8.4f);
            stationRoot.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            var pedestalMat = GetOrCreateMaterial("Assets/Materials/M_ArenaPedestal.mat", new Color(0.14f, 0.16f, 0.20f), 0.2f);
            var socketRingMat = GetOrCreateMaterial("Assets/Materials/M_SocketRing.mat", new Color(0.92f, 0.65f, 0.15f), 0.5f);

            float tableHeight = 0.82f;
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Station_Table";
            table.transform.SetParent(stationRoot.transform, false);
            table.transform.localPosition = new Vector3(0f, tableHeight * 0.5f, 0f);
            table.transform.localScale = new Vector3(1.6f, tableHeight, 0.7f);
            if (pedestalMat != null)
                table.GetComponent<Renderer>().sharedMaterial = pedestalMat;

            // Load or build water pistol prefab
            var pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPistolSetupUtility.TargetPrefabPath);
            if (pistolPrefab == null)
            {
                pistolPrefab = WaterPistolSetupUtility.CreateOrUpdateWaterPistolPrefab();
            }

            if (pistolPrefab != null)
            {
                // Right hand pistol - resting ready to be picked up
                var pistolRight = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab, stationRoot.transform);
                pistolRight.name = "WaterPistol_Right";
                pistolRight.transform.localPosition = new Vector3(0.35f, tableHeight + 0.04f, 0f);
                pistolRight.transform.localRotation = Quaternion.Euler(0f, 180f, 90f);

                // Left hand pistol - resting ready to be picked up
                var pistolLeft = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab, stationRoot.transform);
                pistolLeft.name = "WaterPistol_Left";
                pistolLeft.transform.localPosition = new Vector3(-0.35f, tableHeight + 0.04f, 0f);
                pistolLeft.transform.localRotation = Quaternion.Euler(0f, 180f, -90f);
            }

            // Holster Socket in the center of the table
            var socketPad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socketPad.name = "Holster_Socket_Pad";
            socketPad.transform.SetParent(stationRoot.transform, false);
            socketPad.transform.localPosition = new Vector3(0f, tableHeight + 0.01f, 0f);
            socketPad.transform.localScale = new Vector3(0.24f, 0.015f, 0.24f);
            if (socketRingMat != null)
                socketPad.GetComponent<Renderer>().sharedMaterial = socketRingMat;

            var cylCol = socketPad.GetComponent<Collider>();
            if (cylCol != null) UnityEngine.Object.DestroyImmediate(cylCol);

            var socketObj = new GameObject("Holster_Socket");
            socketObj.transform.SetParent(socketPad.transform, false);
            socketObj.transform.localPosition = new Vector3(0f, 0.12f, 0f);

            var socketCol = socketObj.AddComponent<SphereCollider>();
            socketCol.isTrigger = true;
            socketCol.radius = 0.22f;

            var socket = socketObj.AddComponent<XRSocketInteractor>();
            socket.socketActive = true;
            socket.showInteractableHoverMeshes = true;
            socket.interactionLayers = unchecked((int)2147483648) | 1;

            Debug.Log("[TP1ArenaBuilder] Water Pistol Pickup Station placed in Demo scene in front of player spawn with 2 grabbable pistols & holster socket.");
        }

        private static XRInteractionManager EnsureInteractionManager()
        {
            var mgr = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
            if (mgr == null)
            {
                var mgrObj = new GameObject("XR Interaction Manager");
                mgr = mgrObj.AddComponent<XRInteractionManager>();
                Debug.Log("[TP1ArenaBuilder] Created XRInteractionManager in scene.");
            }
            return mgr;
        }

        private static GameObject SetupXRRig(Scene scene, Vector3? initialWristPos = null, Vector3? initialWristRot = null)
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
            if (rigPrefab == null)
            {
                Debug.LogError($"[TP1ArenaBuilder] Failed to load XR Origin prefab at {XrOriginPrefabPath}!");
                return null;
            }

            var rigInstance = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            PrefabUtility.UnpackPrefabInstance(rigInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            rigInstance.name = "XR Origin (XR Rig)";
            rigInstance.transform.position = new Vector3(0f, 0f, -4f);
            rigInstance.transform.rotation = Quaternion.identity;

            // Locate Camera
            var cam = rigInstance.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.tag = "MainCamera";
                cam.gameObject.tag = "MainCamera";

                // Setup ScreenFadeCanvas on camera for Blink teleportation
                ScreenFadeCanvas.CreateOnCamera(cam);

                // Setup Tunneling Vignette on camera
                var vignettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TunnelingVignettePrefabPath);
                if (vignettePrefab != null)
                {
                    var existingVignette = cam.GetComponentInChildren<TunnelingVignetteController>(true);
                    if (existingVignette == null)
                    {
                        var vignetteObj = (GameObject)PrefabUtility.InstantiatePrefab(vignettePrefab, scene);
                        vignetteObj.name = "TunnelingVignette";
                        vignetteObj.transform.SetParent(cam.transform, false);
                        vignetteObj.transform.localPosition = Vector3.zero;
                        vignetteObj.transform.localRotation = Quaternion.identity;
                    }
                }
            }

            // Setup CharacterController on Origin for vertical collision stopping
            var xrOrigin = rigInstance.GetComponent<XROrigin>();
            if (xrOrigin != null)
            {
                xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            }

            GameObject originObj = xrOrigin != null && xrOrigin.Origin != null ? xrOrigin.Origin : rigInstance;

            var characterController = originObj.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = originObj.AddComponent<CharacterController>();
            }
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.skinWidth = 0.05f;
            characterController.minMoveDistance = 0f;

            // Attach CharacterControllerDriver to dynamically track HMD height & position
#pragma warning disable CS0618
            var ccDriver = rigInstance.GetComponent<CharacterControllerDriver>();
            if (ccDriver == null)
            {
                ccDriver = rigInstance.AddComponent<CharacterControllerDriver>();
            }
            ccDriver.minHeight = 0.5f;
            ccDriver.maxHeight = 2.2f;
#pragma warning restore CS0618

            // Ensure LocomotionMediator & XRBodyTransformer exist
            var mediator = rigInstance.GetComponentInChildren<LocomotionMediator>(true);
            if (mediator == null)
            {
                mediator = rigInstance.AddComponent<LocomotionMediator>();
            }

            var bodyTransformer = mediator.GetComponent<XRBodyTransformer>();
            if (bodyTransformer != null)
            {
                bodyTransformer.useCharacterControllerIfExists = true;
            }

            // Setup Locomotion Providers
            var locomotionObj = rigInstance.transform.Find("Locomotion");
            GameObject locomotionHost = locomotionObj != null ? locomotionObj.gameObject : rigInstance;

            // DashProvider
            var dashProvider = locomotionHost.GetComponent<DashProvider>();
            if (dashProvider == null)
            {
                dashProvider = locomotionHost.AddComponent<DashProvider>();
            }
            dashProvider.mediator = mediator;
            dashProvider.dashDuration = 0.20f;
            dashProvider.vignetteController = rigInstance.GetComponentInChildren<TunnelingVignetteController>(true);

            // ComfortTeleportationProvider
            var teleportObj = locomotionHost.transform.Find("Teleportation");
            GameObject teleportHost = teleportObj != null ? teleportObj.gameObject : locomotionHost;

            // Remove existing standard TeleportationProvider if present to avoid duplicate handling
            var oldTeleport = teleportHost.GetComponent<TeleportationProvider>();
            if (oldTeleport != null && !(oldTeleport is ComfortTeleportationProvider))
            {
                UnityEngine.Object.DestroyImmediate(oldTeleport);
            }

            var comfortTeleport = teleportHost.GetComponent<ComfortTeleportationProvider>();
            if (comfortTeleport == null)
            {
                comfortTeleport = teleportHost.AddComponent<ComfortTeleportationProvider>();
            }
            comfortTeleport.mediator = mediator;
            comfortTeleport.dashProvider = dashProvider;
            comfortTeleport.screenFade = rigInstance.GetComponentInChildren<ScreenFadeCanvas>(true);

            // Connect scene's floor and platform TeleportationAreas to this ComfortTeleportationProvider
            var teleportAreas = UnityEngine.Object.FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None);
            foreach (var area in teleportAreas)
            {
                area.teleportationProvider = comfortTeleport;
                area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
                area.matchOrientation = MatchOrientation.WorldSpaceUp;
                area.interactionLayers = unchecked((int)2147483648) | 1 | InteractionLayerMask.GetMask("Teleport");
            }

            // Locate ContinuousMoveProvider
            var moveProvider = locomotionHost.GetComponentInChildren<ContinuousMoveProvider>(true);

            // Setup TP1ComfortManager
            var comfortManager = rigInstance.GetComponent<TP1ComfortManager>();
            if (comfortManager == null)
            {
                comfortManager = rigInstance.AddComponent<TP1ComfortManager>();
            }

            // Setup Left Wrist UI
            Transform leftController = rigInstance.transform.Find("Camera Offset/Left Controller");
            if (leftController == null)
            {
                var controllers = rigInstance.GetComponentsInChildren<Transform>(true);
                foreach (var t in controllers)
                {
                    if (t.name.Contains("Left Controller") || t.name.Contains("LeftController") || t.name.Contains("LeftHand"))
                    {
                        leftController = t;
                        break;
                    }
                }
            }

            if (leftController != null)
            {
                // Preserve user-configured position/rotation if existing wrist UI was customized by user
                var existingWrist = leftController.Find("Wrist_Comfort_UI");
                Vector3? userPos = initialWristPos;
                Vector3? userRot = initialWristRot;
                if (existingWrist != null)
                {
                    var wristComp = existingWrist.GetComponent<WristUIController>();
                    Vector3 currentPos = wristComp != null ? wristComp.uiLocalPosition : existingWrist.localPosition;
                    Vector3 currentRot = wristComp != null ? wristComp.uiLocalEuler : existingWrist.localEulerAngles;
                    userPos = currentPos;
                    userRot = currentRot;
                    UnityEngine.Object.DestroyImmediate(existingWrist.gameObject);
                }

                var wristController = WristUIController.CreateWristUI(leftController, userPos, userRot);

                // Load custom 256x256 UI icons
                var walkSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icons/icon_walk.png");
                var teleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icons/icon_teleport.png");
                var dashSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icons/icon_dash.png");
                var blinkSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icons/icon_blink.png");
                var vigSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icons/icon_vignette.png");
                var turnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icons/icon_turn.png");

                wristController.SetSprites(walkSprite, teleSprite, dashSprite, blinkSprite, vigSprite, turnSprite);

                var soWrist = new SerializedObject(wristController);
                soWrist.Update();
                var walkProp = soWrist.FindProperty("m_WalkSprite");
                if (walkProp != null) walkProp.objectReferenceValue = walkSprite;
                var teleProp = soWrist.FindProperty("m_TeleportSprite");
                if (teleProp != null) teleProp.objectReferenceValue = teleSprite;
                var dashProp = soWrist.FindProperty("m_DashSprite");
                if (dashProp != null) dashProp.objectReferenceValue = dashSprite;
                var blinkProp = soWrist.FindProperty("m_BlinkSprite");
                if (blinkProp != null) blinkProp.objectReferenceValue = blinkSprite;
                var vigProp = soWrist.FindProperty("m_VignetteSprite");
                if (vigProp != null) vigProp.objectReferenceValue = vigSprite;
                var turnProp = soWrist.FindProperty("m_TurnSprite");
                var posProp = soWrist.FindProperty("m_UiLocalPosition");
                if (posProp != null && userPos.HasValue) posProp.vector3Value = userPos.Value;
                var rotProp = soWrist.FindProperty("m_UiLocalEuler");
                if (rotProp != null && userRot.HasValue) rotProp.vector3Value = userRot.Value;
                soWrist.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(wristController);

                Debug.Log("[TP1ArenaBuilder] Forearm Wrist UI created on Left Controller with 6 custom icons.");
            }
            else
            {
                Debug.LogWarning("[TP1ArenaBuilder] Left Controller transform not found for Wrist UI attachment!");
            }

            // Configure Joystick Assignments: Left Stick = Locomotion Only, Right Stick = View Only
            ConfigureJoystickAssignments(rigInstance);

            Debug.Log("[TP1ArenaBuilder] XR Origin Rig configured with CharacterController, ComfortTeleportation, Dash, Vignette, Forearm Wrist UI, and Joystick Mappings.");
            return rigInstance;
        }

        private static InputActionReference FindActionReference(string mapName, string actionName)
        {
            const string inputAssetPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/XRI Default Input Actions.inputactions";
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(inputAssetPath);
            foreach (var asset in allAssets)
            {
                if (asset is InputActionReference actionRef && actionRef.action != null)
                {
                    if (string.Equals(actionRef.action.name, actionName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(actionRef.action.actionMap?.name, mapName, StringComparison.OrdinalIgnoreCase))
                    {
                        return actionRef;
                    }
                }
            }
            Debug.LogWarning($"[TP1ArenaBuilder] InputActionReference not found for {mapName}/{actionName}");
            return null;
        }

        private static void ConfigureJoystickAssignments(GameObject rigInstance)
        {
            var mediator = rigInstance.GetComponentInChildren<LocomotionMediator>(true);
            var locomotionObj = rigInstance.transform.Find("Locomotion");
            GameObject locomotionHost = locomotionObj != null ? locomotionObj.gameObject : rigInstance;

            // Load Input Action References from Starter Assets
            var leftMoveAction = FindActionReference("XRI Left Locomotion", "Move");
            var leftTeleportModeAction = FindActionReference("XRI Left Locomotion", "Teleport Mode");
            var leftTeleportModeCancelAction = FindActionReference("XRI Left Locomotion", "Teleport Mode Cancel");

            var rightTurnAction = FindActionReference("XRI Right Locomotion", "Turn");
            var rightSnapTurnAction = FindActionReference("XRI Right Locomotion", "Snap Turn");

            var cam = rigInstance.GetComponentInChildren<Camera>(true);
            Transform headTransform = cam != null ? cam.transform : rigInstance.transform;

            // 1. ContinuousMoveProvider: Exclusively on LEFT Joystick (Translation only, no rotation)
            var moveProvider = locomotionHost.GetComponentInChildren<ContinuousMoveProvider>(true);
            if (moveProvider == null)
            {
                var moveObj = new GameObject("Continuous Move");
                moveObj.transform.SetParent(locomotionHost.transform, false);
                moveProvider = moveObj.AddComponent<ContinuousMoveProvider>();
            }
            moveProvider.mediator = mediator;

            var soMove = new SerializedObject(moveProvider);
            soMove.Update();
            var moveSpeedProp = soMove.FindProperty("m_MoveSpeed");
            if (moveSpeedProp != null) moveSpeedProp.floatValue = 4.0f;
            var strafeProp = soMove.FindProperty("m_EnableStrafe");
            if (strafeProp != null) strafeProp.boolValue = true;
            var forwardProp = soMove.FindProperty("m_ForwardSource");
            if (forwardProp != null) forwardProp.objectReferenceValue = headTransform;

            // Left Stick: Enabled for translation (InputSourceMode.InputActionReference = 2)
            var leftMoveMode = soMove.FindProperty("m_LeftHandMoveInput.m_InputSourceMode");
            if (leftMoveMode != null) leftMoveMode.intValue = 2; // InputActionReference
            var leftMoveRef = soMove.FindProperty("m_LeftHandMoveInput.m_InputActionReference");
            if (leftMoveRef != null) leftMoveRef.objectReferenceValue = leftMoveAction;

            // Right Stick: Disabled for translation
            var rightMoveMode = soMove.FindProperty("m_RightHandMoveInput.m_InputSourceMode");
            if (rightMoveMode != null) rightMoveMode.intValue = 0; // Unused
            var rightMoveRef = soMove.FindProperty("m_RightHandMoveInput.m_InputActionReference");
            if (rightMoveRef != null) rightMoveRef.objectReferenceValue = null;

            soMove.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(moveProvider);

            // 2. SnapTurnProvider: Exclusively on RIGHT Joystick (Rotation only, no translation)
            var snapTurn = locomotionHost.GetComponentInChildren<SnapTurnProvider>(true);
            if (snapTurn == null)
            {
                var snapObj = new GameObject("Snap Turn");
                snapObj.transform.SetParent(locomotionHost.transform, false);
                snapTurn = snapObj.AddComponent<SnapTurnProvider>();
            }
            snapTurn.mediator = mediator;

            var soSnap = new SerializedObject(snapTurn);
            soSnap.Update();
            var turnAmountProp = soSnap.FindProperty("m_TurnAmount");
            if (turnAmountProp != null) turnAmountProp.floatValue = 45.0f;
            var debounceProp = soSnap.FindProperty("m_DebounceTime");
            if (debounceProp != null) debounceProp.floatValue = 0.5f;
            var enableTurnProp = soSnap.FindProperty("m_EnableTurnLeftRight");
            if (enableTurnProp != null) enableTurnProp.boolValue = true;
            var enableAroundProp = soSnap.FindProperty("m_EnableTurnAround");
            if (enableAroundProp != null) enableAroundProp.boolValue = true;

            // Left Stick: Disabled for Snap Turn
            var leftSnapMode = soSnap.FindProperty("m_LeftHandTurnInput.m_InputSourceMode");
            if (leftSnapMode != null) leftSnapMode.intValue = 0; // Unused
            var leftSnapRef = soSnap.FindProperty("m_LeftHandTurnInput.m_InputActionReference");
            if (leftSnapRef != null) leftSnapRef.objectReferenceValue = null;

            // Right Stick: Enabled for Snap Turn (InputSourceMode.InputActionReference = 2)
            var rightSnapMode = soSnap.FindProperty("m_RightHandTurnInput.m_InputSourceMode");
            if (rightSnapMode != null) rightSnapMode.intValue = 2; // InputActionReference
            var rightSnapRef = soSnap.FindProperty("m_RightHandTurnInput.m_InputActionReference");
            if (rightSnapRef != null) rightSnapRef.objectReferenceValue = rightSnapTurnAction;

            soSnap.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(snapTurn);

            // 3. ContinuousTurnProvider: Exclusively on RIGHT Joystick (Rotation only, no translation)
            var continuousTurn = locomotionHost.GetComponentInChildren<ContinuousTurnProvider>(true);
            if (continuousTurn == null)
            {
                var contObj = new GameObject("Continuous Turn");
                contObj.transform.SetParent(locomotionHost.transform, false);
                continuousTurn = contObj.AddComponent<ContinuousTurnProvider>();
            }
            continuousTurn.mediator = mediator;

            var soCont = new SerializedObject(continuousTurn);
            soCont.Update();
            var turnSpeedProp = soCont.FindProperty("m_TurnSpeed");
            if (turnSpeedProp != null) turnSpeedProp.floatValue = 60.0f;
            var contEnableTurnProp = soCont.FindProperty("m_EnableTurnLeftRight");
            if (contEnableTurnProp != null) contEnableTurnProp.boolValue = true;
            var contEnableAroundProp = soCont.FindProperty("m_EnableTurnAround");
            if (contEnableAroundProp != null) contEnableAroundProp.boolValue = false;

            // Left Stick: Disabled for Continuous Turn
            var leftContMode = soCont.FindProperty("m_LeftHandTurnInput.m_InputSourceMode");
            if (leftContMode != null) leftContMode.intValue = 0; // Unused
            var leftContRef = soCont.FindProperty("m_LeftHandTurnInput.m_InputActionReference");
            if (leftContRef != null) leftContRef.objectReferenceValue = null;

            // Right Stick: Enabled for Continuous Smooth Turn (InputSourceMode.InputActionReference = 2)
            var rightContMode = soCont.FindProperty("m_RightHandTurnInput.m_InputSourceMode");
            if (rightContMode != null) rightContMode.intValue = 2; // InputActionReference
            var rightContRef = soCont.FindProperty("m_RightHandTurnInput.m_InputActionReference");
            if (rightContRef != null) rightContRef.objectReferenceValue = rightTurnAction;

            soCont.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(continuousTurn);

            // Default to Snap Turn enabled, Continuous Turn disabled
            snapTurn.enabled = true;
            continuousTurn.enabled = false;

            // 4. Configure ControllerInputActionManagers on Left & Right Controllers
            var leftController = rigInstance.transform.Find("Camera Offset/Left Controller");
            var rightController = rigInstance.transform.Find("Camera Offset/Right Controller");

            if (leftController != null)
            {
                var leftActionMgr = leftController.GetComponent<ControllerInputActionManager>();
                if (leftActionMgr != null)
                {
                    var soLeft = new SerializedObject(leftActionMgr);
                    soLeft.Update();
                    var moveP = soLeft.FindProperty("m_Move");
                    if (moveP != null) moveP.objectReferenceValue = leftMoveAction;
                    var turnP = soLeft.FindProperty("m_Turn");
                    if (turnP != null) turnP.objectReferenceValue = null;
                    var snapP = soLeft.FindProperty("m_SnapTurn");
                    if (snapP != null) snapP.objectReferenceValue = null;
                    var teleP = soLeft.FindProperty("m_TeleportMode");
                    if (teleP != null) teleP.objectReferenceValue = leftTeleportModeAction;
                    var teleCanP = soLeft.FindProperty("m_TeleportModeCancel");
                    if (teleCanP != null) teleCanP.objectReferenceValue = leftTeleportModeCancelAction;
                    var smoothMotP = soLeft.FindProperty("m_SmoothMotionEnabled");
                    if (smoothMotP != null) smoothMotP.boolValue = false;
                    var smoothTurnP = soLeft.FindProperty("m_SmoothTurnEnabled");
                    if (smoothTurnP != null) smoothTurnP.boolValue = false;
                    soLeft.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(leftActionMgr);
                }
            }

            if (rightController != null)
            {
                var rightActionMgr = rightController.GetComponent<ControllerInputActionManager>();
                if (rightActionMgr != null)
                {
                    var soRight = new SerializedObject(rightActionMgr);
                    soRight.Update();
                    var moveP = soRight.FindProperty("m_Move");
                    if (moveP != null) moveP.objectReferenceValue = null;
                    var turnP = soRight.FindProperty("m_Turn");
                    if (turnP != null) turnP.objectReferenceValue = null;
                    var snapP = soRight.FindProperty("m_SnapTurn");
                    if (snapP != null) snapP.objectReferenceValue = null;
                    var teleP = soRight.FindProperty("m_TeleportMode");
                    if (teleP != null) teleP.objectReferenceValue = null;
                    var teleCanP = soRight.FindProperty("m_TeleportModeCancel");
                    if (teleCanP != null) teleCanP.objectReferenceValue = null;
                    var smoothMotP = soRight.FindProperty("m_SmoothMotionEnabled");
                    if (smoothMotP != null) smoothMotP.boolValue = false;
                    var smoothTurnP = soRight.FindProperty("m_SmoothTurnEnabled");
                    if (smoothTurnP != null) smoothTurnP.boolValue = false;
                    soRight.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(rightActionMgr);
                }
            }

            // 5. Wire locomotion providers & input actions into TP1ComfortManager
            var comfortManager = rigInstance.GetComponent<TP1ComfortManager>();
            if (comfortManager != null)
            {
                var soComfort = new SerializedObject(comfortManager);
                soComfort.Update();
                var teleRefProp = soComfort.FindProperty("m_TeleportProvider");
                if (teleRefProp != null) teleRefProp.objectReferenceValue = rigInstance.GetComponentInChildren<ComfortTeleportationProvider>(true);
                var dashRefProp = soComfort.FindProperty("m_DashProvider");
                if (dashRefProp != null) dashRefProp.objectReferenceValue = rigInstance.GetComponentInChildren<DashProvider>(true);
                var moveRefProp = soComfort.FindProperty("m_ContinuousMoveProvider");
                if (moveRefProp != null) moveRefProp.objectReferenceValue = moveProvider;
                var snapRefProp = soComfort.FindProperty("m_SnapTurnProvider");
                if (snapRefProp != null) snapRefProp.objectReferenceValue = snapTurn;
                var contTurnRefProp = soComfort.FindProperty("m_ContinuousTurnProvider");
                if (contTurnRefProp != null) contTurnRefProp.objectReferenceValue = continuousTurn;
                var vigRefProp = soComfort.FindProperty("m_VignetteController");
                if (vigRefProp != null) vigRefProp.objectReferenceValue = rigInstance.GetComponentInChildren<TunnelingVignetteController>(true);
                var snapActProp = soComfort.FindProperty("m_RightSnapTurnAction");
                if (snapActProp != null) snapActProp.objectReferenceValue = rightSnapTurnAction;
                var contActProp = soComfort.FindProperty("m_RightTurnAction");
                if (contActProp != null) contActProp.objectReferenceValue = rightTurnAction;
                soComfort.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(comfortManager);
            }

            // 6. Connect CharacterControllerDriver locomotion provider
#pragma warning disable CS0618
            var ccDriver = rigInstance.GetComponent<CharacterControllerDriver>();
            if (ccDriver != null)
            {
                ccDriver.locomotionProvider = moveProvider;
                EditorUtility.SetDirty(ccDriver);
            }
#pragma warning restore CS0618

            Debug.Log("[TP1ArenaBuilder] Joystick assignments configured: Left = Locomotion Only (Move/Teleport/Dash), Right = View Only (Snap/Smooth Turn).");
        }

        private static void SetupEventSystem()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing == null)
            {
                var eventObj = new GameObject("EventSystem");
                existing = eventObj.AddComponent<EventSystem>();
                eventObj.AddComponent<XRUIInputModule>();
            }
            else
            {
                if (existing.GetComponent<XRUIInputModule>() == null)
                    existing.gameObject.AddComponent<XRUIInputModule>();

                // Remove duplicate InputSystemUIInputModule if present to prevent conflict
                var duplicate = existing.GetComponent<InputSystemUIInputModule>();
                if (duplicate != null)
                {
                    UnityEngine.Object.DestroyImmediate(duplicate);
                }
            }
        }

        private static void RegisterSceneAsScene0(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Remove any existing entry for this scene
            scenes.RemoveAll(s => s.path.Equals(scenePath, StringComparison.OrdinalIgnoreCase));

            // Insert at index 0 enabled
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[TP1ArenaBuilder] Registered {scenePath} as Scene 0 (enabled) in EditorBuildSettings.");
        }
    }
}
