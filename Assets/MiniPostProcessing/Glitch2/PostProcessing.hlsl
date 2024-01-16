#ifndef POSTPROCESSING_INCLUDED
#define POSTPROCESSING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

TEXTURE2D(_SourceTexture);
SAMPLER(sampler_SourceTexture);
float4 _SourceTexture_TexelSize;

struct Varyings {
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

half4 GetSource(float2 uv) {
    return SAMPLE_TEXTURE2D(_SourceTexture, sampler_SourceTexture, uv);
}

half4 GetSource(Varyings input) {
    return GetSource(input.uv);
}

float4 GetSourceTexelSize() {
    return _SourceTexture_TexelSize;
}

struct ScreenSpaceData {
    float4 positionCS;
    float4 positionNDC;
    float2 uv;
};

ScreenSpaceData GetScreenSpaceData(uint vertexID : SV_VertexID) {
    ScreenSpaceData output;
    // 根据id判断三角形顶点的坐标
    // 坐标顺序为(-1, -1) (-1, 3) (3, -1)
    output.positionCS = float4(vertexID <= 1 ? -1.0 : 3.0, vertexID == 1 ? 3.0 : -1.0, 0.0, 1.0);
    output.uv = float2(vertexID <= 1 ? 0.0 : 2.0, vertexID == 1 ? 2.0 : 0.0);
    // 不同API可能会产生颠倒的情况 进行判断
    if (_ProjectionParams.x < 0.0) {
        output.uv.y = 1.0 - output.uv.y;
    }
    output.positionNDC = float4(output.uv * 2.0 - 1.0, UNITY_NEAR_CLIP_VALUE, 1.0);
    return output;
}

Varyings Vert(uint vertexID : SV_VertexID) {
    Varyings output;
    ScreenSpaceData ssData = GetScreenSpaceData(vertexID);
    output.positionCS = ssData.positionCS;
    output.uv = ssData.uv;
    return output;
}

#endif
