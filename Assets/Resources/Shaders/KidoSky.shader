// 漸層天空盒
Shader "Kido/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.23,0.42,0.72,1)
        _Mid ("Horizon", Color) = (0.61,0.77,0.92,1)
        _Bottom ("Bottom", Color) = (0.95,0.89,0.78,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Top, _Mid, _Bottom;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float y = normalize(i.dir).y;
                fixed4 c = y > 0 ? lerp(_Mid, _Top, pow(saturate(y), 0.6)) : lerp(_Mid, _Bottom, saturate(-y * 4));
                return c;
            }
            ENDCG
        }
    }
}
