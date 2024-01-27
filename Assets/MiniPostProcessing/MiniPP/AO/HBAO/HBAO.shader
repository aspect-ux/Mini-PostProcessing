Shader "Hidden/AO/HBAO" {
    SubShader {
        Tags {
            "RenderType"="Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "HBAO.hlsl"
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
            Blend One SrcAlpha, Zero One
            BlendOp Add, Add

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