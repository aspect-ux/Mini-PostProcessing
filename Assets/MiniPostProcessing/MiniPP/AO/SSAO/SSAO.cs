using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SSAO{
	[Serializable]
	internal class SSAOSettings{
		[SerializeField] internal float Intensity = 0.5f;
		[SerializeField] internal float Radius = 0.25f;
		[SerializeField] internal float Falloff = 100f;
	}

	[DisallowMultipleRendererFeature("SSAO")]
	public class SSAO : ScriptableRendererFeature{
		[SerializeField] private SSAOSettings mSettings = new SSAOSettings();

		private const string mShaderName = "Hidden/AO/SSAO";
		private Shader mShader;

		private SSAOPass mSSAOPass;
		private Material mMaterial;

		public override void Create() {
			if (mSSAOPass == null) {
				mSSAOPass = new SSAOPass();
				mSSAOPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
			}
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
			// 当前渲染的相机支持后处理
			if (renderingData.cameraData.postProcessEnabled) {
				if (!GetMaterials()) {
					Debug.LogErrorFormat("{0}.AddRenderPasses(): Missing material. {1} render pass will not be added.", GetType().Name, name);
					return;
				}

				bool shouldAdd = mSSAOPass.Setup(ref mSettings, ref renderer, ref mMaterial);

				if (shouldAdd)
					renderer.EnqueuePass(mSSAOPass);
			}
		}

		protected override void Dispose(bool disposing) {
			CoreUtils.Destroy(mMaterial);

			mSSAOPass?.Dispose();
			mSSAOPass = null;
		}

		private bool GetMaterials() {
			if (mShader == null)
				mShader = Shader.Find(mShaderName);
			if (mMaterial == null && mShader != null)
				mMaterial = CoreUtils.CreateEngineMaterial(mShader);
			return mMaterial != null;
		}

		class SSAOPass : ScriptableRenderPass{
			private SSAOSettings mSettings;

			private Material mMaterial;
			private ScriptableRenderer mRenderer;

			private ProfilingSampler mProfilingSampler = new ProfilingSampler("SSAO");
			private RenderTextureDescriptor mSSAODescriptor;

			private RTHandle mSourceTexture;
			private RTHandle mDestinationTexture;

			private static readonly int mProjectionParams2ID = Shader.PropertyToID("_ProjectionParams2"),
				mCameraViewTopLeftCornerID = Shader.PropertyToID("_CameraViewTopLeftCorner"),
				mCameraViewXExtentID = Shader.PropertyToID("_CameraViewXExtent"),
				mCameraViewYExtentID = Shader.PropertyToID("_CameraViewYExtent"),
				mSSAOParamsID = Shader.PropertyToID("_SSAOParams"),
				mSSAOBlurRadiusID = Shader.PropertyToID("_SSAOBlurRadius");

			private RTHandle mSSAOTexture0, mSSAOTexture1;

			private const string mSSAOTexture0Name = "_SSAO_OcclusionTexture0",
				mSSAOTexture1Name = "_SSAO_OcclusionTexture1";

			internal SSAOPass() {
				mSettings = new SSAOSettings();
			}

			internal bool Setup(ref SSAOSettings featureSettings, ref ScriptableRenderer renderer, ref Material material) {
				mMaterial = material;
				mRenderer = renderer;
				mSettings = featureSettings;

				ConfigureInput(ScriptableRenderPassInput.Normal);

				return mMaterial != null;
			}

			public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
				// 发送参数
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
				// Vector4 topRightCorner = cviewProjInv * new Vector4(near, near, -near, near);
				// Vector4 bottomLeftCorner = cviewProjInv * new Vector4(-near, -near, -near, near);
				Vector4 topLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1.0f, 1.0f, -1.0f, 1.0f));
				Vector4 topRightCorner = cviewProjInv.MultiplyPoint(new Vector4(1.0f, 1.0f, -1.0f, 1.0f));
				Vector4 bottomLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1.0f, -1.0f, -1.0f, 1.0f));

				// 计算相机近平面上方向向量
				Vector4 cameraXExtent = topRightCorner - topLeftCorner;
				Vector4 cameraYExtent = bottomLeftCorner - topLeftCorner;

				near = renderingData.cameraData.camera.nearClipPlane;

				// 发送ReconstructViewPos参数
				mMaterial.SetVector(mCameraViewTopLeftCornerID, topLeftCorner);
				mMaterial.SetVector(mCameraViewXExtentID, cameraXExtent);
				mMaterial.SetVector(mCameraViewYExtentID, cameraYExtent);
				mMaterial.SetVector(mProjectionParams2ID, new Vector4(1.0f / near, renderingData.cameraData.worldSpaceCameraPos.x, renderingData.cameraData.worldSpaceCameraPos.y, renderingData.cameraData.worldSpaceCameraPos.z));

				// 发送SSAO参数
				mMaterial.SetVector(mSSAOParamsID, new Vector4(mSettings.Intensity, mSettings.Radius * 1.5f, mSettings.Falloff));

				mSSAODescriptor = renderingData.cameraData.cameraTargetDescriptor;
				mSSAODescriptor.msaaSamples = 1;
				mSSAODescriptor.depthBufferBits = 0;
				// mSSAODescriptor.colorFormat = RenderTextureFormat.ARGB32;

				// 分配纹理
				//RenderingUtils.SupportsRenderTextureFormat()
				//RenderingUtils.ReAllocateIfNeeded(ref mSSAOTexture0, mSSAODescriptor, name: mSSAOTexture0Name);
				//RenderingUtils.ReAllocateIfNeeded(ref mSSAOTexture1, mSSAODescriptor, name: mSSAOTexture1Name);
				
				// 配置目标和清除
				ConfigureTarget(mRenderer.cameraColorTarget);
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

				//mSourceTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
				//mDestinationTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;


				using (new ProfilingScope(cmd, mProfilingSampler)) {
					cmd.SetGlobalVector("_SourceSize", new Vector4(mSSAODescriptor.width, mSSAODescriptor.height, 1.0f / mSSAODescriptor.width, 1.0f / mSSAODescriptor.height));

					// SSAO
					CoreUtils.SetRenderTarget(cmd, mSSAOTexture0);
					cmd.DrawProcedural(Matrix4x4.identity, mMaterial, 0, MeshTopology.Triangles, 3);

					// Horizontal Blur
					cmd.SetGlobalVector(mSSAOBlurRadiusID, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
					Blitter.BlitCameraTexture(cmd, mSSAOTexture0, mSSAOTexture1, mMaterial, 1);

					// Vertical Blur
					cmd.SetGlobalVector(mSSAOBlurRadiusID, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
					Blitter.BlitCameraTexture(cmd, mSSAOTexture1, mSSAOTexture0, mMaterial, 1);

					// Final Pass
					Blitter.BlitCameraTexture(cmd, mSSAOTexture0, mDestinationTexture, mMaterial, 2);
				}

				context.ExecuteCommandBuffer(cmd);
				CommandBufferPool.Release(cmd);
			}


			public override void OnCameraCleanup(CommandBuffer cmd) {
				mSourceTexture = null;
				mDestinationTexture = null;
			}

			public void Dispose() {
				mSSAOTexture0?.Release();
				mSSAOTexture1?.Release();
			}
		}
	}
}