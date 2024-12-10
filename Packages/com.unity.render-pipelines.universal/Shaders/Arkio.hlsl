#pragma shader_feature _ ARKIO_VEIL
#pragma multi_compile  _ ARKIO_VERTEX_COLORS
#pragma multi_compile  _ ARKIO_SECTION

// #define ARKIO_SHADER_DEBUG

#ifndef ARKIO_INCLUDED
#define ARKIO_INCLUDED


    #ifdef ARKIO_VEIL
    uniform float _GlobalVeilAlpha; // arkio-specific
    #endif

    #ifdef ARKIO_SECTION
    uniform float3 _SectionPlanePosition;
    uniform float3 _SectionPlaneNormal;
    
    bool sectioned(float3 worldPosition) {
        return dot(worldPosition - _SectionPlanePosition, _SectionPlaneNormal) 
        #ifdef ARKIO_SECTION_INVERSE
            <= 0;
        #else
            > 0;
        #endif
    }
    #endif

#endif