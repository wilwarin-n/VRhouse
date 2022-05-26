#if ZIBRA_LIQUID_PAID_VERSION

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace com.zibra.liquid.Manipulators
{
    [AddComponentMenu("Zibra/Zibra Liquid Detector")]
    public class ZibraLiquidDetector : Manipulator
    {
        [NonSerialized]
        public int particlesInside = 0;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.matrix = GetTransform();
            Gizmos.DrawWireCube(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
        }

        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }

        ZibraLiquidDetector()
        {
            ManipType = ManipulatorType.Detector;
        }

        private void Start()
        {
            ManipType = ManipulatorType.Detector;
        }
    }
}

#endif