using com.zibra.liquid.DataStructures;
using com.zibra.liquid.Manipulators;
using com.zibra.liquid.SDFObjects;
using com.zibra.liquid.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

#if UNITY_PIPELINE_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif // UNITY_PIPELINE_HDRP

#if !ZIBRA_LIQUID_PAID_VERSION && !ZIBRA_LIQUID_FREE_VERSION
#error Missing plugin version definition
#endif

namespace com.zibra.liquid.Solver
{
    /// <summary>
    /// Main ZibraFluid solver component
    /// </summary>
    [AddComponentMenu("Zibra/Zibra Liquid")]
    [RequireComponent(typeof(ZibraLiquidMaterialParameters))]
    [RequireComponent(typeof(ZibraLiquidSolverParameters))]
    [RequireComponent(typeof(ZibraManipulatorManager))]
    [ExecuteInEditMode] // Careful! This makes script execute in edit mode.
    // Use "EditorApplication.isPlaying" for play mode only check.
    // Encase this check and "using UnityEditor" in "#if UNITY_EDITOR" preprocessor directive to prevent build errors
    public class ZibraLiquid : MonoBehaviour
    {
        /// <summary>
        /// A list of all instances of the ZibraFluid solver
        /// </summary>
        public static List<ZibraLiquid> AllFluids = new List<ZibraLiquid>();

        public static int ms_NextInstanceId = 0;
        public const int MPM_THREADS = 256;
        public const int RADIX_THREADS = 256;
        public const int HISTO_WIDTH = 16;
        public const int SCANBLOCKS = 1;
        public const int STATISTICS_PER_MANIPULATOR = 8;
        // Unique ID that always present in each baked state asset
        public const int BAKED_LIQUID_HEADER_VALUE = 0x071B9AA1;

#if UNITY_PIPELINE_URP
        static int upscaleColorTextureID = Shader.PropertyToID("Zibra_DownscaledLiquidColor");
        static int upscaleDepthTextureID = Shader.PropertyToID("Zibra_DownscaledLiquidDepth");
#endif

#if UNITY_EDITOR
        // Used to update editors
        public event Action onChanged;
        public void NotifyChange()
        {
            if (onChanged != null)
            {
                onChanged.Invoke();
            }
        }
#endif

#region PARTICLES

