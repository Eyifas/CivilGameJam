using System;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer.UIElement
{
    [UxmlElement]
    partial class CellLabelWithIcon:VisualElement
    {
        const string k_Uxml = "Packages/com.unity.2d.tooling/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/UIElement/CellLabelWithIcon/CellLabelWithIcon.uxml";

        Label m_Label;
        VisualElement m_Icon;
        public CellLabelWithIcon()
        {
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).CloneTree(this);
            m_Label = this.Q<Label>("Label");
            m_Icon = this.Q<VisualElement>("Icon");
        }

        public void BindLabel(DataBinding binding)
        {
            if (binding != null)
                m_Label.SetBinding("text", binding);
        }

        public void SetLabelDataSoruce(object source)
        {
            m_Label.dataSource  = source;
        }

        public void SetIconClassName(string iconClassName)
        {
            m_Icon.AddToClassList(iconClassName);
        }

        public void RemoveIconClassName(string iconClassName)
        {
            m_Icon.RemoveFromClassList(iconClassName);
        }
        // static public VisualElement Create(DataBinding binding)
        // {
        //     var ve = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).Instantiate();
        //     if (binding != null)
        //         ve.Q<Label>("CellLabel").SetBinding("text", binding);
        //     return ve;
        // }
    }
}
