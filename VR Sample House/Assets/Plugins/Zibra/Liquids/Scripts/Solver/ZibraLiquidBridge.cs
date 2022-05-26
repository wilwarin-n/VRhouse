using System;
using System.Runtime.InteropServices;

namespace com.zibra.liquid.Solver
{
    public static class ZibraLiquidBridge
    {
#if (UNITY_IOS || UNITY_TVOS || UNITY_WEBGL) && !UNITY_EDITOR
        public const String PluginLibraryName = "__Internal";
#elif UNITY_WSA && !UNITY_EDITOR
        public const String PluginLibraryName = "ZibraFluidNative_WSA";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        public const String PluginLibraryName = "ZibraFluidNative_Mac";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        public const String PluginLibraryName = "ZibraFluidNative_Win";
#else
#error Unsupported platform
#endif

        [DllImport(PluginLibraryName)]
        public static extern IntPtr GetRenderEventFunc();

        [DllImport(PluginLibraryName)]
        public static extern IntPtr GetRenderEventWithDataFunc();

        [DllImport(PluginLibraryName)]
        public static extern IntPtr GetCameraUpdateFunction();

        [DllImport(PluginLibraryName)]
        public static extern IntPtr GPUReadbackGetData(Int32 InstanceID, UInt32 size);

        [DllImport(PluginLibraryName)]
        public static extern Int32 GetCurrentAffineBufferIndex(Int32 InstanceID);

        [DllImport(PluginLibraryName)]
        public static extern bool IsPaidVersion();

        [DllImport(PluginLibraryName)]
        public static extern IntPtr GetVersionString();

        public static readonly string version = Marshal.PtrToStringAnsi(GetVersionString());

        public enum EventID : int
        {
            None = 0, // only used for GetCameraUpdateFunction
            StepPhysics = 1,
            Draw = 2,
            InitializeManipulators = 3,
            UpdateLiquidParameters = 4,
            UpdateManipulatorParameters = 5,
            ClearSDFAndID = 6,
            CreateFluidInstance = 7,
            RegisterParticlesBuffers = 8,
            SetCameraParameters = 9,
            SetRenderParameters = 10,
            RegisterManipulators = 11,
            RegisterSolverBuffers = 12,
            RegisterRenderResources = 13,
            ReleaseResources = 14,
            InitializeGpuReadback = 15,
            UpdateReadback = 16,
        }

        public static int EventAndInstanceID(EventID eventID, int InstanceID)
        {
            return (int)eventID | (InstanceID << 8);
        }
    }
}
