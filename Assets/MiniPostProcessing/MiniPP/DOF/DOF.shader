Shader "URP/PPS/DOF_BokehBlurShader"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        half4 _MainTex_TexelSize;
        half4 _DepthOfFieldTex_TexelSize;
        
        float _BlurSize; //模糊强度
        float _Iteration; //迭代次数
        float _DownSample; //降采样次数

        // Camera parameters
        float _Distance;
        float _LensCoeff; // f^2 / (N * (S1 - f) * film_width * 2)
        float _MaxCoC;
        float _RcpMaxCoC;
        float _RcpAspect;
        half3 _TaaParams; // Jitter.x, Jitter.y, Blending
        CBUFFER_END

        TEXTURE2D(_MainTex);                                SAMPLER(sampler_MainTex);
        TEXTURE2D_X_FLOAT(_CameraDepthTexture);             SAMPLER(sampler_CoCTex);
        TEXTURE2D_X_FLOAT(_CameraMotionVectorsTexture);     SAMPLER(sampler_CameraMotionVectorsTexture);
        TEXTURE2D_X_FLOAT(_CoCTex);                         SAMPLER(sampler_CameraDepthTexture);
        TEXTURE2D_X_FLOAT(_DepthOfFieldTex);                SAMPLER(sampler_DepthOfFieldTex);

        
        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 texcoord : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float2 texcoordStereo : TEXCOORD1;
        };

        Varyings vert (Attributes input)
        {
            Varyings output;

            output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.texcoord;
            output.texcoordStereo = input.texcoord;

            return output;
        }
        

        half Luminance(half3 linearRgb)
        {
            return dot(linearRgb, float3(0.2126729, 0.7151522, 0.0721750));
        }

        half3 SRGBToLinear(half3 c)
        {
            #if USE_VERY_FAST_SRGB
                return c * c;
            #elif USE_FAST_SRGB
                return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878);
            #else
            half3 linearRGBLo = c / 12.92;
            half3 linearRGBHi = PositivePow((c + 0.055) / 1.055, half3(2.4, 2.4, 2.4));
            half3 linearRGB = (c <= 0.04045) ? linearRGBLo : linearRGBHi;
            return linearRGB;
            #endif
        }

        half3 LinearToSRGB(half3 c)
        {
            #if USE_VERY_FAST_SRGB
                return sqrt(c);
            #elif USE_FAST_SRGB
                return max(1.055 * PositivePow(c, 0.416666667) - 0.055, 0.0);
            #else
            half3 sRGBLo = c * 12.92;
            half3 sRGBHi = (PositivePow(c, half3(1.0 / 2.4, 1.0 / 2.4, 1.0 / 2.4)) * 1.055) - 0.055;
            half3 sRGB = (c <= 0.0031308) ? sRGBLo : sRGBHi;
            return sRGB;
            #endif
        }

        half4 LinearToSRGB(half4 c)
        {
            return half4(LinearToSRGB(c.rgb), c.a);
        }
		
        //像素shader
        half4 frag(Varyings input) : SV_Target
        {
            float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, input.uv).r;
            float linearDepth = Linear01Depth(depth, _ZBufferParams);
            half coc = (linearDepth - _Distance) * _LensCoeff / max(depth, 1e-4);
            coc = saturate(coc * 0.5 * _RcpMaxCoC + 0.5);
            half4 finalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

            //散景模糊
            float c = cos(2.39996323f);
            float s = sin(2.39996323f);
            half4 _GoldenRot = half4(c, s, -s, c);

            half2x2 rot = half2x2(_GoldenRot);
            half4 accumulator = 0.0; //累加器
            half4 divisor = 0.0; //因子

            half r = 1.0;
            half2 angle = half2(0.0, _BlurSize);

            for (int j = 0; j < _Iteration; j++)
            {
                r += 1.0 / r; //每次 + r分之一 1.1
                angle = mul(rot, angle);
                half4 bokeh = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(input.uv + (r - 1.0) * angle));
                accumulator += bokeh * bokeh;
                divisor += bokeh;
            }
            half4 BokehBlur = accumulator / divisor;

            finalColor.rgb = lerp(finalColor.rgb, BokehBlur.rgb, coc);

            return finalColor;
        }
        
        ENDHLSL
        
        Cull Off ZWrite Off ZTest Always

        Pass 
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
			
            ENDHLSL
        }

    }
}