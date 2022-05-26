using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using com.zibra.liquid.Solver;

namespace com.zibra.liquid.SDFObjects
{
    public class SDFColliderCompare : Comparer<SDFCollider>
    {
        // Compares manipulator type ID
        public override int Compare(SDFCollider x, SDFCollider y)
        {
            int result = x.chosenSDFType.CompareTo(y.chosenSDFType);
            if (result != 0)
            {
                return result;
            }
            return x.GetHashCode().CompareTo(y.GetHashCode());
        }
    }

    // SDF Collider template
    [ExecuteInEditMode] // Careful! This makes script execute in edit mode.
                        // Use "EditorApplication.isPlaying" for play mode only check.
                        // Encase this check and "using UnityEditor" in "#if UNITY_EDITOR" preprocessor directive to
                        // prevent build errors
    [DisallowMultipleComponent]
    public class SDFCollider : Manipulators.Manipulator
    {
        /// <summary>
        /// Types of Analytical SDF's
        /// </summary>
        public enum SDFType
        {
            Sphere,
            Box,
            Capsule,
            Torus,
            Cylinder,
        }
        /// <summary>
        /// Currently chosen type of SDF collider
        /// </summary>
        public SDFType chosenSDFType = SDFType.Sphere;

        protected int ColliderIndex = 0;

        // Store all colliders separately from all manipulators
        public static readonly List<SDFCollider> AllColliders = new List<SDFCollider>();

        [StructLayout(LayoutKind.Sequential)]
        public class ColliderParams
        {
            public Vector4 Rotation;
            public Vector3 BBoxMin;
            public Int32 SDFType;
            public Vector3 BBoxMax;
            public Int32 iteration;
            public Vector3 Position;
            public Int32 VoxelCount;
            public Vector3 Scale;
            public Int32 OpType;
            public Int32 Elements;
            public Int32 CurrentID;
            public Int32 colliderIndex;
        };

        protected ColliderParams colliderParams;
        protected IntPtr NativeDataPtr;

#if ZIBRA_LIQUID_PAID_VERSION
        [Tooltip(
            "0.0 fluid flows without friction, 1.0 fluid sticks to the surface (0 is hydrophobic, 1 is hydrophilic)")]
        [Range(0.0f, 1.0f)]
        public float FluidFriction = 0.0f;

        [Tooltip("Allows the fluid to apply force to the object")]
        public bool ForceInteraction;
#endif

        [Tooltip("Inverts collider so liquid can only exist inside.")]
        public bool InvertSDF = false;

        public virtual ulong GetMemoryFootrpint()
        {
            return 0;
        }

        private void Update()
        {
            AdditionalData.x = (float)chosenSDFType;
            AdditionalData.z = InvertSDF ? -1.0f : 1.0f;
#if ZIBRA_LIQUID_PAID_VERSION
            AdditionalData.w = FluidFriction;
#endif
        }

        public void ApplyForceTorque(Vector3 Force, Vector3 Torque)
        {
#if ZIBRA_LIQUID_PAID_VERSION
            if (ForceInteraction)
            {
                Rigidbody rg = GetComponent<Rigidbody>();
                if (rg != null)
                {
                    rg.AddForce(Force, ForceMode.Force);
                    rg.AddTorque(Torque, ForceMode.Force);
                }
                else
                {
                    Debug.LogWarning(
                        "No rigid body component attached to collider, please add one for force interaction to work");
                }
            }
#endif
        }

        // clang-format doesn't parse code with new keyword properly
        // clang-format off
        
        protected new void OnEnable()
        {
            if (!AllColliders?.Contains(this) ?? false)
            {
                AllColliders.Add(this);
            }
        }

        protected new void OnDisable()
        {
            if (AllColliders?.Contains(this) ?? false)
            {
                AllColliders.Remove(this);
            }
        }

#if UNITY_EDITOR
        protected new void OnDestroy()
        {
            base.OnDestroy();

            ZibraLiquid[] components = FindObjectsOfType<ZibraLiquid>();
            foreach (var liquidInstance in components)
            {
                liquidInstance.RemoveCollider(this);
            }
        }
#endif
    }
}
