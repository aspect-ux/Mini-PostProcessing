// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "AspectURP/Mini-PostProcessing/BrightnessSaturationAndContrast" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Brightness ("Brightness", Float) = 1
		_Saturation("Saturation", Float) = 1
		_Contrast("Contrast", Float) = 1
	}
	SubShader {

        Tags
        {
           "RenderPipeline" = "UniversalRenderPipeline" "RenderType"="Opaque"
        }
		
        LOD 100
		Pass {  
			ZTest Always Cull Off ZWrite Off
			
			HLSLPROGRAM  
			#pragma vertex vert  
			#pragma fragment frag  
			  
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
			half _Brightness;
			half _Saturation;
			half _Contrast;
            CBUFFER_END

            TEXTURE2D(_MainTex);       
            SAMPLER(sampler_MainTex); 


            // Custom
            struct app_img
            {
                float3 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
			  
			struct v2f {
				float4 pos : SV_POSITION;
				half2 uv: TEXCOORD0;
			};
			  
			v2f vert(app_img v) {
				v2f o;
				
				o.pos = TransformObjectToHClip(v.vertex);
				
				o.uv = v.uv;
						 
				return o;
			}
		
			half4 frag(v2f i) : SV_Target {
				half4 renderTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
				  
				// Apply brightness
				float3 finalColor = renderTex.rgb * _Brightness;
				
				// Apply saturation
				half luminance = 0.2125 * renderTex.r + 0.7154 * renderTex.g + 0.0721 * renderTex.b;
				float3 luminanceColor = float3(luminance, luminance, luminance);
				finalColor = lerp(luminanceColor, finalColor, _Saturation);
				
				// Apply contrast
				float3 avgColor = float3(0.5, 0.5, 0.5);
				finalColor = lerp(avgColor, finalColor, _Contrast);
				
				return half4(finalColor, renderTex.a);  
			}  
			  
			ENDHLSL
		}  
	}
	
	Fallback Off
}
