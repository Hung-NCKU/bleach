// 動畫風卡通渲染：二階明暗（陰影帶色）＋髮絲高光＋邊緣光＋反向外殼描邊（內建渲染管線）
Shader "Kido/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Surface Culling", Float) = 2
        _TextureSaturation ("Texture Saturation", Range(0,1)) = 1
        _TextureContrast ("Texture Contrast", Range(0,1)) = 1
        _ShadeColor ("Shade Tint", Color) = (0.72,0.68,0.8,1)
        _ShadeStep ("Shade Step", Range(-1,1)) = 0.05
        _Spec ("Hair Highlight", Range(0,1)) = 0
        _Rim ("Rim", Range(0,1)) = 0.35
        _Emission ("Emission", Color) = (0,0,0,0)
        _Outline ("Outline Width (m)", Float) = 0.012
        _OutlineColor ("Outline Color", Color) = (0.08,0.06,0.07,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        // 描邊：沿世界法線外推，只畫背面；遠處稍微加粗以維持線寬
        Pass
        {
            Name "OUTLINE"
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _Outline;
            fixed4 _OutlineColor;
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; UNITY_FOG_COORDS(0) };
            v2f vert (appdata v)
            {
                v2f o;
                float3 wn = UnityObjectToWorldNormal(v.normal);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float dist = distance(wp, _WorldSpaceCameraPos);
                wp += wn * _Outline * clamp(dist / 8.0, 0.6, 2.0);
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                clip(_Outline - 0.000001);
                fixed4 c = fixed4(lerp(_OutlineColor.rgb, _Color.rgb * 0.25, 0.25), 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            fixed4 _Color, _ShadeColor, _Emission;
            float _ShadeStep, _Spec, _Rim, _TextureSaturation, _TextureContrast, _Cutoff;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wn : TEXCOORD0;
                float3 wp : TEXCOORD1;
                SHADOW_COORDS(2)
                float2 uv : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float ndl = dot(n, l);
                float sh = SHADOW_ATTENUATION(i);
                // 二階明暗：亮部／帶色陰影，交界柔化
                float lit = smoothstep(_ShadeStep - 0.04, _ShadeStep + 0.04, ndl) * smoothstep(0.3, 0.6, sh);
                float4 sampled = tex2D(_MainTex, i.uv);
                clip(sampled.a - _Cutoff);
                float3 texel = sampled.rgb;
                texel = lerp(dot(texel, float3(0.2126, 0.7152, 0.0722)).xxx, texel, _TextureSaturation);
                texel = lerp(float3(1, 1, 1), texel, _TextureContrast);
                float3 albedo = _Color.rgb * texel;
                float3 amb = ShadeSH9(float4(n, 1));
                float3 shade = albedo * _ShadeColor.rgb * (0.55 + amb * 0.6);
                float3 bright = albedo * (_LightColor0.rgb * 0.62 + amb * 0.22);
                float3 c = lerp(shade, bright, lit);
                // 髮絲高光：沿頭髮的環狀光帶
                float3 h = normalize(l + v);
                float band = smoothstep(0.86, 0.9, dot(n, h)) * (1 - smoothstep(0.97, 0.995, dot(n, h)));
                c += band * _Spec * _LightColor0.rgb * 0.45 * lit;
                // 邊緣光
                float rim = smoothstep(0.55, 0.75, 1 - saturate(dot(n, v))) * saturate(ndl + 0.4);
                c += rim * _Rim * _LightColor0.rgb * albedo * 0.35;
                c += _Emission.rgb;
                fixed4 col = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"

        // 點光源（鬼道發光照到角色）
        Pass
        {
            Tags { "LightMode"="ForwardAdd" }
            Blend One One
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            fixed4 _Color;
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wn : TEXCOORD0;
                float3 wp : TEXCOORD1;
                LIGHTING_COORDS(2, 3)
            };
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 ld = normalize(_WorldSpaceLightPos0.xyz - i.wp * _WorldSpaceLightPos0.w);
                float ndl = saturate(dot(normalize(i.wn), ld));
                float att = LIGHT_ATTENUATION(i);
                float s = step(0.05, ndl * att) * 0.6 + ndl * att * 0.4;
                return fixed4(_Color.rgb * _LightColor0.rgb * s, 1);
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
