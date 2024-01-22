Shader "Hidden/PostProcessing/SSAO" {
    SubShader {
        Tags {
            "RenderType"="Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Common/PostProcessing.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        #include "SSAOPass.hlsl"
        ENDHLSL

        Pass {
            Name "SSAO Occlusion Pass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment SSAOPassFragment
            ENDHLSL
        }

        Pass {
            Name "SSAO Bilateral Blur Pass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment BlurPassFragment
            ENDHLSL
        }

        Pass {
            Name "SSAO Final Pass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FinalPassFragment
            ENDHLSL
        }

    }
}