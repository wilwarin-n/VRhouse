#if UNITY_PIPELINE_URP

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using com.zibra.liquid.Solver;

namespace com.zibra.liquid
{
    public class LiquidURPRenderComponent : ScriptableRendererFeature
    {
        [System.Serializable]
        public class LiquidURPRenderSettings
        {
            // we're free to put whatever we want here, public fields will be exposed in the inspector
            public bool IsEnabled = true;
        }
        // Must be called exactly "settings" so Unity shows this as render feature settings in editor
        public LiquidURPRenderSettings settings = new LiquidURPRenderSettings();

        public class CopyBackgroundURPRenderPass : ScriptableRenderPass
        {
            public ZibraLiquid liquid;

            RenderTargetIdentifier cameraColorTexture;

            public CopyBackgroundURPRenderPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

#if UNITY_PIPELINE_URP_9_0_OR_HIGHER
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                cameraColorTexture = renderingData.cameraData.renderer.cameraColorTarget;
            }
#else
            public void Setup(ScriptableRenderer renderer, ref RenderingData renderingData)
            {
                cameraColorTexture = renderer.cameraColorTarget;
            }
#endif

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                Camera camera = renderingData.cameraData.camera;

                CommandBuffer cmd = CommandBufferPool.Get("ZibraLiquid.Render");

