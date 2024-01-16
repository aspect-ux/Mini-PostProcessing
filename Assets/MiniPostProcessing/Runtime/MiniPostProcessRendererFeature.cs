using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aspect.MiniPostProcessing
{
/// <summary>
/// 自定义后处理Renderer Feature
/// </summary>
public class MiniPostProcessRendererFeature : ScriptableRendererFeature
{
	// 不同插入点的render pass
	private MiniPostProcessRenderPass m_AfterOpaqueAndSkyPass,m_BeforePostProcessPass,m_AfterPostProcessPass;

	// 所有自定义的VolumeComponent
	private List<MiniVolumeComponent> components;

	// 单独用于after PostProcess的render target
	private RenderTargetHandle m_AfterPostProcessTextureHandle;

	// 初始化Feature资源，每当序列化发生时都会调用
	public override void Create()
	{
		// 1. 从VolumeManager获取所有自定义的VolumeComponent: MiniVolumeComponent
		var stack = VolumeManager.instance.stack;
		components = VolumeManager.instance.baseComponentTypeArray
			.Where(t => t.IsSubclassOf(typeof(MiniVolumeComponent)) && stack.GetComponent(t) != null)
			.Select(t => stack.GetComponent(t) as MiniVolumeComponent)
			.ToList();

		// 2. 初始化不同插入点的render pass
		// 将上述获取的VolumeComponent根据InjectionPoint分成三组，然后分别排序
		var afterOpaqueAndSkyComponents = components
			.Where(c => c.InjectionPoint == MiniVolumeComponent.MiniPostProcessInjectionPoint.AfterOpaqueAndSky)
			.OrderBy(c => c.OrderInPass)
			.ToList();
		m_AfterOpaqueAndSkyPass = new MiniPostProcessRenderPass("Mini PostProcess Pass after Opaque and Sky", afterOpaqueAndSkyComponents);
		m_AfterOpaqueAndSkyPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;

		var beforePostProcessComponents = components
			.Where(c => c.InjectionPoint == MiniVolumeComponent.MiniPostProcessInjectionPoint.BeforePostProcess)
			.OrderBy(c => c.OrderInPass)
			.ToList();
		m_BeforePostProcessPass = new MiniPostProcessRenderPass("Mini PostProcess Pass before PostProcess", beforePostProcessComponents);
		m_BeforePostProcessPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

		var afterPostProcessComponents = components
			.Where(c => c.InjectionPoint == MiniVolumeComponent.MiniPostProcessInjectionPoint.AfterPostProcess)
			.OrderBy(c => c.OrderInPass)
			.ToList();
		m_AfterPostProcessPass = new MiniPostProcessRenderPass("Mini PostProcess Pass after PostProcess", afterPostProcessComponents);
		// 为了确保输入为_AfterPostProcessTexture，这里插入到AfterRendering而不是AfterRenderingPostProcessing
		m_AfterPostProcessPass.renderPassEvent = RenderPassEvent.AfterRendering;

		// 3. 初始化用于after PostProcess的render target
		m_AfterPostProcessTextureHandle.Init("_AfterPostProcessTexture");
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing && components != null)
		{
			foreach (var item in components)
			{
				item.Dispose();
			}
		}
	}

	// 你可以在这里将一个或多个render pass注入到renderer中。
	// 当为每个摄影机设置一次渲染器时，将调用此方法。
	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (renderingData.cameraData.postProcessEnabled)
		{
			// 为每个render pass设置render target
			var source = new RenderTargetHandle(renderer.cameraColorTarget);
			var destination = source;
			
			if (m_AfterOpaqueAndSkyPass.SetupComponents())
			{
				m_AfterOpaqueAndSkyPass.Setup(source, destination);
				renderer.EnqueuePass(m_AfterOpaqueAndSkyPass); //入队
			}

			if (m_BeforePostProcessPass.SetupComponents())
			{
				m_BeforePostProcessPass.Setup(source, destination);
				renderer.EnqueuePass(m_BeforePostProcessPass); //入队
			}

			if (m_AfterPostProcessPass.SetupComponents())
			{
				// 如果下一个Pass是FinalBlit，则输入与输出均为_AfterPostProcessTexture
				source = renderingData.cameraData.resolveFinalTarget ? m_AfterPostProcessTextureHandle : source;
				destination = source;
				m_AfterPostProcessPass.Setup(source, destination);
				renderer.EnqueuePass(m_AfterPostProcessPass); //入队
			}
		}
	}
}
}
