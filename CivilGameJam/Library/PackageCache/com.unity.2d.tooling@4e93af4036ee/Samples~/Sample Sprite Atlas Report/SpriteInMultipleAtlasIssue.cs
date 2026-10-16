using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Properties;
using UnityEditor;
using UnityEditor.U2D.Tooling.Analyzer;
using UnityEditor.U2D.Tooling.Analyzer.UIElement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SampleReport.Reports
{
    /// <summary>
    /// Generates a report identifying Sprites that are included in multiple Sprite Atlases.
    /// </summary>
    class SpriteInMultipleAtlasIssue : AnalyzerIssueReportBase
    {
        /// The GUID for the associated style sheet asset.
        const string k_USSPath = "99d5a6a1b13e4a648392a5aef877ed80";

        /// The UI element displaying the report content in a multi-column tree view.
        MultiColumnTreeView m_ReportContent;

        /// The data source providing Sprite Atlas data for the report.
        SpriteAtlasDataSource m_DataSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpriteInMultipleAtlasIssue"/> class and sets up the report UI.
        /// </summary>
        public SpriteInMultipleAtlasIssue()
            : base(new [] {typeof(SpriteAtlasDataSource)})
        {
            SetReportListItemName("Sprites in Multiple Atlases");
            SetReportListemCount("0");
            SetupReportContent();
            if(EditorGUIUtility.isProSkin)
                m_ReportContent.AddToClassList("dark");
            m_ReportContent.AddToClassList("report-content");
            var path = AssetDatabase.GUIDToAssetPath(k_USSPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            m_ReportContent.styleSheets.Add(styleSheet);
        }

        /// <summary>
        /// Sets up the columns and tree view for the report UI.
        /// </summary>
        void SetupReportContent()
        {
            var columns = new Columns()
            {
                new Column()
                {
                    name = "Name",
                    title = "Name",
                    width = Length.Pixels(100),
                    makeCell = () =>
                    {
                        var ve = new VisualElement()
                        {
                            name = "cell"
                        };
                        var icon = new VisualElement()
                        {
                            name = "icon"
                        };
                        var label = new Label()
                        {
                            name ="name",
                            text = ""
                        };
                        ve.Add(icon);
                        ve.Add(label);
                        return ve;
                    },
                    bindCell = (e, i) =>
                    {
                        var cellData = m_ReportContent.GetItemDataForIndex<SpriteInMultipleAtlasCellData>(i);
                        if (cellData != null)
                        {
                            e.Q<Label>().SetBinding("text", new DataBinding()
                            {
                                dataSourcePath = new PropertyPath("name")
                            });
                            e.dataSource = cellData;
                            var icon = e.Q("icon");
                            icon.RemoveFromClassList("spriteatlas-icon");
                            icon.RemoveFromClassList("sprite-icon");
                            icon.AddToClassList(cellData.icon);
                        }
                    }
                },
                new Column()
                {
                    name = "Count",
                    title = "Count",
                    width = Length.Pixels(100),
                    makeCell = () => new Label()
                    {
                        name = "countLabel",
                        text = ""
                    },
                    bindCell = (element, i) =>
                    {
                        var cellData = m_ReportContent.GetItemDataForIndex<SpriteInMultipleAtlasCellData>(i);
                        ((Label)element).text = cellData.childCount;
                    }
                }
            };
            m_ReportContent = new MultiColumnTreeView(columns)
            {
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };

            m_ReportContent.selectionChanged += OnSelectionChanged;
        }

        /// <summary>
        /// Handles selection changes in the report list, opening the selected Sprite or Sprite Atlas in the inspector.
        /// </summary>
        /// <param name="obj">The selected objects.</param>
        void OnSelectionChanged(IEnumerable<object> obj)
        {
            var item = m_ReportContent.GetItemDataForIndex<SpriteInMultipleAtlasCellData>(m_ReportContent.selectedIndex);
            if(item != null)
                InspectObject(GlobalObjectIdtoObect(item.objectGlobalID));
        }

        /// <summary>
        /// Converts a global object ID string to a UnityEngine.Object instance.
        /// </summary>
        /// <param name="itemObjectGlobalID">The global object ID of the Sprite or Sprite Atlas.</param>
        /// <returns>The corresponding UnityEngine.Object, or null if not found.</returns>
        Object GlobalObjectIdtoObect(string itemObjectGlobalID)
        {
            if(GlobalObjectId.TryParse(itemObjectGlobalID, out var id))
            {
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                return obj;
            }

            return null;
        }

        /// <summary>
        /// Gets the main report content UI element.
        /// </summary>
        public override VisualElement reportContent => m_ReportContent;

        /// <summary>
        /// Gets the settings UI element for the report (not used in this report).
        /// </summary>
        public override VisualElement settingsContent => null;

        /// <summary>
        /// Gets the title of the report.
        /// </summary>
        public override string reportTitle => "Sprites in Multiple Atlases";

        /// <summary>
        /// Called when the report data source changes, triggering a data refresh.
        /// </summary>
        /// <param name="reportDataSource">The new data source.</param>
        protected override async void OnReportDataSourceChanged(IReportDataSource reportDataSource)
        {
            if (reportDataSource is SpriteAtlasDataSource dataSource)
            {
                m_DataSource = dataSource;
                isFilteringReport = true;
                var filteredData = await FilterData(dataSource.data);
                m_ReportContent.SetRootItems(filteredData);
                m_ReportContent.Rebuild();
                SetReportListemCount($"{filteredData.Count}");
            }
        }

        /// <summary>
        /// Filters the Sprite Atlas data to find Sprites included in multiple Atlases and prepares the tree view items.
        /// </summary>
        /// <param name="dataSource">The Sprite Atlas data source.</param>
        /// <returns>A list of tree view items representing Sprites in multiple Atlases.</returns>
        async Task<List<TreeViewItemData<SpriteInMultipleAtlasCellData>>> FilterData(List<EditorAtlasInfo> dataSource)
        {
            List<TreeViewItemData<SpriteInMultipleAtlasCellData>> result = new();
            Dictionary<string, (EditorSpriteInfo spriteInfo, List<EditorAtlasInfo> editorAtlasInfoList)> uniqueSprite = new ();
            for(int i = 0; i < dataSource.Count; ++i)
            {
                var atlasInfo = dataSource[i];
                if (atlasInfo.isVariant)
                    continue;
                await Task.Delay(10);

                foreach(var spriteInfo in atlasInfo.spriteInfo)
                {
                    if (spriteInfo == null)
                        continue;
                    uniqueSprite.TryGetValue(spriteInfo.globalObjectIDString, out var existingAtlasInfo);
                    if (existingAtlasInfo.editorAtlasInfoList == null)
                    {
                        existingAtlasInfo = new ();
                        existingAtlasInfo.spriteInfo = spriteInfo;
                        existingAtlasInfo.editorAtlasInfoList = new();
                    }
                    if(!existingAtlasInfo.editorAtlasInfoList.Contains(atlasInfo))
                        existingAtlasInfo.editorAtlasInfoList.Add(atlasInfo);
                    uniqueSprite[spriteInfo.globalObjectIDString] = existingAtlasInfo;
                }
            }

            int id = 0;
            foreach(var item in uniqueSprite)
            {
                if (item.Value.editorAtlasInfoList.Count > 1)
                {
                    List<TreeViewItemData<SpriteInMultipleAtlasCellData>> children = new();
                    for(int i = 0; i < item.Value.editorAtlasInfoList.Count; ++i)
                    {
                        var atlasInfo = item.Value.editorAtlasInfoList[i];
                        var atlasCellData = new SpriteInMultipleAtlasCellData
                        {
                            name = atlasInfo.name,
                            icon = "spriteatlas-icon",
                            objectGlobalID = atlasInfo.globalObjectIDString,
                            childCount = ""
                        };
                        children.Add(new (id++, atlasCellData));
                    }
                    var cellData = new SpriteInMultipleAtlasCellData
                    {
                        name = item.Value.spriteInfo.name,
                        icon = "sprite-icon",
                        objectGlobalID = item.Value.spriteInfo.globalObjectIDString,
                        childCount = children.Count.ToString()
                    };
                    result.Add(new TreeViewItemData<SpriteInMultipleAtlasCellData>(id++,cellData,children));
                }
            }
            return result;
        }
    }
}
