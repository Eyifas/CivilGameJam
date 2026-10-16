using System;
using UnityEditor.U2D.Tooling.Analyzer.UIElement.OKCancel;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class AtlasPageSettings : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.2d.tooling/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/SpriteAtlasPageIssue/AtlasPageReportSettings.uxml";
        IntegerField m_PageCountField;
        event Action<int> OnPageCountChanged;
        int m_PageCount;
        OKCancelElement m_OkCancelElement;
        public AtlasPageSettings(int pageCount)
        {
            m_PageCount = pageCount;
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            asset.CloneTree(this);
            m_OkCancelElement = this.Q<OKCancelElement>();
            m_OkCancelElement.SetOKButtonText("Apply");
            m_OkCancelElement.SetCancelButtonText("Revert");
            m_OkCancelElement.ShowCancelButton(false);
            m_OkCancelElement.onOKClicked += OnOKClicked;
            m_OkCancelElement.EnableOKButton(false);

            m_PageCountField = this.Q<IntegerField>("AtlasPages");
            m_PageCountField.RegisterValueChangedCallback(x =>
            {
                m_PageCountField.SetValueWithoutNotify(Math.Clamp(x.newValue,0, 1000));
                m_OkCancelElement.EnableOKButton(true);
            });
            m_PageCountField.SetValueWithoutNotify(m_PageCount);
        }

        public event Action<int> pageCountChanged
        {
            add => OnPageCountChanged += value;
            remove => OnPageCountChanged -= value;
        }

        void OnOKClicked()
        {
            m_PageCount = m_PageCountField.value;
            OnPageCountChanged?.Invoke(m_PageCountField.value);
            m_OkCancelElement.EnableOKButton(false);
        }
    }
}
