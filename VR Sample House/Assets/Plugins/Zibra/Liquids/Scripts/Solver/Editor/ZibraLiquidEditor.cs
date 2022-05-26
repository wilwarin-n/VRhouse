using com.zibra.liquid.Solver;
using com.zibra.liquid.SDFObjects;
using com.zibra.liquid.Manipulators;
using com.zibra.liquid.Utilities;
using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#if UNITY_PIPELINE_URP
using System.Reflection;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
#endif

namespace com.zibra.liquid.Editor.Solver
{
    [CustomEditor(typeof(ZibraLiquid), true)]
    [CanEditMultipleObjects]
    public class ZibraLiquidEditor : UnityEditor.Editor
    {
        public static int liquidCount = 0;

        [MenuItem("GameObject/Zibra/Zibra Liquid", false, 10)]
        private static void CreateZibraLiquid(MenuCommand menuCommand)
        {
            liquidCount++;
            // Create a custom game object
            var go = new GameObject("Zibra Liquid " + liquidCount);
            ZibraLiquid liquid = go.AddComponent<ZibraLiquid>();
            // Moving component up the list, so important parameters are at the top
            for (int i = 0; i < 3; i++)
                UnityEditorInternal.ComponentUtility.MoveComponentUp(liquid);
            // Ensure it gets reparented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            // Create emitter for new liquid
            String emitterName = "ZibraLiquid Emitter " + (Manipulator.AllManipulators.Count + 1);
            var emitterGameObject = new GameObject(emitterName);
            var emitter = emitterGameObject.AddComponent<ZibraLiquidEmitter>();
            // Add emitter as child to liquid and add it to liquids manipulators
            GameObjectUtility.SetParentAndAlign(emitterGameObject, go);
            liquid.AddManipulator(emitter);
            Selection.activeObject = go;
        }

        [MenuItem("GameObject/Zibra/Zibra Liquid Emitter", false, 10)]
        private static void CreateZibraEmitter(MenuCommand menuCommand)
        {
            // Create a custom game object
            String name = "ZibraLiquid Emitter " + (Manipulator.AllManipulators.Count + 1);
            var go = new GameObject(name);
            var newEmitter = go.AddComponent<ZibraLiquidEmitter>();
            // Ensure it gets reparented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            // Add manipulator to liquid automatically, if parent object is liquid
            GameObject parentLiquidGameObject = menuCommand.context as GameObject;
            ZibraLiquid parentLiquid = parentLiquidGameObject?.GetComponent<ZibraLiquid>();
            parentLiquid?.AddManipulator(newEmitter);
            Selection.activeObject = go;
        }

#if ZIBRA_LIQUID_PAID_VERSION
        [MenuItem("GameObject/Zibra/Zibra Liquid Void", false, 10)]
        private static void CreateZibraVoid(MenuCommand menuCommand)
        {
            String name = "ZibraLiquid Void " + (Manipulator.AllManipulators.Count + 1);
            // Create a custom game object
            var go = new GameObject(name);
            var newVoid = go.AddComponent<ZibraLiquidVoid>();
            // Ensure it gets reparented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            // Add manipulator to liquid automatically, if parent object is liquid
            GameObject parentLiquidGameObject = menuCommand.context as GameObject;
            ZibraLiquid parentLiquid = parentLiquidGameObject?.GetComponent<ZibraLiquid>();
            parentLiquid?.AddManipulator(newVoid);
            Selection.activeObject = go;
        }

        [MenuItem("GameObject/Zibra/Zibra Liquid Detector", false, 10)]
        private static void CreateZibraDetector(MenuCommand menuCommand)
        {
            String name = "ZibraLiquid Detector " + (Manipulator.AllManipulators.Count + 1);
            // Create a custom game object
            var go = new GameObject(name);
            var newDetector = go.AddComponent<ZibraLiquidDetector>();
            // Ensure it gets reparented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            // Add manipulator to liquid automatically, if parent object is liquid
            GameObject parentLiquidGameObject = menuCommand.context as GameObject;
            ZibraLiquid parentLiquid = parentLiquidGameObject?.GetComponent<ZibraLiquid>();
            parentLiquid?.AddManipulator(newDetector);
            Selection.activeObject = go;
        }
#endif

