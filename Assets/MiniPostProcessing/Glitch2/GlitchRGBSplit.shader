Shader "Hidden/PostProcessing/GlitchRGBSplit" {
    SubShader {
        Tags {
            "RenderType"="Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 200
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "PostProcessing.hlsl"
        #include "GlitchRGBSplitPass.hlsl"
        ENDHLSL

        Pass {
            Name "Glitch RGB Split Horizontal Pass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GlitchRGBSplitHorizontalPassFragment
            #pragma shader_feature _INFINITEFREQUENCY
            ENDHLSL
        }

        Pass {
            Name "Glitch RGB Split Vertical Pass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GlitchRGBSplitVerticalPassFragment
            #pragma shader_feature _INFINITEFREQUENCY
            ENDHLSL
        }

        Pass {
            Name "Glitch RGB Split Mixed Pass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GlitchRGBSplitMixedPassFragment
            #pragma shader_feature _INFINITEFREQUENCY
            ENDHLSL
        }
    }
}