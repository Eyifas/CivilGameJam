using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Properties;
using UnityEditor.U2D.Sprites;
using UnityEditor.U2D.Tooling.Analyzer.UIElement;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class SpriteWithSecondaryTextureIssue : AnalyzerIssueReportBase
    {
        const string k_Uxml = "Packages/com.unity.2d.tooling/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/SpriteWithSecondaryTextureIssue/SpriteWithSecondaryTextureIssue.uxml";
        const string k_SaveFilePath = "Library/com.unity.2d.tooling/AnalyzerWindow/SpriteWithSecondaryTextureIssue.json";
        VisualElement m_IssueView;
        MultiColumnTreeView m_Table;
        List<TreeViewItemData<SpriteWithSecondayTextureCellData>> m_TableData = new();
        Label m_NoDataLabel;
        SpriteAtlasDataSource m_DataSource;
        public SpriteWithSecondaryTextureIssue(): base(new [] {typeof(SpriteAtlasDataSource)})
        {
            SetReportListItemName("Textures contain different secondary texture count in Sprite Atlas");
            SetReportListemCount("0");

            m_IssueView = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).Instantiate();

            m_Table = m_IssueView.Q<MultiColumnTreeView>("Table");
            //m_Table.AddManipulator(new ContextualMenuManipulator(OnContextualMenuManipulator));
            m_Table.selectionChanged += OnSelectionChanged;
            if(EditorGUIUtility.isProSkin)
                m_IssueView.AddToClassList("dark");
            else
                m_IssueView.AddToClassList("light");
            SetupTable();
            m_NoDataLabel = m_IssueView.Q<Label>("NoDataLabel");
            ShowTable(false, "Analyze has not been done yet.");
        }

        void ShowTable(bool show, string noShowReason)
        {
            m_Table.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            m_NoDataLabel.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            if (!string.IsNullOrEmpty(noShowReason) && !show)
                m_NoDataLabel.text = noShowReason;
        }

        void OnSelectionChanged(IEnumerable<object> obj)
        {
            var data = m_Table.GetItemDataForIndex<SpriteWithSecondayTextureCellData>(m_Table.selectedIndex);
            if(data != null)
                InspectObject(data.instanceId.GetAsset());
        }

        public override VisualElement reportContent => m_IssueView;
        public override VisualElement settingsContent => null;
        public override string reportTitle => "Atlas Secondary Texture";

        protected override async void OnReportDataSourceChanged(IReportDataSource reportDataSource)
        {
            if (reportDataSource is SpriteAtlasDataSource dataSource)
            {
                await SetDataSourceProvider(dataSource);
            }

        }

        public async Task SetDataSourceProvider(SpriteAtlasDataSource dataSource)
        {
            if (dataSource?.data != null)
            {
                await SetDataSource(dataSource);
            }
        }

        async Task SetDataSource(SpriteAtlasDataSource dataSource)
        {
            if (dataSource == null)
                return;
            isFilteringReport = true;
            m_DataSource = dataSource;
            await UpdateTableView(false);
        }

        async Task<List<TreeViewItemData<SpriteWithSecondayTextureCellData>>> FilterDataAsync(List<EditorAtlasInfo> dataSource)
        {
            SpriteDataProviderFactories spriteDataFactory = new();
            spriteDataFactory.Init();
            var result = new List<TreeViewItemData<SpriteWithSecondayTextureCellData>>();
            // collect all unique data provider
            var uniqueDataPath = new Dictionary<string, (string assetName, EntityId entityId, int textureCount)>();
            var spriteProcessed = 0;
            for (int i = 0; i < dataSource?.Count; ++i)
            {
                var atlasInfo = dataSource[i];
                if (atlasInfo.isVariant)
                    continue;
                for (int j = 0; j < atlasInfo?.textureInfo.Count; ++j)
                {
                    var textureInfo = atlasInfo.textureInfo[j];
                    for (int k = 0; k < textureInfo?.spriteInfo.Count; ++k, ++spriteProcessed)
                    {
                        if (spriteProcessed > 100)
                        {
                            await Task.Delay(10);
                            spriteProcessed = 0;
                        }

                        var si = textureInfo.spriteInfo[k];

                        if (!uniqueDataPath.ContainsKey(si.assetPath))
                        {
                            var sprite = AssetDatabase.LoadMainAssetAtPath(si.assetPath);
                            if (sprite != null)
                            {
                                var dp = spriteDataFactory.GetSpriteEditorDataProviderFromObject(sprite);
                                dp.InitSpriteEditorDataProvider();
                                var stdp = dp.GetDataProvider<ISecondaryTextureDataProvider>();
                                uniqueDataPath.Add(si.assetPath, (sprite.name, sprite.GetEntityId(), stdp?.textures?.Length ?? 0));
                            }
                        }
                    }
                }
            }

            ShowTable(false, $"Building table...");
            int rowId = 0;
            // check if any of the sprite atlas has different texture count
            for (int i = 0; i < dataSource?.Count; ++i)
            {
                var atlasInfo = dataSource[i];
                if (atlasInfo.isVariant)
                    continue;
                if (atlasInfo?.textureInfo.Count <= 0)
                    continue;

                // Get first sprite from first texture to establish baseline
                EditorSpriteInfo firstSprite = null;
                foreach (var textureInfo in atlasInfo.textureInfo)
                {
                    if (textureInfo?.spriteInfo.Count > 0)
                    {
                        firstSprite = textureInfo.spriteInfo[0];
                        break;
                    }
                }

                if (firstSprite == null || !uniqueDataPath.ContainsKey(firstSprite.assetPath))
                    continue;

                var sanity = uniqueDataPath[firstSprite.assetPath].textureCount;
                bool foundDifference = false;

                for (int j = 0; j < atlasInfo.textureInfo.Count; ++j)
                {
                    var textureInfo = atlasInfo.textureInfo[j];
                    for (int k = 0; k < textureInfo?.spriteInfo.Count; ++k, ++spriteProcessed)
                    {
                        if (spriteProcessed > 100)
                        {
                            await Task.Delay(10);
                            spriteProcessed = 0;
                        }
                        var si = textureInfo.spriteInfo[k];
                        if(!uniqueDataPath.ContainsKey(si.assetPath))
                            continue;
                        if (uniqueDataPath[si.assetPath].textureCount != sanity)
                        {
                            foundDifference = true;
                            break;
                        }
                    }
                    if (foundDifference)
                        break;
                }

                if (foundDifference)
                {
                    var children = BuildTextureTree(ref rowId, atlasInfo, uniqueDataPath);
                    // we have a problematic atlas
                    result.Add(new (rowId++,
                        new SpriteWithSecondayTextureCellData
                        {
                            icon = "spriteatlas-icon",
                            name = atlasInfo.name,
                            count = children.Count,
                            instanceId =  atlasInfo.entityId
                        }, children));
                    ++rowId;
                }
            }
            return result;
        }

        List<TreeViewItemData<SpriteWithSecondayTextureCellData>> BuildTextureTree(ref int rowId, EditorAtlasInfo atlasInfo,
            Dictionary<string, (string assetName, EntityId entityId, int textureCount)> data)
        {
            List<TreeViewItemData<SpriteWithSecondayTextureCellData>> result = new();
            HashSet<string> uniquePath = new();
            for (int j = 0; j < atlasInfo?.textureInfo.Count; ++j)
            {
                var textureInfo = atlasInfo.textureInfo[j];
                for (int k = 0; k < textureInfo?.spriteInfo.Count; ++k)
                {
                    var spriteInfo = textureInfo.spriteInfo[k];
                    if (!uniquePath.Contains(spriteInfo.assetPath))
                    {
                        result.Add(new(rowId++,
                            new SpriteWithSecondayTextureCellData
                            {
                                icon = "texture-icon",
                                name = data[spriteInfo.assetPath].assetName,
                                count = data[spriteInfo.assetPath].textureCount,
                                instanceId = data[spriteInfo.assetPath].entityId
                            }));
                        uniquePath.Add(spriteInfo.assetPath);
                    }
                }
            }
            return result;
        }

        void SetNameColumnCellIcon(VisualElement ele, SpriteWithSecondayTextureCellData data)
        {
            var icon = ele.Q("Icon");
            icon.RemoveFromClassList("texture-icon");
            icon.RemoveFromClassList("sprite-icon");
            icon.RemoveFromClassList("spriteatlas-icon");
            if(data.icon?.Length > 0)
                icon.AddToClassList(data.icon);
        }

        void SetupTable()
        {
            m_Table.sortingMode = ColumnSortingMode.Custom;
            m_Table.columnSortingChanged += OnColumnSortingChanged;
            m_Table.SetRootItems(m_TableData);
            for(int i = 0; i < m_Table.columns.Count; ++i)
            {
                var column = m_Table.columns[i];
                column.sortable = true;
                if (column.name == "Name")
                {
                    column.bindCell = (element, i) => // item is template
                    {
                        var itemData = m_Table.GetItemDataForIndex<SpriteWithSecondayTextureCellData>(i);
                        (element as CellLabelWithIcon).BindLabel(new DataBinding
                        {
                            dataSourcePath = new PropertyPath(column.bindingPath),
                        });
                        element.dataSource = itemData;
                        SetNameColumnCellIcon(element, itemData);
                    };
                }
                else
                {
                    column.bindCell = (element, i) => // item is template
                    {
                        var itemData = m_Table.GetItemDataForIndex<SpriteWithSecondayTextureCellData>(i);
                        (element as CellLabelWithIcon).BindLabel(new DataBinding
                        {
                            dataSourcePath = new PropertyPath(column.bindingPath),
                        });
                        element.dataSource = itemData;
                    };
                }

                column.comparison = (a, b) =>
                {
                    var itemA = m_Table.GetItemDataForIndex<SpriteWithSecondayTextureCellData>(a);
                    var itemB = m_Table.GetItemDataForIndex<SpriteWithSecondayTextureCellData>(b);
                    return SpriteWithSecondayTextureCellData.Compare(itemA, itemB, column.bindingPath);
                };
                column.makeCell = () =>
                {
                    return new CellLabelWithIcon();
                };
            }
        }

        async void OnColumnSortingChanged()
        {
            await UpdateTableView(true, true);
        }

        async Task UpdateTableView(bool keepExpand, bool sortUpdate = false)
        {
            using (new ExecutionTime("SpriteWithSecondaryTextureIssue Report"))
            {
                ShowTable(false, "Filtering data in progress...");
                if (!sortUpdate)
                {
                    var saveFile = Utilities.LoadSaveDataFromFile<ReportSaveDataRoot<SpriteWithSecondayTextureCellData>>(k_SaveFilePath);
                    if (saveFile == null || saveFile.lastCaptureTime != m_DataSource.lastCaptureTime)
                    {
                        m_TableData = await FilterDataAsync(m_DataSource.data);
                        Utilities.WriteSaveDataToFile(k_SaveFilePath, new ReportSaveDataRoot<SpriteWithSecondayTextureCellData>()
                        {
                            lastCaptureTime = m_DataSource.lastCaptureTime,
                            root = ReportSaveDataRoot<SpriteWithSecondayTextureCellData>.CovertToSaveFormat(m_TableData)
                        });
                    }
                    else
                    {
                        m_TableData = ReportSaveDataRoot<SpriteWithSecondayTextureCellData>.ConvertToTreeViewItemData(saveFile.root);
                    }
                }

                SortTableData();

                List<int> expandedIds = new();
                if (keepExpand)
                {
                    foreach (var d in m_TableData)
                    {
                        if(m_Table.IsExpanded(d.id))
                            expandedIds.Add(d.id);
                    }
                }
                m_Table.SetRootItems(m_TableData);
                m_Table.Rebuild();
                foreach(var expand in expandedIds)
                    m_Table.ExpandItem(expand);
                isFilteringReport = false;
                SetReportListemCount($"{m_TableData.Count}");
                ShowTable(m_TableData.Count > 0, "No Sprite Atlas where source texture has different secondary texture found.");
            }
        }

        void SortTableData()
        {
            if(m_Table.sortedColumns != null)
            {
                var rawData = ReportSaveDataRoot<SpriteWithSecondayTextureCellData>.CovertToSaveFormat(m_TableData);
                SortTableData(rawData);
                m_TableData = ReportSaveDataRoot<SpriteWithSecondayTextureCellData>.ConvertToTreeViewItemData(rawData);
            }
        }

        void SortTableData(List<ReportSaveData<SpriteWithSecondayTextureCellData>> rawData)
        {
            rawData.Sort(SortRawData);
            foreach(var row in rawData)
            {
                if(row.children != null && row.children.Count > 0)
                {
                    row.children.Sort(SortRawData);
                    SortTableData(row.children);
                }
            }
        }

        int SortRawData(ReportSaveData<SpriteWithSecondayTextureCellData> x , ReportSaveData<SpriteWithSecondayTextureCellData> y)
        {
            using (var enumerator = m_Table.sortedColumns.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    int result = SpriteWithSecondayTextureCellData.Compare(x.data, y.data, enumerator.Current.column.bindingPath);
                    if (result != 0)
                        return result * (enumerator.Current.direction == SortDirection.Ascending ? 1 : -1);
                }
            }

            return SpriteWithSecondayTextureCellData.Compare(x.data, y.data, null);
        }
    }
}
