using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace com.zibra.liquid.DataStructures
{
    [Serializable]
    public class ZibraLiquidSolverParameters : MonoBehaviour
    {
        public const float GRAVITY_THRESHOLD = 100f;

        public Vector3 Gravity = new Vector3(0.0f, -9.81f, 0.0f);

        [Tooltip("The stiffness of the liquid.")]
        [Min(0.0f)]
        public float FluidStiffness = 0.1f;

        [Tooltip("The sharpness of the stiffness.")]
        [Range(1.0f, 8.0f)]
        [HideInInspector]
        public float FluidStiffnessPower = 3.0f;

        [NonSerialized]
        [HideInInspector]
        [Obsolete("ParticlesPerCell is deprecated. Use ParticleDensity instead.", true)]
        public float ParticlesPerCell;

        [Tooltip(
            "Resting density of particles. measured in particles/cell. This option directly affects volume of each particle. Higher values correspond to less volume, but higher quality simulation.")]
        [FormerlySerializedAs("ParticlesPerCell")]
        [Range(0.1f, 10.0f)]
        public float ParticleDensity = 1f;

        [NonSerialized]
        [HideInInspector]
        [Obsolete("VelocityLimit is deprecated. Use MaximumVelocity instead.", true)]
        public float VelocityLimit;

        [Tooltip("The velocity limit of the particles")]
        [FormerlySerializedAs("VelocityLimit")]
        [Range(0.0f, 10.0f)]
        public float MaximumVelocity = 3.0f;

#if ZIBRA_LIQUID_PAID_VERSION
        [Tooltip(
            "Minimum velocity the particles can have, non-zero values make an infinite flow. For normal liquid this value should be 0.")]
        [Range(0.0f, 10.0f)]
        public float MinimumVelocity = 0.0f;
#endif

        [Range(0.0f, 1.0f)]
        public float Viscosity = 0.0f;

#if ZIBRA_LIQUID_PAID_VERSION
        [Tooltip("You can set this parameter to negative value to get a spagettification effect")]
        [Range(-1.0f, 1.0f)]
        public float SurfaceTension = 0.0f;

        [Tooltip("The strength of the force acting on rigid bodies. Have exponential scale, from exp(-4) to exp(4).")]
        [Range(-1.0f, 1.0f)]
        public float ForceInteractionStrength = 0.0f;
#endif
#if UNITY_EDITOR
        public void OnValidate()
        {
            ValidateGravity();
        }
#endif
        public void ValidateGravity()
        {
            Gravity.x = Mathf.Clamp(Gravity.x, -GRAVITY_THRESHOLD, GRAVITY_THRESHOLD);
            Gravity.y = Mathf.Clamp(Gravity.y, -GRAVITY_THRESHOLD, GRAVITY_THRESHOLD);
            Gravity.z = Mathf.Clamp(Gravity.z, -GRAVITY_THRESHOLD, GRAVITY_THRESHOLD);
        }
    }
}