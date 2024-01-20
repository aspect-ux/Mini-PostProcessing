Shader "AspectURP/Mini-PostProcessing/GlitchSplitRGB"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Amplitude ("Amplitude", Range(-0.2, 0)) = -0.15
        _Amount ("Amount", Range(-5, 5)) = 0.5
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            CBUFFER_START(UnityPerMaterial)
            half4 _MainTex_TexelSize;

            float _Amplitude;
            float _Amount;
            CBUFFER_END

            TEXTURE2D(_MainTex);	SAMPLER(sampler_MainTex);
            
            float Noise()
            {
                float _TimeX = _Time.y;
                float splitAmout = (1.0 + sin(_TimeX * 6.0)) * 0.5;
                splitAmout *= 1.0 + sin(_TimeX * 16.0) * 0.5;
                splitAmout *= 1.0 + sin(_TimeX * 19.0) * 0.5;
                splitAmout *= 1.0 + sin(_TimeX * 27.0) * 0.5;
                splitAmout = pow(splitAmout, _Amplitude);
                splitAmout *= (0.05 * _Amount);
                return splitAmout;
            }

            half4 SplitRGB(v2f i)
            {
                float splitAmout = Noise();

                half3 finalColor;
                finalColor.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(i.uv.x + splitAmout, i.uv.y)).r;
                finalColor.g = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, i.uv).g;
                finalColor.b = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(i.uv.x - splitAmout, i.uv.y)).b;

                return half4(finalColor, 1.0);
            }

            float4 frag(v2f i) : SV_Target
            {
                return SplitRGB(i);
            }
            ENDHLSL
        }
    }
}

