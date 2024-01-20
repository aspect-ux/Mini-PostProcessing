Shader "AspectURP/Mini-PostProcessing/Template"
{
    Properties 
    {
        _MainTex ("Base (RGB)", 2D) = "white" { }
    }
    SubShader 
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
        //如该 shader 会出现在单帧中重复绘制调用的情况下,那么建议将下面的 uniform 都提取到 CBUFFER 中，否则不用提取
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        half _ShaderParam;
        CBUFFER_END

		TEXTURE2D(_MainTex);	SAMPLER(sampler_MainTex);

        ENDHLSL

        Pass {
            
			HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
			{
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
			{
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
			{
                Varyings output;

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
			{
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            } 
            ENDHLSL
        }
    }
}
