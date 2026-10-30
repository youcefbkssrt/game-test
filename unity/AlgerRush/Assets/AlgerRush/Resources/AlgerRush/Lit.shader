// Flat-shaded, vertex-coloured Lambert surface used for everything solid in the game.
Shader "AlgerRush/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _EmisTex ("Emission map", 2D) = "black" {}
        _EmisColor ("Emission colour", Color) = (0,0,0,1)
        _Emis ("Emission map strength at night", Float) = 0
        _Unlit ("Unlit", Float) = 0
        _NoBend ("No bend", Float) = 0
        _Haze ("Fixed haze (-1 = distance fog)", Float) = -1
        _Cutoff ("Alpha cutoff", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _OffsetF ("Depth offset", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull [_Cull]
            Offset [_OffsetF], [_OffsetF]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "AlgerRush.cginc"

            sampler2D _MainTex, _EmisTex;
            float4 _MainTex_ST;
            float4 _Color, _EmisColor;
            float _Emis, _Unlit, _NoBend, _Haze, _Cutoff;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 col : COLOR; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 wp : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                float4 wp = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                o.wp = wp.xyz;
                wp = ARBend(wp, _NoBend);
                o.pos = mul(UNITY_MATRIX_VP, wp);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.col = ARVertexColor(v.color.rgb);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 tex = tex2D(_MainTex, i.uv);
                clip(tex.a - _Cutoff);
                float3 albedo = i.col * tex.rgb * _Color.rgb;
                float3 lit = albedo * ARLight(normalize(i.n));
                float3 col = lerp(lit, albedo, _Unlit);
                col += tex2D(_EmisTex, i.uv).rgb * _Emis * _AR_Night + _EmisColor.rgb;
                float f = _Haze >= 0 ? _Haze : ARFog(i.wp);
                col = lerp(col, _AR_FogCol.rgb, f);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
