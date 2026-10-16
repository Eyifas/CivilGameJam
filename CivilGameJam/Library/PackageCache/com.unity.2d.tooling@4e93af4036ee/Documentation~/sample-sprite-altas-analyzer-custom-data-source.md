# Sample Sprite Data Source and Report

This sample demonstrates how to create a custom data source and report for the Sprite Atlas Analyzer. It shows how to collect sprite mesh information (vertex and triangle counts) and create a filterable report to analyze sprite performance.

## Overview

The sample consists of two main parts:

### Data Source Components
1. **SpriteDataSource.cs** - Custom data source that implements `IReportDataSource`
2. **SpriteCaptureData.cs** - Data structures for storing captured sprite information

### Report Components
1. **SpriteMeshSizeReport.cs** - Custom report that inherits from `AnalyzerIssueReportBase`
2. **SpriteMeshSizeFilteredData.cs** - Data structure for filtered report items
3. **SpriteMeshSizeReportSettings.cs** - Settings UI for configuring report filters
4. **SpriteMeshSizeReportSettings.uxml** - UXML layout for the settings UI
5. **SpriteMeshSizeReport.uss** - Styling for the report UI

## How to Create a Custom Data Source

### 1. Create Data Structures

First, define the data structures to store your captured information:

```csharp
[Serializable]
class YourCaptureData : ISaveData
{
    public List<YourAssetData> assetData = new();
    public long lastCaptureTime;
}

[Serializable]
class YourAssetData
{
    public string assetPathGuid;
    public long fileModifiedTime;
    public long metaFileModifiedTime;
    // Add your specific data fields
}
```

### 2. Implement IReportDataSource

Create a class that implements `IReportDataSource`:

```csharp
class YourDataSource : IReportDataSource
{
    YourCaptureData m_Capture = new();
    bool m_Cancel;
    Task<YourCaptureData> m_CaptureTask;

    public event Action<IReportDataSource> onDataSourceChanged;
    public event Action<IReportDataSource> onCaptureStart;
    public event Action<IReportDataSource> onCaptureEnd;

    public bool capturing { get; private set; }
    public string name => "Your Data Source Name";
    public long lastCaptureTime => m_Capture.lastCaptureTime;
    public List<YourAssetData> data => m_Capture.assetData;

    public async void Capture(string[] assetSearchPath)
    {
        m_Cancel = false;
        capturing = true;
        onCaptureStart?.Invoke(this);
        m_CaptureTask = CaptureData(m_Capture, assetSearchPath);
        await m_CaptureTask;
        m_Capture = m_CaptureTask.Result;
        capturing = false;
        onCaptureEnd?.Invoke(this);
        onDataSourceChanged?.Invoke(this);
    }

    public void StopCapture() => m_Cancel = true;
    public void Dispose() => m_Cancel = true;
    public void Save(ISaveFile saveData) => saveData.AddSaveData(m_Capture);
    public void Load(ISaveFile saveData) { /* Load implementation */ }
}
```

### 3. Implement Data Capture Logic

Create the asynchronous capture method:

```csharp
async Task<YourCaptureData> CaptureData(YourCaptureData prevCapture, string[] assetSearchPath)
{
    int id = Progress.Start("Your Data Capture");
    var capture = new YourCaptureData();

    // Find assets of your target type
    string[] guids = AssetDatabase.FindAssets("t:YourAssetType", assetSearchPath);
    HashSet<string> pathVisited = new();

    for (int i = 0; i < guids.Length && !m_Cancel; ++i)
    {
        Progress.Report(id, i, guids.Length, "Capturing data");
        var path = AssetDatabase.GUIDToAssetPath(guids[i]);

        if (!pathVisited.Add(path))
            continue;

        // Check if asset changed since last capture
        if (!HasAssetChanged(prevCapture, path))
        {
            // Reuse existing data
            continue;
        }

        // Capture new data
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        // Process your assets here

        await Task.Delay(10); // Yield control periodically
    }

    Progress.Remove(id);
    capture.lastCaptureTime = DateTime.UtcNow.ToFileTimeUtc();
    return capture;
}
```

## How to Create a Custom Report

### 1. Create a Report Class

Create a class that inherits from `AnalyzerIssueReportBase`:

```csharp
class YourCustomReport : AnalyzerIssueReportBase
{
    MultiColumnListView m_ReportContent;
    YourDataSource m_DataSource;
    List<YourFilteredData> m_Filtered = new();

    public YourCustomReport() : base(new[] { typeof(YourDataSource) })
    {
        SetReportListItemName("Your Report Name");
        SetReportListemCount("0");
        SetupReportContent();
    }

    public override VisualElement reportContent => m_ReportContent;
    public override VisualElement settingsContent => m_Settings; // or null
    public override string reportTitle => "Your Report Title";
}
```

### 2. Handle Data Source Changes

Override `OnReportDataSourceChanged` to process data:

```csharp
protected override async void OnReportDataSourceChanged(IReportDataSource reportDataSource)
{
    if (reportDataSource is YourDataSource dataSource)
    {
        m_DataSource = dataSource;
        await FilterData(dataSource);
    }
}
```

### 3. Implement Data Filtering

Create asynchronous filtering logic:

```csharp
async Task FilterData(YourDataSource dataSource)
{
    m_Filtered = new();
    var task = Task.Run(() =>
    {
        for (int i = 0; i < dataSource?.data?.Count; ++i)
        {
            var assetData = dataSource.data[i];
            // Apply your filtering logic here
            if (ShouldIncludeInReport(assetData))
            {
                m_Filtered.Add(new YourFilteredData()
                {
                    // Map your data
                });
            }
        }
    });

    isFilteringReport = true;
    await task;
    m_ReportContent.itemsSource = m_Filtered;
    m_ReportContent.Rebuild();
    isFilteringReport = false;
    SetReportListemCount($"{m_Filtered.Count}");
}
```

### 4. Setup Report UI

Configure the MultiColumnListView:

```csharp
void SetupReportContent()
{
    var columns = new[]
    {
        new Column()
        {
            title = "Name",
            width = Length.Pixels(100),
            makeCell = () => new Label(),
            bindingPath = "name"
        },
        // Add more columns as needed
    };

    m_ReportContent = new MultiColumnListView();
    m_ReportContent.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
    m_ReportContent.selectionChanged += OnSelectionChanged;

    // Setup column binding
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
        m_ReportContent.columns.Add(columns[i]);
    }
}
```

### 5. Create Settings UI (Optional)

If your report needs configurable settings:

```csharp
public class YourReportSettings : VisualElement
{
    const string k_Uxml = "your-uxml-guid";

    public event Action<SettingsType> onApplyClickedEvent;

    public YourReportSettings(SettingsType initialSettings)
    {
        var path = AssetDatabase.GUIDToAssetPath(new GUID(k_Uxml));
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        uxml.CloneTree(this);

        // Setup UI elements and event handlers
    }
}
```

## Key Features Demonstrated

### Data Source Features
- **Asynchronous Data Capture**: Non-blocking data collection with progress reporting
- **Incremental Updates**: Only recapture data when assets have changed
- **Save/Load Support**: Persist captured data across sessions
- **Progress Reporting**: Visual feedback during long-running operations

### Report Features
- **Custom Data Source Integration**: Works with your custom data source
- **Asynchronous Filtering**: Non-blocking data processing
- **Multi-Column Display**: Flexible columnar data presentation
- **Interactive Selection**: Object inspection integration
- **Configurable Settings**: User-customizable report parameters
- **Real-time Updates**: Automatic refresh when data changes

