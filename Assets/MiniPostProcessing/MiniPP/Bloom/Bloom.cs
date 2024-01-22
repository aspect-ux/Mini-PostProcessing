using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aspect.MiniPostProcessing
{

// you should add a path in MiniVolume first,then amend the menu below
[VolumeComponentMenu(MiniVolume.Template + "Mini-Bloom")]
public class Bloom : MiniVolumeComponent
{
	public ClampedFloatParameter blurSpread = new ClampedFloatParameter(0.6f, 0.2f, 3.0f);
	public ClampedIntParameter iterations = new ClampedIntParameter(1,0,5); 
	public ClampedIntParameter downSample = new ClampedIntParameter(2, 1, 8);
	 public ClampedFloatParameter luminanceThreshold = new ClampedFloatParameter(0.6f,0,1);

	// 也可以通过ID来设置Material对应Shader的参数
	internal static readonly int BufferRT1 = Shader.PropertyToID("_BufferRT1");
	internal static readonly int BufferRT2 = Shader.PropertyToID("_BufferRT2");

	Material bloomMaterial,gaussianBlurMaterial;
	const string shaderName = "AspectURP/Mini-PostProcessing/Bloom0";

	protected override void OnEnable()
	{
		base.OnEnable();
		this.defaultName = "Mini-Bloom0";
		this.InjectionPoint = MiniPostProcessInjectionPoint.BeforePostProcess;
	}

	public override void Setup()
	{
		if (bloomMaterial == null)
		{
			//使用CoreUtils.CreateEngineMaterial来从Shader创建材质
			//CreateEngineMaterial：使用提供的着色器路径创建材质。hideFlags将被设置为 HideFlags.HideAndDontSave。
			bloomMaterial = CoreUtils.CreateEngineMaterial(shaderName);
			gaussianBlurMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("AspectURP/Mini-PostProcessing/GaussianBlur"));
		}
	}

	//需要注意的是，IsActive方法最好要在组件无效时返回false，避免组件未激活时仍然执行了渲染，
	//原因之前提到过，无论组件是否添加到Volume菜单中或是否勾选，VolumeManager总是会初始化所有的VolumeComponent。
	// 你也可以设置参数，判断是否>0来决定是否激活，前提是默认为0
	public override bool IsActive() => bloomMaterial != null && this.miniActived;

	public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
	{
		if (bloomMaterial == null)
			return;

		var data = renderingData.cameraData.cameraTargetDescriptor;
			
		var width = data.width/ downSample.value;
		var height = data.height / downSample.value;
		
		int destination0 = BufferRT1;
		int destination1 = BufferRT2;
		
		cmd.SetGlobalFloat("_LuminanceThreshold",luminanceThreshold.value);
		
		// 先存到临时的地方
		cmd.GetTemporaryRT(destination0, width, height, 0, FilterMode.Trilinear, RenderTextureFormat.ARGB32);
		cmd.Blit(source, destination0,bloomMaterial,0);

		for (int i = 0; i < iterations.value; ++i)
		{
			cmd.SetGlobalFloat("_BlurSpread", 1.0f + i * blurSpread.value);

			// 第一轮
			cmd.GetTemporaryRT(destination1, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
			cmd.Blit(destination0, destination1, gaussianBlurMaterial, 0);
			cmd.ReleaseTemporaryRT(destination0);

			destination0 = destination1;
			// 第二轮
			cmd.GetTemporaryRT(destination1, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
			cmd.Blit(destination0, destination1, gaussianBlurMaterial, 1);
			
			cmd.ReleaseTemporaryRT(destination0);

			destination0 = destination1;
		}

		cmd.SetGlobalTexture("_BloomTex",destination0);
		cmd.Blit(destination0, destination, bloomMaterial , 1);
		
		cmd.ReleaseTemporaryRT(destination0);
	}

	public override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		CoreUtils.Destroy(bloomMaterial);
		CoreUtils.Destroy(gaussianBlurMaterial);
	}
}
}

