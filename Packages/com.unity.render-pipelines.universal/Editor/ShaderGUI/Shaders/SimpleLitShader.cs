using System;
using UnityEngine;
using static UnityEditor.Progress;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{

    internal class ArkioSimpleLitShader : SimpleLitShader
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);

            Material material = materialEditor.target as Material;

            CoreEditorUtils.DrawSplitter(materialEditor);
            
            arkioExpanded = CoreEditorUtils.DrawHeaderFoldout("Arkio specific settings", arkioExpanded, isBoxed: false, null, null, isTitleHeader: false);
            
            if (arkioExpanded) {
                KeywordToggle("Obey veil", "ARKIO_VEIL");
                KeywordToggle("Sectioned", "ARKIO_SECTION");
                KeywordToggle("Use Vertex Colors", "ARKIO_VERTEX_COLORS");
                //bool isVeil = EditorGUILayout.Toggle("Obey veil", material.IsKeywordEnabled("ARKIO_VEIL"));
                //if (isVeil)     material.EnableKeyword("ARKIO_VEIL");
                //else            material.DisableKeyword("ARKIO_VEIL");

                //bool isSection = EditorGUILayout.Toggle("Sectioned", material.IsKeywordEnabled("ARKIO_SECTION"));
                //if (isSection) material.EnableKeyword("ARKIO_SECTION");
                //else material.DisableKeyword("ARKIO_SECTION");
            }
        }

        void KeywordToggle(string name, string keyword)
        {
            // use initial toggle as tempate

            bool prevHasKeyword = ((Material)materialEditor.target).IsKeywordEnabled(keyword);
            bool hasKeyword = EditorGUILayout.Toggle(name, prevHasKeyword);
            if (hasKeyword != prevHasKeyword) { 
                // then apply that to all materials.
                foreach (var t in materialEditor.targets) {
                    var material = (Material)t;
                    if (hasKeyword) material.EnableKeyword(keyword);
                    else material.DisableKeyword(keyword);
                    EditorUtility.SetDirty(material);
                }
            }
        }

        bool arkioExpanded;
    }

    internal class SimpleLitShader : BaseShaderGUI
    {
        // Properties
        private SimpleLitGUI.SimpleLitProperties shadingModelProperties;

        // collect properties from the material properties
        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);
            shadingModelProperties = new SimpleLitGUI.SimpleLitProperties(properties);
        }

        // material changed check
        public override void ValidateMaterial(Material material)
        {
            SetMaterialKeywords(material, SimpleLitGUI.SetMaterialKeywords);
        }

        // material main surface options
        public override void DrawSurfaceOptions(Material material)
        {
            if (material == null)
                throw new ArgumentNullException("material");

            // Use default labelWidth
            EditorGUIUtility.labelWidth = 0f;

            base.DrawSurfaceOptions(material);
        }

        // material main surface inputs
        public override void DrawSurfaceInputs(Material material)
        {
            base.DrawSurfaceInputs(material);
            SimpleLitGUI.Inputs(shadingModelProperties, materialEditor, material);
            DrawEmissionProperties(material, true);
            DrawTileOffset(materialEditor, baseMapProp);
        }

        public override void DrawAdvancedOptions(Material material)
        {
            SimpleLitGUI.Advanced(shadingModelProperties);
            base.DrawAdvancedOptions(material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            if (material == null)
                throw new ArgumentNullException("material");

            // _Emission property is lost after assigning Standard shader to the material
            // thus transfer it before assigning the new shader
            if (material.HasProperty("_Emission"))
            {
                material.SetColor("_EmissionColor", material.GetColor("_Emission"));
            }

            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            if (oldShader == null || !oldShader.name.Contains("Legacy Shaders/"))
            {
                SetupMaterialBlendMode(material);
                return;
            }

            SurfaceType surfaceType = SurfaceType.Opaque;
            BlendMode blendMode = BlendMode.Alpha;
            if (oldShader.name.Contains("/Transparent/Cutout/"))
            {
                surfaceType = SurfaceType.Opaque;
                material.SetFloat("_AlphaClip", 1);
            }
            else if (oldShader.name.Contains("/Transparent/"))
            {
                // NOTE: legacy shaders did not provide physically based transparency
                // therefore Fade mode
                surfaceType = SurfaceType.Transparent;
                blendMode = BlendMode.Alpha;
            }
            material.SetFloat("_Surface", (float)surfaceType);
            material.SetFloat("_Blend", (float)blendMode);
        }
    }
}
