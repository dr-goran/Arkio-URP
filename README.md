## What is this?

This is a fork of the Meta fork of Unity-Graphics, which is a monorepo that contains, among other things, URP.

Meta maintains its own fork to make URP compatible with its spacewarp tech (AppSW = application spacewarp).

The most recent official URP (17.0.3 at the time of writing) does have native support for the MV buffer and its default (lit and unlit) shaders ARE AppSW compatible.

However, shadergraph in the official URP does not output AppSW compliant shaders (even though it does output an MV pass, so one wonders just what Unity folks mucked up with that one).

So, only the Meta fork has a shadergraph that is capable of outputting AppSW compliant shaders, and for that reason, we fork that.

Now that Unity adopted AppSW natively, it is expected they will also adapt their shadergraph in the future. When that happens, we can ditch the meta fork intermediary and base everything around the default URP fork.

Regardless of which we pick as a base, though, it has become clear that we definitely need our own branch of URP. Earlier, we were stuck maintaining copies of URP shaders and #includes, which were just a nightmare to maintain every upgrade cycle.

The intent is to have a dedicated Arkio URP - a small set of changes which leverage URP's (admittedly impressive) scriptability to expand its capabilities to fit our unique use cases.

This document will list ALL the changes made to URP, so that URP upgrades can be easily made.

=========================================

## CHANGES

- (pending) Shader generator should check if the shader in question has the _ARKIO_VEIL keyword defined; and, if so, output a special combination of blend modes with separate modes for alpha; i.e.:
Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha Zero

- (pending) if the shader has _ARKIO_VEIL keyword defined, relevant shader includes for forward passes must check against it and if it is defined, they should NOT set the alpha to 1.0 even if the shader is opaque.

==============================================