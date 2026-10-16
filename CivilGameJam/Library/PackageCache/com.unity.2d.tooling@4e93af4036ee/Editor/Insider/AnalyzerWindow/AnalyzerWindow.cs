using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor.U2D.Tooling.Analyzer.UIElement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class AnalyzerWindow : EditorWindow, IDataSourceProvider
    {
        const string k_SaveFilePath = "Library/com.unity.2d.tooling/AnalyzerWindow/AnalyzerData.json";
        AnalyzerWindowSaveData m_SaveData = new();
        List<IAnalyzerReport> m_AllReports;
        List<IAnalyzerReport> m_Reports;
        List<IReportDataSource> m_DataSource;
        event Action m_OnDataSourceChange;
        Button m_AnalyzeButton;
        Button m_AnalyzeDataSourceButton;
        Button m_ClearDataSourceDataButton;
        ReportListView m_ReportListView;
        VisualElement m_ReportContentView;
        VisualElement m_ReportContentWithSettingsView;
        VisualElement m_ReportSettingView;
        VisualElement m_EmptyState;
        VisualElement m_DataSourceSetting;
        ToolbarButton m_HelpButton;
        DataSourceList m_DataSourceListView;
        TwoPaneSplitView m_SplitViewReport;
        Label m_ReportHeaderLabel;
        VisualElement m_ReportArea;
        bool m_Capturing;

        [MenuItem("Window/Analysis/Sprite Atlas Analyzer")]
        static void OpenWindow()
        {
            var window = GetWindow<AnalyzerWindow>();
            window.Show();
        }

        internal async Task Analyze(string[] path)
        {
            if (m_Capturing)
            {
                for(int i = 0; i < m_SaveData.reportDataSource.Count; ++i)
                {
                    if(m_SaveData.reportDataSource[i].reportDataSource.capturing)
                        m_SaveData.reportDataSource[i].reportDataSource.StopCapture();
                }

                OnDataSourceCaptureEnd(null);
            }

            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Analyzer", "Play Mode is active. Please exit Play Mode to start analysis Analysis", "OK");
                return;
            }

            if (path == null)
            {
                if (!m_Capturing)
                {
                    Capture(null, false);
                }
            }
            else
            {
                // wait for previous capture to finish before we start a new one from link
                while (m_Capturing)
                    await Task.Delay(100);
                Capture(path, true);
            }
        }

        void Capture(string[] path, bool enableAll)
        {
            m_Capturing = true;
            m_AnalyzeButton.AddToClassList("analyze-button-stop");
            m_AnalyzeDataSourceButton.AddToClassList("analyze-button-stop");
            m_AnalyzeButton.text = "Stop";
            m_AnalyzeDataSourceButton.SetEnabled(false);
            int dataSourceCapturing = 0;
            enableAll |= m_SaveData.reportDataSource.Count == 1;
            for(int i = 0; i < m_SaveData.reportDataSource.Count; ++i)
            {
                if (m_SaveData.reportDataSource[i].enabled || enableAll)
                {
                    var reportDataSource = m_SaveData.reportDataSource[i].reportDataSource;
                    reportDataSource.onCaptureEnd -= OnDataSourceCaptureEnd;
                    var paths = path ?? m_SaveData.reportDataSource[i].assetSearchPath;
                    reportDataSource.Capture(Utilities.RemoveChildDirectories(paths));
                    ++dataSourceCapturing;
                }
            }

            for (int i = 0; i < m_SaveData.reportDataSource.Count; ++i)
            {
                if (m_SaveData.reportDataSource[i].enabled || enableAll)
                {
                    var reportDataSource = m_SaveData.reportDataSource[i].reportDataSource;
                    if(reportDataSource.capturing)
                        reportDataSource.onCaptureEnd += OnDataSourceCaptureEnd;
                    else
                        OnDataSourceCaptureEnd(reportDataSource);
                }
            }

            if (dataSourceCapturing == 0)
                OnDataSourceCaptureEnd(null);
        }

        protected void OnEnable()
        {

            titleContent = new GUIContent("Sprite Atlas Analyzer");
            m_SaveData = Utilities.LoadSaveDataFromFile<AnalyzerWindowSaveData>(k_SaveFilePath) ?? new AnalyzerWindowSaveData();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.ExitingEditMode)
            {
                m_AnalyzeButton.SetEnabled(false);
                m_AnalyzeButton.tooltip = "Play Mode is active. Please exit Play Mode to start analysis Analysis";
            }
            else if (obj == PlayModeStateChange.EnteredEditMode)
            {
                m_AnalyzeButton.SetEnabled(true);
                m_AnalyzeButton.tooltip = "";
            }
        }

        protected  void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            for(int i = 0; i < m_AllReports?.Count; ++i)
            {
                m_AllReports[i].onInspectObject -= SelectUnityObject;
                m_AllReports[i].Dispose();
            }

            m_AllReports = null;

            for(int i = 0; i < m_DataSource?.Count; ++i)
            {
                m_DataSource[i].Dispose();
            }

            m_DataSource = null;
            Utilities.WriteSaveDataToFile(k_SaveFilePath, m_SaveData);
        }
        public void CreateGUI()
        {
            rootVisualElement.Add(GetWindowContent());
        }

        VisualElement GetWindowContent()
        {
            // Instantiate UXML
            VisualElement view = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.2d.tooling/Editor/Insider/AnalyzerWindow/AnalyzerWindow.uxml").Instantiate();

            if(EditorGUIUtility.isProSkin)
                view.AddToClassList("dark");

            m_ReportListView = view.Q<ReportListView>("IssueList");
            m_ReportListView.makeHeader = () =>
            {
                var label = new Label
                {
                    name = "IssueListHeaderLabel",
                    text = m_ReportListView.headerTitle
                };
                label.AddToClassList("issuelist-header-label");
                return label;
            };
            m_ReportListView.selectionChanged += OnSelectionChanged;

            m_ReportArea = view.Q<VisualElement>("ReportArea");
            m_ReportArea.style.display = DisplayStyle.None;
            m_ReportContentView = view.Q<VisualElement>("ReportContainer");
            m_ReportContentView.style.display = DisplayStyle.None;
            m_ReportHeaderLabel = view.Q<Label>("HeaderLabel");

            m_SplitViewReport = view.Q<TwoPaneSplitView>("SplitViewReport");
            m_SplitViewReport.style.display = DisplayStyle.None;

            m_ReportContentWithSettingsView = view.Q<VisualElement>("ReportContainerWithSettings");
            m_ReportSettingView = view.Q<VisualElement>("ReportSettings");

            m_EmptyState = view.Q<VisualElement>("EmptyState");
            m_DataSourceSetting = view.Q<VisualElement>("DataSourceSetting");
            m_DataSourceSetting.style.display = DisplayStyle.None;
            m_DataSourceListView = view.Q<DataSourceList>("DataSourceListView");
            InitReports();
            m_DataSourceListView.SetDataSource(m_SaveData.reportDataSource);

            m_ClearDataSourceDataButton = view.Q<Button>("ClearDataSourcesButton");
            m_ClearDataSourceDataButton.clickable.clicked += OnClearDataSourceDataButtonClicked;

            m_ReportListView.SetListDataSource(m_Reports);
            m_ReportListView.itemIndexChanged += OnItemIndexChanged;

            m_AnalyzeButton = view.Q<Button>("AnalyzeButton");
            m_AnalyzeButton.clicked += OnAnalyzeButtonClicked;
            if (Application.isPlaying)
            {
                m_AnalyzeButton.SetEnabled(false);
                m_AnalyzeButton.tooltip = "Play Mode is active. Please exit Play Mode to start analysis Analysis";
            }

            m_AnalyzeDataSourceButton = view.Q<Button>("AnalyzeDataSourceButton");
            m_AnalyzeDataSourceButton.clicked += OnAnalyzeDataSourceButtonClicked;

            m_HelpButton = view.Q<ToolbarButton>("HelpButton");
            m_HelpButton.clickable.clicked += () =>
            {
                //Help.BrowseURL("https://docs.unity3d.com/Packages/com.unity.2d.tooling@latest");
                Help.ShowHelpPage("https://docs.unity3d.com/Packages/com.unity.2d.tooling@latest");
            };
            return view;
        }

        void OnAnalyzeDataSourceButtonClicked()
        {
            m_ReportListView.selectedIndex = -1;
            m_DataSourceSetting.style.display = DisplayStyle.Flex;
            m_EmptyState.style.display = DisplayStyle.None;
            m_ReportArea.style.display = DisplayStyle.None;
            m_ReportHeaderLabel.text = "Data Source Configuration";
        }

        void OnItemIndexChanged(int arg1, int arg2)
        {
            m_SaveData.SaveReportPosition(m_Reports);
        }

        void InitReports()
        {
            if (m_DataSource == null)
            {
                m_DataSource = CollectReportDataSource();
                var dataSourceData = new List<DataSourceData>();
                for(int i = 0; i < m_DataSource.Count; ++i)
                {
                    if (m_SaveData?.saveData != null)
                        m_DataSource[i].Load(m_SaveData.saveData);
                    dataSourceData.Add(new DataSourceData
                    {
                        enabled = true,
                        typeName = m_DataSource[i].GetType().FullName,
                        reportDataSource = m_DataSource[i]
                    });
                    for(int j = 0; j < m_SaveData.reportDataSource?.Count; ++j)
                    {
                        if (m_SaveData.reportDataSource[j].typeName == dataSourceData[i].typeName)
                        {
                            dataSourceData[i].enabled = m_SaveData.reportDataSource[j].enabled;
                            dataSourceData[i].assetSearchPath = m_SaveData.reportDataSource[j].assetSearchPath;
                            break;
                        }
                    }
                }

                m_OnDataSourceChange?.Invoke();
                m_SaveData.reportDataSource = dataSourceData;
                if (m_DataSource.Count < 1)
                {
                    m_AnalyzeDataSourceButton.visible = false;
                }
            }

            if (m_AllReports == null)
            {
                m_AllReports = CollectAnalyzerReport(this);
                SetReportOrder();
                for(int i = 0; i < m_Reports.Count; ++i)
                {
                    //m_Reports[i].onShowReport += ShowReport;
                    m_Reports[i].onInspectObject += SelectUnityObject;
                }
            }
        }

        void SetReportOrder()
        {
            m_SaveData ??= new AnalyzerWindowSaveData();
            m_Reports = m_SaveData.OrderReport(m_AllReports);
        }

        async void OnAnalyzeButtonClicked()
        {
            var reportFiltering = false;
            foreach (var report in m_AllReports)
            {
                if (report.isFilteringReport)
                {
                    reportFiltering = true;
                    break;
                }
            }

            bool analyze = true;
            if (reportFiltering)
            {
                analyze = EditorUtility.DisplayDialog(
                    "Report Filtering Active",
                    "One or more reports are currently being processed. Proceeding with the analysis will reset all reports. Do you want to continue?",
                    "Proceed",
                    "Cancel");

            }
            if(analyze)
                await Analyze(null);
        }


       async void OnDataSourceCaptureEnd(IReportDataSource dataSource)
        {
            if (m_Capturing)
            {
                for (int i = 0; i < m_DataSource.Count; ++i)
                {
                    if (m_DataSource[i].capturing)
                        return;
                }
                m_AnalyzeButton.SetEnabled(false);
                var saveData = new SaveData();
                for(int i = 0; i < m_DataSource.Count; ++i)
                {
                    m_DataSource[i].Save(saveData);
                }

                m_SaveData.saveData = saveData;

                await UnloadResources();

                m_AnalyzeButton.RemoveFromClassList("analyze-button-stop");
                m_AnalyzeDataSourceButton.RemoveFromClassList("analyze-button-stop");
                m_AnalyzeButton.text = "Analyze";
                m_Capturing = false;
                m_AnalyzeButton.SetEnabled(true);
                m_AnalyzeDataSourceButton.SetEnabled(true);
            }
        }

        static async Task UnloadResources()
        {
            var op = Resources.UnloadUnusedAssets();
            var progress = new AnalyzerProgress();
            progress.StartProgressTrack("Atlas Analyzer");
            while (!op.isDone)
            {
                progress.UpdateProgressTrack(op.progress, "Unloading analyzed resources...");
                await Task.Delay(100);
            }
            progress.EndProgressTrack();
        }

        static List<IAnalyzerReport> CollectAnalyzerReport(IDataSourceProvider dataSourceProvider)
        {
            List<IAnalyzerReport> report = new List<IAnalyzerReport>();
            foreach (var moduleClassType in TypeCache.GetTypesDerivedFrom<IAnalyzerReport>())
            {
                if (moduleClassType.IsAbstract || !moduleClassType.IsClass)
                    continue;
                bool notDirectlyImplement = false;
                // we only want anyone that is directly implementing IAnalyzerReport
                foreach (var @interface in moduleClassType.GetInterfaces())
                {
                    if (!(@interface == typeof(IAnalyzerReport)) &&
                        typeof(IAnalyzerReport).IsAssignableFrom(@interface))
                    {
                        notDirectlyImplement = true;
                        break;
                    }
                }

                if (notDirectlyImplement)
                    continue;
                var constructorType = new Type[0];
                // Get the public instance constructor that takes ISpriteEditorModule parameter.
                var constructorInfoObj = moduleClassType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public, null,
                    CallingConventions.HasThis, constructorType, null);
                if (constructorInfoObj != null)
                {
                    try
                    {
                        var newInstance = constructorInfoObj.Invoke(new object[0]) as IAnalyzerReport;
                        if (newInstance != null)
                        {
                            newInstance.SetDataSourceProvider(dataSourceProvider);
                            report.Add(newInstance);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Unable to instantiate Analyzer Report " + moduleClassType.FullName + ". Exception:" + ex);
                    }
                }
                else
                    Debug.LogWarning(moduleClassType.FullName + " does not have a parameterless constructor");
            }

            return report;
        }

        static List<IReportDataSource> CollectReportDataSource()
        {
            List<IReportDataSource> report = new List<IReportDataSource>();
            foreach (var moduleClassType in TypeCache.GetTypesDerivedFrom<IReportDataSource>())
            {
                if (moduleClassType.IsAbstract && !moduleClassType.IsClass)
                    continue;
                var constructorType = new Type[0];
                // Get the public instance constructor that takes ISpriteEditorModule parameter.
                var constructorInfoObj = moduleClassType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public, null,
                    CallingConventions.HasThis, constructorType, null);
                if (constructorInfoObj != null)
                {
                    try
                    {
                        var newInstance = constructorInfoObj.Invoke(new object[0]) as IReportDataSource;
                        if (newInstance != null)
                        {
                            report.Add(newInstance);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Unable to instantiate Analyzer Report " + moduleClassType.FullName + ". Exception:" + ex);
                    }
                }
                else
                    Debug.LogWarning(moduleClassType.FullName + " does not have a parameterless constructor");
            }

            return report;
        }

        public T GetDataSource<T>() where T : class, IReportDataSource
        {
            for(int i = 0; i < m_DataSource.Count; ++i)
            {
                if(m_DataSource[i] is T result)
                    return result;
            }

            return null;
        }

        public IReportDataSource GetDataSource(Type t)
        {
            for(int i = 0; i < m_DataSource.Count; ++i)
            {
                if(m_DataSource[i].GetType() == t)
                    return m_DataSource[i];
            }
            return null;
        }

        public event Action onDataSourceChanged
        {
            add => m_OnDataSourceChange += value;
            remove => m_OnDataSourceChange -= value;
        }

        public void SelectUnityObject(IAnalyzerReport report, Object obj)
        {
            Selection.activeObject = obj;
        }

        void OnSelectionChanged(IEnumerable<object> _)
        {
            var index = m_ReportListView.selectedIndex;
            if(index >= 0 && index < m_Reports.Count)
            {
                m_EmptyState.style.display = DisplayStyle.None;
                m_DataSourceSetting.style.display = DisplayStyle.None;
                m_ReportArea.style.display = DisplayStyle.Flex;
                var contentItem = m_Reports[index].reportContent;
                var settingsContent = m_Reports[index].settingsContent;
                m_ReportContentView.Clear();
                m_ReportSettingView.Clear();
                m_ReportContentWithSettingsView.Clear();
                m_ReportHeaderLabel.text = m_Reports[index].reportTitle;
                if (settingsContent == null)
                {
                    m_SplitViewReport.style.display = DisplayStyle.None;
                    m_ReportContentView.style.display = DisplayStyle.Flex;
                    m_ReportContentView.Add(contentItem);
                }
                else
                {
                    m_SplitViewReport.style.display = DisplayStyle.Flex;
                    m_ReportContentView.style.display = DisplayStyle.None;
                    m_ReportContentWithSettingsView.Add(contentItem);
                    m_ReportSettingView.Add(settingsContent);
                }
            }
            else
            {
                m_SplitViewReport.style.display = DisplayStyle.None;
                m_ReportContentView.style.display = DisplayStyle.None;
                m_EmptyState.style.display = DisplayStyle.Flex;
                m_ReportHeaderLabel.text = "No Report Selected";
            }
        }

        void OnClearDataSourceDataButtonClicked()
        {
            m_SaveData.saveData = new SaveData();
            for(int i = 0; i < m_DataSource.Count; ++i)
            {
                m_DataSource[i].Load(m_SaveData.saveData);
            }
            m_OnDataSourceChange?.Invoke();
        }

        public static void ClearSaveData()
        {
            Utilities.WriteSaveDataToFile(k_SaveFilePath, new AnalyzerWindowSaveData());
        }
    }
}
