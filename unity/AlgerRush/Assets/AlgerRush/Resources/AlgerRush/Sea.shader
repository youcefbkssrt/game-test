// Faceted sea: travelling waves in the vertex shader, flat facets and a sun glint in the fragment shader.
Shader "AlgerRush/Sea"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "AlgerRush.cginc"

            float4 _AR_SeaCol, _AR_SpecCol;

            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; };

            v2f vert (float4 vertex : POSITION)
            {
                v2f o;
                float4 wp = mul(unity_ObjectToWorld, float4(vertex.xyz, 1.0));
                float t = _AR_Time;
                wp.y += 0.42 * sin(wp.x * 0.07 + t * 0.9) + 0.3 * sin(wp.z * 0.11 - t * 1.3) + 0.18 * sin((wp.x + wp.z) * 0.23 + t * 2.0);
                o.wp = wp.xyz;
                o.pos = mul(UNITY_MATRIX_VP, ARBend(wp, 0));
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(cross(ddy(i.wp), ddx(i.wp)));
                if (n.y < 0) n = -n;
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.wp);
                float3 h = normalize(_AR_SunDir.xyz + v);
                float3 col = _AR_SeaCol.rgb * ARLight(n) + _AR_SpecCol.rgb * _AR_SunCol.rgb * pow(max(0.0, dot(n, h)), 60.0);
                col = lerp(col, _AR_FogCol.rgb, ARFog(i.wp));
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
