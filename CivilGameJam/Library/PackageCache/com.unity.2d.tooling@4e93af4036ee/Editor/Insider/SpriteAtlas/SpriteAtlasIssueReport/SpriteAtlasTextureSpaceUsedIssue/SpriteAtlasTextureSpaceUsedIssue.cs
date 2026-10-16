using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Properties;
using UnityEditor.U2D.Tooling.Analyzer.UIElement;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class SpriteAtlasTextureSpaceUsedIssue : AnalyzerIssueReportBase
    {
        CommonSpriteAtlasIssueView m_View;
        SpriteAtlasReportTable m_Table;
        List<EditorAtlasInfo> m_Data;
        List<EditorAtlasInfo> m_Filtered = new();
        Column[] m_Columns;
        int m_Memory = 400;
        SpriteAtlasTextureSpaceUsedIssueSettings m_Settings;

        public SpriteAtlasTextureSpaceUsedIssue(): base(new [] {typeof(SpriteAtlasDataSource)})
        {
            m_View = new CommonSpriteAtlasIssueView();
            m_View.styleSheets.Add(CommonStyleSheet.iconStyleSheet);
            m_Settings = new SpriteAtlasTextureSpaceUsedIssueSettings(m_Memory);
            m_Settings.onMemorySizeChanged += OnMemorySizeChanged;
            SetReportListItemName();
            SetReportListemCount("0");
            m_View.ShowTable(false, "Analyze has not been done yet.");


            m_Table = m_View.table;
            m_Table.sortingMode = ColumnSortingMode.Default;
            m_Table.AddManipulator(new ContextualMenuManipulator(OnContextualMenuManipulator));
            table.selectionChanged += OnSelectionChanged;
            SetupColumns();
        }

        void OnSelectionChanged(IEnumerable<object> obj)
        {
            foreach (var o in obj)
            {
                if (o != null && o is EditorAtlasInfo record)
                {
                    InspectObject(record.GetObject());
                    break;
                }
            }
        }

        void OnContextualMenuManipulator(ContextualMenuPopulateEvent obj)
        {
            var menuStatus = m_Filtered.Count > 0 && table.selectedIndex >= 0 &&
                table.selectedIndex < table.itemsSource.Count ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            obj.menu.AppendAction("Reanalyze Atlas", a => RecheckAtlas(), menuStatus);
        }

        void RecheckAtlas()
        {
            var item = m_Filtered[table.selectedIndex].GetObject();
            if (item != null)
            {
                RequestCapture(new [] {AssetDatabase.GetAssetPath(item)});
            }
        }

        void SetReportListItemName()
        {
            SetReportListItemName($"Texture Space Wastage > {m_Memory} KB");
        }

        void SetupColumns()
        {
            m_Columns = new []
            {
                new Column
                {
                    title = "Name",
                    width = Length.Pixels(80),
                    sortable = true
                },
                new Column
                {
                    title = "Unused Memory",
                    width = Length.Pixels(80),
                    sortable = true
                }
            };

            m_Columns[0].makeCell = () =>
            {
                var ele = new CellLabelWithIcon();
                ele.SetIconClassName("spriteatlas-icon");
                return ele;
            };
            m_Columns[0].bindCell = (e, i) =>
            {
                (e as CellLabelWithIcon).BindLabel(new DataBinding
                {
                    dataSourcePath = new PropertyPath("m_Name"),
                });
                e.dataSource = m_Filtered[i];
            };
            m_Columns[0].comparison = (a, b) =>
            {
                var itemA = m_Filtered[a];
                var itemB = m_Filtered[b];
                return string.Compare(itemA.name.ToLower(), itemB.name.ToLower(), StringComparison.Ordinal);
            };
            m_Columns[1].makeCell = () =>
            {
                return new CellLabelWithIcon();
            };
            m_Columns[1].bindCell = (e, i) =>
            {
                (e as CellLabelWithIcon).BindLabel(new DataBinding
                {
                    dataSourcePath = new PropertyPath(""),
                });
                e.dataSource = EditorUtility.FormatBytes((long)m_Filtered[i].unusedMemory);
            };
            m_Columns[1].comparison = (a, b) =>
            {
                var itemA = m_Filtered[a];
                var itemB = m_Filtered[b];
                return itemA.unusedMemory.CompareTo(itemB.unusedMemory);
            };
            for (int i = 0; i < m_Columns.Length; ++i)
                table.columns.Add(m_Columns[i]);

        }

        MultiColumnListView table => m_Table.multiColumnListView;
        public override VisualElement reportContent => m_View;
        public override VisualElement settingsContent => m_Settings;
        public override string reportTitle => "Atlas Space Usage";

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
                await SetDataSource(dataSource.data);
            }
        }

        async Task SetDataSource(List<EditorAtlasInfo> dataSource)
        {
            using(new ExecutionTime("SpriteAtlasTextureSpaceUsedIssue Report"))
            {
                if (dataSource == null)
                    return;
                m_Data = dataSource;
                isFilteringReport = true;
                m_View.ShowTable(false, "Filtering data in progress...");
                m_Filtered = await FilterDataAsync(m_Data);
                table.itemsSource = m_Filtered;
                table.Rebuild();
                isFilteringReport = false;
                SetReportListemCount($"{m_Filtered.Count}");
                m_View.ShowTable(m_Filtered.Count > 0, $"No Sprite Atlas with greater than {m_Memory}KB wastage found.");
            }
        }

        async Task<List<EditorAtlasInfo>> FilterDataAsync(List<EditorAtlasInfo> dataSource)
        {
            var result = new List<EditorAtlasInfo>();
            var task = Task.Run(() =>
            {
                for(int i = 0; i < dataSource.Count; ++i)
                {
                    var atlasInfo = dataSource[i];
                    if (atlasInfo.isVariant)
                        continue;
                    if (atlasInfo.unusedMemory > m_Memory * 1024)
                    {
                        result.Add(atlasInfo);
                    }
                }
            });
            await task;
            return result;
        }

        async void OnMemorySizeChanged(int obj)
        {
            m_Memory = obj;
            SetReportListItemName();
            await SetDataSource(m_Data);
        }
    }
}
