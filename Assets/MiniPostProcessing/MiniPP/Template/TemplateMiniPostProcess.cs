using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aspect.MiniPostProcessing
{

// you should add a path in MiniVolume first,then amend the menu below
[VolumeComponentMenu(MiniVolume.Template + "Template(Please amend this)")]
public class TemplateMiniPostProcess : MiniVolumeComponent
{
	public ClampedIntParameter shaderParam = new ClampedIntParameter(10,1,100); 

	// 可以通过ID来设置Material对应Shader的参数
	internal static readonly int BufferRT1 = Shader.PropertyToID("_BufferRT1");

	Material material;
	const string shaderName = "AspectURP/Mini-PostProcessing/Template";

	protected override void OnEnable()
	{
		base.OnEnable();
		this.defaultName = "Mini PP Label";
		this.InjectionPoint = MiniPostProcessInjectionPoint.BeforePostProcess;
	}

	public override void Setup()
	{
		if (material == null)
		{
			//使用CoreUtils.CreateEngineMaterial来从Shader创建材质
			//CreateEngineMaterial：使用提供的着色器路径创建材质。hideFlags将被设置为 HideFlags.HideAndDontSave。
			material = CoreUtils.CreateEngineMaterial(shaderName);
		}
	}

	//需要注意的是，IsActive方法最好要在组件无效时返回false，避免组件未激活时仍然执行了渲染，
	//原因之前提到过，无论组件是否添加到Volume菜单中或是否勾选，VolumeManager总是会初始化所有的VolumeComponent。
	// 你也可以设置参数，判断是否>0来决定是否激活，前提是默认为0
	public override bool IsActive() => material != null && this.miniActived;

	public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
	{
		if (material == null)
			return;

		material.SetFloat("_ShaderParam",shaderParam.value);
		
		//源纹理到临时RT
		cmd.Blit(source, BufferRT1);
		//临时RT到目标纹理
		cmd.Blit(BufferRT1, destination, material);
		//释放临时RT
		cmd.ReleaseTemporaryRT(BufferRT1);
	}

	public override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		CoreUtils.Destroy(material);
	}
}
}

