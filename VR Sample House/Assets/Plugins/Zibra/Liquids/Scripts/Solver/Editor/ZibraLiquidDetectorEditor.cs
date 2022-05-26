#if ZIBRA_LIQUID_PAID_VERSION
using com.zibra.liquid.Manipulators;
using UnityEditor;
using UnityEngine;

namespace com.zibra.liquid.Editor.Solver
{
    [CustomEditor(typeof(ZibraLiquidDetector))]
    [CanEditMultipleObjects]
    public class ZibraLiquidDetectorEditor : ZibraLiquidManipulatorEditor
    {
        private ZibraLiquidDetector[] DetectorInstances;

        public override void OnInspectorGUI()
        {
            if (DetectorInstances.Length > 1)
                GUILayout.Label("Multiple detectors selected. Showing sum of all selected instances.");
            int particlesInside = 0;
            foreach (var instance in DetectorInstances)
            {
                particlesInside += instance.particlesInside;
            }
            GUILayout.Label("Amount of particles inside the detector: " + particlesInside);
        }

        // clang-format doesn't parse code with new keyword properly
        // clang-format off

        protected new void OnEnable()
        {
            base.OnEnable();

            DetectorInstances = new ZibraLiquidDetector[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                DetectorInstances[i] = targets[i] as ZibraLiquidDetector;
            }
        }
    }
}
#endif