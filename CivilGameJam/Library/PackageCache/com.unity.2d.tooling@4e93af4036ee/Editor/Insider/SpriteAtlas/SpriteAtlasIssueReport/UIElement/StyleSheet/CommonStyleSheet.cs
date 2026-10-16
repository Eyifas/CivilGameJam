using System;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer.UIElement
{
    static class CommonStyleSheet
    {
        const string k_IconStyleSheetPath = "Packages/com.unity.2d.tooling/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/UIElement/StyleSheet/Icon.uss";
        const string k_TableStyleSheetPath = "Packages/com.unity.2d.tooling/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/UIElement/StyleSheet/TableStyle.uss";

        public static StyleSheet iconStyleSheet => AssetDatabase.LoadAssetAtPath<StyleSheet>(k_IconStyleSheetPath);
        public static StyleSheet tableStyleSheet => AssetDatabase.LoadAssetAtPath<StyleSheet>(k_TableStyleSheetPath);
    }
}