        [StructLayout(LayoutKind.Sequential)]
        private class RegisterParticlesBuffersBridgeParams
        {
            public IntPtr PositionMass;
            public IntPtr AffineVelocity0;
            public IntPtr AffineVelocity1;
            public IntPtr PositionRadius;
            public IntPtr ParticleNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class InitializeGPUReadbackParams
        {
            public UInt32 readbackBufferSize;
            public Int32 maxFramesInFlight;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class RegisterManipulatorsBridgeParams
        {
            public Int32 ManipulatorNum;
            public IntPtr ManipulatorBufferDynamic;
            public IntPtr ManipulatorBufferConst;
            public IntPtr ManipulatorBufferStatistics;
            public IntPtr ManipulatorParams;
            public Int32 ConstDataSize;
            public IntPtr ConstManipulatorData;
            public IntPtr ManipIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class RegisterSolverBuffersBridgeParams
        {
            public IntPtr SimParams;
            public IntPtr PositionMassCopy;
            public IntPtr GridData;
            public IntPtr IndexGrid;
            public IntPtr GridBlur0;
            public IntPtr GridBlur1;
            public IntPtr GridNormal;
            public IntPtr GridSDF;
            public IntPtr GridNodePositions;
            public IntPtr NodeParticlePairs;
            public IntPtr SortTempBuf;
            public IntPtr RadixGroupData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class RegisterRenderResourcesBridgeParams
        {

            public IntPtr Depth;
            public IntPtr Color0;
            public IntPtr Color1;
            public IntPtr AtomicGrid;
            public IntPtr JFA0;
            public IntPtr JFA1;
#if ZIBRA_LIQUID_DEBUG
            public IntPtr SDFRender;
#endif
        }

        [StructLayout(LayoutKind.Sequential)]
        private class CameraParams
        {
            public Matrix4x4 View;
            public Matrix4x4 Projection;
            public Matrix4x4 ProjectionInverse;
            public Matrix4x4 ViewProjection;
            public Matrix4x4 ViewProjectionInverse;
            public Vector3 WorldSpaceCameraPos;
            public Int32 CameraID;
            public Vector2 CameraResolution;
            Single CameraParamsPadding1;
            Single CameraParamsPadding2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class RenderParams
        {
            public Single BlurRadius;
            public Single Diameter;
            Single RenderParamsPadding1;
            Single RenderParamsPadding2;
        }

        public class CameraResources
        {
            public RenderTexture background;
            public bool isDirty = true;
        }

        [NonSerialized]
        public RenderTexture color0;
        [NonSerialized]
        public RenderTexture color1;
#if ZIBRA_LIQUID_DEBUG
        [NonSerialized]
        public RenderTexture SDFRender;
#endif
        [NonSerialized]
        public RenderTexture depth;
        [NonSerialized]
        public ComputeBuffer atomicGrid;
        [NonSerialized]
        public ComputeBuffer JFAGrid0;
        [NonSerialized]
        public ComputeBuffer JFAGrid1;

        [NonSerialized]
        private Vector2Int CurrentTextureResolution;

        // List of all cameras we have added a command buffer to
        private readonly Dictionary<Camera, CommandBuffer> cameraCBs = new Dictionary<Camera, CommandBuffer>();
        // Each camera needs its own resources
        [NonSerialized]
        public List<Camera> cameras = new List<Camera>();
        [NonSerialized]
        public Dictionary<Camera, CameraResources> cameraResources = new Dictionary<Camera, CameraResources>();
        [NonSerialized]
        public Dictionary<Camera, IntPtr> camNativeParams = new Dictionary<Camera, IntPtr>();
        [NonSerialized]
        public Dictionary<Camera, Vector2Int> camResolutions = new Dictionary<Camera, Vector2Int>();

#if ZIBRA_LIQUID_PAID_VERSION
        [Range(1024, 10000000)]
#else
        // Increasing this limit won't allow you to spawn more particles
        [Range(1024, 2097152)]
#endif
        public int MaxNumParticles = 262144;
        public ComputeBuffer PositionMass { get; private set; }
        public ComputeBuffer PositionRadius { get; private set; } // in world space
        public ComputeBuffer Velocity { get; private set; }
        public ComputeBuffer[] Affine { get; private set; }
        public ComputeBuffer ParticleNumber { get; private set; }
        [NonSerialized]

        public bool isEnabled = true;
        [NonSerialized]
        public float particleDiameter = 0.1f;
        [NonSerialized]
        public float particleMass = 1.0f;
        public Bounds bounds;
        // If set to false resolution is always 100%
        // If set to true DownscaleFactor is applied to liquid rendering
        public bool EnableDownscale = false;
        // Scale width/height of liquid render target
        // Pixel count is decreased by factor of DownscaleFactor * DownscaleFactor
        // So DownscaleFactor of 0.7 result in about 50% less pixels in render target
        // Doesn't have any effect unless EnableDownscale is set to true
        [Range(0.2f, 0.99f)]
        public float DownscaleFactor = 0.5f;

        private bool usingCustomReflectionProbe;

        private CameraParams cameraRenderParams;
        private RenderParams renderParams;

#endregion

#region SOLVER

#if ZIBRA_LIQUID_PAID_VERSION
        /// <summary>
        /// Types of initial conditions
        /// </summary>
        public enum InitialStateType
        {
            NoParticles,
            BakedLiquidState
        }

        [Serializable]
        public class BakedInitialState
        {
            [SerializeField]
            public int ParticleCount;

            [SerializeField]
            public Vector4[] Positions;

            [SerializeField]
            public Vector2Int[] AffineVelocity;
        }

        public InitialStateType InitialState = InitialStateType.NoParticles;

        [Tooltip("Baked state saved with Baking Utility. Will reset to None if incompatible file is detected.")]
        public TextAsset BakedInitialStateAsset;
#endif

        /// <summary>
        /// Native solver instance ID number
        /// </summary>
        [NonSerialized]
        public int CurrentInstanceID;

        [StructLayout(LayoutKind.Sequential)]
        private class SimulationParams
        {
            public Vector3 GridSize;
            public Int32 ParticleCount;

            public Vector3 ContainerScale;
            public Int32 NodeCount;

            public Vector3 ContainerPos;
            public Single TimeStep;

            public Vector3 Gravity;
            public Int32 SimulationFrame;

            public Vector3 BlurDirection;
            public Single AffineAmmount;

            public Vector3 ParticleTranslation;
            public Single VelocityLimit;

            public Single LiquidStiffness;
            public Single RestDensity;
            public Single SurfaceTension;
            public Single AffineDivergenceDecay;

            public Single MinimumVelocity;
            public Single BlurNormalizationConstant;
            public Int32 MaxParticleCount;
            public Int32 VisualizeSDF;
        }

        private const int BlockDim = 8;
        public ComputeBuffer GridData { get; private set; }
        public ComputeBuffer IndexGrid { get; private set; }
        public ComputeBuffer GridBlur0 { get; private set; }
        public ComputeBuffer GridNormal { get; private set; }
        public ComputeBuffer GridSDF { get; private set; }
        public ComputeBuffer SurfaceGridType { get; private set; }

        /// <summary>
        /// Current timestep
        /// </summary>
        public float timestep = 0.0f;

        /// <summary>
        /// Simulation time passed (in simulation time units)
        /// </summary>
        [NonSerialized]
        public float simulationInternalTime;

        /// <summary>
        /// Number of simulation iterations done so far
        /// </summary>
        [NonSerialized]
        public int simulationInternalFrame;

        private int numNodes;
        private SimulationParams fluidParameters;
        private ComputeBuffer positionMassCopy;
        private ComputeBuffer gridBlur1;
        private ComputeBuffer gridNodePositions;
        private ComputeBuffer nodeParticlePairs;
        private ComputeBuffer SortTempBuffer;
        private ComputeBuffer RadixGroupData;

        private IntPtr updateColliderParamsNative;
        private CommandBuffer solverCommandBuffer;

#endregion

        public bool IsSimulatingInBackground { get; set; }

        /// <summary>
        /// The grid size of the simulation
        /// </summary>
        public Vector3Int GridSize { get; private set; }

        [NonSerialized]
        [Obsolete(
            "reflectionProbe is deprecated. Use reflectionProbeSRP or reflectionProbeHDRP instead depending on your Rendering Pipeline (URP uses reflectionProbeSRP).",
            true)]
        public ReflectionProbe reflectionProbe;

#if UNITY_PIPELINE_HDRP
        [FormerlySerializedAs("reflectionProbe")]
        [Tooltip("Use a custom reflection probe")]
        public HDProbe reflectionProbeHDRP;
        [Tooltip("Use a custom light")]
        public Light customLightHDRP;
#else
        [FormerlySerializedAs("reflectionProbe")]
#endif // UNITY_PIPELINE_HDRP
        [Tooltip("Use a custom reflection probe")]
        public ReflectionProbe reflectionProbeSRP;

        [Tooltip("The maximum allowed simulation timestep")]
        [Range(1e-1f, 1.0f)]
        public float timeStepMax = 1.00f;

        [Tooltip("Fallback max frame latency. Used when it isn't possible to retrieve Unity's max frame latency.")]
        [Range(2, 16)]
        public UInt32 maxFramesInFlight = 3;

        [Tooltip("The speed of the simulation, how many simulation time units per second")]
        [Range(1.0f, 100.0f)]
        public float simTimePerSec = 40.0f;

        public int activeParticleNumber { get; private set; } = 262144;

        [Tooltip("The number of solver iterations per frame, in most cases one iteration is sufficient")]
        [Range(1, 10)]
        public int iterationsPerFrame = 1;

        protected float cellSize;

        [Tooltip("Sets the resolution of the largest sid of the grids container equal to this value")]
        [Min(16)]
        public int gridResolution = 128;

        [Range(1e-2f, 16.0f)]
        public float emitterDensity = 1.0f;

        public bool runSimulation = true;

#if ZIBRA_LIQUID_DEBUG
        public bool visualizeSceneSDF = false;
#endif

        /// <summary>
        /// Main parameters of the simulation
        /// </summary>
        public ZibraLiquidSolverParameters solverParameters;

        /// <summary>
        /// Main rendering parameters
        /// </summary>
        public ZibraLiquidMaterialParameters materialParameters;

        /// <summary>
        /// Solver container size
        /// </summary>
        public Vector3 containerSize = new Vector3(10, 10, 10);

        /// <summary>
        /// Solver container position
        /// </summary>
        public Vector3 containerPos;

        /// <summary>
        /// Initial velocity of the fluid
        /// </summary>
        public Vector3 fluidInitialVelocity;

        /// <summary>
        /// Manager for all objects interacting in some way with the simulation
        /// </summary>
        [HideInInspector]
        [SerializeField]
        public ZibraManipulatorManager manipulatorManager;
        private IntPtr NativeManipData;
        private IntPtr NativeFluidData;

        /// <summary>
        /// Compute buffer with dynamic manipulator data
        /// </summary>
        public ComputeBuffer DynamicManipulatorData { get; private set; }

        /// <summary>
        /// Compute buffer with constant manipulator data
        /// </summary>
        public ComputeBuffer ConstManipulatorData { get; private set; }

        /// <summary>
        /// Compute buffer with statistics about the manipulators
        /// </summary>
        public ComputeBuffer ManipulatorStatistics { get; private set; }

        /// <summary>
        /// List of used SDF colliders
        /// </summary>
        [SerializeField]
        private List<SDFCollider> sdfColliders = new List<SDFCollider>();

        /// <summary>
        /// List of used manipulators
        /// </summary>
        [SerializeField]
        private List<Manipulator> manipulators = new List<Manipulator>();

        public int avgFrameRate;
        public float deltaTime;
        public float smoothDeltaTime;

        public bool forceTextureUpdate = false;

        /// <summary>
        /// Is solver initialized
        /// </summary>
        //[NonSerialized]
        public bool initialized { get; private set; } = false;

        /// <summary>
        /// Is solver using fixed unity time steps
        /// </summary>
        public bool useFixedTimestep = false;

        /// <summary>
        /// Instance of fluid material set in ZibraLiquidMaterialParameters
        /// </summary>
        private Material FluidMaterial;

        /// <summary>
        /// Original fluid material set in ZibraLiquidMaterialParameters
        /// </summary>
        private Material SharedFluidMaterial;

        /// <summary>
        /// Instance of upscale material set in ZibraLiquidMaterialParameters
        /// May be null if EnableDownscale is false, since it's unused in that case
        /// </summary>
        private Material UpscaleMaterial;

        /// <summary>
        /// Original upscale material set in ZibraLiquidMaterialParameters
        /// May be null if EnableDownscale is false, since it's unused in that case
        /// </summary>
        private Material SharedUpscaleMaterial;

#if UNITY_EDITOR
        private bool ForceRepaint = false;
#endif

#if UNITY_PIPELINE_HDRP
        private LiquidHDRPRenderComponent hdrpRenderer;
#endif // UNITY_PIPELINE_HDRP

        /// <summary>
        /// Activate the solver
        /// </summary>
        public void Run()
        {
            runSimulation = true;
        }

        /// <summary>
        /// Stop the solver
        /// </summary>
        public void Stop()
        {
            runSimulation = false;
        }

        void SetupScriptableRenderComponents()
        {
#if UNITY_EDITOR
#if UNITY_PIPELINE_HDRP
            if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
            {
                hdrpRenderer = gameObject.GetComponent<LiquidHDRPRenderComponent>();
                if (hdrpRenderer == null)
                {
                    hdrpRenderer = gameObject.AddComponent<LiquidHDRPRenderComponent>();
                    hdrpRenderer.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
                    hdrpRenderer.AddPassOfType(typeof(LiquidHDRPRenderComponent.FluidHDRPRender));
                    LiquidHDRPRenderComponent.FluidHDRPRender renderer =
                        hdrpRenderer.customPasses[0] as LiquidHDRPRenderComponent.FluidHDRPRender;
                    renderer.name = "ZibraLiquidRenderer";
                    renderer.liquid = this;
                }
            }
#endif // UNITY_PIPELINE_HDRP
#endif // UNITY_EDITOR
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, containerSize);
        }
        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }

        void Start()
        {
            materialParameters = gameObject.GetComponent<ZibraLiquidMaterialParameters>();
            solverParameters = gameObject.GetComponent<ZibraLiquidSolverParameters>();
            manipulatorManager = gameObject.GetComponent<ZibraManipulatorManager>();

            // Instantiate fluid material to not leak parameters between liquid instances
            SharedFluidMaterial = materialParameters.FluidMaterial;
            FluidMaterial = Material.Instantiate(SharedFluidMaterial);
            if (EnableDownscale)
            {
                SharedUpscaleMaterial = materialParameters.UpscaleMaterial;
                UpscaleMaterial = Material.Instantiate(SharedUpscaleMaterial);
            }
        }

        protected void OnEnable()
        {
            SetupScriptableRenderComponents();

#if ZIBRA_LIQUID_PAID_VERSION
            if (!ZibraLiquidBridge.IsPaidVersion())
            {
                Debug.LogError(
                    "Free version of native plugin used with paid version of C# plugin. If you just replaced your Zibra Liquids version you need to restart Unity Editor.");
            }
#else
            if (ZibraLiquidBridge.IsPaidVersion())
            {
                Debug.LogError(
                    "Paid version of native plugin used with free version of C# plugin. If you just replaced your Zibra Liquids version you need to restart Unity Editor.");
            }
#endif

            AllFluids?.Add(this);

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                return;
            }
#endif

            Init();
        }

        public void UpdateGridSize()
        {
            cellSize = Math.Max(containerSize.x, Math.Max(containerSize.y, containerSize.z)) / gridResolution;

            GridSize = Vector3Int.CeilToInt(containerSize / cellSize);
        }

        private void InitializeParticles()
        {
            UpdateGridSize();

            fluidParameters = new SimulationParams();

            NativeFluidData = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SimulationParams)));

            isEnabled = true;
            var numParticlesRounded =
                (int)Math.Ceiling((double)MaxNumParticles / MPM_THREADS) * MPM_THREADS; // round to workgroup size

            PositionMass = new ComputeBuffer(MaxNumParticles, 4 * sizeof(float));
            PositionRadius = new ComputeBuffer(MaxNumParticles, 4 * sizeof(float));
            Affine = new ComputeBuffer[2];
            Affine[0] = new ComputeBuffer(4 * numParticlesRounded, 2 * sizeof(int));
            Affine[1] = new ComputeBuffer(4 * numParticlesRounded, 2 * sizeof(int));
            ParticleNumber = new ComputeBuffer(128, sizeof(int));

#if ZIBRA_LIQUID_DEBUG
            PositionMass.name = "PositionMass";
            PositionRadius.name = "PositionRadius";
            Affine[0].name = "Affine0";
            Affine[1].name = "Affine1";
            ParticleNumber.name = "ParticleNumber";
#endif

#if ZIBRA_LIQUID_PAID_VERSION
            // We mush apply state before we send buffers to native plugin
            // SetData seems to recreate buffers at least on Metal
            ApplyInitialState();
#endif

            int[] Pnums = new int[128];
            for (int i = 0; i < 128; i++)
            {
                Pnums[i] = 0;
            }
            ParticleNumber.SetData(Pnums);

