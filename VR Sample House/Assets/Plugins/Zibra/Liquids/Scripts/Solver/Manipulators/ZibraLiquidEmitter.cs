using UnityEngine;
using System;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

namespace com.zibra.liquid.Manipulators
{
    [AddComponentMenu("Zibra/Zibra Liquid Emitter")]
    public class ZibraLiquidEmitter : Manipulator
    {
#if ZIBRA_LIQUID_PAID_VERSION
        [NonSerialized]
        public long createdParticlesTotal = 0;
        [NonSerialized]
        public int createdParticlesPerFrame = 0;
#endif

        public enum ClampBehaviorType
        {
            DontClamp,
            Clamp
        }

        [Tooltip("Emitted particles per second")]
        [Min(0.0f)]
        public float ParticlesPerSec = 6000.0f;

        [NonSerialized]
        [Obsolete("VelocityMagnitude is deprecated. Use InitialVelocity instead.", true)]
        public float VelocityMagnitude;

        [SerializeField]
        [FormerlySerializedAs("VelocityMagnitude")]
        private float VelocityMagnitudeOld;

        [NonSerialized]
        [Obsolete("CustomEmitterTransform is deprecated. Modify emitter's transform directly instead.", true)]
        public Transform CustomEmitterTransform;

        [SerializeField]
        [FormerlySerializedAs("CustomEmitterTransform")]
        private Transform CustomEmitterTransformOld;

        [Tooltip("Initial velocity of newly created particles")]
        // Rotated with object
        // Used velocity will be equal to GetRotatedInitialVelocity
        public Vector3 InitialVelocity = new Vector3(0, 0, 0);

        [Tooltip("Controls what whether effective position of emitter will clamp to container bounds.")]
        public ClampBehaviorType PositionClampBehavior = ClampBehaviorType.Clamp;

        [HideInInspector]
        [SerializeField]
        private int ObjectVersion = 1;

#if UNITY_EDITOR
        void OnSceneOpened(Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            Debug.Log("Zibra Liquid Emitter format was updated. Please resave scene.");
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif

        [ExecuteInEditMode]
        public void Awake()
        {
            // If Emitter is in old format we need to parse old parameters and come up with equivalent new ones
            if (ObjectVersion == 1)
            {
                InitialVelocity = transform.rotation * new Vector3(VelocityMagnitudeOld, 0, 0);
                VelocityMagnitudeOld = 0;
                transform.rotation = Quaternion.identity;
                if (CustomEmitterTransformOld)
                {
                    transform.position = CustomEmitterTransformOld.position;
                    transform.rotation = CustomEmitterTransformOld.rotation;
                    CustomEmitterTransformOld = null;
                }

                ObjectVersion = 2;
#if UNITY_EDITOR
                // Can't mark object dirty in Awake, since scene is not fully loaded yet
                UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
#endif
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (InitialVelocity.sqrMagnitude > Vector3.kEpsilon)
            {
                Utilities.GizmosHelper.DrawArrow(transform.position, GetRotatedInitialVelocity(), Color.blue, 0.5f);
            }

            Gizmos.color = Color.blue;
            Gizmos.matrix = GetTransform();
            Gizmos.DrawWireCube(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
        }

        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }
#endif
        ZibraLiquidEmitter()
        {
            ManipType = ManipulatorType.Emitter;
        }

        public Vector3 GetRotatedInitialVelocity()
        {
            return transform.rotation * InitialVelocity;
        }

        private void Update()
        {
            Vector3 rotatedInitialVelocity = GetRotatedInitialVelocity();
            AdditionalData.y = rotatedInitialVelocity.x;
            AdditionalData.z = rotatedInitialVelocity.y;
            AdditionalData.w = rotatedInitialVelocity.z;
        }

        override public Matrix4x4 GetTransform()
        {
            return transform.localToWorldMatrix;
        }

        override public Quaternion GetRotation()
        {
            return transform.rotation;
        }

        override public Vector3 GetPosition()
        {
            return transform.position;
        }
        override public Vector3 GetScale()
        {
            return transform.lossyScale;
        }

        // clang-format doesn't parse code with new keyword properly
        // clang-format off

#if UNITY_EDITOR
        public new void OnDestroy()
        {
            base.OnDestroy();
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
        }
#endif
    }
}