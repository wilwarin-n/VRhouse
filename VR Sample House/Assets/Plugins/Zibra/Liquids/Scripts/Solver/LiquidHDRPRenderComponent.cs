#if UNITY_PIPELINE_HDRP

using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using com.zibra.liquid.Solver;

namespace com.zibra.liquid
{
    public class LiquidHDRPRenderComponent : CustomPassVolume
    {
        public class FluidHDRPRender : CustomPass
        {
            public ZibraLiquid liquid;
            RTHandle Depth;

            protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
            {
                Depth = RTHandles.Alloc(Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
                                        colorFormat: GraphicsFormat.R32_SFloat,
                                        // We don't need alpha for this effect
                                        useDynamicScale: true, name: "Depth buffer");
            }

#if UNITY_PIPELINE_HDRP_9_0_OR_HIGHER
            protected override void Execute(CustomPassContext ctx)
#else
            protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera,
                                            CullingResults cullingResult)
#endif
            {
                if (liquid && liquid.initialized && liquid.simulationInternalFrame > 1)
                {

                    RTHandle cameraColor, cameraDepth;
#if UNITY_PIPELINE_HDRP_9_0_OR_HIGHER
                    cameraColor = ctx.cameraColorBuffer;
                    cameraDepth = ctx.cameraDepthBuffer;

                    HDCamera hdCamera = ctx.hdCamera;
                    CommandBuffer cmd = ctx.cmd;
#else
                    GetCameraBuffers(out cameraColor, out cameraDepth);
#endif

                    if ((hdCamera.camera.cullingMask & (1 << liquid.gameObject.layer)) ==
                        0) // fluid gameobject layer is not in the culling mask of the camera
                        return;

                    liquid.RenderCallBack(hdCamera.camera);

                    var depth = Shader.PropertyToID("_CameraDepthTexture");
                    cmd.GetTemporaryRT(depth, hdCamera.camera.pixelWidth, hdCamera.camera.pixelHeight, 32,
                                       FilterMode.Point, RenderTextureFormat.RFloat);

                    // copy screen to background
                    var scale = RTHandles.rtHandleProperties.rtHandleScale;
                    if (liquid.IsBackgroundCopyNeeded(hdCamera.camera))
                    {
                        cmd.Blit(cameraColor, liquid.cameraResources[hdCamera.camera].background,
                                 new Vector2(scale.x, scale.y), Vector2.zero, 0, 0);
                    }
                    // blit depth to temp RT
                    HDUtils.BlitCameraTexture(cmd, cameraDepth, Depth);
                    cmd.Blit(Depth, depth, new Vector2(scale.x, scale.y), Vector2.zero, 1, 0);

                    liquid.RenderParticlesNative(cmd);
                    // bind temp depth RT
                    cmd.SetGlobalTexture("_CameraDepthTexture", depth);
                    liquid.RenderFluid(cmd, hdCamera.camera, cameraColor, cameraDepth, hdCamera.camera.pixelRect);
                    cmd.ReleaseTemporaryRT(depth);
                }
            }

            protected override void Cleanup()
            {
                RTHandles.Release(Depth);
            }
        }

        public FluidHDRPRender fluidPass;
    }
}

#endif // UNITY_PIPELINE_HDRP