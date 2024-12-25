#define MAP_SIDE_COLOR half3(0.75,0.75,0.75)

int    _ArkioShowMap;
float4 _ArkioModelSpaceBounds;

half3 ComputeMapColor(Varyings input) 
{    
    float2 uv = input.uv;
    half3 diffuse = _BaseColor.rgb;

    if (_ArkioShowMap > 0.5) {
        float d = dot(input.normalWS, float3(0, 1, 0));
        if (d > 0.1) { // top
            float3 modelPos = (0);
            modelPos.x = input.positionOS.x;
            modelPos.z = input.positionOS.z;

            // sample map:
            float2 samplingPos = (float2(modelPos.z, -modelPos.x) - float2(_ArkioModelSpaceBounds.y, -_ArkioModelSpaceBounds.z));
            samplingPos.x /= _ArkioModelSpaceBounds.w - _ArkioModelSpaceBounds.y;
            samplingPos.y /= _ArkioModelSpaceBounds.z - _ArkioModelSpaceBounds.x;
            diffuse.rgb = SampleAlbedoAlpha(samplingPos, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
        } else { // side is unconditionally equal to _MapSideColor
            diffuse.rgb = MAP_SIDE_COLOR;
        }
    }

    return diffuse;

}