// Shared bits of the Alger Rush shaders: the curved-world bend, the sky/sun lighting and the fog.
// Every value here is a global set once a frame from C# (Shader.SetGlobal*), so the shaders do not
// depend on Unity's lights or on any render pipeline: they work the same in Built-in and URP.
#ifndef ALGER_RUSH_INCLUDED
#define ALGER_RUSH_INCLUDED
#include "UnityCG.cginc"

float _AR_BendX, _AR_BendY, _AR_CamZ, _AR_Time, _AR_Night;
float4 _AR_SunDir;
float4 _AR_SunCol, _AR_SkyCol, _AR_GroundCol, _AR_FogCol;
float _AR_FogNear, _AR_FogFar;

// the road ahead bends left/right and over crests without any geometry moving
float4 ARBend(float4 wp, float noBend)
{
    if (noBend < 0.5)
    {
        float d = max(0.0, wp.z - _AR_CamZ);
        wp.x += _AR_BendX * d * d;
        wp.y += _AR_BendY * d * d;
    }
    return wp;
}

float3 ARVertexColor(float3 c)
{
#ifndef UNITY_COLORSPACE_GAMMA
    c = GammaToLinearSpace(c);
#endif
    return c;
}

float3 ARLight(float3 n)
{
    float3 hemi = lerp(_AR_GroundCol.rgb, _AR_SkyCol.rgb, n.y * 0.5 + 0.5);
    return hemi + _AR_SunCol.rgb * max(0.0, dot(n, _AR_SunDir.xyz));
}

float ARFog(float3 wp)
{
    float d = distance(wp, _WorldSpaceCameraPos.xyz);
    return saturate((d - _AR_FogNear) / max(1.0, _AR_FogFar - _AR_FogNear));
}
#endif
