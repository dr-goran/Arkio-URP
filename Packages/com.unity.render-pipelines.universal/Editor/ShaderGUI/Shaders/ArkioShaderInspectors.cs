using System;
using UnityEngine;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{

    internal class ArkioUnlitShader : UnlitShader
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);

            Material material = materialEditor.target as Material;
            CoreEditorUtils.DrawSplitter(materialEditor);            
            arkioExpanded = CoreEditorUtils.DrawHeaderFoldout("Arkio specific settings", arkioExpanded, isBoxed: false, null, null, isTitleHeader: false);            
            if (arkioExpanded) {
                KeywordToggle("Obey veil", "ARKIO_VEIL");
                KeywordToggle("Use Vertex Colors", "ARKIO_VERTEX_COLORS");
            }
        }

        void KeywordToggle(string name, string keyword)
        {
            // use initial toggle as template
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
                KeywordToggle("X-Ray", "ARKIO_XRAY");
            }
        }

        void KeywordToggle(string name, string keyword)
        {
            // use initial toggle as template
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
}
