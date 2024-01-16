using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SampleRendererFeature : ScriptableRendererFeature
{
    // 故障效果Pass
    private GlitchImageBlockPass m_GlitchImageBlockPass;
    
    public override void Create()
    {
        // 初始化Pass
        m_GlitchImageBlockPass = new GlitchImageBlockPass(RenderPassEvent.AfterRenderingTransparents);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 添加屏幕拷贝Pass
        renderer.EnqueuePass(m_GlitchImageBlockPass);
    }
}
 