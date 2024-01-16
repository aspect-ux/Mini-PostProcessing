Shader "AspectURP/Mini-PostProcessing/Glitch"
{
   SubShader
   {
      Cull Off 
      ZWrite Off 
      
      Pass
      {
         HLSLPROGRAM
         
         #pragma vertex Vert
         #pragma fragment Frag

         #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
         #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
   

         half3 _Params;
         half4 _Params2;
         half3 _Params3;

         #define _TimeX _Params.x
         #define _Offset _Params.y
         #define _Fade _Params.z

         #define _BlockLayer1_U _Params2.w
         #define _BlockLayer1_V _Params2.x
         #define _BlockLayer2_U _Params2.y
         #define _BlockLayer2_V _Params2.z

         #define _RGBSplit_Indensity _Params3.x
         #define _BlockLayer1_Indensity _Params3.y
         #define _BlockLayer2_Indensity _Params3.z
         
         Texture2D _MainTex;
         SamplerState sampler_MainTex;

         float randomNoise(float2 seed)
         {
            return frac(sin(dot(seed * floor(_TimeX * 30.0), float2(127.1, 311.7))) * 43758.5453123);
         }
   
         float randomNoise(float seed)
         {
            return randomNoise(float2(seed, 1.0));
         }
         
         float4 Frag(Varyings i): SV_Target
         {
            float2 uv = i.uv;
      
            //求解第一层blockLayer
            float2 blockLayer1 = floor(uv * float2(_BlockLayer1_U, _BlockLayer1_V));
            float2 blockLayer2 = floor(uv * float2(_BlockLayer2_U, _BlockLayer2_V));
      
            float lineNoise1 = pow(randomNoise(blockLayer1), _BlockLayer1_Indensity);
            float lineNoise2 = pow(randomNoise(blockLayer2), _BlockLayer2_Indensity);
            float RGBSplitNoise = pow(randomNoise(5.1379), 7.1) * _RGBSplit_Indensity;
            float lineNoise = lineNoise1 * lineNoise2 * _Offset  - RGBSplitNoise;
      
            float4 colorR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            float4 colorG = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(lineNoise * 0.05 * randomNoise(7.0), 0));
            float4 colorB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(lineNoise * 0.05 * randomNoise(23.0), 0));
      
            float4 result = float4(float3(colorR.x, colorG.y, colorB.z), colorR.a + colorG.a + colorB.a);
            result = lerp(colorR, result, _Fade);
      
            return result;
         }
         ENDHLSL
         
      }
   }
}