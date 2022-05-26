using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace com.zibra.liquid.Manipulators
{
    [AddComponentMenu("Zibra/Zibra Liquid Force Field")]
    public class ZibraLiquidForceField : Manipulator
    {
        public enum ForceFieldType
        {
            Radial,
#if ZIBRA_LIQUID_PAID_VERSION
            Directional,
            Swirl
#endif
        }

        public enum ForceFieldShape
        {
            Sphere,
            Cube
        }

        public const float STRENGTH_DRAW_THRESHOLD = 0.001f;

#if !ZIBRA_LIQUID_PAID_VERSION
        [HideInInspector]
#endif
        public ForceFieldType Type = ForceFieldType.Radial;
        public ForceFieldShape Shape = ForceFieldShape.Sphere;
        [Tooltip("The strength of the force acting on the liquid")]
        [Range(-1.0f, 1.0f)]
        public float Strength = 1.0f;
        [Tooltip("How fast does the force lose its strenght with distance to the center")]
        [Range(0.0f, 10.0f)]
        public float DistanceDecay = 1.0f;

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = GetTransform();

            switch (Shape)
            {
            case ForceFieldShape.Sphere:
                Gizmos.DrawWireSphere(Vector3.zero, 1.0f);
                break;
            case ForceFieldShape.Cube:
                Gizmos.DrawWireCube(Vector3.zero, 2.0f * Vector3.one);
                break;
            }

            if (Math.Abs(Strength) < STRENGTH_DRAW_THRESHOLD)
                return;
            switch (Type)
            {
            case ForceFieldType.Radial:
                Utilities.GizmosHelper.DrawArrowsSphereRadial(Vector3.zero, Strength, 32, Color.blue);
                break;
#if ZIBRA_LIQUID_PAID_VERSION
            case ForceFieldType.Directional:
                Utilities.GizmosHelper.DrawArrowsSphereDirectional(Vector3.zero, Vector3.right * Strength, 32,
                                                                   Color.blue);
                break;
            case ForceFieldType.Swirl:
                Utilities.GizmosHelper.DrawArrowsSphereTangent(Vector3.zero, Vector3.up * Strength, 32, Color.blue);
                break;
#endif
            }
        }

        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }
#endif

        ZibraLiquidForceField()
        {
            ManipType = ManipulatorType.ForceField;
        }

        private void Start()
        {
            ManipType = ManipulatorType.ForceField;
        }

        private void Update()
        {
            AdditionalData.x = (int)Type;
            AdditionalData.y = Strength;
            AdditionalData.z = DistanceDecay;
            AdditionalData.w = (int)Shape;
        }
    }
}