            if (manipulatorManager != null)
            {
                manipulatorManager.UpdateConst(manipulators, sdfColliders);
                manipulatorManager.UpdateDynamic(containerPos, containerSize);

                int ManipSize = Marshal.SizeOf(typeof(ZibraManipulatorManager.ManipulatorParam));

                // Need to create at least some buffer to bind to shaders
                NativeManipData = Marshal.AllocHGlobal(manipulatorManager.Elements * ManipSize);
                DynamicManipulatorData = new ComputeBuffer(Math.Max(manipulatorManager.Elements, 1), ManipSize);
                int ConstDataLength = manipulatorManager.ConstAdditionalData.Length;
                ConstManipulatorData = new ComputeBuffer(Math.Max(ConstDataLength, 1), sizeof(int));
                ManipulatorStatistics = new ComputeBuffer(
                    Math.Max(STATISTICS_PER_MANIPULATOR * manipulatorManager.Elements, 1), sizeof(int));
                int constDataSize = ConstDataLength * sizeof(int);

#if ZIBRA_LIQUID_DEBUG
                DynamicManipulatorData.name = "DynamicManipulatorData";
                ConstManipulatorData.name = "ConstManipulatorData";
                ManipulatorStatistics.name = "ManipulatorStatistics";
#endif
                var gcparamBuffer0 =
                    GCHandle.Alloc(manipulatorManager.ManipulatorParams.ToArray(), GCHandleType.Pinned);
                IntPtr gcparamBuffer1Native = IntPtr.Zero;
                GCHandle gcparamBuffer1 = new GCHandle();
                if (ConstDataLength > 0)
                {
                    gcparamBuffer1 = GCHandle.Alloc(manipulatorManager.ConstAdditionalData, GCHandleType.Pinned);
                    gcparamBuffer1Native = gcparamBuffer1.AddrOfPinnedObject();
                }
                var gcparamBuffer2 = GCHandle.Alloc(manipulatorManager.indices, GCHandleType.Pinned);

                var registerManipulatorsBridgeParams = new RegisterManipulatorsBridgeParams();
                registerManipulatorsBridgeParams.ManipulatorNum = manipulatorManager.Elements;
                registerManipulatorsBridgeParams.ManipulatorBufferDynamic = DynamicManipulatorData.GetNativeBufferPtr();
                registerManipulatorsBridgeParams.ManipulatorBufferConst = ConstManipulatorData.GetNativeBufferPtr();
                registerManipulatorsBridgeParams.ManipulatorBufferStatistics =
                    ManipulatorStatistics.GetNativeBufferPtr();
                registerManipulatorsBridgeParams.ManipulatorParams = gcparamBuffer0.AddrOfPinnedObject();
                registerManipulatorsBridgeParams.ConstDataSize = constDataSize;
                registerManipulatorsBridgeParams.ConstManipulatorData = gcparamBuffer1Native;
                registerManipulatorsBridgeParams.ManipIndices = gcparamBuffer2.AddrOfPinnedObject();

                IntPtr nativeRegisterManipulatorsBridgeParams =
                    Marshal.AllocHGlobal(Marshal.SizeOf(registerManipulatorsBridgeParams));
                Marshal.StructureToPtr(registerManipulatorsBridgeParams, nativeRegisterManipulatorsBridgeParams, true);
                solverCommandBuffer.Clear();
                solverCommandBuffer.IssuePluginEventAndData(
                    ZibraLiquidBridge.GetRenderEventWithDataFunc(),
                    ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.RegisterManipulators,
                                                         CurrentInstanceID),
                    nativeRegisterManipulatorsBridgeParams);
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);

                gcparamBuffer0.Free();
                if (ConstDataLength > 0)
                {
                    gcparamBuffer1.Free();
                }
                gcparamBuffer2.Free();

                if (manipulatorManager.Elements > 0)
                {
                    solverCommandBuffer.Clear();
                    solverCommandBuffer.IssuePluginEvent(
                        ZibraLiquidBridge.GetRenderEventFunc(),
                        ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.InitializeManipulators,
                                                             CurrentInstanceID));
                    Graphics.ExecuteCommandBuffer(solverCommandBuffer);
                    solverCommandBuffer.Clear();
                }
            }
            else
            {
                Debug.LogWarning("No manipulator manipulatorManager has been set");
            }

            cameraRenderParams = new CameraParams();
            renderParams = new RenderParams();

            var registerParticlesBuffersParams = new RegisterParticlesBuffersBridgeParams();
            registerParticlesBuffersParams.PositionMass = PositionMass.GetNativeBufferPtr();
            registerParticlesBuffersParams.AffineVelocity0 = Affine[0].GetNativeBufferPtr();
            registerParticlesBuffersParams.AffineVelocity1 = Affine[1].GetNativeBufferPtr();
            registerParticlesBuffersParams.PositionRadius = PositionRadius.GetNativeBufferPtr();
            registerParticlesBuffersParams.ParticleNumber = ParticleNumber.GetNativeBufferPtr();

            IntPtr nativeRegisterParticlesBuffersParams =
                Marshal.AllocHGlobal(Marshal.SizeOf(registerParticlesBuffersParams));
            Marshal.StructureToPtr(registerParticlesBuffersParams, nativeRegisterParticlesBuffersParams, true);
            solverCommandBuffer.Clear();
            solverCommandBuffer.IssuePluginEventAndData(
                ZibraLiquidBridge.GetRenderEventWithDataFunc(),
                ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.RegisterParticlesBuffers,
                                                     CurrentInstanceID),
                nativeRegisterParticlesBuffersParams);
            Graphics.ExecuteCommandBuffer(solverCommandBuffer);
        }

        public int GetParticleCountRounded()
        {
            return (int)Math.Ceiling((double)MaxNumParticles / MPM_THREADS) * MPM_THREADS; // round to workgroup size;
        }

        public ulong GetParticleCountFootprint()
        {
            ulong result = 0;
            int particleCountRounded = GetParticleCountRounded();
            result += (ulong)(MaxNumParticles * 4 * sizeof(float));            // PositionMass
            result += (ulong)(MaxNumParticles * 4 * sizeof(float));            // PositionRadius
            result += (ulong)(2 * 4 * particleCountRounded * 2 * sizeof(int)); // Affine
            result += (ulong)(particleCountRounded * 4 * sizeof(float));       // positionMassCopy
            result += (ulong)(particleCountRounded * 2 * sizeof(int));         // nodeParticlePairs

            int RadixWorkGroups = (int)Math.Ceiling((float)MaxNumParticles / (float)(RADIX_THREADS * SCANBLOCKS));
            result += (ulong)(particleCountRounded * 2 * sizeof(int));                 // SortTempBuffer
            result += (ulong)((RadixWorkGroups + 1) * 3 * HISTO_WIDTH * sizeof(uint)); // RadixGroupData

            return result;
        }

        public ulong GetCollidersFootprint()
        {
            ulong result = 0;

            foreach (var collider in sdfColliders)
            {
                result += collider.GetMemoryFootrpint();
            }

            int ManipSize = Marshal.SizeOf(typeof(ZibraManipulatorManager.ManipulatorParam));

            result += (ulong)(manipulators.Count * ManipSize);   // DynamicManipData
            result += (ulong)(manipulators.Count * sizeof(int)); // ConstManipData

            return result;
        }

        public ulong GetGridFootprint()
        {
            ulong result = 0;

            GridSize = Vector3Int.CeilToInt(containerSize / cellSize);
            numNodes = GridSize[0] * GridSize[1] * GridSize[2];

            result += (ulong)(numNodes * 4 * sizeof(int));   // GridData
            result += (ulong)(numNodes * 4 * sizeof(float)); // GridNormal
            result += (ulong)(numNodes * 4 * sizeof(int));   // GridBlur0
            result += (ulong)(numNodes * 4 * sizeof(int));   // gridBlur1
            result += (ulong)(numNodes * sizeof(float));     // GridSDF
            result += (ulong)(numNodes * 4 * sizeof(float)); // gridNodePositions
            result += (ulong)(numNodes * 2 * sizeof(int));   // IndexGrid

            return result;
        }

        private void InitializeSolver()
        {
            simulationInternalTime = 0.0f;
            simulationInternalFrame = 0;
            numNodes = GridSize[0] * GridSize[1] * GridSize[2];
            GridData = new ComputeBuffer(numNodes, 4 * sizeof(int));
            GridNormal = new ComputeBuffer(numNodes, 4 * sizeof(float));
            GridBlur0 = new ComputeBuffer(numNodes, 4 * sizeof(int));
            gridBlur1 = new ComputeBuffer(numNodes, 4 * sizeof(int));
            GridSDF = new ComputeBuffer(numNodes, sizeof(float));
            gridNodePositions = new ComputeBuffer(numNodes, 4 * sizeof(float));

            IndexGrid = new ComputeBuffer(numNodes, 2 * sizeof(int));

            int NumParticlesRounded = GetParticleCountRounded();

            positionMassCopy = new ComputeBuffer(NumParticlesRounded, 4 * sizeof(float));
            nodeParticlePairs = new ComputeBuffer(NumParticlesRounded, 2 * sizeof(int));

            int RadixWorkGroups = (int)Math.Ceiling((float)MaxNumParticles / (float)(RADIX_THREADS * SCANBLOCKS));
            SortTempBuffer = new ComputeBuffer(NumParticlesRounded, 2 * sizeof(int));
            RadixGroupData = new ComputeBuffer((RadixWorkGroups + 1) * 3 * HISTO_WIDTH, sizeof(uint));

#if ZIBRA_LIQUID_DEBUG
            GridData.name = "GridData";
            GridNormal.name = "GridNormal";
            GridBlur0.name = "GridBlur0";
            gridBlur1.name = "gridBlur1";
            GridSDF.name = "GridSDF";
            gridNodePositions.name = "gridNodePositions";
            IndexGrid.name = "IndexGrid";
            positionMassCopy.name = "positionMassCopy";
            nodeParticlePairs.name = "nodeParticlePairs";
            SortTempBuffer.name = "SortTempBuffer";
            RadixGroupData.name = "RadixGroupData";
#endif

            SetFluidParameters();

            var gcparamBuffer = GCHandle.Alloc(fluidParameters, GCHandleType.Pinned);

            var registerSolverBuffersBridgeParams = new RegisterSolverBuffersBridgeParams();
            registerSolverBuffersBridgeParams.SimParams = gcparamBuffer.AddrOfPinnedObject();
            registerSolverBuffersBridgeParams.PositionMassCopy = positionMassCopy.GetNativeBufferPtr();
            registerSolverBuffersBridgeParams.GridData = GridData.GetNativeBufferPtr();
            registerSolverBuffersBridgeParams.IndexGrid = IndexGrid.GetNativeBufferPtr();
            registerSolverBuffersBridgeParams.GridBlur0 = GridBlur0.GetNativeBufferPtr();
            registerSolverBuffersBridgeParams.GridBlur1 = gridBlur1.GetNativeBufferPtr();
            registerSolverBuffersBridgeParams.GridNormal = GridNormal.GetNativeBufferPtr();
            registerSolverBuffersBridgeParams.GridSDF = GridSDF.GetNativeBufferPtr();
            registerSolverBuffersBridgeParams.GridNodePositions = gridNodePositions.GetNativeBufferPtr();
            registerSolverBuffersBridgeParams.NodeParticlePairs = nodeParticlePairs.GetNativeBufferPtr();
            registerSolverBuffersBridgeParams.SortTempBuf = SortTempBuffer.GetNativeBufferPtr();
            registerSolverBuffersBridgeParams.RadixGroupData = RadixGroupData.GetNativeBufferPtr();

            IntPtr nativeRegisterSolverBuffersBridgeParams =
                Marshal.AllocHGlobal(Marshal.SizeOf(registerSolverBuffersBridgeParams));
            Marshal.StructureToPtr(registerSolverBuffersBridgeParams, nativeRegisterSolverBuffersBridgeParams, true);
            solverCommandBuffer.Clear();
            solverCommandBuffer.IssuePluginEventAndData(
                ZibraLiquidBridge.GetRenderEventWithDataFunc(),
                ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.RegisterSolverBuffers,
                                                     CurrentInstanceID),
                nativeRegisterSolverBuffersBridgeParams);
            Graphics.ExecuteCommandBuffer(solverCommandBuffer);

            gcparamBuffer.Free();

            Graphics.ExecuteCommandBuffer(solverCommandBuffer);
            solverCommandBuffer.Clear();
        }

        /// <summary>
        /// Initializes a new instance of ZibraFluid
        /// </summary>
        public void Init()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            try
            {
#if UNITY_PIPELINE_HDRP
                if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
                {
                    bool missingRequiredParameter = false;

                    if (customLightHDRP == null)
                    {
                        Debug.LogError("No Custom Light set in Zibra Liquid.");
                        missingRequiredParameter = true;
                    }

                    if (reflectionProbeHDRP == null)
                    {
                        Debug.LogError("No reflection probe added to Zibra Liquid.");
                        missingRequiredParameter = true;
                    }

                    if (missingRequiredParameter)
                    {
                        throw new Exception("Liquid creation failed due to missing parameter.");
                    }
                }
#endif

#if ZIBRA_LIQUID_PAID_VERSION
                if (InitialState == ZibraLiquid.InitialStateType.NoParticles || BakedInitialStateAsset == null)
#endif
                {
                    bool haveEmitter = false;
                    foreach (var manipulator in manipulators)
                    {
                        if (manipulator.ManipType == Manipulator.ManipulatorType.Emitter)
                        {
                            haveEmitter = true;
                            break;
                        }
                    }
                    if (!haveEmitter)
                    {
#if ZIBRA_LIQUID_PAID_VERSION
                        throw new Exception("Liquid creation failed. Liquid have neither initial state nor emitters.");
#else
                        throw new Exception("Liquid creation failed. Liquid have don't have any emitters.");
#endif
                    }
                }

                Camera.onPreRender += RenderCallBack;

                solverCommandBuffer = new CommandBuffer { name = "ZibraLiquid.Solver" };

                CurrentInstanceID = ms_NextInstanceId++;

                solverCommandBuffer.IssuePluginEvent(
                    ZibraLiquidBridge.GetRenderEventFunc(),
                    ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.CreateFluidInstance,
                                                         CurrentInstanceID));
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);

                InitializeParticles();

                var initializeGPUReadbackParamsBridgeParams = new InitializeGPUReadbackParams();
