using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CopyTransparentColorPass : ScriptableRenderPass
{
	// 用于FrameDebugger或其他Profiler中显示的名字
	private const string m_ProfilerTag = "Copy Transparent Color";

	private RenderTargetIdentifier m_Source;
	private RenderTargetHandle m_Destination;
	
	public CopyTransparentColorPass(RenderPassEvent evt)
	{
		// 设置Pass的执行顺序
		renderPassEvent = evt;
	}
	
	public void Setup(RenderTargetIdentifier source, RenderTargetHandle destination)
	{
		m_Source = source;
		m_Destination = destination;
	}

	public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
	{
		// 给拷贝目标分配实际显存
		var descriptor = cameraTextureDescriptor;
		descriptor.depthBufferBits = 0;
		cmd.GetTemporaryRT(m_Destination.id, descriptor, FilterMode.Point);
	}

	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
	{
		// 执行拷贝命令
		CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
		cmd.Blit(m_Source, m_Destination.Identifier());
		context.ExecuteCommandBuffer(cmd);
		CommandBufferPool.Release(cmd);
	}

	public override void FrameCleanup(CommandBuffer cmd)
	{
		if (m_Destination != RenderTargetHandle.CameraTarget)
		{
			// 释放拷贝目标
			cmd.ReleaseTemporaryRT(m_Destination.id);
			m_Destination = RenderTargetHandle.CameraTarget;
		}
	}
}