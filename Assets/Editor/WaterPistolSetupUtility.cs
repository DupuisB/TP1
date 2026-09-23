using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using LOG8704.Interactions;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LOG8704.Editor
{
    /// <summary>
    /// Editor utility for creating and configuring grabbable water pistol prefabs (SM_Wep_WaterPistol_01).
    /// Sets up Rigidbody, Convex MeshCollider, Dual Attach points (Right/Left hand),
    /// MuzzlePoint, and the WaterPistolInteractable component.
    /// </summary>
    public static class WaterPistolSetupUtility
    {
        public const string SourcePrefabPath = "Assets/Synty/PolygonStarter/Prefabs/SM_Wep_WaterPistol_01.prefab";
        public const string TargetPrefabDir = "Assets/Prefabs/Weapons";
        public const string TargetPrefabPath = "Assets/Prefabs/Weapons/WaterPistol_Grabbable.prefab";

        [MenuItem("LOG8704/Weapons/Create Grabbable Water Pistol Prefab")]
        public static GameObject CreateOrUpdateWaterPistolPrefab()
        {
            EnsureDirectories();

            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (sourcePrefab == null)
            {
                Debug.LogError($"[WaterPistolSetupUtility] Source prefab not found at {SourcePrefabPath}!");
                return null;
            }

            // Instantiate a temporary instance to modify
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            instance.name = "WaterPistol_Grabbable";

            // 1. Configure Rigidbody
            var rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.mass = 0.35f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // 2. Configure an interaction collider. The source art prefab has no
            // collider, so a convex root BoxCollider is the reliable fallback.
            var col = instance.GetComponent<MeshCollider>();
            if (col != null)
            {
                col.convex = true;
            }
            else
            {
                var box = instance.GetComponent<BoxCollider>();
                if (box == null) box = instance.AddComponent<BoxCollider>();
                box.size = new Vector3(0.08f, 0.18f, 0.30f);
                box.center = new Vector3(0f, 0.04f, 0.08f);
            }

            // 3. Configure Attach Points
            // Forward is +Z, Up is +Y. Attach points align hand palm with the grip handle.
            var attachRight = instance.transform.Find("Attach_Right");
            if (attachRight == null)
            {
                var rightObj = new GameObject("Attach_Right");
                rightObj.transform.SetParent(instance.transform, false);
                rightObj.transform.localPosition = new Vector3(0f, 0.005f, 0.015f);
                rightObj.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
                attachRight = rightObj.transform;
            }

            var attachLeft = instance.transform.Find("Attach_Left");
            if (attachLeft == null)
            {
                var leftObj = new GameObject("Attach_Left");
                leftObj.transform.SetParent(instance.transform, false);
                leftObj.transform.localPosition = new Vector3(0f, 0.005f, 0.015f);
                leftObj.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
                attachLeft = leftObj.transform;
            }

            // 4. Configure MuzzlePoint (Nozzle tip)
            var muzzle = instance.transform.Find("MuzzlePoint");
            if (muzzle == null)
            {
                var muzzleObj = new GameObject("MuzzlePoint");
                muzzleObj.transform.SetParent(instance.transform, false);
                muzzleObj.transform.localPosition = new Vector3(0f, 0.065f, 0.22f);
                muzzleObj.transform.localRotation = Quaternion.identity;
                muzzle = muzzleObj.transform;
            }

            // 5. Configure WaterPistolInteractable
            var pistolInteractable = instance.GetComponent<WaterPistolInteractable>();
            if (pistolInteractable == null)
                pistolInteractable = instance.AddComponent<WaterPistolInteractable>();

            pistolInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
            pistolInteractable.smoothPosition = true;
            pistolInteractable.smoothRotation = true;
            pistolInteractable.throwOnDetach = false;
            pistolInteractable.throwVelocityScale = 1.25f;
            pistolInteractable.throwAngularVelocityScale = 1.0f;
            pistolInteractable.interactionLayers = unchecked((int)2147483648) | 1;

            var triggerTransform = instance.transform.Find("WaterPistol_01_Trigger");

            var so = new SerializedObject(pistolInteractable);
            so.Update();
            var rightAttachProp = so.FindProperty("m_RightAttachTransform");
            if (rightAttachProp != null) rightAttachProp.objectReferenceValue = attachRight;
            var leftAttachProp = so.FindProperty("m_LeftAttachTransform");
            if (leftAttachProp != null) leftAttachProp.objectReferenceValue = attachLeft;
            var muzzleProp = so.FindProperty("m_MuzzlePoint");
            if (muzzleProp != null) muzzleProp.objectReferenceValue = muzzle;
            var triggerProp = so.FindProperty("m_TriggerMesh");
            if (triggerProp != null && triggerTransform != null) triggerProp.objectReferenceValue = triggerTransform;
            var baseAttachProp = so.FindProperty("m_AttachTransform");
            if (baseAttachProp != null) baseAttachProp.objectReferenceValue = attachRight;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Save as prefab
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, TargetPrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WaterPistolSetupUtility] Grabbable Water Pistol prefab successfully created at {TargetPrefabPath}");
            return savedPrefab;
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Weapons"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Weapons");
        }
    }
}