        [MenuItem("GameObject/Zibra/Zibra Liquid Force Field", false, 10)]
        private static void CreateZibraForceField(MenuCommand menuCommand)
        {
            String name = "ZibraLiquid Force Field " + (Manipulator.AllManipulators.Count + 1);
            // Create a custom game object
            var go = new GameObject(name);
            var newForceField = go.AddComponent<ZibraLiquidForceField>();
            // Ensure it gets reparented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            // Add manipulator to liquid automatically, if parent object is liquid
            GameObject parentLiquidGameObject = menuCommand.context as GameObject;
            ZibraLiquid parentLiquid = parentLiquidGameObject?.GetComponent<ZibraLiquid>();
            parentLiquid?.AddManipulator(newForceField);
            Selection.activeObject = go;
        }

        private const int GRID_NODE_COUNT_WARNING_THRESHOLD = 6000000;
        private const int PARTICLE_COUNT_WARNING_THRESHOLD = 5000000;

        private enum EditMode
        {
            None,
            Container,
            Emitter
        }

        private static readonly Color containerColor = new Color(1f, 0.8f, 0.4f);

        private ZibraLiquid[] ZibraLiquidInstances;

        private SerializedProperty ContainerSize;
        private SerializedProperty TimeStepMax;
        private SerializedProperty SimTimePerSec;

        private SerializedProperty MaxParticleNumber;
        private SerializedProperty IterationsPerFrame;
        private SerializedProperty GridResolution;
        private SerializedProperty RunSimulation;
        private SerializedProperty sdfColliders;
        private SerializedProperty manipulators;
        private SerializedProperty reflectionProbeHDRP;
        private SerializedProperty customLightHDRP;
        private SerializedProperty reflectionProbeSRP;
        private SerializedProperty useFixedTimestep;
        private SerializedProperty InitialState;
#if ZIBRA_LIQUID_DEBUG
        private SerializedProperty visualizeSceneSDF;
#endif
        private SerializedProperty EnableDownscale;
        private SerializedProperty DownscaleFactor;
        private SerializedProperty BakedInitialStateAsset;

        private bool colliderDropdownToggle = true;
        private bool manipulatorDropdownToggle = true;
        private bool statsDropdownToggle = true;
        private bool footprintDropdownToggle = true;
        private EditMode editMode;
        private readonly BoxBoundsHandle boxBoundsHandleContainer = new BoxBoundsHandle();

        private GUIStyle containerText;

        protected void TriggerRepaint()
        {
            Repaint();
        }

        protected void OnEnable()
        {
            // Only need to add callback to one of liquids
            ZibraLiquid anyInstance = target as ZibraLiquid;
            // Disabled, we don't want to repain liquid editor each frame
            // anyInstance.onChanged += TriggerRepaint;

            ZibraLiquidInstances = new ZibraLiquid[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                ZibraLiquidInstances[i] = targets[i] as ZibraLiquid;
            }

            serializedObject.Update();

#if UNITY_PIPELINE_HDRP
            reflectionProbeHDRP = serializedObject.FindProperty("reflectionProbeHDRP");
            customLightHDRP = serializedObject.FindProperty("customLightHDRP");
#endif
            reflectionProbeSRP = serializedObject.FindProperty("reflectionProbeSRP");
            ContainerSize = serializedObject.FindProperty("containerSize");
            TimeStepMax = serializedObject.FindProperty("timeStepMax");
            SimTimePerSec = serializedObject.FindProperty("simTimePerSec");

            MaxParticleNumber = serializedObject.FindProperty("MaxNumParticles");
            IterationsPerFrame = serializedObject.FindProperty("iterationsPerFrame");
            GridResolution = serializedObject.FindProperty("gridResolution");

            RunSimulation = serializedObject.FindProperty("runSimulation");

            manipulators = serializedObject.FindProperty("manipulators");

            sdfColliders = serializedObject.FindProperty("sdfColliders");

            useFixedTimestep = serializedObject.FindProperty("useFixedTimestep");
#if ZIBRA_LIQUID_DEBUG
            visualizeSceneSDF = serializedObject.FindProperty("visualizeSceneSDF");
#endif

            EnableDownscale = serializedObject.FindProperty("EnableDownscale");
            DownscaleFactor = serializedObject.FindProperty("DownscaleFactor");
            BakedInitialStateAsset = serializedObject.FindProperty("BakedInitialStateAsset");

            InitialState = serializedObject.FindProperty("InitialState");
            serializedObject.ApplyModifiedProperties();

            containerText = new GUIStyle { alignment = TextAnchor.MiddleLeft, normal = { textColor = containerColor } };
        }

