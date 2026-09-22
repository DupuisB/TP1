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

        public Transform muzzlePoint => m_MuzzlePoint;
        public bool isTriggerPressed => m_IsTriggerPressed;
        public IXRSelectInteractor currentHolder => m_CurrentHolder;

        protected override void Awake()
        {
            base.Awake();

            // Setup default physics for throwing and collision response
            movementType = MovementType.VelocityTracking;
            throwOnDetach = true;
            throwVelocityScale = 1.25f;
            throwAngularVelocityScale = 1.0f;

            // Ensure interaction layers include default (1)
            interactionLayers = unchecked((int)2147483648) | 1;

            // Ensure attach transforms exist if not assigned in inspector
            EnsureAttachTransforms();

            // Cache trigger resting pose
            if (m_TriggerMesh != null)
            {
                m_TriggerRestLocalPos = m_TriggerMesh.localPosition;
                m_TriggerRestLocalRot = m_TriggerMesh.localRotation;
            }
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
            // Extension point for shooting phase (FX, particles, audio, hit detection)
            Debug.Log($"[WaterPistol] Trigger pulled on {name}! Muzzle at {m_MuzzlePoint.position}");
        }

        protected virtual void Update()
        {
            AnimateTrigger();
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
