using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GlitchImageBlockPass : ScriptableRenderPass
{
    // 用于Shader中使用的Uniform变量
    static class ShaderIDs
    {
        internal static readonly int Params = Shader.PropertyToID("_Params");
        internal static readonly int Params2 = Shader.PropertyToID("_Params2");
        internal static readonly int Params3 = Shader.PropertyToID("_Params3");
    }
    
    // 用于FrameDebugger或其他Profiler中显示的名字
    private const string m_ProfilerTag = "Glitch Image Block";
    
    // 后处理配置类
    private Glitch m_Glitch;
    
    private float TimeX = 1.0f;
    
    public GlitchImageBlockPass(RenderPassEvent evt)
    {
        renderPassEvent = evt;
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        // 获取后处理配置
        var stack = VolumeManager.instance.stack;
        m_Glitch  = stack.GetComponent<Glitch>();

            
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        // 当效果激活时才执行逻辑
        bool active = m_Glitch.IsActive();
        if (active)
        {
            TimeX += Time.deltaTime;
            if (TimeX > 100)
            {
                TimeX = 0;
            }
            
            cmd.SetGlobalVector(ShaderIDs.Params, new Vector3(TimeX * m_Glitch.Speed.value, m_Glitch.Amount.value, m_Glitch.Fade.value));
            cmd.SetGlobalVector(ShaderIDs.Params2, new Vector4(m_Glitch.BlockLayer1_U.value, m_Glitch.BlockLayer1_V.value, m_Glitch.BlockLayer2_U.value, m_Glitch.BlockLayer2_V.value));
            cmd.SetGlobalVector(ShaderIDs.Params3, new Vector3(m_Glitch.RGBSplitIndensity.value, m_Glitch.BlockLayer1_Indensity.value, m_Glitch.BlockLayer2_Indensity.value));
            
            // 开启故障宏
            cmd.EnableShaderKeyword("_GLITCH");
        }
        else
        {
            // 关闭故障宏
            cmd.EnableShaderKeyword("_GLITCH");
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}