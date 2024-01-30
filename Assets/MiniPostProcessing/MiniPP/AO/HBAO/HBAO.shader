Shader "Hidden/AO/HBAO" {
    Properties
    {
        [HideInInspector]_MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "HBAO.hlsl"
        //TEXTURE2D(_MainTex);
		//SAMPLER(sampler_MainTex);
        ENDHLSL

        Pass {
            Name "HBAO Occlusion Pass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment HBAOPassFragment
            ENDHLSL
        }

        Pass {
            Name "HBAO Gaussian Blur Pass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment BlurPassFragment
            ENDHLSL
        }

        Pass {
            Name "HBAO Gaussian Final Pass"

            ZTest NotEqual
            ZWrite Off
            Cull Off
            Blend One SrcAlpha, Zero One //这里是为RGB 和Alpha单独设置混合系数
            BlendOp Add, Add //Add是默认值

            //finalValue = sourceFactor * sourceValue operation destinationFactor * destinationValue

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FinalPassFragment
            ENDHLSL
        }

        Pass {
            Name "HBAO Preview Pass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment PreviewPassFragment
            ENDHLSL
        }
    }
}