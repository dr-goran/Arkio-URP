// #define ARKIO_SHADER_DEBUG

#ifndef ARKIO_INCLUDED
#define ARKIO_INCLUDED

#pragma shader_feature_local _ ARKIO_VEIL
#pragma shader_feature_local _ ARKIO_VERTEX_COLORS
#pragma shader_feature_local _ ARKIO_XRAY

    #ifdef ARKIO_VEIL
    uniform float _ArkioGlobalVeilAlpha; // arkio-specific
    #endif

    #ifdef ARKIO_XRAY
    uniform float _ArkioGlobalXRayAlphaMultiplier;
    #endif
#endif
