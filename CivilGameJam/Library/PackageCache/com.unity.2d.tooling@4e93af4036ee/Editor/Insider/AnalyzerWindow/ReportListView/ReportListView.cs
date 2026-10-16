using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [UxmlElement]
    partial class ReportListView : ListView
    {
        const string k_UssPath = "Packages/com.unity.2d.tooling/Editor/Insider/AnalyzerWindow/ReportListView/ReportListView.uss";
        List<IAnalyzerReport> m_ReportListItems;
        public ReportListView()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_UssPath));
            makeItem += MakeItem;
            bindItem += BindItem;
            unbindItem += UnbindItem;
        }

        public void SetListDataSource(List<IAnalyzerReport> reportListItems)
        {
            m_ReportListItems = reportListItems;
            itemsSource = m_ReportListItems;
            Rebuild();
        }

        void UnbindItem(VisualElement arg1, int arg2)
        {
            arg1.Clear();
        }

        void BindItem(VisualElement arg1, int arg2)
        {
            arg1.Add(m_ReportListItems[arg2].listItem);
        }

        VisualElement MakeItem()
        {
            var item = new VisualElement();
            item.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_UssPath));
            item.AddToClassList("report-list-item-container");
            return item;
        }
    }
}
