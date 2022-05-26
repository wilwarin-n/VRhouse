using AOT;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace com.zibra.liquid.Solver
{
    public static class ZibraLiquidDebug
    {

        [RuntimeInitializeOnLoadMethod]
        static void InitializeDebug()
        {
            SetDebugLogWrapperPointer(DebugLogCallback);
        }

        delegate void debugLogCallback(IntPtr message);
        [MonoPInvokeCallback(typeof(debugLogCallback))]
        static void DebugLogCallback(IntPtr request)
        {
            Debug.Log(Marshal.PtrToStringAnsi(request));
        }

        [DllImport(ZibraLiquidBridge.PluginLibraryName)]
        static extern void SetDebugLogWrapperPointer(debugLogCallback callback);
    }
}