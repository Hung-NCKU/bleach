// 後製：泛光（Bloom）＋調色＋暗角（內建渲染管線，OnRenderImage 使用）
Shader "Kido/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _BloomTex;
    float4 _MainTex_TexelSize;
    float _Threshold, _Knee, _Intensity, _Contrast, _Saturation, _Vignette, _Exposure;
    float2 _Dir;
    fixed4 _Tint, _Shadow;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    // 0：擷取亮部（柔性門檻）
    fixed4 fragPrefilter (v2f i) : SV_Target
    {
        float3 c = tex2D(_MainTex, i.uv).rgb;
        float br = max(c.r, max(c.g, c.b));
        float soft = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
        soft = soft * soft / (4 * _Knee + 1e-4);
        float w = max(soft, br - _Threshold) / max(br, 1e-4);
        return fixed4(c * w, 1);
    }

    // 1：九點高斯模糊（方向由 _Dir 指定）
    fixed4 fragBlur (v2f i) : SV_Target
    {
        float2 d = _Dir * _MainTex_TexelSize.xy;
        float3 c = tex2D(_MainTex, i.uv).rgb * 0.227;
        c += tex2D(_MainTex, i.uv + d * 1.385).rgb * 0.316;
        c += tex2D(_MainTex, i.uv - d * 1.385).rgb * 0.316;
        c += tex2D(_MainTex, i.uv + d * 3.231).rgb * 0.070;
        c += tex2D(_MainTex, i.uv - d * 3.231).rgb * 0.070;
        return fixed4(c, 1);
    }

    // 2：合成＋調色＋暗角
    fixed4 fragComposite (v2f i) : SV_Target
    {
        float3 c = tex2D(_MainTex, i.uv).rgb;
        float3 b = tex2D(_BloomTex, i.uv).rgb;
        c += b * _Intensity;
        c *= _Exposure;
        // 陰影偏冷、亮部偏暖（動畫風的色調）
        float lum = dot(c, float3(0.299, 0.587, 0.114));
        c = lerp(c * _Shadow.rgb, c * _Tint.rgb, saturate(lum * 1.2));
        c = lerp(lum.xxx, c, _Saturation);
        c = (c - 0.5) * _Contrast + 0.5;
        // 柔和的 ACES 近似色調映射
        c = saturate((c * (2.51 * c + 0.03)) / (c * (2.43 * c + 0.59) + 0.14));
        float2 v = i.uv - 0.5;
        c *= 1 - dot(v, v) * _Vignette;
        return fixed4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            ENDCG }
    }
}
