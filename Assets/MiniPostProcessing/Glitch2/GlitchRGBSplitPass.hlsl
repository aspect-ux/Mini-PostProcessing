#ifndef _GLITCHRGBSPLIT_PASS_INCLUDED
#define _GLITCHRGBSPLIT_PASS_INCLUDED

#define _Frequency _GlitchRGBSplitParams.x
#define _Amount _GlitchRGBSplitParams.y
#define _Speed _GlitchRGBSplitParams.z

float4 _GlitchRGBSplitParams;

half3 RGBSplitHorizontal(float2 uv, float amount, float time) {
    amount *= 0.001f;

    float3 splitAmountX = uv.x;
    splitAmountX.x += sin(time * 0.2f) * amount;
    splitAmountX.y += sin(time * 0.1f) * amount;

    half3 splitColor = 0.0f;
    splitColor.r = GetSource(float2(splitAmountX.x, uv.y)).r;
    splitColor.g = GetSource(float2(splitAmountX.y, uv.y)).g;
    splitColor.b = GetSource(float2(splitAmountX.z, uv.y)).b;

    return splitColor;
}

half3 RGBSplitVertical(float2 uv, float amount, float time) {
    amount *= 0.001;

    float3 splitAmountY = uv.y;
    splitAmountY.x += sin(time * 0.2f) * amount;
    splitAmountY.y += sin(time * 0.1f) * amount;

    half3 splitColor = 0.0f;

    splitColor.r = GetSource(float2(uv.x, splitAmountY.x)).r;
    splitColor.g = GetSource(float2(uv.x, splitAmountY.y)).g;
    splitColor.b = GetSource(float2(uv.x, splitAmountY.z)).b;

    return splitColor;
}

half3 RGBSplitMixed(float2 uv, float amount, float time) {
    amount *= 0.001;

    float2 splitAmount = 0.0f;
    splitAmount.y = sin(time * 0.2f) * amount;
    splitAmount.x = sin(time * 0.1f) * amount;

    half3 splitColor = 0.0f;

    splitColor.r = GetSource(uv + splitAmount.xx).r;
    splitColor.g = GetSource(uv).g;
    splitColor.b = GetSource(uv + splitAmount.yy).b;

    return splitColor;
}

half4 GlitchRGBSplitHorizontalPassFragment(Varyings input) : SV_Target {
    float strength = 0.0f;
    #ifdef _INFINITEFREQUENCY
    strength = 1.0f;
    #else
    strength = 0.5f + 0.5f * cos(_Time.y * _Frequency);
    #endif

    half3 color = RGBSplitHorizontal(input.uv, _Amount * strength, _Time.y * _Speed);

    return half4(color, 1.0);
}

half4 GlitchRGBSplitVerticalPassFragment(Varyings input) : SV_Target {
    float strength = 0.0f;
    #ifdef _INFINITEFREQUENCY
    strength = 1.0f;
    #else
    strength = 0.5f + 0.5f * cos(_Time.y * _Frequency);
    #endif

    half3 color = RGBSplitVertical(input.uv, _Amount * strength, _Time.y * _Speed);

    return half4(color, 1.0);
}

half4 GlitchRGBSplitMixedPassFragment(Varyings input) : SV_Target {
    float strength = 0.0f;
    #ifdef _INFINITEFREQUENCY
    strength = 1.0f;
    #else
    strength = 0.5f + 0.5f * cos(_Time.y * _Frequency);
    #endif

    half3 color = RGBSplitMixed(input.uv, _Amount * strength, _Time.y * _Speed);

    return half4(color, 1.0);
}

#endif
