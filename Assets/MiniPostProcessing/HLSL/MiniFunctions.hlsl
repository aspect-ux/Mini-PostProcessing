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
#endif