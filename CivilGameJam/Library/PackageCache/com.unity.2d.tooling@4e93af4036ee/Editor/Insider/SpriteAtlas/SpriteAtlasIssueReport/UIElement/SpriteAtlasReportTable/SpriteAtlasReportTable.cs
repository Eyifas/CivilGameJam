using System;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer.UIElement
{
    [UxmlElement]
    partial class SpriteAtlasReportTable : MultiColumnListView
    {
        const string k_Uss = "Packages/com.unity.2d.tooling/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/UIElement/SpriteAtlasReportTable/SpriteAtlasReportTable.uss";
        MultiColumnListView m_Table;

        public SpriteAtlasReportTable()
        {
            InitView();
        }
        void InitView()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_Uss));
            showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        }

        public MultiColumnListView multiColumnListView => this;
    }
}
