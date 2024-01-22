#ifndef _GAUSSIANBLUR_PASS_INCLUDED
#define _GAUSSIANBLUR_PASS_INCLUDED

#ifndef SAMPLE_COUNT
#define SAMPLE_COUNT 12
#endif

#define INTENSITY _SSAOParams.x
#define RADIUS _SSAOParams.y
#define FALLOFF _SSAOParams.z

float4 _SSAOParams;

float4 _ProjectionParams2;
float4 _CameraViewTopLeftCorner;
float4 _CameraViewXExtent;
float4 _CameraViewYExtent;
float4 _SSAOBlurRadius;

half4 PackAONormal(half ao, half3 n) {
    return half4(ao, n * 0.5 + 0.5);
}

half3 GetPackedNormal(half4 p) {
    return p.gba * 2.0 - 1.0;
}

half GetPackedAO(half4 p) {
    return p.r;
}

float Random(float2 p) {
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

// 获取半球上随机一点
half3 PickSamplePoint(float2 uv, int sampleIndex, half rcpSampleCount, half3 normal) {
    // 一坨随机数
    half gn = InterleavedGradientNoise(uv * _ScreenParams.xy, sampleIndex);
    half u = frac(Random(half2(0.0, sampleIndex)) + gn) * 2.0 - 1.0;
    half theta = Random(half2(1.0, sampleIndex) + gn) * TWO_PI;
    half u2 = sqrt(1.0 - u * u);

    // 全球上随机一点
    half3 v = half3(u2 * cos(theta), u2 * sin(theta), u);
    v *= sqrt((sampleIndex + 1.0) * rcpSampleCount); // 随着采样次数越向外采样

    // 半球上随机一点 逆半球法线翻转
    // https://thebookofshaders.com/glossary/?search=faceforward
    v = faceforward(v, -normal, v); // 确保v跟normal一个方向

    // 缩放到[0, RADIUS]
    v *= RADIUS;

    return v;
}

// 根据线性深度值和屏幕UV，还原世界空间下，相机到顶点的位置偏移向量
half3 ReconstructViewPos(float2 uv, float linearEyeDepth) {
    // Screen is y-inverted
    uv.y = 1.0 - uv.y;

    float zScale = linearEyeDepth * _ProjectionParams2.x; // divide by near plane
    float3 viewPos = _CameraViewTopLeftCorner.xyz + _CameraViewXExtent.xyz * uv.x + _CameraViewYExtent.xyz * uv.y;
    viewPos *= zScale;

    return viewPos;
}

half4 SSAOPassFragment(Varyings input) : SV_Target {
    // 采样深度
    float rawDepth = SampleSceneDepth(input.uv);
    float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
    // 采样法线 法线图里的值未pack
    float3 normal = SampleSceneNormals(input.uv);

    // 还原世界空间相机到顶点偏移向量
    float3 vpos = ReconstructViewPos(input.uv, linearDepth);

    // Early Out for FallOff
    if (linearDepth > FALLOFF)
        return 0.0;

    const half rcpSampleCount = rcp(SAMPLE_COUNT);

    half ao = 0.0;

    UNITY_UNROLL
    for (int i = 0; i < SAMPLE_COUNT; i++) {
        // 取正半球上随机一点
        half3 offset = PickSamplePoint(input.uv, i, rcpSampleCount, normal);
        half3 vpos2 = vpos + offset;

        // 把采样点从世界坐标变换到裁剪空间
        half4 spos2 = mul(UNITY_MATRIX_VP, vpos2);
        // 计算采样点的屏幕uv
        half2 uv2 = half2(spos2.x, spos2.y * _ProjectionParams.x) / spos2.w * 0.5 + 0.5;

        // 计算采样点的depth
        float rawDepth2 = SampleSceneDepth(uv2);
        float linearDepth2 = LinearEyeDepth(rawDepth2, _ZBufferParams);

        // 判断采样点是否被遮蔽
        half IsInsideRadius = abs(spos2.w - linearDepth2) < RADIUS ? 1.0 : 0.0;

        // 光线与着色点夹角越大，贡献越小
        half3 difference = ReconstructViewPos(uv2, linearDepth2) - vpos; // 光线向量
        half inten = max(dot(difference, normal) - 0.004 * linearDepth, 0.0) * rcp(dot(difference, difference) + 0.0001);
        ao += inten * IsInsideRadius;
    }

    ao *= RADIUS;

    // 计算falloff
    half falloff = 1.0 - linearDepth * rcp(FALLOFF);
    falloff = falloff * falloff;

    // 提高AO对比度，使SSAO的效果更为显著
    // Apply contrast + intensity + falloff^2
    ao = PositivePow(saturate(ao * INTENSITY * falloff * rcpSampleCount), 0.6);

    return PackAONormal(ao, normal);
}

half CompareNormal(half3 d1, half3 d2) {
    return smoothstep(0.8, 1.0, dot(d1, d2));
}

half4 BlurPassFragment(Varyings input) : SV_Target {
    float2 delta = _SSAOBlurRadius.xy * GetSourceTexelSize().xy;
    // 进行一堆魔法参数偏移的采样
    half4 p0 = GetSource(input.uv);
    half4 p1a = GetSource(input.uv - delta * 1.3846153846);
    half4 p1b = GetSource(input.uv + delta * 1.3846153846);
    half4 p2a = GetSource(input.uv - delta * 3.2307692308);
    half4 p2b = GetSource(input.uv + delta * 3.2307692308);

    half3 n0 = GetPackedNormal(p0);

    // 计算权重
    half w0 = half(0.2270270270);
    half w1a = CompareNormal(n0, GetPackedNormal(p1a)) * half(0.3162162162);
    half w1b = CompareNormal(n0, GetPackedNormal(p1b)) * half(0.3162162162);
    half w2a = CompareNormal(n0, GetPackedNormal(p2a)) * half(0.0702702703);
    half w2b = CompareNormal(n0, GetPackedNormal(p2b)) * half(0.0702702703);

    // 进行Blur
    half s = half(0.0);
    s += GetPackedAO(p0) * w0;
    s += GetPackedAO(p1a) * w1a;
    s += GetPackedAO(p1b) * w1b;
    s += GetPackedAO(p2a) * w2a;
    s += GetPackedAO(p2b) * w2b;
    s *= rcp(w0 + w1a + w1b + w2a + w2b);

    return PackAONormal(s, n0);
}

TEXTURE2D(_SSAOSourceTexture);
SAMPLER(sampler_SSAOSourceTexture);

half4 FinalPassFragment(Varyings input) : SV_Target {
    half ao = 1.0 - GetSource(input.uv).r;
    half3 color = SAMPLE_TEXTURE2D(_SSAOSourceTexture, sampler_SSAOSourceTexture, input.uv) * ao;
    return half4(color, 1.0);
}

#endif
