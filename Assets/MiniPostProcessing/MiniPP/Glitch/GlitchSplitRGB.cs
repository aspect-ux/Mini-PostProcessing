using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aspect.MiniPostProcessing
{
	[VolumeComponentMenu(MiniVolume.Glitch + "GlitchSplitRGB(故障2)")]	
	public class GlitchSplitRGB : MiniVolumeComponent
	{
		public FloatParameter Amplitude = new(1);
		public FloatParameter Amount = new(1);

		private Material material;

		private const string ShaderName = "AspectURP/Mini-PostProcessing/GlitchSplitRGB";
		private static readonly int amplitudeID = Shader.PropertyToID("_Amplitude");
		private static readonly int amountID = Shader.PropertyToID("_Amount");
		
		//在同一个插入点可能会存在多个后处理组件，所以还需要一个排序编号来确定谁先谁后：
		//在InjectionPoint中的渲染顺序
		public override int OrderInPass => 10;
		
		protected override void OnEnable() {
			base.OnEnable();
			this.defaultName = "GlitchSplitRGB";
			this.InjectionPoint = MiniPostProcessInjectionPoint.BeforePostProcess;
		}

		public override void Setup()
		{
			if (material == null)
				material = CoreUtils.CreateEngineMaterial(ShaderName);
		}

		public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
		{
			if (material == null)
				return;

			material.SetFloat("_Amplitude",Amplitude.value);
			material.SetFloat("_Amount", Amount.value);

			cmd.Blit(source, destination, material);
		}

		public override bool IsActive() => material != null && this.miniActived;

		public override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			CoreUtils.Destroy(material);
		}
		
	}
}
