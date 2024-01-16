using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aspect.MiniPostProcessing
{
	[VolumeComponentMenu("AspectURP/Mini-PostProcessing/Glitch")]
	public class Glitch : MiniVolumeComponent
	{
	   [Range(0.0f, 1.0f)] 
		public FloatParameter Fade = new FloatParameter(1); 
	
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
		public FloatParameter RGBSplitIndensity = new FloatParameter(0.5f); 

		private Material material;

		private const string ShaderName = "AspectURP/Mini-PostProcessing/Glitch";

		public override int OrderInPass => 10;

		public override void Setup()
		{
			if (material == null)
				material = CoreUtils.CreateEngineMaterial(ShaderName);
		}

		public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
		{
			if (material == null)
				return;
			material.SetVector("_Param", new Vector3(Speed.value,Amount.value,Fade.value));
			material.SetVector("_Param2", new Vector4(BlockLayer1_V.value,BlockLayer2_U.value,BlockLayer2_V.value,BlockLayer1_U.value));
			material.SetVector("_Param3", new Vector3(RGBSplitIndensity.value,BlockLayer1_Indensity.value,BlockLayer2_Indensity.value));

			cmd.Blit(source, destination, material, 0);
		}

		public override bool IsActive() => material != null;// && (Intensity.value.x > 0f || Intensity.value.y > 0f);

		public override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			CoreUtils.Destroy(material);
		}
		
	}
}