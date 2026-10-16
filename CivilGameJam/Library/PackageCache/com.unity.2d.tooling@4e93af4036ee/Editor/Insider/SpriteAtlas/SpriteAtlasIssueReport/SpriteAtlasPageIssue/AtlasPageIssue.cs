using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Properties;
using UnityEditor.U2D.Tooling.Analyzer.UIElement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.U2D.Sprites;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class AtlasPageIssue : AnalyzerIssueReportBase
    {
        [Serializable]
        record AtlasPageIssueRecord
        {
            public EditorAtlasInfo atlasInfo;
            public int pageCount;
        }

        [Serializable]
        record AtlasPageIssueRecords
        {
            public List<AtlasPageIssueRecord> record;
        }

        const string k_SaveFilePath = "Library/com.unity.2d.tooling/AnalyzerWindow/AtlasPageIssue.json";
        CommonSpriteAtlasIssueView m_View;
        SpriteAtlasReportTable m_Table;
        List<AtlasPageIssueRecord> m_Filtered = new();
        Column[] m_Columns;
        int m_PageCont = 1;
        AtlasPageSettings m_Settings;


        public AtlasPageIssue(): base(new [] {typeof(SpriteAtlasDataSource)})
        {
            m_View = new CommonSpriteAtlasIssueView();
            m_View.styleSheets.Add(CommonStyleSheet.iconStyleSheet);

            SetReportListItemName();
            SetReportListemCount("0");
            m_View.ShowTable(false, "Analyze has not been done yet.");

            m_Settings = new AtlasPageSettings(m_PageCont);
            m_Settings.pageCountChanged += OnSettingsPageCountChanged;
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
                if (o != null && o is AtlasPageIssueRecord record)
                {
                    InspectObject(record.atlasInfo.GetObject());
                    break;
                }
            }
        }

        void OnContextualMenuManipulator(ContextualMenuPopulateEvent obj)
        {
            var menuStatus = m_Filtered.Count > 0 && table.selectedIndex >= 0 &&
                table.selectedIndex < table.itemsSource.Count ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            obj.menu.AppendAction("Reanalyze Atlas", (a) => RecheckAtlas(),
                menuStatus);
        }

        void RecheckAtlas()
        {
            var item = m_Filtered[table.selectedIndex].atlasInfo.GetObject();
            if (item != null)
            {
                RequestCapture(new [] {AssetDatabase.GetAssetPath(item)});
            }
        }

        void SetReportListItemName()
        {
            SetReportListItemName($"Atlas Page Count > {m_PageCont}");
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
                }, new Column
                {
                    title = "Page Count",
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
                    dataSourcePath = new PropertyPath("atlasInfo.m_Name"),
                });
                e.dataSource = m_Filtered[i];
            };
            m_Columns[0].comparison = (a, b) =>
            {
                var itemA = m_Filtered[a];
                var itemB = m_Filtered[b];
                return string.Compare(itemA.atlasInfo.name.ToLower(), itemB.atlasInfo.name.ToLower(), StringComparison.Ordinal);
            };
            m_Columns[1].makeCell = () =>
            {
                var ele = new CellLabelWithIcon();
                return ele;
            };
            m_Columns[1].bindCell = (e, i) =>
            {
                (e as CellLabelWithIcon).BindLabel(new DataBinding
                {
                    dataSourcePath = new PropertyPath(""),
                });
                e.dataSource = m_Filtered[i].pageCount;
            };
            m_Columns[1].comparison = (a, b) =>
            {
                var itemA = m_Filtered[a].pageCount;
                var itemB = m_Filtered[b].pageCount;
                return itemA.CompareTo(itemB);
            };
            for (int i = 0; i < m_Columns.Length; ++i)
                table.columns.Add(m_Columns[i]);
        }

        MultiColumnListView table => m_Table.multiColumnListView;

        public override VisualElement reportContent => m_View;
        public override VisualElement settingsContent => m_Settings;
        public override string reportTitle => "Atlas Texture Pages";

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
                isFilteringReport = true;
                try
                {
                    var saveFile = Utilities.LoadSaveDataFromFile<SaveData>(k_SaveFilePath);
                    List<AtlasPageIssueRecord> allData = null;
                    if (saveFile == null || dataSource.lastCaptureTime != saveFile.lastCaptureTime)
                    {
                        m_View.ShowTable(false, "Filtering data in progress...");
                        allData = await CollectDataAsync(dataSource.data);
                        AtlasPageIssueRecords records = new AtlasPageIssueRecords() { record = allData };
                        saveFile = new SaveData() { lastCaptureTime = dataSource.lastCaptureTime, root = JsonUtility.ToJson(records) };
                        Utilities.WriteSaveDataToFile(k_SaveFilePath, saveFile);
                    }
                    else
                    {
                        allData = JsonUtility.FromJson<AtlasPageIssueRecords>(saveFile.root)?.record ?? new List<AtlasPageIssueRecord>();
                    }

                    await SetDataSource(allData);
                }
                finally
                {
                    isFilteringReport = false;
                }
            }
        }

        async Task SetDataSource(List<AtlasPageIssueRecord> dataSource)
        {
            using (new ExecutionTime("AtlasPageIssue Report"))
            {
                if (dataSource == null)
                    return;
                m_Filtered = await FilterDataAsync(dataSource);
                table.itemsSource = m_Filtered;
                table.Rebuild();
                SetReportListemCount($"{m_Filtered.Count}");
                m_View.ShowTable(m_Filtered.Count > 0, $"No Sprite Atlas with pages greater than {m_PageCont} found.");
            }
        }

        async Task<List<AtlasPageIssueRecord>> CollectDataAsync(List<EditorAtlasInfo> dataSource)
        {
            var result = new List<AtlasPageIssueRecord>();
            var atlasprocessed = 0;
            for(int i = 0; i < dataSource.Count; ++i, ++atlasprocessed)
            {
                var atlasInfo = dataSource[i];
                // ignore variants
                if(atlasInfo.isVariant)
                    continue;
                if (atlasprocessed > 100)
                {
                    await Task.Delay(10);
                    atlasprocessed = 0;
                }

                if (atlasInfo.textureInfo != null)
                {
                    result.Add(new AtlasPageIssueRecord()
                    {
                        atlasInfo = atlasInfo,
                        pageCount = atlasInfo.textureInfo.Count
                    });
                }

            }
            return result;
        }
        Task<List<AtlasPageIssueRecord>> FilterDataAsync(List<AtlasPageIssueRecord> dataSource)
        {
            var result = new List<AtlasPageIssueRecord>();
            for(int i = 0; i < dataSource.Count; ++i)
            {
                var atlasInfo = dataSource[i];
                // ignore variants
                if (atlasInfo.pageCount > m_PageCont)
                {
                    result.Add(atlasInfo);
                }
            }
            return Task.FromResult(result);
        }

        async void OnSettingsPageCountChanged(int obj)
        {
            m_PageCont = obj;
            SetReportListItemName();
            var saveFile = Utilities.LoadSaveDataFromFile<SaveData>(k_SaveFilePath);
            List<AtlasPageIssueRecord> allData = null;
            if (saveFile != null)
            {
                allData = JsonUtility.FromJson<AtlasPageIssueRecords>(saveFile.root)?.record ?? new List<AtlasPageIssueRecord>();
                await SetDataSource(allData);
            }
        }

        record SaveData
        {
            public long lastCaptureTime;
            public string root;
        }
    }
}
