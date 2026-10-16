using System;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer.UIElement.OKCancel
{
    [UxmlElement]
    partial class OKCancelElement:VisualElement
    {
        const string k_Uxml = "Packages/com.unity.2d.tooling/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/UIElement/OKCancel/OKCancelElement.uxml";
        Button m_OK;
        Button m_Cancel;
        public OKCancelElement()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);
            m_OK = this.Q<Button>("OK");
            m_Cancel = this.Q<Button>("Cancel");
        }

        public event Action onOKClicked
        {
            add => m_OK.RegisterCallback<ClickEvent>(e => value?.Invoke());
            remove => m_OK.UnregisterCallback<ClickEvent>(e => value?.Invoke());
        }

        public event Action onCancelClicked
        {
            add => m_Cancel.RegisterCallback<ClickEvent>(e => value?.Invoke());
            remove => m_Cancel.UnregisterCallback<ClickEvent>(e => value?.Invoke());
        }

        public void SetOKButtonText(string text)
        {
            m_OK.text = text;
        }

        public void SetCancelButtonText(string text)
        {
            m_Cancel.text = text;
        }

        public void ShowCancelButton(bool show)
        {
            m_Cancel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void EnableOKButton(bool enable)
        {
            m_OK.SetEnabled(enable);
        }
    }
}
