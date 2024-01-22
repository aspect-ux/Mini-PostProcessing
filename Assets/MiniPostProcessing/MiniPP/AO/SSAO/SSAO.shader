Shader "AspectURP/Mini-PostProcessing/AO/SSAO"
{
    Properties
    {
        [HideInInspector]_MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {	
    	Tags
        {
           "RenderPipeline" = "UniversalRenderPipeline" "RenderType"="Opaque"
        }
		
        LOD 100
    	
    	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

		#include "../../../HLSL/MiniFunctions.hlsl"

		#define MAX_SAMPLE_KERNEL_COUNT 64

		CBUFFER_START(UnityPerMaterial)
		float4 _SampleKernelArray[MAX_SAMPLE_KERNEL_COUNT];
		float _SampleKernelCount;
		float _SampleKeneralRadius;
		float _DepthBiasValue;
		float _RangeStrength;
		float _AOStrength;

		//Blur
		float _BilaterFilterFactor;
		float2 _MainTex_TexelSize;
		float2 _BlurRadius;
		CBUFFER_END

		//获取深度法线图 URP里通过添加头文件可以省去声明
		//sampler2D _CameraDepthNormalsTexture;
		/*TEXTURE2D_X_FLOAT(_CameraDepthTexture);
		SAMPLER(sampler_CameraDepthTexture);

		TEXTURE2D(_CameraNormalsTexture);
		SAMPLER(sampler_CameraNormalsTexture);*/

		TEXTURE2D(_MainTex);
		SAMPLER(sampler_MainTex);

		//AO
		TEXTURE2D(_AOTex);
		SAMPLER(sampler_AOTex);

		TEXTURE2D(_NoiseTex);
		SAMPLER(sampler_NoiseTex);
		
		struct appdata
	    {
	        float4 vertex : POSITION;
	        float2 uv : TEXCOORD0;
	    };

	    struct v2f
	    {
	        float2 uv : TEXCOORD0;
	        float4 vertex : SV_POSITION;
			float3 viewVec : TEXCOORD1;
			float3 viewRay : TEXCOORD2;
	    	float4 screenPos : TEXCOORD3;
	    };

		///基于法线的双边滤波（Bilateral Filter）
		//https://blog.csdn.net/puppet_master/article/details/83066572
		float3 GetNormal(float2 uv)
		{
			float4 cdn = SAMPLE_TEXTURE2D(_CameraNormalsTexture,sampler_CameraNormalsTexture, uv);	
			return DecodeViewNormalStereo(cdn);
		}

		half CompareNormal(float3 n1,float3 n2)
		{
			return smoothstep(_BilaterFilterFactor,1.0,dot(n1,n2));
		}

		// 参考：https://zhuanlan.zhihu.com/p/648793922
		// 根据线性深度值和屏幕UV，还原世界空间下，相机到顶点的位置偏移向量
		half3 ReconstructViewPos(float2 uv, float linearEyeDepth) {
			// Screen is y-inverted
			uv.y = 1.0 - uv.y;

			float zScale;// = linearEyeDepth * _ProjectionParams2.x; // divide by near plane
			float3 viewPos;// = _CameraViewTopLeftCorner.xyz + _CameraViewXExtent.xyz * uv.x + _CameraViewYExtent.xyz * uv.y;
			viewPos *= zScale;

			return viewPos;
		}

		float Random(float2 p) {
			return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
		}

		// 获取半球上随机一点
		half3 PickSamplePoint(float2 uv, int sampleIndex, half rcpSampleCount, half3 normal) {
			// 一坨随机数
			half gn = InterleavedGradientNoise(uv * _ScreenParams.xy, sampleIndex);
			half u = frac(Random(half2(0.0, sampleIndex)) + gn) * 2.0 - 1.0;
			half theta = Random(half2(1.0, sampleIndex) + gn) * TWO_PI;
			half u2 = sqrt(1.0 - u * u);

			// 全球上随机一点
			half3 v = half3(u2 * cos(theta), u2 * sin(theta), u);
			v *= sqrt((sampleIndex + 1.0) * rcpSampleCount); // 随着采样次数越向外采样

			// 半球上随机一点 逆半球法线翻转
			// https://thebookofshaders.com/glossary/?search=faceforward
			v = faceforward(v, -normal, v); // 确保v跟normal一个方向

			// 缩放到[0, RADIUS]
			v *= _SampleKeneralRadius;

			return v;
		}

    	//Vert AO
	    v2f vertAO(appdata v)
	    {
	        v2f o;
	        o.vertex = TransformObjectToHClip(v.vertex);
	        o.uv = v.uv;
			
			//计算相机空间中的像素方向（相机到像素的方向）
			//https://zhuanlan.zhihu.com/p/92315967
			//屏幕纹理坐标
			float4 screenPos = ComputeScreenPos(o.vertex);
	    	o.screenPos = screenPos;
			// NDC position
			float4 ndcPos = (screenPos / screenPos.w) * 2 - 1;
			// 计算至远屏幕方向
			float3 clipVec = float3(ndcPos.x, ndcPos.y, 1.0) * _ProjectionParams.z;
			o.viewVec = mul(unity_CameraInvProjection, clipVec.xyzz).xyz;
	        return o;
	    }

		//Frag AO
	    float4 fragAO (v2f i) : SV_Target
	    {
		 	// 采样深度
			float rawDepth = SampleSceneDepth(i.uv);
			float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
			// 采样法线 法线图里的值未pack
			float3 normal = SampleSceneNormals(i.uv);
	        //采样屏幕纹理
	        //float4 col = tex2D(_MainTex, i.uv);

			return float4(linearDepth,linearDepth,linearDepth,1.0);

			//采样获得深度值和法线值
			float3 viewNormal = normal;
			float linear01Depth = linearDepth;

			/*
	    	// depth
	    	half existingDepth01 = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture,
			i.screenPos.xy).r;
			linear01Depth = LinearEyeDepth(existingDepth01, _ZBufferParams);

	    	// normal
	    	float4 existingNormal = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture,
		    i.screenPos.xy);
			viewNormal = UnpackNormalOctRectEncode(existingNormal);*/

			//获取像素相机屏幕坐标位置
			float3 viewPos = linear01Depth * i.viewVec;

			//获取像素相机屏幕法线，法相z方向相对于相机为负（so 需要乘以-1置反），并处理成单位向量
			viewNormal = normalize(viewNormal) * float3(1, 1, -1);

			//铺平纹理
			float2 noiseScale = _ScreenParams.xy / 4.0;
			float2 noiseUV = i.uv * noiseScale;
			//randvec法线半球的随机向量
			float3 randvec = SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,noiseUV).xyz;
			//Gramm-Schimidt处理创建正交基
			//法线&切线&副切线构成的坐标空间
			float3 tangent = normalize(randvec - viewNormal * dot(randvec,viewNormal));
			float3 bitangent = cross(viewNormal,tangent);
			float3x3 TBN = float3x3(tangent,bitangent,viewNormal);

			//采样核心
			float ao = 0;
			int sampleCount = _SampleKernelCount;//每个像素点上的采样次数
			//https://blog.csdn.net/qq_39300235/article/details/102460405
			for(int i=0;i<sampleCount;i++){
				//随机向量，转化至法线切线空间中
				float3 randomVec = mul(_SampleKernelArray[i].xyz,TBN);
				
				//ao权重
				float weight = smoothstep(0,0.2,length(randomVec.xy));
				
				//计算随机法线半球后的向量
				float3 randomPos = viewPos + randomVec * _SampleKeneralRadius;
				//转换到屏幕坐标
				float3 rclipPos = mul((float3x3)unity_CameraProjection, randomPos);
				float2 rscreenPos = (rclipPos.xy / rclipPos.z) * 0.5 + 0.5;

				float randomDepth;
				float3 randomNormal;

				// NORMAL
				float4 rcdn = SAMPLE_TEXTURE2D(_CameraNormalsTexture,sampler_CameraNormalsTexture,
					rscreenPos);
				randomNormal = UnpackNormalOctRectEncode(rcdn);
				// DEPTH
				randomDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture,
				rscreenPos.xy).r, _ZBufferParams);

				//SampleSceneNormals
				//TODO:DecodeDepthNormal(rcdn, randomDepth, randomNormal);
				
				//判断累加ao值
				float range = abs(randomDepth - linear01Depth) > _RangeStrength ? 0.0 : 1.0;
				float selfCheck = randomDepth + _DepthBiasValue < linear01Depth ? 1.0 : 0.0;

				//采样点的深度值和样本深度比对前后关系
				ao += range * selfCheck * weight;
			}

			ao = ao/sampleCount;
			ao = max(0.0, 1 - ao * _AOStrength);
			return float4(ao,ao,ao,1);
	    }

		//Frag Blur
		float4 fragBlur (v2f i) : SV_Target
		{
			//_MainTex_TexelSize -> https://forum.unity.com/threads/_maintex_texelsize-whats-the-meaning.110278/
			float2 delta = _MainTex_TexelSize.xy * _BlurRadius.xy;
			
			float2 uv = i.uv;
			float2 uv0a = i.uv - delta;
			float2 uv0b = i.uv + delta;	
			float2 uv1a = i.uv - 2.0 * delta;
			float2 uv1b = i.uv + 2.0 * delta;
			float2 uv2a = i.uv - 3.0 * delta;
			float2 uv2b = i.uv + 3.0 * delta;
			
			float3 normal = GetNormal(uv);
			float3 normal0a = GetNormal(uv0a);
			float3 normal0b = GetNormal(uv0b);
			float3 normal1a = GetNormal(uv1a);
			float3 normal1b = GetNormal(uv1b);
			float3 normal2a = GetNormal(uv2a);
			float3 normal2b = GetNormal(uv2b);
			
			float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,uv);
			float4 col0a = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, uv0a);
			float4 col0b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,uv0b);
			float4 col1a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,uv1a);
			float4 col1b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,uv1b);
			float4 col2a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,uv2a);
			float4 col2b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,uv2b);
			
			half w = 0.37004405286;
			half w0a = CompareNormal(normal, normal0a) * 0.31718061674;
			half w0b = CompareNormal(normal, normal0b) * 0.31718061674;
			half w1a = CompareNormal(normal, normal1a) * 0.19823788546;
			half w1b = CompareNormal(normal, normal1b) * 0.19823788546;
			half w2a = CompareNormal(normal, normal2a) * 0.11453744493;
			half w2b = CompareNormal(normal, normal2b) * 0.11453744493;
			
			half3 result;
			result = w * col.rgb;
			result += w0a * col0a.rgb;
			result += w0b * col0b.rgb;
			result += w1a * col1a.rgb;
			result += w1b * col1b.rgb;
			result += w2a * col2a.rgb;
			result += w2b * col2b.rgb;
			
			result /= w + w0a + w0b + w1a + w1b + w2a + w2b;
			return float4(result, 1.0);
		}

		// Frag Composite
		float4 frag_Composite(v2f i) : SV_Target
		{
			float4 col = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, i.uv);
			float4 ao = SAMPLE_TEXTURE2D(_AOTex,sampler_AOTex, i.uv);
			col.rgb *= ao.r;
			return col;
		}

		ENDHLSL
		Cull Off ZWrite Off ZTest Always

		//Pass 0 : Generate AO 
		Pass
        {
            HLSLPROGRAM
            #pragma vertex vertAO
            #pragma fragment fragAO
            ENDHLSL
        }
		//Pass 1 : Bilateral Filter Blur
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vertAO
			#pragma fragment fragBlur
			ENDHLSL
		}

		//Pass 2 : Composite AO
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vertAO
			#pragma fragment frag_Composite
			ENDHLSL
		}
    }
}
