using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace LOG8704.Interactions
{
    /// <summary>
    /// XR Grab Interactable for water pistols (SM_Wep_WaterPistol_01).
    /// Provides ergonomic dual-hand attach transforms (Right/Left hand grip alignment),
    /// haptic feedback on grab and trigger pull, visual trigger depression animation,
    /// and a forward-aligned muzzle transform ready for water shooting mechanics.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class WaterPistolInteractable : XRGrabInteractable
    {
        [Header("Ergonomic Grip Attach Points")]
        [Tooltip("Attach transform used when grabbed by the Right Hand controller.")]
        [SerializeField] private Transform m_RightAttachTransform;

        [Tooltip("Attach transform used when grabbed by the Left Hand controller.")]
        [SerializeField] private Transform m_LeftAttachTransform;

        [Header("Weapon Architecture (Shooting Readiness)")]
        [Tooltip("Nozzle/tip transform where water streams/projectiles will emerge.")]
        [SerializeField] private Transform m_MuzzlePoint;

        [Tooltip("Movable trigger mesh for pull feedback.")]
        [SerializeField] private Transform m_TriggerMesh;

        [Header("Water Shot")]
        [SerializeField, Min(0.1f)] private float m_WaterRange = 8f;
        [SerializeField, Min(0.001f)] private float m_WaterWidth = 0.012f;
        [SerializeField] private Color m_WaterColor = new Color(0.35f, 0.8f, 1f, 0.9f);
        [SerializeField, Min(0.01f)] private float m_ShotFlashDuration = 0.07f;

        [Header("Trigger Animation Parameters")]
        [SerializeField] private Vector3 m_TriggerPulledLocalEuler = new Vector3(15f, 0f, 0f);
        [SerializeField] private Vector3 m_TriggerPulledLocalOffset = new Vector3(0f, -0.002f, -0.006f);
        [SerializeField] private float m_TriggerReturnSpeed = 18f;

        [Header("Haptic Feedback Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float m_GrabHapticAmplitude = 0.25f;
        [SerializeField] private float m_GrabHapticDuration = 0.08f;
        [Range(0f, 1f)]
        [SerializeField] private float m_TriggerHapticAmplitude = 0.45f;
        [SerializeField] private float m_TriggerHapticDuration = 0.12f;

        // Trigger animation runtime state
        private Vector3 m_TriggerRestLocalPos;
        private Quaternion m_TriggerRestLocalRot;
        private bool m_IsTriggerPressed;
        private IXRSelectInteractor m_CurrentHolder;
        private LineRenderer m_WaterStream;
        private Material m_WaterMaterial;
        private float m_ShotFlashRemaining;

        public Transform muzzlePoint => m_MuzzlePoint;
        public bool isTriggerPressed => m_IsTriggerPressed;
        public IXRSelectInteractor currentHolder => m_CurrentHolder;

        protected override void Awake()
        {
            // XRI needs at least one collider to register hover/select. The source
            // art prefab has no collider, so make the weapon usable even when an
            // older generated prefab is still in the project.
            EnsureInteractionCollider();
            base.Awake();

            // Setup default physics for throwing and collision response
            movementType = MovementType.Kinematic;
            smoothPosition = true;
            smoothRotation = true;
            throwOnDetach = false;
            throwVelocityScale = 1.25f;
            throwAngularVelocityScale = 1.0f;

            // Ensure interaction layers include default (1)
            interactionLayers = unchecked((int)2147483648) | 1;

            // Ensure attach transforms exist if not assigned in inspector
            EnsureAttachTransforms();
            SetupWaterStream();

            // Cache trigger resting pose
            if (m_TriggerMesh != null)
            {
                m_TriggerRestLocalPos = m_TriggerMesh.localPosition;
                m_TriggerRestLocalRot = m_TriggerMesh.localRotation;
            }
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            // The Quest rig uses XRI Near-Far interactors, which are not
            // XRDirectInteractor instances. Let XRI decide based on the active
            // interactor, layers, and selection state.
            return base.IsSelectableBy(interactor);
        }

        private void EnsureInteractionCollider()
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
                return;

            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.04f, 0.08f);
            box.size = new Vector3(0.08f, 0.18f, 0.30f);
        }

        private void EnsureAttachTransforms()
        {
            if (m_RightAttachTransform == null)
            {
                var rightObj = transform.Find("Attach_Right");
                if (rightObj != null)
                {
                    m_RightAttachTransform = rightObj;
                }
                else
                {
                    var created = new GameObject("Attach_Right");
                    created.transform.SetParent(transform, false);
                    created.transform.localPosition = new Vector3(0f, 0.005f, 0.015f);
                    created.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
                    m_RightAttachTransform = created.transform;
                }
            }

            if (m_LeftAttachTransform == null)
            {
                var leftObj = transform.Find("Attach_Left");
                if (leftObj != null)
                {
                    m_LeftAttachTransform = leftObj;
                }
                else
                {
                    var created = new GameObject("Attach_Left");
                    created.transform.SetParent(transform, false);
                    created.transform.localPosition = new Vector3(0f, 0.005f, 0.015f);
                    created.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
                    m_LeftAttachTransform = created.transform;
                }
            }

            // Set default base attach transform to right hand grip
            attachTransform = m_RightAttachTransform;

            // Ensure MuzzlePoint exists
            if (m_MuzzlePoint == null)
            {
                var muzzleObj = transform.Find("MuzzlePoint");
                if (muzzleObj != null)
                {
                    m_MuzzlePoint = muzzleObj;
                }
                else
                {
                    var created = new GameObject("MuzzlePoint");
                    created.transform.SetParent(transform, false);
                    created.transform.localPosition = new Vector3(0f, 0.065f, 0.22f);
                    created.transform.localRotation = Quaternion.identity;
                    m_MuzzlePoint = created.transform;
                }
            }

            // Locate trigger mesh if not assigned
            if (m_TriggerMesh == null)
            {
                var trg = transform.Find("WaterPistol_01_Trigger");
                if (trg != null)
                {
                    m_TriggerMesh = trg;
                }
            }
        }

        /// <summary>
        /// Dynamically selects the appropriate attach transform based on the interactor's handedness.
        /// </summary>
        public override Transform GetAttachTransform(IXRInteractor interactor)
        {
            if (interactor != null && interactor.transform != null)
            {
                bool isLeft = IsLeftHandInteractor(interactor);
                if (isLeft && m_LeftAttachTransform != null)
                {
                    return m_LeftAttachTransform;
                }
                if (!isLeft && m_RightAttachTransform != null)
                {
                    return m_RightAttachTransform;
                }
            }

            return base.GetAttachTransform(interactor);
        }

        protected override void OnSelectEntering(SelectEnterEventArgs args)
        {
            base.OnSelectEntering(args);

            m_CurrentHolder = args.interactorObject;

            // Dynamically assign primary attachTransform before grab snapshot
            bool isLeft = IsLeftHandInteractor(args.interactorObject);
            attachTransform = isLeft && m_LeftAttachTransform != null ? m_LeftAttachTransform : m_RightAttachTransform;

            // Trigger gentle grab haptic
            SendHapticImpulse(args.interactorObject, m_GrabHapticAmplitude, m_GrabHapticDuration);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            if (m_CurrentHolder == args.interactorObject)
            {
                m_CurrentHolder = null;
            }

            m_IsTriggerPressed = false;
        }

        protected override void OnActivated(ActivateEventArgs args)
        {
            base.OnActivated(args);

            m_IsTriggerPressed = true;

            // Squeeze haptic buzz
            SendHapticImpulse(args.interactorObject, m_TriggerHapticAmplitude, m_TriggerHapticDuration);

            // Virtual hook ready for projectile/water beam spawning
            FireWaterShot();
        }

        protected override void OnDeactivated(DeactivateEventArgs args)
        {
            base.OnDeactivated(args);

            m_IsTriggerPressed = false;
        }

        protected virtual void FireWaterShot()
        {
            // XRI Activate is edge triggered, so each press produces one short
            // hitscan flash instead of a continuous beam while held.
            m_ShotFlashRemaining = m_ShotFlashDuration;
            UpdateWaterShotLine();
        }

        protected virtual void Update()
        {
            AnimateTrigger();
            if (m_ShotFlashRemaining > 0f)
            {
                m_ShotFlashRemaining -= Time.deltaTime;
                UpdateWaterShotLine();
            }
            else if (m_WaterStream != null)
            {
                m_WaterStream.enabled = false;
            }
        }

        private void SetupWaterStream()
        {
            if (m_MuzzlePoint == null) return;

            var streamObject = new GameObject("WaterStream");
            streamObject.transform.SetParent(transform, false);
            m_WaterStream = streamObject.AddComponent<LineRenderer>();
            m_WaterStream.useWorldSpace = true;
            m_WaterStream.positionCount = 2;
            m_WaterStream.startWidth = m_WaterWidth;
            m_WaterStream.endWidth = m_WaterWidth * 0.35f;
            m_WaterStream.startColor = m_WaterColor;
            m_WaterStream.endColor = m_WaterColor;
            m_WaterStream.numCapVertices = 2;
            m_WaterStream.enabled = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                m_WaterMaterial = new Material(shader) { color = m_WaterColor };
                m_WaterStream.material = m_WaterMaterial;
            }
        }

        private void UpdateWaterShotLine()
        {
            if (m_WaterStream == null || m_MuzzlePoint == null) return;

            m_WaterStream.enabled = true;

            Vector3 start = m_MuzzlePoint.position;
            Vector3 direction = m_MuzzlePoint.forward;
            Vector3 end = start + direction * m_WaterRange;
            if (Physics.Raycast(start, direction, out RaycastHit hit, m_WaterRange, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(transform))
                end = hit.point;

            m_WaterStream.startWidth = m_WaterWidth;
            m_WaterStream.endWidth = m_WaterWidth * 0.35f;
            m_WaterStream.SetPosition(0, start);
            m_WaterStream.SetPosition(1, end);
        }

        private void OnDestroy()
        {
            if (m_WaterMaterial != null)
            {
                if (Application.isPlaying) Destroy(m_WaterMaterial);
                else DestroyImmediate(m_WaterMaterial);
            }
        }

        private void AnimateTrigger()
        {
            if (m_TriggerMesh == null) return;

            Vector3 targetPos = m_TriggerRestLocalPos;
            Quaternion targetRot = m_TriggerRestLocalRot;

            if (m_IsTriggerPressed)
            {
                targetPos = m_TriggerRestLocalPos + m_TriggerPulledLocalOffset;
                targetRot = m_TriggerRestLocalRot * Quaternion.Euler(m_TriggerPulledLocalEuler);
            }

            m_TriggerMesh.localPosition = Vector3.Lerp(m_TriggerMesh.localPosition, targetPos, Time.deltaTime * m_TriggerReturnSpeed);
            m_TriggerMesh.localRotation = Quaternion.Slerp(m_TriggerMesh.localRotation, targetRot, Time.deltaTime * m_TriggerReturnSpeed);
        }

        private bool IsLeftHandInteractor(IXRInteractor interactor)
        {
            if (interactor == null || interactor.transform == null) return false;

            string name = interactor.transform.name;
            Transform parent = interactor.transform.parent;
            string parentName = parent != null ? parent.name : "";

            return name.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   parentName.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SendHapticImpulse(IXRInteractor interactor, float amplitude, float duration)
        {
            if (interactor == null || interactor.transform == null) return;

            // XRI 3.x: XRBaseController is obsolete. Use HapticImpulsePlayer instead.
            var hapticPlayer = interactor.transform.GetComponentInParent<HapticImpulsePlayer>();
            if (hapticPlayer != null)
            {
                hapticPlayer.SendHapticImpulse(amplitude, duration);
            }
        }
    }
}
