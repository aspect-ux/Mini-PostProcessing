using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aspect.MiniPostProcessing
{

[VolumeComponentMenu(MiniVolume.Pixelate + "Pixelize(像素化)")]
public class Pixelate : MiniVolumeComponent
{
	public BoolParameter Enable = new(false);
	public ClampedIntParameter PixelSize = new ClampedIntParameter(10,1,100); 

	internal static readonly int BufferRT1 = Shader.PropertyToID("_BufferRT1");

	Material material;
	const string shaderName = "AspectURP/Mini-PostProcessing/Pixelate";

	protected override void OnEnable()
	{
		base.OnEnable();
		this.defaultName = "Mini-Pixelate";
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
	 public override bool IsActive() => material != null && Enable.value && PixelSize.value > 1 && this.miniActived;

	public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
	{
		if (material == null)
			return;

		material.SetFloat("_PixelSize",PixelSize.value);
		
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
		CoreUtils.Destroy(material); //在Dispose中销毁材质
	}
}
}

