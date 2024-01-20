using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aspect.MiniPostProcessing
{
   [VolumeComponentMenu(MiniVolume.Glitch + "GlitchImageBlock(故障)")]
	public class GlitchImageBlock : MiniVolumeComponent
	{
		[Range(0.0f, 1.0f)]
		public FloatParameter Fade = new FloatParameter(0f);

		[Range(0.0f, 1.0f)]
		public FloatParameter Speed = new FloatParameter(0.5f);

		[Range(0.0f, 10.0f)]
		public FloatParameter Amount = new FloatParameter(1f);

		[Range(0.0f, 50.0f)]
		public FloatParameter BlockLayer1_U = new FloatParameter(9f);

		[Range(0.0f, 50.0f)]
		public FloatParameter BlockLayer1_V = new FloatParameter(9f);

		[Range(0.0f, 50.0f)]
		public FloatParameter BlockLayer2_U = new FloatParameter(5f);

		[Range(0.0f, 50.0f)]
		public FloatParameter BlockLayer2_V = new FloatParameter(5f);

		[Range(0.0f, 50.0f)]
		public FloatParameter BlockLayer1_Indensity = new FloatParameter(8f);

		[Range(0.0f, 50.0f)]
		public FloatParameter BlockLayer2_Indensity = new FloatParameter(4f);

		[Range(0.0f, 50.0f)]
		public FloatParameter RGBSplitIndensity = new FloatParameter(5f);

		
		public BoolParameter BlockVisualizeDebug = new BoolParameter(false);
		
		//在同一个插入点可能会存在多个后处理组件，所以还需要一个排序编号来确定谁先谁后：
		//在InjectionPoint中的渲染顺序
		public override int OrderInPass => 1;
		
		static class ShaderIDs
		{
			internal static readonly int Params = Shader.PropertyToID("_Params");
			internal static readonly int Params2 = Shader.PropertyToID("_Params2");
			internal static readonly int Params3 = Shader.PropertyToID("_Params3");
		}
		
		internal static readonly int BufferRT1 = Shader.PropertyToID("_BufferRT1");
		
		private const string PROFILER_TAG = "GlitchImageBlock";
		Material material;
		const string shaderName = "AspectURP/Mini-PostProcessing/GlitchSplitRGB";
		private float TimeX = 1.0f;

		protected override void OnEnable()
		{
			base.OnEnable();
			this.defaultName = "GlitchImageBlock";
			this.InjectionPoint = MiniPostProcessInjectionPoint.AfterPostProcess;
		}

		public override void Setup()
		{
			if (material == null)
			{
				//使用CoreUtils.CreateEngineMaterial来从Shader创建材质
				//CreateEngineMaterial：使用提供的着色器路径创建材质。hideFlags将被设置为 HideFlags.HideAndDontSave。
				material = CoreUtils.CreateEngineMaterial(shaderName);
				this.active = true;
			}
		}

		public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
		{
			cmd.BeginSample(PROFILER_TAG);

			TimeX += Time.deltaTime;
			if (TimeX > 100)
			{
				TimeX = 0;
			}

			material.SetVector(ShaderIDs.Params, new Vector3(TimeX * Speed.value, Amount.value,Fade.value));
			material.SetVector(ShaderIDs.Params2, new Vector4(BlockLayer1_U.value, (float)BlockLayer1_V, BlockLayer2_U.value, BlockLayer2_V.value));
			material.SetVector(ShaderIDs.Params3, new Vector3(RGBSplitIndensity.value, BlockLayer1_Indensity.value, BlockLayer2_Indensity.value));

			//源纹理到临时RT
			cmd.Blit(source, BufferRT1);
			//临时RT到目标纹理
			cmd.Blit(BufferRT1, destination, material);
			//释放临时RT
			cmd.ReleaseTemporaryRT(BufferRT1);
			
			cmd.EndSample(PROFILER_TAG);
		}

		public override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			CoreUtils.Destroy(material); //在Dispose中销毁材质
		}
		
		public override bool IsActive() => material != null && this.miniActived;
	}
}