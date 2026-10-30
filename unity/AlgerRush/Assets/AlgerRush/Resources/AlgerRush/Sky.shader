// Sky dome around the camera: gradient, sun disc with its glow, moon.
Shader "AlgerRush/Sky"
{
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" }
        Pass
        {
            Cull Off
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _AR_SkyTop, _AR_SkyBot, _AR_SkyFog, _AR_SunDisc, _AR_Sun, _AR_Moon;
            float _AR_SunA, _AR_MoonA;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.dir = vertex.xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col = lerp(_AR_SkyBot.rgb, _AR_SkyTop.rgb, smoothstep(0.0, 0.5, h));
                col = lerp(_AR_SkyFog.rgb, col, smoothstep(-0.01, 0.12, h));
                float s = max(dot(d, _AR_Sun.xyz), 0.0);
                col += _AR_SunDisc.rgb * (pow(s, 8.0) * 0.28 + pow(s, 64.0) * 0.4) * _AR_SunA;
                col = lerp(col, _AR_SunDisc.rgb * 1.15 + 0.15, smoothstep(0.99935, 0.99965, s) * _AR_SunA * step(-0.02, h));
                float m = max(dot(d, _AR_Moon.xyz), 0.0);
                col += float3(0.6, 0.7, 0.9) * pow(m, 40.0) * 0.15 * _AR_MoonA;
                col = lerp(col, float3(0.93, 0.93, 0.86), smoothstep(0.99975, 0.99988, m) * _AR_MoonA);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
