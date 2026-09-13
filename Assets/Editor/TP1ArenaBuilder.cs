using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
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
            };
        }

        [MenuItem("LOG8704/Build TP1 Test Arena Scene")]
        public static void BuildArenaScene()
        {
            Debug.Log("[TP1ArenaBuilder] Starting automated creation of TP1 Test Arena scene...");

            // 1. Ensure directories exist
            EnsureDirectory(TargetSceneDir);
            EnsureDirectory(MaterialsDir);

            // 2. Create / load materials
            var floorMat = GetOrCreateMaterial("Assets/Materials/M_ArenaFloor.mat", new Color(0.18f, 0.21f, 0.26f), 0.3f);
            var wallMat = GetOrCreateMaterial("Assets/Materials/M_ArenaWall.mat", new Color(0.11f, 0.12f, 0.15f), 0.1f);
            var obstacleMat = GetOrCreateMaterial("Assets/Materials/M_ArenaObstacle.mat", new Color(0.88f, 0.45f, 0.10f), 0.4f);
            var obstacleTopMat = GetOrCreateMaterial("Assets/Materials/M_ArenaObstacleTop.mat", new Color(0.65f, 0.15f, 0.15f), 0.2f);

            // 3. Create a fresh scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 4. Setup Lighting
            SetupLighting();

            // 5. Build Environment Hierarchy
            var envRoot = new GameObject("Environment");

            // A. Walkable Floor with TeleportationArea
            BuildFloor(envRoot.transform, floorMat);

            // B. Boundary Walls with Colliders (blocking joystick move & teleportation)
            BuildBoundaryWalls(envRoot.transform, wallMat);

            // C. Obstacle Box (blocking joystick move, top ray aimable but rejects teleportation)
            BuildObstacleBox(envRoot.transform, obstacleMat, obstacleTopMat);

            // 6. Setup XR Rig and Locomotion
            var rigInstance = SetupXRRig(scene);

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

            // Signage / Label on top surface
            var signObj = new GameObject("SignText");
            signObj.transform.SetParent(topSurface.transform, false);
            signObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            signObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            signObj.transform.localScale = Vector3.one * 0.05f;

            var tmp = signObj.AddComponent<TextMeshPro>();
            tmp.text = "OBSTACLE\n[TELEPORT REJETE]";
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;

            // Signage on front vertical face facing player spawn
            var frontSignObj = new GameObject("FrontSignText");
            frontSignObj.transform.SetParent(box.transform, false);
            frontSignObj.transform.localPosition = new Vector3(0f, 0f, -0.51f);
            frontSignObj.transform.localRotation = Quaternion.identity;
            frontSignObj.transform.localScale = Vector3.one * 0.05f;

            var frontTmp = frontSignObj.AddComponent<TextMeshPro>();
            frontTmp.text = "OBSTACLE BLOC\nPassage Bloque";
            frontTmp.fontSize = 18;
            frontTmp.alignment = TextAlignmentOptions.Center;
            frontTmp.fontStyle = FontStyles.Bold;
            frontTmp.color = Color.white;

            Debug.Log("[TP1ArenaBuilder] Obstacle Box created with vertical colliders and non-teleportable top surface.");
        }

        private static GameObject SetupXRRig(Scene scene)
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

            // Connect scene's floor TeleportationArea to this ComfortTeleportationProvider
            var floorArea = UnityEngine.Object.FindFirstObjectByType<TeleportationArea>();
            if (floorArea != null)
            {
                floorArea.teleportationProvider = comfortTeleport;
                floorArea.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
                floorArea.interactionLayers = unchecked((int)2147483648) | 1 | InteractionLayerMask.GetMask("Teleport");
            }

            // Locate ContinuousMoveProvider
            var moveProvider = locomotionHost.GetComponentInChildren<ContinuousMoveProvider>(true);

            // Setup TP1ComfortManager
            var comfortManager = rigInstance.GetComponent<TP1ComfortManager>();
            if (comfortManager == null)
            {
                comfortManager = rigInstance.AddComponent<TP1ComfortManager>();
            }

            // Setup LocomotionSelfTest
            var selfTest = rigInstance.GetComponent<LOG8704.Tests.LocomotionSelfTest>();
            if (selfTest == null)
            {
                selfTest = rigInstance.AddComponent<LOG8704.Tests.LocomotionSelfTest>();
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
                // Remove existing wrist UI if any
                var existingWrist = leftController.Find("Wrist_Comfort_UI");
                if (existingWrist != null)
                    UnityEngine.Object.DestroyImmediate(existingWrist.gameObject);

                WristUIController.CreateWristUI(leftController);
                Debug.Log("[TP1ArenaBuilder] Wrist UI created on Left Controller.");
            }
            else
            {
                Debug.LogWarning("[TP1ArenaBuilder] Left Controller transform not found for Wrist UI attachment!");
            }

            Debug.Log("[TP1ArenaBuilder] XR Origin Rig configured with CharacterController, ComfortTeleportation, Dash, Vignette, and Wrist UI.");
            return rigInstance;
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