                if (liquid.cameraResources.ContainsKey(camera))
                {
#if UNITY_PIPELINE_URP_9_0_OR_HIGHER
                    Blit(cmd, cameraColorTexture, liquid.cameraResources[camera].background);
#else
                    // For some reason old version of URP don't want to blit texture via correct API
                    cmd.Blit(cameraColorTexture, liquid.cameraResources[camera].background);
#endif
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public class LiquidURPRenderPass : ScriptableRenderPass
        {
            public ZibraLiquid liquid;

            RenderTargetIdentifier cameraColorTexture;
            RenderTargetIdentifier cameraDepthTexture;

            static int upscaleColorTextureID = Shader.PropertyToID("ZibraLiquid_LiquidTempColorTexture");
            static int upscaleDepthTextureID = Shader.PropertyToID("ZibraLiquid_LiquidTempDepthTexture");
            RenderTargetIdentifier upscaleColorTexture;
            RenderTargetIdentifier upscaleDepthTexture;

            public LiquidURPRenderPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

#if UNITY_PIPELINE_URP_9_0_OR_HIGHER
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                cameraColorTexture = renderingData.cameraData.renderer.cameraColorTarget;
                cameraDepthTexture = renderingData.cameraData.renderer.cameraDepthTarget;
            }
#else
            public void Setup(ScriptableRenderer renderer, ref RenderingData renderingData)
            {
                cameraColorTexture = renderer.cameraColorTarget;
                cameraDepthTexture = renderer.cameraDepth;
            }
#endif

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                if (liquid.EnableDownscale)
                {
                    RenderTextureDescriptor descriptor = cameraTextureDescriptor;

                    Vector2Int dimensions = new Vector2Int(descriptor.width, descriptor.height);
                    dimensions = liquid.ApplyDownscaleFactor(dimensions);
                    descriptor.width = dimensions.x;
                    descriptor.height = dimensions.y;

                    descriptor.msaaSamples = 1;

                    descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
                    descriptor.depthBufferBits = 0;

                    cmd.GetTemporaryRT(upscaleColorTextureID, descriptor, FilterMode.Bilinear);

                    descriptor.colorFormat = RenderTextureFormat.Depth;
                    descriptor.depthBufferBits = 16;

                    cmd.GetTemporaryRT(upscaleDepthTextureID, descriptor, FilterMode.Bilinear);

                    upscaleColorTexture = new RenderTargetIdentifier(upscaleColorTextureID);
                    upscaleDepthTexture = new RenderTargetIdentifier(upscaleDepthTextureID);
                    ConfigureTarget(upscaleColorTexture, upscaleDepthTexture);
                    ConfigureClear(ClearFlag.All, new Color(0, 0, 0, 0));
                }
                else
                {
                    ConfigureTarget(cameraColorTexture, cameraDepthTexture);
                    // ConfigureClear seems to be persistent, so need to reset it
                    ConfigureClear(ClearFlag.None, new Color(0, 0, 0, 0));
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                Camera camera = renderingData.cameraData.camera;
                camera.depthTextureMode = DepthTextureMode.Depth;
                CommandBuffer cmd = CommandBufferPool.Get("ZibraLiquid.Render");

                liquid.RenderCallBack(renderingData.cameraData.camera);

                // set initial parameters in the native plugin
                cmd.IssuePluginEventAndData(
                    ZibraLiquidBridge.GetCameraUpdateFunction(),
                    ZibraLiquidBridge.EventAndInstanceID(ZibraLiquidBridge.EventID.None, liquid.CurrentInstanceID),
                    liquid.camNativeParams[camera]);

                liquid.RenderParticlesNative(cmd);

                if (liquid.EnableDownscale)
                {
                    cmd.SetRenderTarget(upscaleColorTexture, upscaleDepthTexture);
                }

                liquid.RenderLiquidDirect(cmd, camera);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

#if UNITY_PIPELINE_URP_9_0_OR_HIGHER
            public override void OnCameraCleanup(CommandBuffer cmd)
#else
            public override void FrameCleanup(CommandBuffer cmd)
#endif
            {
                if (liquid.EnableDownscale)
                {
                    cmd.ReleaseTemporaryRT(upscaleColorTextureID);
                    cmd.ReleaseTemporaryRT(upscaleDepthTextureID);
                }
            }
        }

        public class LiquidUpscaleURPRenderPass : ScriptableRenderPass
        {
            public ZibraLiquid liquid;

            RenderTargetIdentifier cameraColorTexture;
            RenderTargetIdentifier cameraDepthTexture;

            static int upscaleColorTextureID = Shader.PropertyToID("ZibraLiquid_LiquidTempColorTexture");
            static int upscaleDepthTextureID = Shader.PropertyToID("ZibraLiquid_LiquidTempDepthTexture");
            RenderTargetIdentifier upscaleColorTexture;
            RenderTargetIdentifier upscaleDepthTexture;

            public LiquidUpscaleURPRenderPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

#if UNITY_PIPELINE_URP_9_0_OR_HIGHER
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                cameraColorTexture = renderingData.cameraData.renderer.cameraColorTarget;
                cameraDepthTexture = renderingData.cameraData.renderer.cameraDepthTarget;
            }
#else
            public void Setup(ScriptableRenderer renderer, ref RenderingData renderingData)
            {
                cameraColorTexture = renderer.cameraColorTarget;
                cameraDepthTexture = renderer.cameraDepth;
            }
#endif
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ConfigureTarget(cameraColorTexture, cameraDepthTexture);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                Camera camera = renderingData.cameraData.camera;
                camera.depthTextureMode = DepthTextureMode.Depth;
                CommandBuffer cmd = CommandBufferPool.Get("ZibraLiquid.Render");

                upscaleColorTexture = new RenderTargetIdentifier(upscaleColorTextureID);
                upscaleDepthTexture = new RenderTargetIdentifier(upscaleDepthTextureID);
                liquid.UpscaleLiquidDirect(cmd, camera, upscaleColorTexture, upscaleDepthTexture);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        // 1 pass per rendered liquid that requires background copy
        public CopyBackgroundURPRenderPass[] copyPasses;
        // 1 pass per rendered liquid
        public LiquidURPRenderPass[] liquidPasses;
        // 1 pass per rendered liquid that have downscale enabled
        public LiquidUpscaleURPRenderPass[] upscalePasses;

        public override void Create()
        {
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.IsEnabled)
            {
                return;
            }

            if (renderingData.cameraData.cameraType != CameraType.Game)
            {
                return;
            }

            Camera camera = renderingData.cameraData.camera;
            camera.depthTextureMode = DepthTextureMode.Depth;

            int liquidsToRenderCount = 0;
            int backgroundsToCopyCount = 0;
            int liquidsToUpscaleCount = 0;

            foreach (var liquid in ZibraLiquid.AllFluids)
            {
                if (liquid != null && liquid.initialized)
                {
                    liquidsToRenderCount++;
                    if (liquid.EnableDownscale)
                    {
                        liquidsToUpscaleCount++;
                    }
                    if (liquid.IsBackgroundCopyNeeded(camera))
                    {
                        backgroundsToCopyCount++;
                    }
                }
            }

            if (copyPasses == null || copyPasses.Length != backgroundsToCopyCount)
            {
                copyPasses = new CopyBackgroundURPRenderPass[backgroundsToCopyCount];
                for (int i = 0; i < backgroundsToCopyCount; ++i)
                {
                    copyPasses[i] = new CopyBackgroundURPRenderPass();
                }
            }

            if (liquidPasses == null || liquidPasses.Length != liquidsToRenderCount)
            {
                liquidPasses = new LiquidURPRenderPass[liquidsToRenderCount];
                for (int i = 0; i < liquidsToRenderCount; ++i)
                {
                    liquidPasses[i] = new LiquidURPRenderPass();
                }
            }

            if (upscalePasses == null || upscalePasses.Length != liquidsToUpscaleCount)
            {
                upscalePasses = new LiquidUpscaleURPRenderPass[liquidsToUpscaleCount];
                for (int i = 0; i < liquidsToUpscaleCount; ++i)
                {
                    upscalePasses[i] = new LiquidUpscaleURPRenderPass();
                }
            }

            int currentCopyPass = 0;
            int currentLiquidPass = 0;
            int currentUpscalePass = 0;

            foreach (var liquid in ZibraLiquid.AllFluids)
            {
                if (liquid != null && liquid.initialized &&
                    ((camera.cullingMask & (1 << liquid.gameObject.layer)) != 0))
                {
                    if (liquid.IsBackgroundCopyNeeded(camera))
                    {
                        copyPasses[currentCopyPass].liquid = liquid;

#if UNITY_PIPELINE_URP_10_0_OR_HIGHER
                        copyPasses[currentCopyPass].ConfigureInput(ScriptableRenderPassInput.Color |
                                                                   ScriptableRenderPassInput.Depth);
#endif

                        renderer.EnqueuePass(copyPasses[currentCopyPass]);
                        currentCopyPass++;
                    }

                    liquidPasses[currentLiquidPass].liquid = liquid;

#if UNITY_PIPELINE_URP_10_0_OR_HIGHER
                    liquidPasses[currentLiquidPass].ConfigureInput(ScriptableRenderPassInput.Color |
                                                                   ScriptableRenderPassInput.Depth);
#endif

#if !UNITY_PIPELINE_URP_9_0_OR_HIGHER
                    liquidPasses[currentLiquidPass].Setup(renderer, ref renderingData);
#endif

                    renderer.EnqueuePass(liquidPasses[currentLiquidPass]);
                    currentLiquidPass++;
                    if (liquid.EnableDownscale)
                    {
                        upscalePasses[currentUpscalePass].liquid = liquid;

#if !UNITY_PIPELINE_URP_9_0_OR_HIGHER
                        upscalePasses[currentUpscalePass].Setup(renderer, ref renderingData);
#endif

                        renderer.EnqueuePass(upscalePasses[currentUpscalePass]);
                        currentUpscalePass++;
                    }
                }
            }
        }
    }
}

#endif // UNITY_PIPELINE_HDRP