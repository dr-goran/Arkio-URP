#ifndef ARKIO_SECTIONS_INCLUDED
#define ARKIO_SECTIONS_INCLUDED
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
