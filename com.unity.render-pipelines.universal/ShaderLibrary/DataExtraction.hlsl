#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Debug.hlsl"

int UNITY_DataExtraction_Mode;
int UNITY_DataExtraction_Space;
int UNITY_DataExtraction_Value;
TEXTURE2D(unity_EditorViz_DepthBuffer); SAMPLER(sampler_unity_EditorViz_DepthBuffer);

#define RENDER_OBJECT_ID               1
#define RENDER_DEPTH                   2
#define RENDER_WORLD_NORMALS_FACE_RGB  3
#define RENDER_WORLD_POSITION_RGB      4
#define RENDER_ENTITY_ID               5
#define RENDER_BASE_COLOR_RGBA         6
#define RENDER_SPECULAR_RGB            7
#define RENDER_METALLIC_R              8
#define RENDER_EMISSION_RGB            9
#define RENDER_WORLD_NORMALS_PIXEL_RGB 10
#define RENDER_SMOOTHNESS_R            11
#define RENDER_OCCLUSION_R             12
#define RENDER_DIFFUSE_COLOR_RGBA      13
#define RENDER_OUTLINE_MASK            14

struct ExtractionInputs
{
    float3 vertexNormalWS;
    float3 pixelNormalWS;
    float3 positionWS;

    float3 baseColor;
    float  alpha;

#ifdef _SPECULAR_SETUP
    float3 specular;
#else
    float  metallic;
#endif
    float  smoothness;
    float  occlusion;
    float3 emission;
};

float4 OutputExtraction(ExtractionInputs inputs)
{
    float3 specular, diffuse, baseColor;
    float metallic;

    #ifdef _SPECULAR_SETUP
        specular = inputs.specular;
        diffuse = inputs.baseColor;
        ConvertSpecularToMetallic(inputs.baseColor, inputs.specular, baseColor, metallic);
    #else
        baseColor = inputs.baseColor;
        metallic = inputs.metallic;
        ConvertMetallicToSpecular(inputs.baseColor, inputs.metallic, diffuse, specular);
    #endif

#ifdef UNITY_DOTS_INSTANCING_ENABLED
    // Entities always have zero ObjectId
    if (UNITY_DataExtraction_Mode == RENDER_OBJECT_ID)
         return 0;
#else
    if (UNITY_DataExtraction_Mode == RENDER_OBJECT_ID)
         return PackId32ToRGBA8888(asuint(unity_LODFade.z));
#endif
    //@TODO
    if (UNITY_DataExtraction_Mode == RENDER_DEPTH)
        return 0;
    if (UNITY_DataExtraction_Mode == RENDER_WORLD_NORMALS_FACE_RGB)
        return float4(PackNormalRGB(inputs.vertexNormalWS), 1.0f);
    if (UNITY_DataExtraction_Mode == RENDER_WORLD_POSITION_RGB)
        return float4(inputs.positionWS, 1.0);
#ifdef UNITY_DOTS_INSTANCING_ENABLED
    if (UNITY_DataExtraction_Mode == RENDER_ENTITY_ID)
        return PackId32ToRGBA8888(unity_EntityId.x);
#else
    // GameObjects always have zero EntityId
    if (UNITY_DataExtraction_Mode == RENDER_ENTITY_ID)
        return 0;
#endif
    if (UNITY_DataExtraction_Mode == RENDER_BASE_COLOR_RGBA)
        return float4(baseColor, inputs.alpha);
    if (UNITY_DataExtraction_Mode == RENDER_SPECULAR_RGB)
        return float4(specular.xxx, 1);
    if (UNITY_DataExtraction_Mode == RENDER_METALLIC_R)
        return float4(metallic.xxx, 1.0);
    if (UNITY_DataExtraction_Mode == RENDER_EMISSION_RGB)
        return float4(inputs.emission, 1.0);
    if (UNITY_DataExtraction_Mode == RENDER_WORLD_NORMALS_PIXEL_RGB)
        return float4(PackNormalRGB(inputs.pixelNormalWS), 1.0f);
    if (UNITY_DataExtraction_Mode == RENDER_SMOOTHNESS_R)
        return float4(inputs.smoothness.xxx, 1.0);
    if (UNITY_DataExtraction_Mode == RENDER_OCCLUSION_R)
       return float4(inputs.occlusion.xxx, 1.0);
    if (UNITY_DataExtraction_Mode == RENDER_DIFFUSE_COLOR_RGBA)
       return float4(diffuse, inputs.alpha);
    if (UNITY_DataExtraction_Mode == RENDER_OUTLINE_MASK)
    {
        // Red channel = unique identifier, can be used to separate groups of objects from each other
        //               to get outlines between them.
        // Green channel = occluded behind depth buffer (0) or not occluded (1)
        // Blue channel  = always 1 = not cleared to zero = there's an outlined object at this pixel
        return ComputeSelectionMask(
            (float)UNITY_DataExtraction_Value / 255.0,
            ComputeNormalizedDeviceCoordinatesWithZ(inputs.positionWS, UNITY_MATRIX_VP),
            TEXTURE2D_ARGS(unity_EditorViz_DepthBuffer, sampler_unity_EditorViz_DepthBuffer));
    }

    return 0;
}
