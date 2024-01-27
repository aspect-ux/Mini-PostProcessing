#ifndef _HBAO_INCLUDED
#define _HBAO_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

#define DIRECTION_COUNT 8
#define STEP_COUNT 6

#define INTENSITY _HBAOParams.x
#define RADIUS _HBAOParams.y
#define MAXRADIUSPIXEL _HBAOParams.z
#define ANGLEBIAS _HBAOParams.w

float4 _ProjectionParams2;
float4 _CameraViewTopLeftCorner;
float4 _CameraViewXExtent;
float4 _CameraViewYExtent;

float4 _HBAOParams;
float _RadiusPixel;

float4 _HBAOBlurRadius;

half4 GetSource(half2 uv) {
    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
}

float Random(float2 p) {
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453123);
}

// 根据线性深度值和屏幕UV，还原视角空间下的顶点位置
half3 ReconstructViewPos(float2 uv, float linearEyeDepth) {
    // Screen(NDC) to CS: uv.y = 1.0 - uv.y
    // CS to VS: uv = 1.0 - uv
    uv.x = 1.0 - uv.x;

    float zScale = -linearEyeDepth * _ProjectionParams2.x; // divide by near plane
    float3 viewPos = _CameraViewTopLeftCorner.xyz + _CameraViewXExtent.xyz * uv.x + _CameraViewYExtent.xyz * uv.y;
    viewPos *= zScale;

    return viewPos;
}


// 还原视角空间法线
half3 ReconstructViewNormals(float2 uv) {
    float3 normal = SampleSceneNormals(uv);
    //TODO: 
    //normal = TransformWorldToViewNormal(normal, true);
    normal = TransformWorldToViewDir(normal, true);
    // inverse z
    normal.z = -normal.z;
    return normal;
}

// 计算距离衰减W
float FallOff(float dist) {
    return 1 - dist * dist / (RADIUS * RADIUS);
}

// https://www.derschmale.com/2013/12/20/an-alternative-implementation-for-hbao-2/
inline float ComputeAO(float3 vpos, float3 stepVpos, float3 normal, inout float topOcclusion) {
    float3 h = stepVpos - vpos;
    float dist = length(h);
    float occlusion = dot(normal, h) / dist;
    float diff = max(occlusion - topOcclusion, 0);
    topOcclusion = max(occlusion, topOcclusion);
    return diff * saturate(FallOff(dist));
}

float4 _SourceSize;

half4 HBAOPassFragment(Varyings input) : SV_Target {
    float rawDepth = SampleSceneDepth(input.texcoord);
    float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
    float3 vpos = ReconstructViewPos(input.texcoord, linearDepth);
    float3 normal = ReconstructViewNormals(input.texcoord);

    float2 noise = float2(Random(input.texcoord.yx), Random(input.texcoord.xy));

    // 计算步近值
    float stride = min(_RadiusPixel / vpos.z, MAXRADIUSPIXEL) / (STEP_COUNT + 1.0);
    // stride至少大于一个像素
    if (stride < 1) return 0.0;
    float stepRadian = TWO_PI / DIRECTION_COUNT;

    half ao = 0.0;

    UNITY_UNROLL
    for (int d = 0; d < DIRECTION_COUNT; d++) {
        // 计算起始随机步近方向
        float radian = stepRadian * (d + noise.x);
        float sinr, cosr;
        sincos(radian, sinr, cosr);
        float2 direction = float2(cosr, sinr);

        // 计算起始随机步近长度
        float rayPixels = frac(noise.y) * stride + 1.0;

        float topOcclusion = ANGLEBIAS; // 上一次（最大的）AO，初始值为angle bias
        // 进行光线步近
        UNITY_UNROLL
        for (int s = 0; s < STEP_COUNT; s++) {
            float2 uv2 = round(rayPixels * direction) * _SourceSize.zw + input.texcoord;
            float3 rawDepth2 = SampleSceneDepth(uv2);
            float3 linearDepth2 = LinearEyeDepth(rawDepth2, _ZBufferParams);
            float3 vpos2 = ReconstructViewPos(uv2, linearDepth2);
            ao += ComputeAO(vpos, vpos2, normal, topOcclusion);
            rayPixels += stride;
        }
    }

    // 提高对比度
    ao = PositivePow(ao * rcp(STEP_COUNT * DIRECTION_COUNT) * INTENSITY, 0.6);

    //return _HBAOBlurRadius;
    return half4(ao, ao, ao, ao);
}

// https://software.intel.com/content/www/us/en/develop/blogs/an-investigation-of-fast-real-time-gpu-based-image-blur-algorithms.html
half GaussianBlur(half2 uv, half2 pixelOffset) {
    half colOut = 0;

    // Kernel width 7 x 7
    const int stepCount = 2;

    const half gWeights[stepCount] = {
        0.44908,
        0.05092
    };
    const half gOffsets[stepCount] = {
        0.53805,
        2.06278
    };

    UNITY_UNROLL
    for (int i = 0; i < stepCount; i++) {
        half2 texCoordOffset = gOffsets[i] * pixelOffset;
        half4 p1 = GetSource(uv + texCoordOffset);
        half4 p2 = GetSource(uv - texCoordOffset);
        half col = p1.r + p2.r;
        colOut += gWeights[i] * col;
    }

    return colOut;
}

half4 BlurPassFragment(Varyings input) : SV_Target {
    float2 delta = _HBAOBlurRadius.xy * _SourceSize.zw;

    return GaussianBlur(input.texcoord, delta);
}

half4 FinalPassFragment(Varyings input) : SV_Target {
    float2 delta = _HBAOBlurRadius.xy * _SourceSize.zw;
    half ao = 1.0 - GaussianBlur(input.texcoord, delta);

    return half4(0.0, 0.0, 0.0, ao);
}

half4 PreviewPassFragment(Varyings input) : SV_Target {
    half ao = GetSource(input.texcoord);
    return ao;
}


#endif