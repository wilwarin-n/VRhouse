#if ZIBRA_LIQUID_PAID_VERSION
using com.zibra.liquid.Manipulators;
using UnityEditor;
using UnityEngine;

namespace com.zibra.liquid.Editor.Solver
{
    [CustomEditor(typeof(ZibraLiquidVoid))]
    [CanEditMultipleObjects]
    public class ZibraLiquidVoidEditor : ZibraLiquidManipulatorEditor
    {
        private ZibraLiquidVoid[] VoidInstances;

        public override void OnInspectorGUI()
        {
            if (VoidInstances.Length > 1)
                GUILayout.Label("Multiple voids selected. Showing sum of all selected instances.");
            long deletedTotal = 0;
            int deletedCurrentFrame = 0;
            foreach (var instance in VoidInstances)
            {
                deletedTotal += instance.deletedParticleCountTotal;
                deletedCurrentFrame += instance.deletedParticleCountPerFrame;
            }
            GUILayout.Label("Total amount of deleted particles: " + deletedTotal);
            GUILayout.Label("Deleted particles per frame: " + deletedCurrentFrame);
        }

        // clang-format doesn't parse code with new keyword properly
        // clang-format off

        protected new void OnEnable()
        {
            base.OnEnable();

            VoidInstances = new ZibraLiquidVoid[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                VoidInstances[i] = targets[i] as ZibraLiquidVoid;
            }
        }
    }
}
#endif