// Soft additive glow quads (lamps, tail lights, sirens, coins, fireworks, stars, city lights)
// and, with _Blob set, the soft dark shadow blobs under cars and people.
Shader "AlgerRush/Glow"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 1
        _NoBend ("No bend", Float) = 0
        _Soft ("Falloff power", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst blend", Float) = 1
        _Blob ("Shadow blob", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend [_Src] [_Dst]
            ZWrite Off
            Cull Off
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "AlgerRush.cginc"

            float _Intensity, _NoBend, _Soft, _Blob;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                float4 wp = ARBend(mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)), _NoBend);
                o.pos = mul(UNITY_MATRIX_VP, wp);
                o.col = float4(ARVertexColor(v.color.rgb), v.color.a);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv * 2 - 1);
                float a = pow(saturate(1 - d), _Soft);
                if (_Blob > 0.5) return float4(0, 0, 0, a * i.col.a * _Intensity);
                return float4(i.col.rgb * a * _Intensity, 1);
            }
            ENDCG
        }
    }
}