        protected void OnDisable()
        {
            ZibraLiquid anyInstance = target as ZibraLiquid;
            anyInstance.onChanged -= TriggerRepaint;
        }

        // Toggled with "Edit Container Area" button
        protected void OnSceneGUI()
        {
            foreach (var instance in ZibraLiquidInstances)
            {
                if (instance.initialized)
                {
                    continue;
                }

                var localToWorld = Matrix4x4.TRS(instance.transform.position, instance.transform.rotation, Vector3.one);

                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                using (new Handles.DrawingScope(containerColor, localToWorld))
                {
                    if (editMode == EditMode.Container)
                    {
                        Handles.Label(Vector3.zero, "Container Area", containerText);

                        boxBoundsHandleContainer.center = Vector3.zero;
                        boxBoundsHandleContainer.size = instance.containerSize;

                        EditorGUI.BeginChangeCheck();
                        boxBoundsHandleContainer.DrawHandle();
                        if (EditorGUI.EndChangeCheck())
                        {
                            // record the target object before setting new values so changes can be undone/redone
                            Undo.RecordObject(instance, "Change Container");

                            instance.containerSize = boxBoundsHandleContainer.size;
                            instance.OnValidate();
                            EditorUtility.SetDirty(instance);
                        }
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            if (ZibraLiquidInstances == null || ZibraLiquidInstances.Length == 0)
            {
                Debug.LogError("ZibraLiquidEditor not attached to ZibraLiquid component.");
                return;
            }

            serializedObject.Update();

#if ZIBRA_LIQUID_DEBUG
            EditorGUILayout.HelpBox("DEBUG VERSION", MessageType.Info);
#endif

#if UNITY_PIPELINE_URP
            // Getting non public list of render features via reflection
            var URPAsset = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
            var field = typeof(ScriptableRenderer)
                            .GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && URPAsset != null)
            {
                var scriptableRendererFeatures =
                    field.GetValue(URPAsset.scriptableRenderer) as List<ScriptableRendererFeature>;
                bool liquidRendererFound = false;
                if (scriptableRendererFeatures != null)
                {
                    foreach (var renderFeature in scriptableRendererFeatures)
                    {
                        if (renderFeature is LiquidURPRenderComponent)
                        {
                            liquidRendererFound = true;
                            break;
                        }
                    }
                }
                if (!liquidRendererFound)
                {
                    EditorGUILayout.HelpBox(
                        "URP Liquid Rendering Component is not added. Liquid will not be rendered, but will still be simulated.",
                        MessageType.Error);
                }

                EditorGUILayout.HelpBox(
                    "On URP liquid will only be rendered in Game view, and will not be rendered in Scene view.",
                    MessageType.Info);
            }
#endif

            bool liquidCanSpawn = true;
            foreach (var liquid in ZibraLiquidInstances)
            {
                bool haveEmitter = liquid.HasEmitter();
#if ZIBRA_LIQUID_PAID_VERSION
                bool haveBakedLiquid = (liquid.InitialState == ZibraLiquid.InitialStateType.BakedLiquidState);
#else
                const bool haveBakedLiquid = false;
#endif
                if (!haveEmitter && !haveBakedLiquid)
                {
                    liquidCanSpawn = false;
                    break;
                }
            }
            if (!liquidCanSpawn)
            {
                EditorGUILayout.HelpBox(
                    "No emitters or initial state added" +
                        (ZibraLiquidInstances.Length == 1 ? "." : " for at least 1 liquid instance.") +
                        " No liquid can spawn under these conditions.",
                    MessageType.Error);
            }

#if UNITY_PIPELINE_HDRP
            if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
            {
                bool lightMissing = false;
                foreach (var liquid in ZibraLiquidInstances)
                {
                    if (liquid.customLightHDRP == null)
                    {
                        lightMissing = true;
                        break;
                    }
                }
                if (lightMissing)
                {
                    EditorGUILayout.HelpBox(
                        "Custom light is not set" +
                            (ZibraLiquidInstances.Length == 1 ? "." : " for at least 1 liquid instance.") +
                            " Liquid will not render properly.",
                        MessageType.Error);
                }
            }
#endif

            bool reflectionProbeMissing = false;
            foreach (var liquid in ZibraLiquidInstances)
            {

                if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
                {
#if UNITY_PIPELINE_HDRP
                    if (liquid.reflectionProbeHDRP == null)
                    {
                        reflectionProbeMissing = true;
                        break;
                    }
#endif
                }
                else if (liquid.reflectionProbeSRP == null)
                {
                    reflectionProbeMissing = true;
                    break;
                }
            }
            if (reflectionProbeMissing)
            {
                EditorGUILayout.HelpBox(
                    "Reflection probe is not set" +
                        (ZibraLiquidInstances.Length == 1 ? "." : " for at least 1 liquid instance.") +
                        " Liquid will not render properly.",
                    MessageType.Error);
            }

            bool gridTooBig = false;
            foreach (var liquid in ZibraLiquidInstances)
            {
                liquid.UpdateGridSize();
                Vector3Int gridSize = liquid.GridSize;
                int nodesCount = gridSize[0] * gridSize[1] * gridSize[2];
                if (nodesCount > GRID_NODE_COUNT_WARNING_THRESHOLD)
                {
                    gridTooBig = true;
                    break;
                }
            }
            if (gridTooBig)
            {
                EditorGUILayout.HelpBox(
                    "Grid resolution is too high" +
                        (ZibraLiquidInstances.Length == 1 ? "." : " for at least 1 liquid instance.") +
                        " High-end hardware is strongly recommended.",
                    MessageType.Info);
            }

            bool particleCountTooHigh = false;
            foreach (var liquid in ZibraLiquidInstances)
            {
                if (liquid.MaxNumParticles >= PARTICLE_COUNT_WARNING_THRESHOLD)
                {
                    particleCountTooHigh = true;
                    break;
                }
            }
            if (particleCountTooHigh)
            {
                EditorGUILayout.HelpBox(
                    "Particle count is too high" +
                        (ZibraLiquidInstances.Length == 1 ? "." : " for at least 1 liquid instance.") +
                        " High-end hardware is strongly recommended.",
                    MessageType.Info);
            }

#if ZIBRA_LIQUID_PAID_VERSION
            bool noInitialState = false;
            foreach (var instance in ZibraLiquidInstances)
            {
                if (instance.InitialState == ZibraLiquid.InitialStateType.BakedLiquidState &&
                    instance.BakedInitialStateAsset == null)
                {
                    noInitialState = true;
                }
            }
            if (noInitialState)
            {
                EditorGUILayout.HelpBox("Initial state is set to baked liquid, but state is not set" +
                                            (ZibraLiquidInstances.Length == 1 ? "." : " in at least 1 instance."),
                                        MessageType.Error);
            }
#endif

            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);

            if (GUILayout.Button(EditorGUIUtility.IconContent("EditCollider"), GUILayout.MaxWidth(40),
                                 GUILayout.Height(30)))
            {
                editMode = editMode == EditMode.Container ? EditMode.None : EditMode.Container;
                SceneView.RepaintAll();
            }

            bool anyInstanceActivated = false;
            foreach (var instance in ZibraLiquidInstances)
            {
                if (instance.initialized)
                {
                    anyInstanceActivated = true;
                    break;
                }
            }

            EditorGUI.BeginDisabledGroup(anyInstanceActivated);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Edit Container Area", containerText, GUILayout.MaxWidth(100),
                                       GUILayout.Height(30));
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            EditorGUILayout.PropertyField(ContainerSize);

            GUILayout.Space(15);

            GUIContent collidersText = new GUIContent("Colliders");
            EditorGUILayout.PropertyField(sdfColliders, collidersText, true);

#if ZIBRA_LIQUID_FREE_VERSION
            bool reachedColliderLimits = false;

            foreach (var instance in ZibraLiquidInstances)
            {
                if (instance.GetColliderList().Count >= 5)
                {
                    reachedColliderLimits = true;
                    break;
                }
            }
#else
            const bool reachedColliderLimits = false;
#endif

            if (!reachedColliderLimits)
            {
                GUIContent btnTxt = new GUIContent("Add Collider");
                var rtBtn = GUILayoutUtility.GetRect(btnTxt, GUI.skin.button);
                rtBtn.center = new Vector2(EditorGUIUtility.currentViewWidth / 2, rtBtn.center.y);

                if (EditorGUI.DropdownButton(rtBtn, btnTxt, FocusType.Keyboard))
                {
                    colliderDropdownToggle = !colliderDropdownToggle;
                }

                if (colliderDropdownToggle)
                {
                    EditorGUI.indentLevel++;

                    var empty = true;

                    foreach (var sdfCollider in SDFCollider.AllColliders)
                    {
                        bool presentInAllInstances = true;

                        foreach (var instance in ZibraLiquidInstances)
                        {
                            if (!instance.HasCollider(sdfCollider))
                            {
                                presentInAllInstances = false;
                                break;
                            }
                        }

                        if (presentInAllInstances)
                        {
                            continue;
                        }

                        empty = false;
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUILayout.ObjectField(sdfCollider, typeof(SDFCollider), false);
                            EditorGUI.EndDisabledGroup();
                            if (GUILayout.Button("Add", GUILayout.ExpandWidth(false)))
                            {
                                foreach (var instance in ZibraLiquidInstances)
                                {
                                    instance.AddCollider(sdfCollider);
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    if (empty)
                    {
                        GUIContent labelText;
#if ZIBRA_LIQUID_FREE_VERSION
                        if (reachedColliderLimits)
                        {
                            labelText =
                                new GUIContent("You already have max number of colliders allowed on free version.");
                        }
                        else
#endif
                        {
                            labelText = new GUIContent("The list is empty.");
                        }
                        var rtLabel = GUILayoutUtility.GetRect(labelText, GUI.skin.label);
                        rtLabel.center = new Vector2(EditorGUIUtility.currentViewWidth / 2, rtLabel.center.y);

                        EditorGUI.LabelField(rtLabel, labelText);
                    }
                    else
                    {
                        if (GUILayout.Button("Add all"))
                        {
                            foreach (var liquid in ZibraLiquidInstances)
                            {
                                foreach (var sdfCollider in SDFCollider.AllColliders)
                                {
#if ZIBRA_LIQUID_FREE_VERSION
                                    if (liquid.GetColliderList().Count >= 5)
                                        break;
#endif
                                    liquid.AddCollider(sdfCollider);
                                }
                            }
                        }
                    }

                    EditorGUI.indentLevel--;
                }
            }

            GUILayout.Space(15);

            EditorGUILayout.PropertyField(manipulators, true);

#if ZIBRA_LIQUID_FREE_VERSION
            bool reachedEmitterLimit = false;
            bool reachedForceFieldLimit = false;

            foreach (var instance in ZibraLiquidInstances)
            {
                foreach (var manipulator in instance.GetManipulatorList())
                {
                    if (manipulator.ManipType == Manipulator.ManipulatorType.Emitter)
                    {
                        reachedEmitterLimit = true;
                    }
                    else if (manipulator.ManipType == Manipulator.ManipulatorType.ForceField)
                    {
                        reachedForceFieldLimit = true;
                    }
                }
            }
#else
            const bool reachedEmitterLimit = false;
            const bool reachedForceFieldLimit = false;
#endif

            if (!(reachedEmitterLimit && reachedForceFieldLimit))
            {
                GUIContent manipBtnTxt = new GUIContent("Add Manipulator");
                var manipBtn = GUILayoutUtility.GetRect(manipBtnTxt, GUI.skin.button);
                manipBtn.center = new Vector2(EditorGUIUtility.currentViewWidth / 2, manipBtn.center.y);

                if (EditorGUI.DropdownButton(manipBtn, manipBtnTxt, FocusType.Keyboard))
                {
                    manipulatorDropdownToggle = !manipulatorDropdownToggle;
                }

                if (manipulatorDropdownToggle)
                {
                    EditorGUI.indentLevel++;

                    var empty = true;
                    foreach (var manipulator in Manipulator.AllManipulators)
                    {
                        bool presentInAllInstances = true;

                        if (manipulator.ManipType == Manipulator.ManipulatorType.Emitter && reachedEmitterLimit)
                        {
                            continue;
                        }

                        if (manipulator.ManipType == Manipulator.ManipulatorType.ForceField && reachedForceFieldLimit)
                        {
                            continue;
                        }

                        foreach (var instance in ZibraLiquidInstances)
                        {
                            if (!instance.HasManipulator(manipulator))
                            {
                                presentInAllInstances = false;
                                break;
                            }
                        }

                        if (presentInAllInstances)
                        {
                            continue;
                        }

                        empty = false;

                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField(manipulator, typeof(Manipulator), false);
                        EditorGUI.EndDisabledGroup();
                        if (GUILayout.Button("Add", GUILayout.ExpandWidth(false)))
                        {
                            foreach (var instance in ZibraLiquidInstances)
                            {
                                instance.AddManipulator(manipulator);
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    if (empty)
                    {
                        GUIContent labelText = new GUIContent("The list is empty.");
                        var rtLabel = GUILayoutUtility.GetRect(labelText, GUI.skin.label);
                        rtLabel.center = new Vector2(EditorGUIUtility.currentViewWidth / 2, rtLabel.center.y);

                        EditorGUI.LabelField(rtLabel, labelText);
                    }
                    else
                    {
                        // No point in "Add all" button on free version since you can only have 2 manipulators anyway
#if ZIBRA_LIQUID_PAID_VERSION
                        if (GUILayout.Button("Add all"))
                        {
                            foreach (var liquid in ZibraLiquidInstances)
                            {
                                foreach (var manipulator in Manipulator.AllManipulators)
                                {
                                    liquid.AddManipulator(manipulator);
                                }
                            }
                        }
#endif
                    }

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.Space(10);

            EditorGUILayout.PropertyField(TimeStepMax, new GUIContent("Maximum Allowed Timestep"));
            EditorGUILayout.PropertyField(SimTimePerSec, new GUIContent("Simulation speed"));
            EditorGUILayout.PropertyField(IterationsPerFrame);

            EditorGUI.BeginDisabledGroup(anyInstanceActivated);
            EditorGUILayout.PropertyField(MaxParticleNumber, new GUIContent("Max particle count"));
            EditorGUILayout.PropertyField(GridResolution);
            EditorGUI.EndDisabledGroup();

            ZibraLiquidInstances[0].UpdateGridSize();
            Vector3Int solverRes = ZibraLiquidInstances[0].GridSize;
            bool[] sameDimensions = new bool[3];
            sameDimensions[0] = true;
            sameDimensions[1] = true;
            sameDimensions[2] = true;
            foreach (var instance in ZibraLiquidInstances)
            {
                instance.UpdateGridSize();
                var currentSolverRes = instance.GridSize;
                for (int i = 0; i < 3; i++)
                {
                    if (solverRes[i] != currentSolverRes[i])
                        sameDimensions[i] = false;
                }
            }
            string effectiveResolutionText =
                $"({(sameDimensions[0] ? solverRes[0].ToString() : "-")}, {(sameDimensions[1] ? solverRes[1].ToString() : "-")}, {(sameDimensions[2] ? solverRes[2].ToString() : "-")})";
            GUILayout.Label("Effective grid resolution:   " + effectiveResolutionText);

            EditorGUILayout.PropertyField(RunSimulation);
            EditorGUILayout.PropertyField(useFixedTimestep);
#if ZIBRA_LIQUID_DEBUG
            EditorGUILayout.PropertyField(visualizeSceneSDF);
#endif

            EditorGUILayout.PropertyField(EnableDownscale, new GUIContent("Enable Render Downscale"));
            if (EnableDownscale.boolValue)
            {
                EditorGUILayout.PropertyField(DownscaleFactor);
            }

            if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
            {
#if UNITY_PIPELINE_HDRP
                EditorGUILayout.PropertyField(customLightHDRP, new GUIContent("Custom Light"));
                EditorGUILayout.PropertyField(reflectionProbeHDRP, new GUIContent("Reflection Probe"));
#endif
            }
            else
            {
                EditorGUILayout.PropertyField(reflectionProbeSRP, new GUIContent("Reflection Probe"));
            }

            EditorGUI.BeginDisabledGroup(anyInstanceActivated);

#if ZIBRA_LIQUID_PAID_VERSION
            EditorGUILayout.PropertyField(InitialState);

            bool showBaking = false;
            foreach (var instance in ZibraLiquidInstances)
            {
                if (instance.InitialState == ZibraLiquid.InitialStateType.BakedLiquidState)
                {
                    showBaking = true;
                    break;
                }
            }

            if (showBaking)
            {
                EditorGUILayout.PropertyField(BakedInitialStateAsset);

                EditorGUI.BeginDisabledGroup(Application.isPlaying);
                if (GUILayout.Button("Open Zibra Liquids Baking Utility (experimental)"))
                {
                    ZibraLiquidsBakingUtility utility = EditorWindow.GetWindow<ZibraLiquidsBakingUtility>(
                        "Zibra Liquids Baking Utility (experimental)");
                    if (ZibraLiquidInstances.Length == 1)
                    {
                        utility.liquidInstance = ZibraLiquidInstances[0];
                    }
                }

                EditorGUI.EndDisabledGroup();
            }
#endif
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();

            GUILayout.Space(10);

            switch (RenderPipelineDetector.GetRenderPipelineType())
            {
            case RenderPipelineDetector.RenderPipeline.SRP:
                GUILayout.Label("Current render pipeline: SRP");
                break;
            case RenderPipelineDetector.RenderPipeline.URP:
                GUILayout.Label("Current render pipeline: URP");
                break;
            case RenderPipelineDetector.RenderPipeline.HDRP:
                GUILayout.Label("Current render pipeline: HDRP");
                break;
            }

            GUILayout.Space(10);

            GUIContent footprintButtonText = new GUIContent("Approximate VRAM footprint");
            var footprintButton = GUILayoutUtility.GetRect(footprintButtonText, GUI.skin.button);
            footprintButton.center = new Vector2(EditorGUIUtility.currentViewWidth / 2, footprintButton.center.y);

            if (EditorGUI.DropdownButton(footprintButton, footprintButtonText, FocusType.Keyboard))
            {
                footprintDropdownToggle = !footprintDropdownToggle;
            }
            if (footprintDropdownToggle)
            {
                if (ZibraLiquidInstances.Length > 1)
                {
                    GUILayout.Label("Selected multiple liquid instances. Showing sum of all selected instances.");
                }

                ulong totalParticleFootprint = 0;
                ulong totalCollidersFootprint = 0;
                ulong totalGridFootprint = 0;

                foreach (var instance in ZibraLiquidInstances)
                {
                    totalParticleFootprint += instance.GetParticleCountFootprint();
                    totalCollidersFootprint += instance.GetCollidersFootprint();
                    totalGridFootprint += instance.GetGridFootprint();
                }

                GUILayout.Label($"Particle count footprint: {(float)totalParticleFootprint / (1 << 20):N2}MB");
                GUILayout.Label($"Colliders footprint: {(float)totalCollidersFootprint / (1 << 20):N2}MB");
                GUILayout.Label($"Grid size footprint: {(float)totalGridFootprint / (1 << 20):N2}MB");
            }

            GUILayout.Space(15);

            if (anyInstanceActivated)
            {
                GUIContent statsButtonText = new GUIContent("Simulation statistics");
                var statsButton = GUILayoutUtility.GetRect(statsButtonText, GUI.skin.button);
                statsButton.center = new Vector2(EditorGUIUtility.currentViewWidth / 2, statsButton.center.y);

                if (EditorGUI.DropdownButton(statsButton, statsButtonText, FocusType.Keyboard))
                {
                    statsDropdownToggle = !statsDropdownToggle;
                }
                if (statsDropdownToggle)
                {
                    if (ZibraLiquidInstances.Length > 1)
                    {
                        GUILayout.Label(
                            "Selected multiple liquid instances. Please select exactly one instance to view statistics.");
                    }
                    else
                    {
                        GUILayout.Label("Current time step: " + ZibraLiquidInstances[0].timestep);
                        GUILayout.Label("Internal time: " + ZibraLiquidInstances[0].simulationInternalTime);
                        GUILayout.Label("Simulation frame: " + ZibraLiquidInstances[0].simulationInternalFrame);
                        GUILayout.Label("Active particles: " + ZibraLiquidInstances[0].activeParticleNumber + " / " +
                                        ZibraLiquidInstances[0].MaxNumParticles);
                    }
                }
            }
        }
    }
}
