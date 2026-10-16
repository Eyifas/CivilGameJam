using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [UxmlElement]
    partial class DataSourceList : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.2d.tooling/Editor/Insider/AnalyzerWindow/DataSourceListItem/DataSourceList.uxml";
        ListView m_DataSourceListView;
        VisualElement m_InfoBox;
        Label m_HintLabel;
        const string k_HintLabelText = "Control data source collection from a list of paths.\n\nUse the checkbox to toggle data collection for the individual data source.\n\nWhen enabled, you can fine-tune data collection by adding or removing paths.";
        const string k_HintLabelTextSingleItem = "Control data source collection from a list of paths.\n\nFine-tune data collection by adding or removing paths.";
        public DataSourceList()
        {
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).CloneTree(this);
            m_DataSourceListView = this.Q<ListView>("DataSourceListView");
            m_InfoBox = this.Q<VisualElement>("InfoBox");
            m_HintLabel = this.Q<Label>("Hint");
            InitDataSourceListView();
        }

        public void SetDataSource(List<DataSourceData> dataSource)
        {
            m_DataSourceListView.itemsSource = dataSource;
            UpdateInfoBoxVisibility(null);
        }

        void UpdateInfoBoxVisibility(Toggle toggle)
        {
            var dataSource = m_DataSourceListView.itemsSource;
            if (dataSource.Count == 1)
            {
                m_InfoBox.style.display = DisplayStyle.None;
                m_HintLabel.text = k_HintLabelTextSingleItem;
                return;
            }
            m_HintLabel.text = k_HintLabelText;
            foreach(var reportDataSoure in dataSource)
            {
                if (toggle?.dataSource == reportDataSoure)
                {
                    if (toggle.value)
                    {
                        m_InfoBox.style.display = DisplayStyle.None;
                        return;
                    }
                }
                else if ((reportDataSoure as DataSourceData)?.enabled == true)
                {
                    m_InfoBox.style.display = DisplayStyle.None;
                    return;
                }
            }
            m_InfoBox.style.display = DisplayStyle.Flex;
        }

        void InitDataSourceListView()
        {
            m_DataSourceListView.makeItem = () =>
            {
                var ve = new DataSourceListItem();
                ve.AddToClassList("data-source-list-item");
                ve.dataSourceToggle.RegisterValueChangedCallback(OnDataSourceToggleChanged);
                return ve;
            };
            m_DataSourceListView.bindItem = ((element, i) =>
            {
                var item = element as DataSourceListItem;
                bool isSingleItem = m_DataSourceListView.itemsSource.Count == 1;
                item.IsSingleItem(isSingleItem);
                var dataSource = m_DataSourceListView.itemsSource[i] as DataSourceData;
                item.dataSourceToggle.text = dataSource.reportDataSource.name;
                if (!isSingleItem)
                {
                    item.dataSourceToggle.SetBinding("value", new DataBinding
                    {
                        bindingMode = BindingMode.TwoWay,
                        dataSourcePath = new PropertyPath("enabled"),
                        dataSource = dataSource
                    });
                    item.assetSearchPath.SetEnabled(dataSource.enabled);
                }
                item.dataSourceToggle.dataSource = dataSource;
                item.assetSearchPath.SetBinding("itemsSource", new DataBinding
                {
                    bindingMode = BindingMode.TwoWay,
                    dataSourcePath = new PropertyPath("assetSearchPath"),
                    dataSource = dataSource
                });
            });
        }

        void OnDataSourceToggleChanged(ChangeEvent<bool> evt)
        {
            UpdateInfoBoxVisibility(evt.target as Toggle);
        }
    }
}
