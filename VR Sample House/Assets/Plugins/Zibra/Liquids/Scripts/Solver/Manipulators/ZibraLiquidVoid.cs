#if ZIBRA_LIQUID_PAID_VERSION

using System;
using UnityEngine;

namespace com.zibra.liquid.Manipulators
{
    [AddComponentMenu("Zibra/Zibra Liquid Void")]
    public class ZibraLiquidVoid : Manipulator
    {
        [NonSerialized]
        public long deletedParticleCountTotal = 0;
        [NonSerialized]
        public int deletedParticleCountPerFrame = 0;

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }

        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }
#endif
        ZibraLiquidVoid()
        {
            ManipType = ManipulatorType.Void;
        }
    }
}

#endif
