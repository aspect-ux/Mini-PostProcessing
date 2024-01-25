using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class SSAORendererFeature : ScriptableRendererFeature
{
	class SSAORenderPass : ScriptableRenderPass
	{
		private static readonly string shaderName = "AspectURP/Mini-PostProcessing/AO/SSAO";
		private static readonly string shaderName1 = "AspectURP/Mini-PostProcessing/GaussianBlur";

		private Material ssaoMaterial, gaussianBlurMaterial;

		private Settings _settings;

		RenderTargetIdentifier currentTarget;       // 用来获取相机rt的id
		
		// 用来暂存纹理
		static readonly int TempTargetId0 = Shader.PropertyToID("_TempTargetColorTint0");      
		static readonly int TempTargetId1 = Shader.PropertyToID("_TempTargetColorTint1");  
		static readonly int TempTargetId2 = Shader.PropertyToID("_TempTargetColorTint2");    
		
		static readonly string k_RenderTag = "SSAO";        
		public SSAORenderPass(Settings settings)
		{
			_settings = settings;
			
			this.renderPassEvent = settings.renderPassEvent;
			Init();
		}
		void Init()
		{
			var shader = Shader.Find(shaderName);
			ssaoMaterial = CoreUtils.CreateEngineMaterial(shader);
			
			//TODO:
			gaussianBlurMaterial = CoreUtils.CreateEngineMaterial(Shader.Find(shaderName1));
		}
		
		public void SetTarget(ScriptableRenderer renderer)
		{
			currentTarget = renderer.cameraColorTarget;
		}
		
		public void GenerateAOSampleKernel()
		{
			if (_settings.sampleKernelCount == _settings.sampleKernelList.Count)
				return;
			_settings.sampleKernelList.Clear();
			for (int i = 0; i < _settings.sampleKernelCount; i++)
			{
				var vec = new Vector4(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(0, 1.0f), 1.0f);
				vec.Normalize();
				var scale = (float)i / _settings.sampleKernelCount;
				//使分布符合二次方程的曲线
				scale = Mathf.Lerp(0.01f, 1.0f, scale * scale);
				vec *= scale;
				_settings.sampleKernelList.Add(vec);
			}
		}
		// This method is called before executing the render pass.
		// It can be used to configure render targets and their clear state. Also to create temporary render target textures.
		// When empty this render pass will render to the active camera render target.
		// You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
		// The render pipeline will ensure target setup and clearing happens in a performant manner.
		public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
		{
			ConfigureInput(ScriptableRenderPassInput.Normal);
			
			
			Matrix4x4 view = renderingData.cameraData.GetViewMatrix();  
			Matrix4x4 proj = renderingData.cameraData.GetProjectionMatrix();  
			Matrix4x4 vp = proj * view;  

			// 将camera view space 的平移置为0，用来计算world space下相对于相机的vector  
			Matrix4x4 cview = view;  
			cview.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));  
			Matrix4x4 cviewProj = proj * cview;  

			// 计算viewProj逆矩阵，即从裁剪空间变换到世界空间  
			Matrix4x4 cviewProjInv = cviewProj.inverse;  

			// 计算世界空间下，近平面四个角的坐标  
			var near = renderingData.cameraData.camera.nearClipPlane;  
			// Vector4 topLeftCorner = cviewProjInv * new Vector4(-near, near, -near, near);  
			// Vector4 topRightCorner = cviewProjInv * new Vector4(near, near, -near, near);    // Vector4 bottomLeftCorner = cviewProjInv * new Vector4(-near, -near, -near, near);    Vector4 topLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1.0f, 1.0f, -1.0f, 1.0f));  
			Vector4 topRightCorner = cviewProjInv.MultiplyPoint(new Vector4(1.0f, 1.0f, -1.0f, 1.0f));  
			Vector4 bottomLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1.0f, -1.0f, -1.0f, 1.0f));  

			/*
			// 计算相机近平面上方向向量  
			Vector4 cameraXExtent = topRightCorner - topLeftCorner;  
			Vector4 cameraYExtent = bottomLeftCorner - topLeftCorner;  

			near = renderingData.cameraData.camera.nearClipPlane;  

			mMaterial.SetVector(mCameraViewTopLeftCornerID, topLeftCorner);  
			mMaterial.SetVector(mCameraViewXExtentID, cameraXExtent);  
			mMaterial.SetVector(mCameraViewYExtentID, cameraYExtent);  
			mMaterial.SetVector(mProjectionParams2ID, new Vector4(1.0f / near, renderingData.cameraData.worldSpaceCameraPos.x, renderingData.cameraData.worldSpaceCameraPos.y, renderingData.cameraData.worldSpaceCameraPos.z));  
			*/
		}

		// Here you can implement the rendering logic.
		// Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
		// https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
		// You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			GenerateAOSampleKernel();

			var cmd = CommandBufferPool.Get(k_RenderTag);
			Render(cmd, ref renderingData);
			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		void Render(CommandBuffer cmd, ref RenderingData renderingData)
		{
			var source = currentTarget;
			int aoRT = TempTargetId0;
			int blurRT = TempTargetId1;
			
			// 获取相机RT Descriptor
			var data = renderingData.cameraData.cameraTargetDescriptor;
			int rtW = data.width >> _settings.downSample;
			int rtH = data.height >> _settings.downSample;
			
			//AO
			//RenderTexture aoRT = RenderTexture.GetTemporary(rtW,rtH,0);
			cmd.GetTemporaryRT(aoRT, data);
			ssaoMaterial.SetVectorArray("_SampleKernelArray", _settings.sampleKernelList.ToArray());
			ssaoMaterial.SetFloat("_RangeStrength", _settings.rangeStrength);
			ssaoMaterial.SetFloat("_AOStrength", _settings.aoStrength);
			ssaoMaterial.SetFloat("_SampleKernelCount", _settings.sampleKernelList.Count);
			ssaoMaterial.SetFloat("_SampleKeneralRadius",_settings.sampleKeneralRadius);
			ssaoMaterial.SetFloat("_DepthBiasValue",_settings.depthBiasValue);
			ssaoMaterial.SetTexture("_NoiseTex", _settings.Nosie);
			
			// Generate AO Pass
			cmd.Blit(source, aoRT,ssaoMaterial,0);
			
			//Blur
			//RenderTexture blurRT = RenderTexture.GetTemporary(rtW,rtH,0);
			cmd.GetTemporaryRT(blurRT, data);
			
			ssaoMaterial.SetFloat("_BilaterFilterFactor", 1.0f -  _settings.bilaterFilterStrength);
			ssaoMaterial.SetVector("_BlurRadius", new Vector4( _settings.blurRadius, 0, 0, 0));
			
			//TODO: Blur Bilateral Filter
			cmd.Blit(aoRT, blurRT, ssaoMaterial, 1);

			ssaoMaterial.SetVector("_BlurRadius", new Vector4(0,  _settings.blurRadius, 0, 0));
			
			// cmd.GetTemporaryRT 和 RenderTexture.GetTemporary混用会出问题
			//cmd.GetTemporaryRT(destination0, rtW, rtH, 0, FilterMode.Trilinear, RenderTextureFormat.ARGB32);
			
			//RenderTexture tempRT = RenderTexture.GetTemporary(rtW,rtH,0);
			int tempRT = TempTargetId2;
			cmd.GetTemporaryRT(tempRT, data);
			if (_settings.onlyShowAO)
			{
				// Bilateral Filter
				cmd.Blit(blurRT, tempRT, ssaoMaterial, 1);
			}
			else
			{
				cmd.Blit(blurRT, aoRT, ssaoMaterial, 1);
				//ssaoMaterial.SetTexture("_AOTex", aoRT);
				cmd.SetGlobalTexture("_AOTex",aoRT);
				cmd.Blit(source, tempRT, ssaoMaterial, 2);
			}

			// 渲染到相机上
			cmd.Blit(tempRT,source);
			
			cmd.ReleaseTemporaryRT(tempRT);
			cmd.ReleaseTemporaryRT(aoRT);
			cmd.ReleaseTemporaryRT(blurRT);
		}

		// Cleanup any allocated resources that were created during the execution of this render pass.
		public override void OnCameraCleanup(CommandBuffer cmd)
		{
		}
	}

	SSAORenderPass m_ScriptablePass;

	[Serializable]
	public class Settings
	{	
		[Range(0f,1f)]
		public float aoStrength = 0f; 
		[Range(4, 64)]
		public int sampleKernelCount = 64;
		
		[HideInInspector]
		public List<Vector4> sampleKernelList = new List<Vector4>();
		
		[Range(0.0001f,10f)]
		public float sampleKeneralRadius = 0.01f;
		
		[Range(0.0001f,1f)]
		public float rangeStrength = 0.001f;
		
		public float depthBiasValue;
		
		public Texture Nosie;//噪声贴图

		[Range(0, 2)]
		public int downSample = 0;

		[Range(1, 4)]
		public int blurRadius = 2;
		[Range(0, 0.2f)]
		public float bilaterFilterStrength = 0.2f;
		public bool onlyShowAO = false;

		public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
	}

	public Settings ssaoSettings;
	
	/// <inheritdoc/>
	public override void Create()
	{
		m_ScriptablePass = new SSAORenderPass(ssaoSettings);
	}

	// Here you can inject one or multiple render passes in the renderer.
	// This method is called when setting up the renderer once per-camera.
	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		m_ScriptablePass.SetTarget(renderer);
		renderer.EnqueuePass(m_ScriptablePass);
	}
}


