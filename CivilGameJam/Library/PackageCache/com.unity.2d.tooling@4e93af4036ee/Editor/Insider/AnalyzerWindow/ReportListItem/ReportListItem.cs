using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [UxmlElement]
    partial class ReportListItem : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.2d.tooling/Editor/Insider/AnalyzerWindow/ReportListItem/ReportListItem.uxml";
        Label m_NameLabel;
        Label m_CountLabel;
        bool m_IsLoading;
        readonly string[] k_LoadingDots =  { ".", "..", "..." };
        public ReportListItem()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);
            InitView();
        }

        void InitView()
        {
            m_CountLabel = this.Q<Label>("ReportCount");
            m_NameLabel = this.Q<Label>("ReportLabel");
            if (EditorGUIUtility.isProSkin)
                AddToClassList("dark");
        }


        public void SetName(string name)
        {
            m_NameLabel.text = name;
        }

        public void SetCount(string count)
        {
            m_IsLoading = false;
            m_CountLabel.text = count;
        }

        public bool isLoading => m_IsLoading;

        public void SetLoading()
        {
            m_IsLoading = true;
            var dots = 0;
            m_CountLabel.schedule.Execute(() =>
            {
                if (m_IsLoading)
                {
                    dots = (++dots)%k_LoadingDots.Length;
                    if (dots >= 0 && dots < k_LoadingDots.Length && m_CountLabel.text != k_LoadingDots[dots])
                    {
                        m_CountLabel.text = k_LoadingDots[dots];
                    }
                }
            }).Every(500).Until(() => !m_IsLoading);

        }
    }
}
