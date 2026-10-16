using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Properties;
using UnityEditor;
using UnityEditor.U2D.Tooling.Analyzer;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleReport.Reports
{
    /// <summary>
    /// Generates a report displaying Sprite mesh sizes, including vertex and triangle counts, with filtering and selection capabilities.
    /// </summary>
    class SpriteMeshSizeReport : AnalyzerIssueReportBase
    {
        /// The GUID for the associated style sheet asset.
        const string k_USSPath = "dc4c87ad85ef43e8a26b34b65a3a1009";

        /// The UI element displaying the report content in a multi-column list view.
        MultiColumnListView m_ReportContent;

        /// The settings UI for configuring mesh size filters.
        SpriteMeshSizeReportSettings m_Settings;

        /// The filtered list of Sprite mesh data based on the current filter settings.
        List<SpriteMeshSizeFilteredData> m_Filtered = new();

        /// The minimum vertex count filter for Sprites.
        int m_VertexCountFilter = 100;

        /// The minimum triangle count filter for Sprites.
        int m_TriangleCountFilter = 100;

        /// The data source providing Sprite data for the report.
        SpriteDataSource m_DataSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpriteMeshSizeReport"/> class and sets up the report UI.
        /// </summary>
        public SpriteMeshSizeReport() : base(new[] { typeof(SpriteDataSource) })
        {
            SetReportListItemName("Sprite Mesh Size");
            SetReportListemCount("0");
            m_ReportContent = new MultiColumnListView();
            if (EditorGUIUtility.isProSkin)
                m_ReportContent.AddToClassList("dark");
            m_ReportContent.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
            m_ReportContent.selectionChanged += OnSelectionChanged;
            SetupColumns();
            m_ReportContent.AddToClassList("report-content");
            var path = AssetDatabase.GUIDToAssetPath(k_USSPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            m_ReportContent.styleSheets.Add(styleSheet);
            m_Settings = new(m_VertexCountFilter, m_TriangleCountFilter);
            m_Settings.onApplyClickedEvent += OnApplyClicked;
        }

        /// <summary>
        /// Converts a global object ID string to a UnityEngine.Object instance.
        /// </summary>
        /// <param name="globalObjectId">The global object ID of the Sprite.</param>
        /// <returns>The corresponding UnityEngine.Object, or null if not found.</returns>
        UnityEngine.Object GlobalObjectIDToObject(string globalObjectId)
        {
            if (string.IsNullOrEmpty(globalObjectId))
                return null;

            if (GlobalObjectId.TryParse(globalObjectId, out var id))
            {
                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            }

            return null;
        }

        /// <summary>
        /// Handles selection changes in the report list, opening the selected Sprite in the inspector.
        /// </summary>
        /// <param name="obj">The selected objects.</param>
        void OnSelectionChanged(IEnumerable<object> obj)
        {
            if (m_ReportContent.selectedIndex >= 0 && m_ReportContent.selectedIndex < m_Filtered.Count)
            {
                var unityObject = GlobalObjectIDToObject(m_Filtered[m_ReportContent.selectedIndex].spriteData.spriteGlobalID);
                InspectObject(unityObject);
            }

            Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// Handles the apply event from the settings UI, updating the mesh size filters and refreshing the report.
        /// </summary>
        /// <param name="arg1">The new vertex count filter value.</param>
        /// <param name="arg2">The new triangle count filter value.</param>
        async void OnApplyClicked(int arg1, int arg2)
        {
            m_VertexCountFilter = arg1;
            m_TriangleCountFilter = arg2;
            await FilterData(m_DataSource);
        }

        /// <summary>
        /// Gets the main report content UI element.
        /// </summary>
        public override VisualElement reportContent => m_ReportContent;

        /// <summary>
        /// Gets the settings UI element for the report.
        /// </summary>
        public override VisualElement settingsContent => m_Settings;

        /// <summary>
        /// Gets the title of the report.
        /// </summary>
        public override string reportTitle => "Sprite Mesh Size";

        /// <summary>
        /// Called when the report data source changes, triggering a data refresh.
        /// </summary>
        /// <param name="reportDataSource">The new data source.</param>
        protected override async void OnReportDataSourceChanged(IReportDataSource reportDataSource)
        {
            if (reportDataSource is SpriteDataSource dataSource)
            {
                m_DataSource = dataSource;
                await FilterData(dataSource);
            }
        }

        /// <summary>
        /// Filters the Sprite data based on the current mesh size filters and updates the report list.
        /// </summary>
        /// <param name="dataSource">The Sprite data source.</param>
        async Task FilterData(SpriteDataSource dataSource)
        {
            m_Filtered = new();
            var t = Task.Run(() =>
            {
                for (int i = 0; i < dataSource?.data?.Count; ++i)
                {
                    var spriteAsset = dataSource.data[i];
                    for (int j = 0; j < spriteAsset.spriteData.Count; ++j)
                    {
                        var spriteData = spriteAsset.spriteData[j];
                        if (spriteData.vertexCount < m_VertexCountFilter && spriteData.triangleCount < m_TriangleCountFilter)
                            continue;
                        m_Filtered.Add(new SpriteMeshSizeFilteredData()
                        {
                            spriteData = spriteData,
                            name = spriteData.name,
                            vertices = spriteData.vertexCount,
                            triangles = spriteData.triangleCount,
                        });
                    }
                }
            });
            isFilteringReport = true;
            await t;
            m_ReportContent.itemsSource = m_Filtered;
            m_ReportContent.Rebuild();
            isFilteringReport = false;
            SetReportListemCount($"{m_Filtered.Count}");
        }

        /// <summary>
        /// Sets up the columns for the multi-column list view in the report UI.
        /// </summary>
        void SetupColumns()
        {
            var columns = new[]
            {
                new Column()
                {
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
                            name = "name",
                            text = "Name"
                        };
                        ve.Add(icon);
                        ve.Add(label);
                        return ve;
                    },
                    bindingPath = "name"
                },
                new Column()
                {
                    title = "Vertices",
                    width = Length.Pixels(80),
                    makeCell = () =>
                    {
                        var label = new Label()
                        {
                            name = "vertices",
                            text = "Vertices"
                        };
                        return label;
                    },
                    bindingPath = "vertices"
                },
                new Column()
                {
                    title = "Triangles",
                    width = Length.Pixels(80),
                    makeCell = () =>
                    {
                        var label = new Label()
                        {
                            name = "triangles",
                            text = "Triangles"
                        };
                        return label;
                    },
                    bindingPath = "triangles"
                }
            };

            for (int i = 0; i < columns.Length; ++i)
            {
                var bindingPath = columns[i].bindingPath;
                columns[i].bindCell = (e, k) =>
                {
                    var label = e.Q<Label>();
                    label.SetBinding("text", new DataBinding()
                    {
                        dataSourcePath = new PropertyPath(bindingPath)
                    });
                    e.dataSource = m_Filtered[k];
                };
            }

            for (int i = 0; i < columns.Length; ++i)
                m_ReportContent.columns.Add(columns[i]);
        }
    }
}
