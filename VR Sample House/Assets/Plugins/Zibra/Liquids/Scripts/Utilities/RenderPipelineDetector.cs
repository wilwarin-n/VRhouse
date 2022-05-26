using UnityEngine;
using UnityEngine.Rendering;

namespace com.zibra.liquid.Utilities
{
    public class RenderPipelineDetector
    {
        public enum RenderPipeline
        {
            SRP,
            URP,
            HDRP
        }
        public static RenderPipeline GetRenderPipelineType()
        {
            if (GraphicsSettings.currentRenderPipeline)
            {
                if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                {
#if !UNITY_PIPELINE_HDRP
                    Debug.LogError("Current detected render pipeline is HDRP, but UNITY_PIPELINE_HDRP is not defined");
#endif
                    return RenderPipeline.HDRP;
                }
                else
                {
#if !UNITY_PIPELINE_URP
                    Debug.LogError("Current detected render pipeline is URP, but UNITY_PIPELINE_URP is not defined");
#endif
                    return RenderPipeline.URP;
                }
            }
            else
            {
                return RenderPipeline.SRP;
            }
        }
    }
}