#if ZIBRA_LIQUID_FREE_VERSION
                UInt32 manipSize = 0;
#else
                UInt32 manipSize = (UInt32)manipulatorManager.Elements * STATISTICS_PER_MANIPULATOR * sizeof(Int32);
#endif
                initializeGPUReadbackParamsBridgeParams.readbackBufferSize = sizeof(Int32) + manipSize;
                switch (SystemInfo.graphicsDeviceType)
                {
                case GraphicsDeviceType.Direct3D11:
                case GraphicsDeviceType.Direct3D12:
                case GraphicsDeviceType.Switch:
                case GraphicsDeviceType.XboxOneD3D12:
                case GraphicsDeviceType.XboxOne:
                    initializeGPUReadbackParamsBridgeParams.maxFramesInFlight = QualitySettings.maxQueuedFrames + 1;
                    break;
                default:
                    initializeGPUReadbackParamsBridgeParams.maxFramesInFlight = (int)this.maxFramesInFlight;
                    break;
                }

                IntPtr nativeCreateInstanceBridgeParams =
                    Marshal.AllocHGlobal(Marshal.SizeOf(initializeGPUReadbackParamsBridgeParams));
                Marshal.StructureToPtr(initializeGPUReadbackParamsBridgeParams, nativeCreateInstanceBridgeParams, true);

                solverCommandBuffer.Clear();
                solverCommandBuffer.IssuePluginEventAndData(
                    ZibraLiquidBridge.GetRenderEventWithDataFunc(),
                    ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.InitializeGpuReadback,
                                                         CurrentInstanceID),
                    nativeCreateInstanceBridgeParams);
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);
                InitializeSolver();

                initialized = true;
                // hack to make editor -> play mode transition work when the liquid is initialized
                forceTextureUpdate = true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                ClearRendering();
                ClearSolver();

                initialized = false;
            }
        }

        protected void Update()
        {
            if (!initialized)
            {
                return;
            }

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                return;
            }
#endif

            if (!useFixedTimestep)
                UpdateSimulation(Time.smoothDeltaTime);

            UpdateReadback();
        }

        protected void FixedUpdate()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                return;
            }
