using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aspect.MiniPostProcessing{
	[Serializable]
	internal class HBAOSettings{
		[SerializeField] internal float Intensity = 0.5f;
		[SerializeField] internal float Radius = 0.5f;
		[SerializeField] internal float MaxRadiusPixels = 32f;
		[SerializeField] internal float AngleBias = 0.1f;
	}


	[DisallowMultipleRendererFeature("HBAO")]
	public class HBAORendererFeature : ScriptableRendererFeature{
		class HBAORenderPass : ScriptableRenderPass
		{
			private HBAOSettings mSettings;

			private Material mMaterial;

			private ProfilingSampler mProfilingSampler = new ProfilingSampler("HBAO");

			private RenderTextureDescriptor mHBAODescriptor;

			/* 版本差异参考：https://zhuanlan.zhihu.com/p/675758658
			private RTHandle mSourceTexture;
			private RTHandle mDestinationTexture;*/
			
			// 
			//private RenderTargetHandle mSourceTexture,mDestinationTexture;
			
			private RenderTargetIdentifier mSourceTexture,mDestinationTexture;

			private static readonly int mProjectionParams2ID = Shader.PropertyToID("_ProjectionParams2"),
				mCameraViewTopLeftCornerID = Shader.PropertyToID("_CameraViewTopLeftCorner"),
				mCameraViewXExtentID = Shader.PropertyToID("_CameraViewXExtent"),
				mCameraViewYExtentID = Shader.PropertyToID("_CameraViewYExtent"),
				mHBAOParamsID = Shader.PropertyToID("_HBAOParams"),
				mRadiusPixelID = Shader.PropertyToID("_RadiusPixel"),
				mSourceSizeID = Shader.PropertyToID("_SourceSize"),
				mHBAOBlurRadiusID = Shader.PropertyToID("_HBAOBlurRadius");

			// URP 14.0 RTHandle ---- 之前 RenderTargetHandle
			//private RTHandle mHBAOTexture0, mHBAOTexture1;
			//private RenderTargetHandle mHBAOTexture0, mHBAOTexture1;
			private RenderTargetIdentifier mHBAOTexture0, mHBAOTexture1;
			
			int _renderTargetId0,_renderTargetId1;

			private const string mHBAOTexture0Name = "_HBAO_OcclusionTexture0",
				mHBAOTexture1Name = "_HBAO_OcclusionTexture1";


			internal HBAORenderPass() {
				mSettings = new HBAOSettings();
			}

			internal bool Setup(ref HBAOSettings featureSettings, ref Material material) {
				mMaterial = material;
				mSettings = featureSettings;

				ConfigureInput(ScriptableRenderPassInput.Normal);

				return mMaterial != null;
			}

			public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
				var renderer = renderingData.cameraData.renderer;
				mHBAODescriptor = renderingData.cameraData.cameraTargetDescriptor;
				mHBAODescriptor.msaaSamples = 1;
				mHBAODescriptor.depthBufferBits = 0;

				// 设置Material属性
				// 发送参数
				Matrix4x4 proj = renderingData.cameraData.GetProjectionMatrix();

				// 计算proj逆矩阵，即从裁剪空间变换到世界空间
				Matrix4x4 projInv = proj.inverse;

				// 计算视角空间下，近平面四个角的坐标
				Vector4 topLeftCorner = projInv.MultiplyPoint(new Vector4(-1.0f, 1.0f, -1.0f, 1.0f));
				Vector4 topRightCorner = projInv.MultiplyPoint(new Vector4(1.0f, 1.0f, -1.0f, 1.0f));
				Vector4 bottomLeftCorner = projInv.MultiplyPoint(new Vector4(-1.0f, -1.0f, -1.0f, 1.0f));

				// 计算相机近平面上方向向量
				Vector4 cameraXExtent = topRightCorner - topLeftCorner;
				Vector4 cameraYExtent = bottomLeftCorner - topLeftCorner;

				// 发送ReconstructViewPos参数
				var camera = renderingData.cameraData.camera;
				var near = camera.nearClipPlane;

				mMaterial.SetVector(mCameraViewTopLeftCornerID, topLeftCorner);
				mMaterial.SetVector(mCameraViewXExtentID, cameraXExtent);
				mMaterial.SetVector(mCameraViewYExtentID, cameraYExtent);
				mMaterial.SetVector(mProjectionParams2ID, new Vector4(1.0f / near, renderingData.cameraData.worldSpaceCameraPos.x, renderingData.cameraData.worldSpaceCameraPos.y, renderingData.cameraData.worldSpaceCameraPos.z));

				// 发送HBAO参数
				var tanHalfFovY = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
				mMaterial.SetVector(mHBAOParamsID, new Vector4(mSettings.Intensity, mSettings.Radius * 1.5f, mSettings.MaxRadiusPixels, mSettings.AngleBias));
				mMaterial.SetFloat(mRadiusPixelID, renderingData.cameraData.camera.pixelHeight * mSettings.Radius * 1.5f / tanHalfFovY / 2.0f);

				//TODO:
				_renderTargetId0 = Shader.PropertyToID("_ImageFilterResult");
				_renderTargetId1 = Shader.PropertyToID("_ImageFilterResult");
				// 分配RTHandle
				//RenderingUtils.ReAllocateIfNeeded(ref mHBAOTexture0, mHBAODescriptor, name: mHBAOTexture0Name);
				//RenderingUtils.ReAllocateIfNeeded(ref mHBAOTexture1, mHBAODescriptor, name: mHBAOTexture1Name);
				cmd.GetTemporaryRT(_renderTargetId0,mHBAODescriptor.width,mHBAODescriptor.height);
				cmd.GetTemporaryRT(_renderTargetId0,mHBAODescriptor.width,mHBAODescriptor.height);
				

				// 配置目标和清除
				ConfigureTarget(renderer.cameraColorTarget);
				ConfigureClear(ClearFlag.None, Color.white);
			}

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
				if (mMaterial == null) {
					Debug.LogErrorFormat("{0}.Execute(): Missing material. ScreenSpaceAmbientOcclusion pass will not execute. Check for missing reference in the renderer resources.", GetType().Name);
					return;
				}

				var cmd = CommandBufferPool.Get();
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				mSourceTexture = renderingData.cameraData.renderer.cameraColorTarget;
				mDestinationTexture = mSourceTexture;

				using (new ProfilingScope(cmd, mProfilingSampler)) {
					cmd.SetGlobalVector(mSourceSizeID, new Vector4(mHBAODescriptor.width, mHBAODescriptor.height, 1.0f / mHBAODescriptor.width, 1.0f / mHBAODescriptor.height));

					// Blit
					CoreUtils.SetRenderTarget(cmd, _renderTargetId0);
					//https://docs.unity.cn/cn/2019.4/ScriptReference/Rendering.CommandBuffer.DrawProcedural.html
					// compute shader
					cmd.DrawProcedural(Matrix4x4.identity, mMaterial, 0, MeshTopology.Triangles, 3);
					//cmd.Blit(mSourceTexture,_renderTargetId0,mMaterial,0);
					
					// Horizontal Blur
					cmd.SetGlobalVector(mHBAOBlurRadiusID, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
					cmd.Blit(_renderTargetId0,_renderTargetId1,mMaterial,1);
					//Blitter.BlitCameraTexture(cmd, mHBAOTexture0, mHBAOTexture1, mMaterial, 1);

					// Final Pass & Vertical Blur
					cmd.SetGlobalVector(mHBAOBlurRadiusID, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
					cmd.Blit(_renderTargetId0,mDestinationTexture,mMaterial,2);
					//Blitter.BlitCameraTexture(cmd, mHBAOTexture1, mDestinationTexture, mMaterial, 2);
				}

				context.ExecuteCommandBuffer(cmd);
				CommandBufferPool.Release(cmd);
			}

			public override void OnCameraCleanup(CommandBuffer cmd) {
				/*
				mSourceTexture = null;
				mDestinationTexture = null;*/
				//cmd.ReleaseTemporaryRT(mSourceTexture.id);
				
				cmd.ReleaseTemporaryRT(_renderTargetId0);
				cmd.ReleaseTemporaryRT(_renderTargetId1);
			}

			public void Dispose() {
				// 释放RTHandle
				//mHBAOTexture0?.Release();
				//mHBAOTexture1?.Release();
			}
		}
		
		[SerializeField] private HBAOSettings mSettings = new HBAOSettings();

		private Shader mShader;
		private const string mShaderName = "Hidden/AO/HBAO";

		private HBAORenderPass mHBAOPass;
		private Material mMaterial;


		public override void Create() {
			if (mHBAOPass == null) {
				mHBAOPass = new HBAORenderPass();
				mHBAOPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
			}
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
			if (renderingData.cameraData.postProcessEnabled) {
				if (!GetMaterials()) {
					Debug.LogErrorFormat("{0}.AddRenderPasses(): Missing material. {1} render pass will not be added.", GetType().Name, name);
					return;
				}

				bool shouldAdd = mHBAOPass.Setup(ref mSettings, ref mMaterial);

				if (shouldAdd)
					renderer.EnqueuePass(mHBAOPass);
			}
		}

		protected override void Dispose(bool disposing) {
			CoreUtils.Destroy(mMaterial);

			mHBAOPass?.Dispose();
			mHBAOPass = null;
		}

		private bool GetMaterials() {
			if (mShader == null)
				mShader = Shader.Find(mShaderName);
			if (mMaterial == null && mShader != null)
				mMaterial = CoreUtils.CreateEngineMaterial(mShader);
			return mMaterial != null;
		}
	}
	
	
}