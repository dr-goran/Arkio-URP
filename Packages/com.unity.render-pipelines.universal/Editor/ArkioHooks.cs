using System.Collections.Generic;
using System.Linq;
using UnityEditor.Rendering.Universal.ShaderGraph;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace UnityEditor.ArkioURP {

    public static class ArkioURPHooks
    {
        internal static void OnShaderAboutToBeGenerated(TargetSetupContext context, Rendering.Universal.ShaderGraph.UniversalTarget target)
        {
            // Special consideration: if we are compiling an opaque shader's forward pass, 
            // we wish to set a custom render state descriptor (One Zero, SrcAlpha Zero)
            // since that is a prerequisite for getting the veil to work.
            foreach (var subshader in context.subShaders) {
                foreach (var pass in subshader.passes) {
                    // we are working inside the target context here, not the generated shader context, so it's impossible for us to access
                    // shader properties to know whether we should or should not include the veil alteration code.
                    // we can't even check if the shader we are in currently, has "Arkio" in its name!
                    // for this reason, we might want to move this generation code into the Generator class itself, but that lives in shadergraph.
                    if (pass.descriptor.referenceName == "SHADERPASS_FORWARD") {
                        if (target.surfaceType == SurfaceType.Opaque) {
                            Debug.LogWarning("<color=cyan>Arkio shader generator has injected relevant veil code into the given shader</color>");
                            __RemoveRenderState(pass.descriptor.renderStates, RenderStateType.Blend);
                            pass.descriptor.renderStates.Add(RenderState.Blend(Blend.One, Blend.Zero, Blend.SrcAlpha, Blend.Zero));

                            var c = new DefineCollection() {
                                {  new KeywordDescriptor {
                                    definition = KeywordDefinition.Predefined,
                                    referenceName = "_ARKIO_VEIL",
                                    displayName = "Arkio veil",
                                    scope = KeywordScope.Global,
                                    stages = KeywordShaderStage.All,
                                    type = KeywordType.Boolean,
                                    value = 1
                                    }, 1
                                }
                            };

                            pass.descriptor.defines.Add(c);
                        }
                    }
                }
            }
        }

        // Arkio utility method. Uses reflection to get around an encapsulation issue that prevents us from modifying render states
        // after they have been added to the context.
        // we could have approached the context building differently, but it would end up being way more disruptive to make these specific
        // Arkio-related checks deep in the bowels of the code that creates a lit material template.
        static void __RemoveRenderState(RenderStateCollection collection, RenderStateType type) {
            var fieldInfo = typeof(RenderStateCollection).GetField("m_Items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var items = fieldInfo.GetValue(collection) as List<RenderStateCollection.Item>;
            for (var i = items.Count - 1; i >= 0; i--) {
                if (items[i].descriptor.type == type) { items.RemoveAt(i); }
            }
        }
    }
}
