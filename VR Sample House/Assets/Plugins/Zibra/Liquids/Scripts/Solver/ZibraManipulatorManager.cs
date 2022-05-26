using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace com.zibra.liquid.Manipulators
{
    public class ZibraManipulatorManager : MonoBehaviour
    {
        [HideInInspector]
        [StructLayout(LayoutKind.Sequential)]
        public struct ManipulatorParam
        {
            public Matrix4x4 Transform;
            public Matrix4x4 PreviousTransform; // the previous frame transform of the object, needed for accurate
                                                // collision detection
            public Matrix4x4 ToObjectSpace;
            public Vector3 Position;
            public Int32 ConstDataBegin;
            public Vector3 Scale;
            Single ManipulatorParamPadding;
            public Quaternion Rotation;
            public Vector3 BBoxMin;
            public Int32 Type;
            public Vector3 BBoxMax;
            public Int32 Enabled;
            public Vector4 AdditionalData;
        }

        [HideInInspector]
        [StructLayout(LayoutKind.Sequential)]
        public struct ManipulatorIndices
        {
            public int EmitterIndex;
            public int EmitterIndexEnd;

            public int VoidIndex;
            public int VoidIndexEnd;

            public int ForceFieldIndex;
            public int ForceFieldIndexEnd;

            public int AnalyticColliderIndex;
            public int AnalyticColliderIndexEnd;

            public int NeuralColliderIndex;
            public int NeuralColliderIndexEnd;

            public int DetectorIndex;
            public int DetectorIndexEnd;

            public int PortalIndex;
            public int PortalIndexEnd;
            Vector2 ManipulatorIndicesPadding;
        }

        int[] TypeIndex = new int[(int)Manipulator.ManipulatorType.TypeNum + 1];

        public ManipulatorIndices indices = new ManipulatorIndices();

        // All data together
        [HideInInspector]
        public int Elements = 0;
        [HideInInspector]
        public List<ManipulatorParam> ManipulatorParams = new List<ManipulatorParam>();

        [HideInInspector]
        public float[] ConstAdditionalData = new float[0];
        [HideInInspector]
        public List<int> ConstDataID = new List<int>();

        private Vector3 VectorClamp(Vector3 x, Vector3 min, Vector3 max)
        {
            return Vector3.Max(Vector3.Min(x, max), min);
        }

        private List<Manipulator> manipulators;

        /// <summary>
        /// Update all arrays and lists with manipulator object data
        /// Should be executed every simulation frame
        /// </summary>
        public void UpdateDynamic(Vector3 containerPos, Vector3 containerSize, float deltaTime = 0.0f)
        {
            int ID = 0;
            ManipulatorParams.Clear();
            // fill arrays

            foreach (var manipulator in manipulators)
            {
                if (manipulator == null)
                    continue;

                ManipulatorParam manip = new ManipulatorParam();

                manip.Transform = manipulator.GetTransform();
                manip.ToObjectSpace = manipulator.transform.worldToLocalMatrix;
                manip.Type = (int)manipulator.ManipType;
                manip.Rotation = manipulator.GetRotation();
                manip.Scale = manipulator.GetScale();
                manip.Position = manipulator.GetPosition();
                manip.Enabled = (manipulator.isActiveAndEnabled && manipulator.gameObject.activeInHierarchy) ? 1 : 0;
                manip.AdditionalData = manipulator.AdditionalData;
                manip.ConstDataBegin = Mathf.Max(ConstDataID[ID], 0);
                manip.PreviousTransform = manipulator.PreviousTransform;

#if ZIBRA_LIQUID_PAID_VERSION
                if (manipulator is SDFObjects.ZibraNeuralCollider)
                {
                    SDFObjects.ZibraNeuralCollider collider = manipulator as SDFObjects.ZibraNeuralCollider;
                    manip.BBoxMax = collider.BoundingBoxMax;
                    manip.BBoxMin = collider.BoundingBoxMin;
                }
#endif
                if (manipulator is ZibraLiquidEmitter)
                {
                    ZibraLiquidEmitter emitter = manipulator as ZibraLiquidEmitter;

                    if (emitter.PositionClampBehavior == ZibraLiquidEmitter.ClampBehaviorType.Clamp)
                    {
                        Vector3[] positions = {
                            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f),
                            new Vector3(-0.5f, 0.5f, -0.5f),  new Vector3(-0.5f, 0.5f, 0.5f),
                            new Vector3(0.5f, -0.5f, -0.5f),  new Vector3(0.5f, -0.5f, 0.5f),
                            new Vector3(0.5f, 0.5f, -0.5f),   new Vector3(0.5f, 0.5f, 0.5f),
                        };
                        Bounds bounds = GeometryUtility.CalculateBounds(positions, manip.Transform);

                        Vector3 containerMin = containerPos - containerSize * 0.45f;
                        Vector3 containerMax = containerPos + containerSize * 0.45f;

                        Vector3 offsetsMin = Vector3.Max(containerMin - bounds.min, new Vector3(0, 0, 0));
                        Vector3 offsetsMax = Vector3.Max(bounds.max - containerMax, new Vector3(0, 0, 0));

                        manip.Transform = Matrix4x4.Translate(offsetsMin - offsetsMax) * manip.Transform;
                    }

                    manip.AdditionalData.x = Mathf.Floor(emitter.ParticlesPerSec * deltaTime);
                }

                ManipulatorParams.Add(manip);
                ID++;
            }

            // ManipulatorParams.ToArray();
            Elements = manipulators.Count;
        }

        private static float INT2Float(int a)
        {
            const float MAX_INT = 2147483647.0f;
            const float F2I_MAX_VALUE = 5000.0f;
            const float F2I_SCALE = (MAX_INT / F2I_MAX_VALUE);

            return a / F2I_SCALE;
        }

        private int GetStatIndex(int id, int offset)
        {
            return id * Solver.ZibraLiquid.STATISTICS_PER_MANIPULATOR + offset;
        }

#if ZIBRA_LIQUID_PAID_VERSION
        /// <summary>
        /// Update manipulator statistics
        /// </summary>
        public void UpdateStatistics(Int32[] data, List<Manipulator> curManipulators,
                                     DataStructures.ZibraLiquidSolverParameters solverParameters,
                                     List<SDFObjects.SDFCollider> sdfObjects)
        {
            int id = 0;
            foreach (var manipulator in manipulators)
            {
                if (manipulator == null)
                    continue;

                switch (manipulator.ManipType)
                {
                default:
                    break;
                case Manipulator.ManipulatorType.Emitter:
                    ZibraLiquidEmitter emitter = manipulator as ZibraLiquidEmitter;
                    emitter.createdParticlesPerFrame = data[GetStatIndex(id, 0)];
                    emitter.createdParticlesTotal += emitter.createdParticlesPerFrame;
                    break;
                case Manipulator.ManipulatorType.Void:
                    ZibraLiquidVoid zibravoid = manipulator as ZibraLiquidVoid;
                    zibravoid.deletedParticleCountPerFrame = data[GetStatIndex(id, 0)];
                    zibravoid.deletedParticleCountTotal += zibravoid.deletedParticleCountPerFrame;
                    break;
                case Manipulator.ManipulatorType.Detector:
                    ZibraLiquidDetector zibradetector = manipulator as ZibraLiquidDetector;
                    zibradetector.particlesInside = data[GetStatIndex(id, 0)];
                    break;
                case Manipulator.ManipulatorType.NeuralCollider:
                case Manipulator.ManipulatorType.AnalyticCollider:
                    SDFObjects.SDFCollider collider = manipulator as SDFObjects.SDFCollider;
                    Vector3 Force =
                        Mathf.Exp(4.0f * solverParameters.ForceInteractionStrength) *
                        new Vector3(INT2Float(data[GetStatIndex(id, 0)]), INT2Float(data[GetStatIndex(id, 1)]),
                                    INT2Float(data[GetStatIndex(id, 2)]));
                    Vector3 Torque =
                        Mathf.Exp(4.0f * solverParameters.ForceInteractionStrength) *
                        new Vector3(INT2Float(data[GetStatIndex(id, 3)]), INT2Float(data[GetStatIndex(id, 4)]),
                                    INT2Float(data[GetStatIndex(id, 5)]));
                    collider.ApplyForceTorque(Force, Torque);
                    break;
                }
#if UNITY_EDITOR
                manipulator.NotifyChange();
#endif

                id++;
            }
        }
#endif

        /// <summary>
        /// Update constant object data and generate and sort the current manipulator list
        /// Should be executed once
        /// </summary>
        public void UpdateConst(List<Manipulator> curManipulators, List<SDFObjects.SDFCollider> sdfObjects)
        {
            manipulators = new List<Manipulator>(curManipulators);

            // add all colliders to the manipulator list
            foreach (var collider in sdfObjects)
            {
                if (collider == null)
                    continue;

#if ZIBRA_LIQUID_PAID_VERSION
                if (collider is SDFObjects.ZibraNeuralCollider)
                {
                    collider.ManipType = Manipulator.ManipulatorType.NeuralCollider;
                }
                else
#endif
                {
                    collider.ManipType = Manipulator.ManipulatorType.AnalyticCollider;
                }
                manipulators.Add(collider);
            }

            // first sort the manipulators
            manipulators.Sort(new ManipulatorCompare());

            // compute prefix sum
            for (int i = 0; i < (int)Manipulator.ManipulatorType.TypeNum; i++)
            {
                int id = 0;
                foreach (var manipulator in manipulators)
                {
                    if ((int)manipulator.ManipType >= i)
                    {
                        TypeIndex[i] = id;
                        break;
                    }
                    id++;
                }

                if (id == manipulators.Count)
                {
                    TypeIndex[i] = manipulators.Count;
                }
            }

            // set last as the total number of manipulators
            TypeIndex[(int)Manipulator.ManipulatorType.TypeNum] = manipulators.Count;

            indices.EmitterIndex = TypeIndex[(int)Manipulator.ManipulatorType.Emitter];
            indices.EmitterIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.Emitter + 1];
            indices.VoidIndex = TypeIndex[(int)Manipulator.ManipulatorType.Void];
            indices.VoidIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.Void + 1];
            indices.ForceFieldIndex = TypeIndex[(int)Manipulator.ManipulatorType.ForceField];
            indices.ForceFieldIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.ForceField + 1];
            indices.AnalyticColliderIndex = TypeIndex[(int)Manipulator.ManipulatorType.AnalyticCollider];
            indices.AnalyticColliderIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.AnalyticCollider + 1];
            indices.NeuralColliderIndex = TypeIndex[(int)Manipulator.ManipulatorType.NeuralCollider];
            indices.NeuralColliderIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.NeuralCollider + 1];
            indices.DetectorIndex = TypeIndex[(int)Manipulator.ManipulatorType.Detector];
            indices.DetectorIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.Detector + 1];
            indices.PortalIndex = TypeIndex[(int)Manipulator.ManipulatorType.Portal];
            indices.PortalIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.Portal + 1];

            if (ConstDataID.Count != 0)
            {
                Array.Resize<float>(ref ConstAdditionalData, 0);
                ConstDataID.Clear();
            }

            foreach (var manipulator in manipulators)
            {
                if (manipulator == null)
                    continue;
                manipulator.InitializeConstData();
                int ID = ConstAdditionalData.Length;
                ConstDataID.Add(ID);
                Array.Resize<float>(ref ConstAdditionalData, ID + manipulator.ConstAdditionalData.Length);
                Array.Copy(manipulator.ConstAdditionalData, 0, ConstAdditionalData, ID,
                           manipulator.ConstAdditionalData.Length);
            }
        }
    }
}
