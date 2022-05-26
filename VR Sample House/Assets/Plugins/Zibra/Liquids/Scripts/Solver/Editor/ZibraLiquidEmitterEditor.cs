using com.zibra.liquid.Manipulators;
using UnityEditor;
using UnityEngine;

namespace com.zibra.liquid.Editor.Solver
{
    [CustomEditor(typeof(ZibraLiquidEmitter))]
    [CanEditMultipleObjects]
    public class ZibraLiquidEmitterEditor : ZibraLiquidManipulatorEditor
    {
        private ZibraLiquidEmitter[] EmitterInstances;

        private SerializedProperty ParticlesPerSec;
        private SerializedProperty InitialVelocity;
        private SerializedProperty PositionClampBehavior;

        public override void OnInspectorGUI()
        {
#if ZIBRA_LIQUID_PAID_VERSION
            if (EmitterInstances.Length > 1)
                GUILayout.Label("Multiple emitters selected. Showing sum of all selected instances.");
            long createdTotal = 0;
            int createdCurrentFrame = 0;
            foreach (var instance in EmitterInstances)
            {
                createdTotal += instance.createdParticlesTotal;
                createdCurrentFrame += instance.createdParticlesPerFrame;
            }
            GUILayout.Label("Total amount of created particles: " + createdTotal);
            GUILayout.Label("Amount of created particles per frame: " + createdCurrentFrame);
            GUILayout.Space(10);
#endif

            serializedObject.Update();

            EditorGUILayout.PropertyField(ParticlesPerSec);
            EditorGUILayout.PropertyField(InitialVelocity);
            EditorGUILayout.PropertyField(PositionClampBehavior);

            serializedObject.ApplyModifiedProperties();
        }

        // clang-format doesn't parse code with new keyword properly
        // clang-format off

        protected new void OnEnable()
        {
            base.OnEnable();

            EmitterInstances = new ZibraLiquidEmitter[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                EmitterInstances[i] = targets[i] as ZibraLiquidEmitter;
            }

            ParticlesPerSec = serializedObject.FindProperty("ParticlesPerSec");
            InitialVelocity = serializedObject.FindProperty("InitialVelocity");
            PositionClampBehavior = serializedObject.FindProperty("PositionClampBehavior");
        }
    }
}