#endif

            if (useFixedTimestep)
                UpdateSimulation(Time.fixedDeltaTime);
        }

        public void UpdateReadback()
        {
            solverCommandBuffer.Clear();

            // This must be called at most ONCE PER FRAME
            // Otherwise you'll get deadlock
            solverCommandBuffer.IssuePluginEvent(
                ZibraLiquidBridge.GetRenderEventFunc(),
                ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.UpdateReadback, CurrentInstanceID));

            Graphics.ExecuteCommandBuffer(solverCommandBuffer);

            /// ParticleNumber GPUReadback
            UInt32 size = sizeof(UInt32);
            IntPtr readbackData = ZibraLiquidBridge.GPUReadbackGetData(CurrentInstanceID, size);
            if (readbackData != IntPtr.Zero)
            {
                activeParticleNumber = Marshal.ReadInt32(readbackData);
            }

            UpdateManipulatorStatistics();
        }

        public void UpdateSimulation(float deltaTime)
        {
            if (!initialized || !runSimulation || simTimePerSec == 0.0f)
                return;

            UpdateNativeRenderParams();

            timestep = Math.Min(simTimePerSec * deltaTime / (float)iterationsPerFrame, timeStepMax);

            for (var i = 0; i < iterationsPerFrame; i++)
            {
                StepPhysics();
            }

#if UNITY_EDITOR
            NotifyChange();
#endif

            particleMass = 1.0f;
        }

        /// <summary>
        /// Update the material parameters
        /// </summary>
        public bool SetMaterialParams()
        {
            bool isDirty = false;

            if (SharedFluidMaterial != materialParameters.FluidMaterial)
            {
                SharedFluidMaterial = materialParameters.FluidMaterial;
                FluidMaterial = Material.Instantiate(SharedFluidMaterial);
                isDirty = true;
            }
            if (EnableDownscale && (SharedUpscaleMaterial != materialParameters.UpscaleMaterial))
            {
                SharedUpscaleMaterial = materialParameters.UpscaleMaterial;
                UpscaleMaterial = Material.Instantiate(SharedUpscaleMaterial);
                isDirty = true;
            }

            if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
            {
#if UNITY_PIPELINE_HDRP
                if (customLightHDRP == null)
                    Debug.LogError("No Custom Light set in Zibra Liquid.");
                else
                    FluidMaterial.SetVector("WorldSpaceLightPos", customLightHDRP.transform.position);

                if (reflectionProbeHDRP == null)
                    Debug.LogError("No reflection probe added to Zibra Liquid.");
#endif // UNITY_PIPELINE_HDRP
            }
            else
            {
                if (reflectionProbeSRP != null) // custom reflection probe
                {
                    usingCustomReflectionProbe = true;
                    FluidMaterial.SetTexture("ReflectionProbe", reflectionProbeSRP.texture);
                    FluidMaterial.SetVector("ReflectionProbe_HDR", reflectionProbeSRP.textureHDRDecodeValues);
                    FluidMaterial.SetVector("ReflectionProbe_BoxMax", reflectionProbeSRP.bounds.max);
                    FluidMaterial.SetVector("ReflectionProbe_BoxMin", reflectionProbeSRP.bounds.min);
                    FluidMaterial.SetVector("ReflectionProbe_ProbePosition", reflectionProbeSRP.transform.position);
                }
                else
                {
                    usingCustomReflectionProbe = false;
                }
            }

            FluidMaterial.SetFloat("Opacity", materialParameters.Opacity);
            FluidMaterial.SetFloat("Metalness", materialParameters.Metalness);
            FluidMaterial.SetFloat("RefractionDistortion", materialParameters.RefractionDistortion);
            FluidMaterial.SetFloat("Roughness", materialParameters.Roughness);
            FluidMaterial.SetVector("RefractionColor", materialParameters.Color);
            FluidMaterial.SetVector("ReflectionColor", materialParameters.ReflectionColor);

            FluidMaterial.SetVector("ContainerScale", containerSize);
            FluidMaterial.SetVector("ContainerPosition", containerPos);
            FluidMaterial.SetVector("GridSize", (Vector3)GridSize);
            FluidMaterial.SetFloat("FoamIntensity", materialParameters.FoamIntensity);
            FluidMaterial.SetFloat("FoamAmount", materialParameters.FoamAmount * solverParameters.ParticleDensity);
            FluidMaterial.SetFloat("ParticleDiameter", particleDiameter);

#if ZIBRA_LIQUID_DEBUG
            if (visualizeSceneSDF)
            {
                FluidMaterial.EnableKeyword("VISUALIZE_SDF");
            }
            else
            {
                FluidMaterial.DisableKeyword("VISUALIZE_SDF");
            }
#endif

            return isDirty;
        }

        public Vector2Int ApplyDownscaleFactor(Vector2Int val)
        {
            if (!EnableDownscale)
                return val;
            return new Vector2Int((int)(val.x * DownscaleFactor), (int)(val.y * DownscaleFactor));
        }

        private bool CreateTexture(ref RenderTexture texture, Vector2Int resolution, bool applyDownscaleFactor,
                                   FilterMode filterMode, int depth, RenderTextureFormat format,
                                   bool enableRandomWrite = false)
        {
            if (texture == null || texture.width != resolution.x || texture.height != resolution.y ||
                forceTextureUpdate)
            {
                if (texture != null)
                {
                    texture.Release();
                    DestroyImmediate(texture);
                }
                texture = new RenderTexture(resolution.x, resolution.y, depth, format);
                texture.enableRandomWrite = enableRandomWrite;
                texture.filterMode = filterMode;
                texture.Create();
                return true;
            }
            return false;
        }

        // Returns resolution that is enoguh for all cameras
        private Vector2Int GetRequiredTextureResolution()
        {
            if (camResolutions.Count == 0)
                Debug.Log("camResolutions dictionary was empty when GetRequiredTextureResolution was called.");

            Vector2Int result = new Vector2Int(0, 0);
            foreach (var item in camResolutions)
            {
                result = Vector2Int.Max(result, item.Value);
            }

            return result;
        }

        public bool IsBackgroundCopyNeeded(Camera cam)
        {
            return !EnableDownscale || (cam.activeTexture == null);
        }

        private RenderTexture GetBackgroundToBind(Camera cam)
        {
            if (!IsBackgroundCopyNeeded(cam))
                return cam.activeTexture;
            return cameraResources[cam].background;
        }

        /// <summary>
        /// Removes disabled/inactive cameras from cameraResources
        /// </summary>
        private void UpdateCameraList()
        {
            List<Camera> toRemove = new List<Camera>();
            foreach (var camResource in cameraResources)
            {
                if (camResource.Key == null ||
                    (!camResource.Key.isActiveAndEnabled && camResource.Key.cameraType != CameraType.SceneView))
                {
                    toRemove.Add(camResource.Key);
                    continue;
                }
            }
            foreach (var cam in toRemove)
            {
                if (cameraResources[cam].background)
                {
                    cameraResources[cam].background.Release();
                    cameraResources[cam].background = null;
                }
                cameraResources.Remove(cam);
            }
        }

        /// <summary>
        /// Update Native textures for a given camera
        /// </summary>
        /// <param name="cam">Camera</param>
        public bool UpdateNativeTextures(Camera cam)
        {
            UpdateCameraList();

            Vector2Int cameraResolution = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            Vector2Int cameraResolutionDownscaled = ApplyDownscaleFactor(cameraResolution);
            camResolutions[cam] = cameraResolutionDownscaled;

            Vector2Int textureResolution = GetRequiredTextureResolution();
            int pixelCount = textureResolution.x * textureResolution.y;

            if (!cameras.Contains(cam))
            {
                // add camera to list
                cameras.Add(cam);
            }

            int CameraID = cameras.IndexOf(cam);

            if (!cameraResources.ContainsKey(cam))
            {
                cameraResources[cam] = new CameraResources();
            }

            bool isGlobalTexturesDirty = false;
            bool isCameraDirty = cameraResources[cam].isDirty;

            FilterMode defaultFilter = EnableDownscale ? FilterMode.Bilinear : FilterMode.Point;

            if (IsBackgroundCopyNeeded(cam))
            {
                if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
                {
#if UNITY_PIPELINE_HDRP
                    isCameraDirty = CreateTexture(ref cameraResources[cam].background, cameraResolution, false,
                                                  FilterMode.Point, 0, RenderTextureFormat.ARGBHalf) ||
                                    isCameraDirty;
#endif
                }
                else
                {
                    isCameraDirty = CreateTexture(ref cameraResources[cam].background, cameraResolution, false,
                                                  FilterMode.Point, 0, RenderTextureFormat.RGB111110Float) ||
                                    isCameraDirty;
                }
            }
            else
            {
                if (cameraResources[cam].background != null)
                {
                    isCameraDirty = true;
                    cameraResources[cam].background.Release();
                    cameraResources[cam].background = null;
                }
            }
            isGlobalTexturesDirty =
                CreateTexture(ref depth, textureResolution, true, defaultFilter, 16, RenderTextureFormat.Depth) ||
                isGlobalTexturesDirty;
            isGlobalTexturesDirty = CreateTexture(ref color0, textureResolution, true, FilterMode.Point, 0,
                                                  RenderTextureFormat.ARGBHalf, true) ||
                                    isGlobalTexturesDirty;

#if ZIBRA_LIQUID_DEBUG
            isGlobalTexturesDirty = CreateTexture(ref SDFRender, textureResolution, true, FilterMode.Point, 0,
                                                  RenderTextureFormat.ARGBHalf, true) ||
                                    isGlobalTexturesDirty;
#endif

            isGlobalTexturesDirty = CreateTexture(ref color1, textureResolution, true, defaultFilter, 0,
                                                  RenderTextureFormat.ARGBHalf, true) ||
                                    isGlobalTexturesDirty;

            if (isGlobalTexturesDirty || isCameraDirty || forceTextureUpdate)
            {
                if (isGlobalTexturesDirty || forceTextureUpdate)
                {
                    foreach (var camera in cameraResources)
                    {
                        camera.Value.isDirty = true;
                    }

                    CurrentTextureResolution = textureResolution;

                    atomicGrid?.Release();
                    JFAGrid0?.Release();
                    JFAGrid1?.Release();

                    atomicGrid = new ComputeBuffer(pixelCount, sizeof(uint) * 2);
                    JFAGrid0 = new ComputeBuffer(pixelCount, sizeof(uint));
                    JFAGrid1 = new ComputeBuffer(pixelCount, sizeof(uint));
                }

                cameraResources[cam].isDirty = false;

#if ZIBRA_LIQUID_DEBUG
                atomicGrid.name = "atomicGrid";
                JFAGrid0.name = "JFAGrid0";
                JFAGrid1.name = "JFAGrid1";
#endif

                var registerRenderResourcesBridgeParams = new RegisterRenderResourcesBridgeParams();
                registerRenderResourcesBridgeParams.Depth = depth.GetNativeTexturePtr();
                registerRenderResourcesBridgeParams.Color0 = color0.GetNativeTexturePtr();
                registerRenderResourcesBridgeParams.Color1 = color1.GetNativeTexturePtr();
                registerRenderResourcesBridgeParams.AtomicGrid = atomicGrid.GetNativeBufferPtr();
                registerRenderResourcesBridgeParams.JFA0 = JFAGrid0.GetNativeBufferPtr();
                registerRenderResourcesBridgeParams.JFA1 = JFAGrid1.GetNativeBufferPtr();
#if ZIBRA_LIQUID_DEBUG
                registerRenderResourcesBridgeParams.SDFRender = SDFRender.GetNativeTexturePtr();
#endif

                IntPtr nativeRegisterRenderResourcesBridgeParams =
                    Marshal.AllocHGlobal(Marshal.SizeOf(registerRenderResourcesBridgeParams));
                Marshal.StructureToPtr(registerRenderResourcesBridgeParams, nativeRegisterRenderResourcesBridgeParams,
                                       true);
                solverCommandBuffer.Clear();
                solverCommandBuffer.IssuePluginEventAndData(
                    ZibraLiquidBridge.GetRenderEventWithDataFunc(),
                    ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.RegisterRenderResources,
                                                         CurrentInstanceID),
                    nativeRegisterRenderResourcesBridgeParams);
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);

                forceTextureUpdate = false;
            }

            return isGlobalTexturesDirty || isCameraDirty;
        }

        /// <summary>
        /// Render the particles from the native plugin
        /// </summary>
        /// <param name="cmdBuffer">Command Buffer to add the rendering commands to</param>
        public void RenderParticlesNative(CommandBuffer cmdBuffer, Rect? viewport = null)
        {
            cmdBuffer.IssuePluginEvent(
                ZibraLiquidBridge.GetRenderEventFunc(),
                ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.Draw, CurrentInstanceID));
        }

        /// <summary>
        /// Render the liquid surface to currently bound render target
        /// Used for URP where we can't change render targets
        /// </summary>
        public void RenderLiquidDirect(CommandBuffer cmdBuffer, Camera cam, Rect? viewport = null)
        {
            Vector2Int cameraRenderResolution = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            cameraRenderResolution = ApplyDownscaleFactor(cameraRenderResolution);

            // Render fluid to temporary RenderTexture if downscale enabled
            // Otherwise render straight to final RenderTexture
            if (EnableDownscale)
            {
                cmdBuffer.SetViewport(new Rect(0, 0, cameraRenderResolution.x, cameraRenderResolution.y));
            }
            else
            {
                if (viewport != null)
                {
                    cmdBuffer.SetViewport(viewport.Value);
                }
            }

#if UNITY_IOS && !UNITY_EDITOR
            if (EnableDownscale && IsBackgroundCopyNeeded(cam))
            {
                FluidMaterial.EnableKeyword("FLIP_BACKGROUND");
            }
            else
            {
                FluidMaterial.DisableKeyword("FLIP_BACKGROUND");
            }
#endif
            cmdBuffer.SetGlobalTexture("Background", GetBackgroundToBind(cam));
            cmdBuffer.SetGlobalTexture("FluidColor", color0);
            FluidMaterial.SetBuffer("GridNormal", GridNormal);

            if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
            {
#if UNITY_PIPELINE_HDRP
                cmdBuffer.SetGlobalTexture("ReflectionProbe", reflectionProbeHDRP.texture);
                cmdBuffer.SetGlobalVector("ReflectionProbe_HDR", new Vector4(0.01f, 1.0f));
                cmdBuffer.SetGlobalVector("ReflectionProbe_BoxMax", reflectionProbeHDRP.bounds.max);
                cmdBuffer.SetGlobalVector("ReflectionProbe_BoxMin", reflectionProbeHDRP.bounds.min);
                cmdBuffer.SetGlobalVector("ReflectionProbe_ProbePosition", reflectionProbeHDRP.transform.position);
                FluidMaterial.EnableKeyword("HDRP");
#endif
            }
            else
            {
                if (usingCustomReflectionProbe)
                {
                    FluidMaterial.EnableKeyword("CUSTOM_REFLECTION_PROBE");
                }
                else
                {
                    FluidMaterial.DisableKeyword("CUSTOM_REFLECTION_PROBE");
                }
            }

#if ZIBRA_LIQUID_DEBUG
            cmdBuffer.SetGlobalTexture("SDFRender", SDFRender);
#endif

            cmdBuffer.DrawProcedural(transform.localToWorldMatrix, FluidMaterial, 0, MeshTopology.Triangles, 6);
        }

        /// <summary>
        /// Upscale the liquid surface to currently bound render target
        /// Used for URP where we can't change render targets
        /// </summary>
        public void UpscaleLiquidDirect(CommandBuffer cmdBuffer, Camera cam,
                                        RenderTargetIdentifier? sourceColorTexture = null,
                                        RenderTargetIdentifier? sourceDepthTexture = null, Rect? viewport = null)
        {
            cmdBuffer.SetViewport(new Rect(0, 0, cam.pixelWidth, cam.pixelHeight));
            if (sourceColorTexture == null)
            {
                cmdBuffer.SetGlobalTexture("ShadedWater", color1);
            }
            else
            {
                cmdBuffer.SetGlobalTexture("ShadedWater", sourceColorTexture.Value);
            }
            if (sourceDepthTexture == null)
            {
                cmdBuffer.SetGlobalTexture("WaterDepth", depth, RenderTextureSubElement.Depth);
            }
            else
            {
                cmdBuffer.SetGlobalTexture("WaterDepth", sourceDepthTexture.Value);
            }
            cmdBuffer.DrawProcedural(transform.localToWorldMatrix, UpscaleMaterial, 0, MeshTopology.Triangles, 6);
        }

        /// <summary>
        /// Render the liquid surface
        /// Camera's targetTexture must be copied to cameraResources[cam].background
        /// using corresponding Render Pipeline before calling this method
        /// </summary>
        /// <param name="cmdBuffer">Command Buffer to add the rendering commands to</param>
        /// <param name="cam">Camera</param>
        public void RenderFluid(CommandBuffer cmdBuffer, Camera cam, RenderTargetIdentifier? renderTargetParam = null,
                                RenderTargetIdentifier? depthTargetParam = null, Rect? viewport = null)
        {
            RenderTargetIdentifier renderTarget =
                renderTargetParam ?? new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);

            // Render fluid to temporary RenderTexture if downscale enabled
            // Otherwise render straight to final RenderTexture
            if (EnableDownscale)
            {
                // color1 is used internally by the plugin but at this point texture can be temporarily reused
                cmdBuffer.SetRenderTarget(color1, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, depth,
                                          RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmdBuffer.ClearRenderTarget(true, true, Color.clear);
            }
            else
            {
                if (depthTargetParam != null)
                {
                    RenderTargetIdentifier depthTarget = depthTargetParam.Value;
                    cmdBuffer.SetRenderTarget(renderTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                                              depthTarget, RenderBufferLoadAction.Load,
                                              RenderBufferStoreAction.DontCare);
                }
                else
                {
                    cmdBuffer.SetRenderTarget(renderTarget);
                }
            }

            RenderLiquidDirect(cmdBuffer, cam, viewport);

            // If downscale enabled then we need to blend it on top of final RenderTexture
            if (EnableDownscale)
            {
                if (depthTargetParam != null)
                {
                    RenderTargetIdentifier depthTarget = depthTargetParam.Value;
                    cmdBuffer.SetRenderTarget(renderTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                                              depthTarget, RenderBufferLoadAction.Load,
                                              RenderBufferStoreAction.DontCare);
                }
                else
                {
                    cmdBuffer.SetRenderTarget(renderTarget);
                }

                UpscaleLiquidDirect(cmdBuffer, cam, null, null, viewport);
            }
        }

        /// <summary>
        /// Update the camera parameters for the particle renderer
        /// </summary>
        /// <param name="cam">Camera</param>
        ///
        public void UpdateCamera(Camera cam)
        {
            Vector2Int resolution = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            resolution = ApplyDownscaleFactor(resolution);

            Matrix4x4 Projection = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
            Matrix4x4 ProjectionInverse = Projection.inverse;
            Matrix4x4 View = cam.worldToCameraMatrix;
            Matrix4x4 ViewInverse = cam.cameraToWorldMatrix;
            Matrix4x4 ViewProjection = Projection * View;

            cameraRenderParams.View = cam.worldToCameraMatrix;
            cameraRenderParams.Projection = Projection;
            cameraRenderParams.ProjectionInverse = cameraRenderParams.Projection.inverse;
            cameraRenderParams.ViewProjection = cameraRenderParams.Projection * cameraRenderParams.View;
            cameraRenderParams.ViewProjectionInverse = cameraRenderParams.ViewProjection.inverse;
            cameraRenderParams.WorldSpaceCameraPos = cam.transform.position;
            cameraRenderParams.CameraResolution = new Vector2(resolution.x, resolution.y);
            cameraRenderParams.CameraID = cameras.IndexOf(cam);

            FluidMaterial.SetMatrix("ProjectionInverse", GL.GetGPUProjectionMatrix(cam.projectionMatrix, true).inverse);

            // update the data at the pointer
            Marshal.StructureToPtr(cameraRenderParams, camNativeParams[cam], true);

            Vector2Int cameraRenderResolution = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            cameraRenderResolution = ApplyDownscaleFactor(cameraRenderResolution);
            Vector2 textureScale = new Vector2((float)cameraRenderResolution.x / CurrentTextureResolution.x,
                                               (float)cameraRenderResolution.y / CurrentTextureResolution.y);
            FluidMaterial.SetVector("TextureScale", textureScale);
            if (EnableDownscale)
            {
                UpscaleMaterial.SetVector("TextureScale", textureScale);
            }
        }

        /// <summary>
        /// Update render parameters for a given camera
        /// </summary>
        /// <param name="cam">Camera</param>
        public void UpdateNativeCameraParams(Camera cam)
        {
            if (!camNativeParams.ContainsKey(cam))
            {
                // allocate memory for camera parameters
                camNativeParams[cam] = Marshal.AllocHGlobal(Marshal.SizeOf(cameraRenderParams));
                // update parameters
                UpdateCamera(cam);
                // set initial parameters in the native plugin

                solverCommandBuffer.Clear();
                solverCommandBuffer.IssuePluginEventAndData(
                    ZibraLiquidBridge.GetRenderEventWithDataFunc(),
                    ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.SetCameraParameters,
                                                         CurrentInstanceID),
                    camNativeParams[cam]);
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);
            }
        }

        public void UpdateNativeRenderParams()
        {
            particleDiameter =
                materialParameters.ParticleScale * cellSize / (float)Math.Pow(solverParameters.ParticleDensity, 0.333f);

            renderParams.BlurRadius = materialParameters.BlurRadius;
            renderParams.Diameter = particleDiameter;

            GCHandle gcparamBuffer = GCHandle.Alloc(renderParams, GCHandleType.Pinned);

            solverCommandBuffer.Clear();
            solverCommandBuffer.IssuePluginEventAndData(
                ZibraLiquidBridge.GetRenderEventWithDataFunc(),
                ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.SetRenderParameters, CurrentInstanceID),
                gcparamBuffer.AddrOfPinnedObject());
            Graphics.ExecuteCommandBuffer(solverCommandBuffer);

            gcparamBuffer.Free();
        }

        /// <summary>
        /// Rendering callback which is called by every camera in the scene
        /// </summary>
        /// <param name="cam">Camera</param>
        public void RenderCallBack(Camera cam)
        {
            bool visibleInCamera =
                (RenderPipelineDetector.GetRenderPipelineType() != RenderPipelineDetector.RenderPipeline.SRP) ||
                ((cam.cullingMask & (1 << this.gameObject.layer)) != 0);

            if (!isEnabled || !visibleInCamera || materialParameters.FluidMaterial == null ||
                (EnableDownscale && materialParameters.UpscaleMaterial == null))
            {
                if (cameraCBs.ContainsKey(cam))
                {
                    CameraEvent cameraEvent = (cam.actualRenderingPath == RenderingPath.Forward)
                                                  ? CameraEvent.BeforeForwardAlpha
                                                  : CameraEvent.AfterLighting;
                    cam.RemoveCommandBuffer(cameraEvent, cameraCBs[cam]);
                    cameraCBs[cam].Clear();
                    cameraCBs.Remove(cam);
                }
                return;
            }

            bool isDirty = SetMaterialParams();
            isDirty = isDirty || UpdateNativeTextures(cam);
            isDirty = isDirty || !cameraCBs.ContainsKey(cam);
#if UNITY_EDITOR
            isDirty = isDirty || ForceRepaint;
#endif
            UpdateNativeCameraParams(cam);
            UpdateCamera(cam);

            if (RenderPipelineDetector.GetRenderPipelineType() != RenderPipelineDetector.RenderPipeline.SRP)
            {
#if UNITY_PIPELINE_HDRP || UNITY_PIPELINE_URP
                // upload camera parameters
                solverCommandBuffer.Clear();
                solverCommandBuffer.IssuePluginEventAndData(
                    ZibraLiquidBridge.GetRenderEventWithDataFunc(),
                    ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.SetCameraParameters,
                                                         CurrentInstanceID),
                    camNativeParams[cam]);
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);
#endif
            }
            else
            {
                if (!cameraCBs.ContainsKey(cam) || isDirty)
                {
                    CameraEvent cameraEvent = (cam.actualRenderingPath == RenderingPath.Forward)
                                                  ? CameraEvent.BeforeForwardAlpha
                                                  : CameraEvent.AfterLighting;
                    CommandBuffer renderCommandBuffer;
                    if (isDirty && cameraCBs.ContainsKey(cam))
                    {
                        renderCommandBuffer = cameraCBs[cam];
                        renderCommandBuffer.Clear();
                    }
                    else
                    {
                        // Create render command buffer
                        renderCommandBuffer = new CommandBuffer { name = "ZibraLiquid.Render" };
                        // add command buffer to camera
                        cam.AddCommandBuffer(cameraEvent, renderCommandBuffer);
                        // add camera to the list
                        cameraCBs[cam] = renderCommandBuffer;
                    }

                    // enable depth texture
                    cam.depthTextureMode = DepthTextureMode.Depth;

                    // update native camera parameters
                    renderCommandBuffer.IssuePluginEventAndData(
                        ZibraLiquidBridge.GetCameraUpdateFunction(),
                        ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.None, CurrentInstanceID),
                        camNativeParams[cam]);

                    if (IsBackgroundCopyNeeded(cam))
                    {
                        renderCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive,
                                                 cameraResources[cam].background);
                    }
                    RenderParticlesNative(renderCommandBuffer);
                    RenderFluid(renderCommandBuffer, cam);
                }
            }
        }

        private void StepPhysics()
        {
            solverCommandBuffer.Clear();

            solverCommandBuffer.IssuePluginEvent(
                ZibraLiquidBridge.GetRenderEventFunc(),
                ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.ClearSDFAndID, CurrentInstanceID));

            SetFluidParameters();

            manipulatorManager.UpdateDynamic(containerPos, containerSize, timestep / simTimePerSec);

            // Update fluid parameters
            Marshal.StructureToPtr(fluidParameters, NativeFluidData, true);
            solverCommandBuffer.IssuePluginEventAndData(
                ZibraLiquidBridge.GetRenderEventWithDataFunc(),
                ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.UpdateLiquidParameters,
                                                     CurrentInstanceID),
                NativeFluidData);

            if (manipulatorManager.Elements > 0)
            {
                // Update manipulator parameters

                // Interop magic
                long LongPtr = NativeManipData.ToInt64(); // Must work both on x86 and x64
                for (int I = 0; I < manipulatorManager.ManipulatorParams.Count; I++)
                {
                    IntPtr Ptr = new IntPtr(LongPtr);
                    Marshal.StructureToPtr(manipulatorManager.ManipulatorParams[I], Ptr, true);
                    LongPtr += Marshal.SizeOf(typeof(Manipulators.ZibraManipulatorManager.ManipulatorParam));
                }

                solverCommandBuffer.IssuePluginEventAndData(
                    ZibraLiquidBridge.GetRenderEventWithDataFunc(),
                    ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.UpdateManipulatorParameters,
                                                         CurrentInstanceID),
                    NativeManipData);
            }

            // execute simulation
            solverCommandBuffer.IssuePluginEvent(
                ZibraLiquidBridge.GetRenderEventFunc(),
                ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.StepPhysics, CurrentInstanceID));

            Graphics.ExecuteCommandBuffer(solverCommandBuffer);

            // update internal time
            simulationInternalTime += timestep;
            simulationInternalFrame++;
        }

        void UpdateManipulatorStatistics()
        {
#if ZIBRA_LIQUID_PAID_VERSION
            /// ManipulatorStatistics GPUReadback
            if (manipulatorManager.Elements > 0)
            {
                UInt32 size = (UInt32)manipulatorManager.Elements * STATISTICS_PER_MANIPULATOR;
                IntPtr readbackData = ZibraLiquidBridge.GPUReadbackGetData(CurrentInstanceID, size * sizeof(Int32));
                if (readbackData != IntPtr.Zero)
                {
                    Int32[] Stats = new Int32[size];
                    Marshal.Copy(readbackData, Stats, 0, (Int32)size);
                    manipulatorManager.UpdateStatistics(Stats, manipulators, solverParameters, sdfColliders);
                }
            }
#endif
        }

        // stability calibration curve fit
        private float DivergenceDecayCurve(float x)
        {
            float a = (0.177f - 0.85f * x + 9.0f * x * x) / 1.8f;
            return 1.8f * a / (a + 1);
        }

        private void SetFluidParameters()
        {
            solverParameters.ValidateGravity();
            containerPos = transform.position;

            fluidParameters.GridSize = GridSize;
            fluidParameters.ContainerScale = containerSize;
            fluidParameters.NodeCount = numNodes;
            fluidParameters.ContainerPos = containerPos;
            fluidParameters.TimeStep = timestep;
            fluidParameters.Gravity = solverParameters.Gravity / 100.0f;
            fluidParameters.SimulationFrame = simulationInternalFrame;
            // BlurDirection set by native plugin
            fluidParameters.AffineAmmount = 4.0f * (1.0f - solverParameters.Viscosity);
            // ParticleTranslation set by native plugin
            fluidParameters.VelocityLimit = solverParameters.MaximumVelocity;
            fluidParameters.LiquidStiffness = solverParameters.FluidStiffness;
            fluidParameters.RestDensity = solverParameters.ParticleDensity;
#if ZIBRA_LIQUID_PAID_VERSION
            fluidParameters.SurfaceTension = solverParameters.SurfaceTension;
#endif
            fluidParameters.AffineDivergenceDecay = DivergenceDecayCurve(timestep);
#if ZIBRA_LIQUID_PAID_VERSION
            fluidParameters.MinimumVelocity = solverParameters.MinimumVelocity;
#endif
            // BlurNormalizationConstant set by native plugin
            fluidParameters.MaxParticleCount = MaxNumParticles;
#if ZIBRA_LIQUID_DEBUG
            fluidParameters.VisualizeSDF = visualizeSceneSDF ? 1 : 0;
#endif
        }

        /// <summary>
        /// Disable fluid render for a given camera
        /// </summary>
        public void DisableForCamera(Camera cam)
        {
            CameraEvent cameraEvent =
                cam.actualRenderingPath == RenderingPath.Forward ? CameraEvent.AfterSkybox : CameraEvent.AfterLighting;
            cam.RemoveCommandBuffer(cameraEvent, cameraCBs[cam]);
            cameraCBs[cam].Dispose();
            cameraCBs.Remove(cam);
        }

        protected void ClearRendering()
        {
            Camera.onPreRender -= RenderCallBack;

            foreach (var cam in cameraCBs)
            {
                if (cam.Key != null)
                {
                    cam.Value.Clear();
                }
            }

            cameraCBs.Clear();
            cameras.Clear();

            // free allocated memory
            foreach (var data in camNativeParams)
            {
                Marshal.FreeHGlobal(data.Value);
            }

            foreach (var resource in cameraResources)
            {
                if (resource.Value.background != null)
                {
                    resource.Value.background.Release();
                    resource.Value.background = null;
                }
            }
            cameraResources.Clear();
            if (atomicGrid != null)
            {
                atomicGrid?.Release();
                atomicGrid = null;
            }
            if (color0 != null)
            {
                color0.Release();
                color0 = null;
            }
            if (color1 != null)
            {
                color1.Release();
                color1 = null;
            }
#if ZIBRA_LIQUID_DEBUG
            if (SDFRender != null)
            {
                SDFRender.Release();
                SDFRender = null;
            }
#endif
            if (JFAGrid0 != null)
            {
                JFAGrid0.Release();
                JFAGrid0 = null;
            }
            if (JFAGrid1 != null)
            {
                JFAGrid1.Release();
                JFAGrid1 = null;
            }
            camNativeParams.Clear();
        }

        protected void ClearSolver()
        {
            if (solverCommandBuffer != null)
            {
                solverCommandBuffer.IssuePluginEvent(
                    ZibraLiquidBridge.GetRenderEventFunc(),
                    ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.ReleaseResources,
                                                         CurrentInstanceID));
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);
            }

            if (solverCommandBuffer != null)
            {
                solverCommandBuffer.Release();
                solverCommandBuffer = null;
            }
            if (PositionMass != null)
                PositionMass.Release();
            if (PositionRadius != null)
                PositionRadius.Release();
            if (Affine != null)
            {
                if (Affine[0] != null)
                    Affine[0].Release();
                if (Affine[1] != null)
                    Affine[1].Release();
            }
            if (GridData != null)
                GridData.Release();
            if (IndexGrid != null)
                IndexGrid.Release();
            if (nodeParticlePairs != null)
                nodeParticlePairs.Release();
            if (positionMassCopy != null)
                positionMassCopy.Release();
            if (GridNormal != null)
                GridNormal.Release();
            if (GridBlur0 != null)
                GridBlur0.Release();
            if (gridBlur1 != null)
                gridBlur1.Release();
            if (GridSDF != null)
                GridSDF.Release();
            if (gridNodePositions != null)
                gridNodePositions.Release();
            if (SortTempBuffer != null)
                SortTempBuffer.Release();
            if (RadixGroupData != null)
                RadixGroupData.Release();

            if (ParticleNumber != null)
                ParticleNumber.Release();
            if (DynamicManipulatorData != null)
                DynamicManipulatorData.Release();
            if (ConstManipulatorData != null)
                ConstManipulatorData.Release();
            if (ManipulatorStatistics != null)
                ManipulatorStatistics.Release();

            Marshal.FreeHGlobal(NativeManipData);
            Marshal.FreeHGlobal(NativeFluidData);

            initialized = false;

            // DO NOT USE AllFluids.Remove(this)
            // This will not result in equivalent code
            // ZibraLiquid::Equals is overriden and don't have correct implementation

            if (AllFluids != null)
            {
                for (int i = 0; i < AllFluids.Count; i++)
                {
                    var fluid = AllFluids[i];
                    if (ReferenceEquals(fluid, this))
                    {
                        AllFluids.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public ReadOnlyCollection<SDFCollider> GetColliderList()
        {
            return sdfColliders.AsReadOnly();
        }

        public bool HasCollider(SDFCollider collider)
        {
            return sdfColliders.Contains(collider);
        }

        public void AddCollider(SDFCollider collider)
        {
            if (initialized)
            {
                Debug.LogWarning("We don't yet support changing number of manipulators/colliders at runtime.");
                return;
            }

            if (!sdfColliders.Contains(collider))
            {
#if !ZIBRA_LIQUID_PAID_VERSION
                if (manipulators.Count >= 5)
                {
                    Debug.LogWarning(
                        "Can't add additional collider to liquid instance. Free version can only have up to 5 colliders.");
                    return;
                }
#endif

                sdfColliders.Add(collider);
                sdfColliders.Sort(new SDFColliderCompare());
            }
        }

        public void RemoveCollider(SDFCollider collider)
        {
            if (initialized)
            {
                Debug.LogWarning("We don't yet support changing number of manipulators/colliders at runtime.");
                return;
            }

            if (sdfColliders.Contains(collider))
            {
                sdfColliders.Remove(collider);
                sdfColliders.Sort(new SDFColliderCompare());
            }
        }

        public bool HasEmitter()
        {
            foreach (var manipulator in manipulators)
            {
                if (manipulator.ManipType == Manipulator.ManipulatorType.Emitter)
                {
                    return true;
                }
            }
            return false;
        }

        public ReadOnlyCollection<Manipulator> GetManipulatorList()
        {
            return manipulators.AsReadOnly();
        }

        public bool HasManipulator(Manipulator manipulator)
        {
            return manipulators.Contains(manipulator);
        }

        public void AddManipulator(Manipulator manipulator)
        {
            if (initialized)
            {
                Debug.LogWarning("We don't yet support changing number of manipulators/colliders at runtime.");
                return;
            }

            if (!manipulators.Contains(manipulator))
            {
#if !ZIBRA_LIQUID_PAID_VERSION
                foreach (var manip in manipulators)
                {
                    if (manip.ManipType == manipulator.ManipType)
                    {
                        Debug.LogWarning(
                            "Can't add additional manipulator to liquid instance. Free version can only have single emitter and single force field.");
                        return;
                    }
                }
#endif

                manipulators.Add(manipulator);
                manipulators.Sort(new ManipulatorCompare());
            }
        }

        public void RemoveManipulator(Manipulator manipulator)
        {
            if (initialized)
            {
                Debug.LogWarning("We don't yet support changing number of manipulators/colliders at runtime.");
                return;
            }

            if (manipulators.Contains(manipulator))
            {
                manipulators.Remove(manipulator);
                manipulators.Sort(new ManipulatorCompare());
            }
        }

#if UNITY_EDITOR
        public void OnValidate()
        {
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            containerSize[0] = Math.Max(containerSize[0], 1e-3f);
            containerSize[1] = Math.Max(containerSize[1], 1e-3f);
            containerSize[2] = Math.Max(containerSize[2], 1e-3f);

            cellSize = Math.Max(containerSize.x, Math.Max(containerSize.y, containerSize.z)) / gridResolution;

            if (sdfColliders != null)
            {
                sdfColliders.RemoveAll(item => item == null);
            }
            if (manipulators != null)
            {
                manipulators.RemoveAll(item => item == null);
            }

#if ZIBRA_LIQUID_PAID_VERSION
            if (BakedInitialStateAsset)
            {
                int bakedLiquidHeader = BitConverter.ToInt32(BakedInitialStateAsset.bytes, 0);
                if (bakedLiquidHeader != BAKED_LIQUID_HEADER_VALUE)
                {
                    BakedInitialStateAsset = null;
                }
            }
#endif

#if !ZIBRA_LIQUID_PAID_VERSION
            // Limit manipulator count to 1
            bool emitterFound = false;
            bool forceFieldFound = false;
            List<Manipulator> manipsToRemove = new List<Manipulator>();

            foreach (var manip in manipulators)
            {
                if (manip.ManipType == Manipulator.ManipulatorType.Emitter)
                {
                    if (!emitterFound)
                    {
                        emitterFound = true;
                    }
                    else
                    {
                        manipsToRemove.Add(manip);
                    }
                }
                if (manip.ManipType == Manipulator.ManipulatorType.ForceField)
                {
                    if (!forceFieldFound)
                    {
                        forceFieldFound = true;
                    }
                    else
                    {
                        manipsToRemove.Add(manip);
                    }
                }
            }

            if (manipsToRemove.Count != 0)
            {
                Debug.LogWarning(
                    "Too many manipulators, some will be removed. Free version limited to 1 emitter and 1 force field.");
                foreach (var manip in manipsToRemove)
                {
                    RemoveManipulator(manip);
                }
            }

            if (sdfColliders.Count > 5)
            {
                Debug.LogWarning(
                    "Too many colliders for free version of Zibra Liquids, some will be removed. Free version limited to 5 SDF colliders.");
                sdfColliders.RemoveRange(5, sdfColliders.Count - 5);
            }
#endif
        }
#endif

        protected void OnApplicationQuit()
        {
            // On quit we need to destroy liquid before destroying any colliders/manipulators
            OnDisable();
        }

        public void StopSolver()
        {
            if (!initialized)
            {
                return;
            }

            initialized = false;
            ClearRendering();
            ClearSolver();
            isEnabled = false;
        }

        // dispose the objects
        protected void OnDisable()
        {
            StopSolver();
        }

#if ZIBRA_LIQUID_PAID_VERSION
        private float ByteArrayToSingle(byte[] array, ref int startIndex)
        {
            float value = BitConverter.ToSingle(array, startIndex);
            startIndex += sizeof(float);
            return value;
        }

        private int ByteArrayToInt(byte[] array, ref int startIndex)
        {
            int value = BitConverter.ToInt32(array, startIndex);
            startIndex += sizeof(int);
            return value;
        }

        private BakedInitialState ConvertBytesToInitialState(byte[] data)
        {
            int startIndex = 0;

            int header = ByteArrayToInt(data, ref startIndex);
            if (header != BAKED_LIQUID_HEADER_VALUE)
            {
                throw new Exception("Invalid baked liquid data.");
            }

            int particleCount = ByteArrayToInt(data, ref startIndex);
            if (particleCount > MaxNumParticles)
            {
                throw new Exception("Baked data have more particles than max particle count.");
            }

            BakedInitialState initialStateData = new BakedInitialState();
            initialStateData.ParticleCount = particleCount;
            initialStateData.Positions = new Vector4[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    initialStateData.Positions[i][j] = ByteArrayToSingle(data, ref startIndex);
                }
                initialStateData.Positions[i].w = 1.0f;
            }

            initialStateData.AffineVelocity = new Vector2Int[4 * particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    initialStateData.AffineVelocity[4 * i + 3][j] = ByteArrayToInt(data, ref startIndex);
                }
            }

            return initialStateData;
        }

        private BakedInitialState LoadInitialStateAsset()
        {
            byte[] data = BakedInitialStateAsset.bytes;
            return ConvertBytesToInitialState(data);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Save current simulation state
        /// </summary>
        public BakedInitialState SerializeCurrentLiquidState()
        {
            int[] ParticleNumberArray = new int[1];
            ParticleNumber.GetData(ParticleNumberArray, 0, 0, 1);

            BakedInitialState initialStateData = new BakedInitialState();

            initialStateData.ParticleCount = ParticleNumberArray[0];

            int currentAffineIndex = 1 - ZibraLiquidBridge.GetCurrentAffineBufferIndex(CurrentInstanceID);

            InitialState = InitialStateType.BakedLiquidState;
            Array.Resize(ref initialStateData.Positions, initialStateData.ParticleCount);
            PositionMass.GetData(initialStateData.Positions);
            Array.Resize(ref initialStateData.AffineVelocity, 4 * initialStateData.ParticleCount);
            Affine[currentAffineIndex].GetData(initialStateData.AffineVelocity);

            ForceRepaint = true;

            return initialStateData;
        }
#endif

        /// <summary>
        /// Apply currently set initial conditions
        /// </summary>
        protected void ApplyInitialState()
        {
            switch (InitialState)
            {
            case InitialStateType.NoParticles:
                fluidParameters.ParticleCount = 0;
                break;
            case InitialStateType.BakedLiquidState:
                if (BakedInitialStateAsset)
                {
                    BakedInitialState initialStateData = LoadInitialStateAsset();
                    PositionMass.SetData(initialStateData.Positions);
                    Affine[0].SetData(initialStateData.AffineVelocity);
                    Affine[1].SetData(initialStateData.AffineVelocity);
                    fluidParameters.ParticleCount = initialStateData.ParticleCount;
                }
                else
                {
                    fluidParameters.ParticleCount = 0;
                }
                break;
            }
        }
#endif
    }
}