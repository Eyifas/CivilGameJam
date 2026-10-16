using System;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer.UIElement
{
    class CommonSpriteAtlasIssueView : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.2d.tooling/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/UIElement/CommonSpriteAtlasIssueView/CommonSpriteAtlasIssueView.uxml";
        SpriteAtlasReportTable m_Table;
        Label m_NoDataLabel;

        public CommonSpriteAtlasIssueView()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);
            InitView();
            if (EditorGUIUtility.isProSkin)
                AddToClassList("dark");
        }

        void InitView()
        {
            m_Table = this.Q<SpriteAtlasReportTable>();
            m_NoDataLabel = this.Q<Label>("NoDataLabel");
        }

        public void ShowTable(bool show, string noShowReason)
        {
            m_Table.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            m_NoDataLabel.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            if(!string.IsNullOrEmpty(noShowReason) && !show)
                m_NoDataLabel.text = noShowReason;
        }

        public SpriteAtlasReportTable table => m_Table;
    }
}
