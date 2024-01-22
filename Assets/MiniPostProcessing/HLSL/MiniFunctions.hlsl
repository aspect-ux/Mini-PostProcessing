#ifndef __MINIFUNCTIONS_H__
#define __MINIFUNCTIONS_H__
// 自定义亮度
float CustomLuminance(in float3 c)
{
    //根据人眼对颜色的敏感度，可以看见对绿色是最敏感的
    return 0.2125 * c.r + 0.7154 * c.g + 0.0721 * c.b;
}

// 当有多个RenderTarget时，需要自己处理UV翻转问题
float2 CorrectUV(in float2 uv, in float4 texelSize)
{
    float2 result = uv;
	
    #if UNITY_UV_STARTS_AT_TOP      // DirectX之类的
    if(texelSize.y < 0.0)           // 开启了抗锯齿
        result.y = 1.0 - uv.y;      // 满足上面两个条件时uv会翻转，因此需要转回来
    #endif

    return result;
}

// from UnityCG.cginc
// Encoding/decoding view space normals into 2D 0..1 vector
inline float2 EncodeViewNormalStereo(float3 n )
{
    float kScale = 1.7777;
    float2 enc;
    enc = n.xy / (n.z+1);
    enc /= kScale;
    enc = enc*0.5+0.5;
    return enc;
}
// Encoding/decoding [0..1) floats into 8 bit/channel RG. Note that 1.0 will not be encoded properly.
inline float2 EncodeFloatRG( float v )
{
    float2 kEncodeMul = float2(1.0, 255.0);
    float kEncodeBit = 1.0/255.0;
    float2 enc = kEncodeMul * v;
    enc = frac(enc);
    enc.x -= enc.y * kEncodeBit;
    return enc;
}
inline float4 EncodeDepthNormal( float depth, float3 normal )
{
    float4 enc;
    enc.xy = EncodeViewNormalStereo (normal);
    enc.zw = EncodeFloatRG (depth);
    return enc;
}

// Decode
inline float3 DecodeViewNormalStereo(float4 enc4)
{
    float kScale = 1.7777;
    float3 nn = enc4.xyz*float3(2*kScale,2*kScale,0) + float3(-kScale,-kScale,1);
    float g = 2.0 / dot(nn.xyz,nn.xyz);
    float3 n;
    n.xy = g*nn.xy;
    n.z = g-1;
    return n;
}
#endif