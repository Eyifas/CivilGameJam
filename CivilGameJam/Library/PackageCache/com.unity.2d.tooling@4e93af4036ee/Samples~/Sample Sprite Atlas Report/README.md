# Sample Sprite Atlas Report

This sample demonstrates how to create a custom report for the Sprite Atlas Analyzer by implementing a report that identifies sprites included in multiple Sprite Atlases.

## Overview

The sample consists of three main components:

1. **SpriteInMultipleAtlasIssue.cs** - The main report class that inherits from `AnalyzerIssueReportBase`
2. **SpriteInMultipleAtlasCellData.cs** - A data structure for representing cell data in the report UI
3. **SpriteInMultipleAtlasIssue.uss** - Styling for the report UI

## How to Create a New Report

To create your own custom report based on this sample:

### 1. Create a Report Class

Create a class that inherits from `AnalyzerIssueReportBase`:

```csharp
class YourCustomReport : AnalyzerIssueReportBase
{
    public YourCustomReport() : base(new [] {typeof(SpriteAtlasDataSource)})
    {
        SetReportListItemName("Your Report Name");
        SetReportListemCount("0");
        // Setup your UI here
    }
}
```

### 2. Define Required Properties

Implement the required abstract properties:

```csharp
public override VisualElement reportContent => m_ReportContent;
public override VisualElement settingsContent => null; // or your settings UI
public override string reportTitle => "Your Report Title";
```

### 3. Handle Data Source Changes

Override `OnReportDataSourceChanged` to process data when it becomes available:

```csharp
protected override async void OnReportDataSourceChanged(IReportDataSource reportDataSource)
{
    if (reportDataSource is SpriteAtlasDataSource dataSource)
    {
        isFilteringReport = true;
        var filteredData = await ProcessData(dataSource.data);
        // Update your UI with the processed data
        SetReportListemCount($"{filteredData.Count}");
    }
}
```

### 4. Create Data Processing Logic

Implement your data filtering and processing logic:

```csharp
async Task<List<TreeViewItemData<YourDataType>>> ProcessData(List<EditorAtlasInfo> dataSource)
{
    List<TreeViewItemData<YourDataType>> result = new();

    // Your custom logic here
    for(int i = 0; i < dataSource.Count; ++i)
    {
        var atlasInfo = dataSource[i];
        await Task.Delay(10); // Yield control periodically for large datasets

        // Process atlas data according to your requirements
    }

    return result;
}
```

### 5. Define Cell Data Structure

Create a data structure for your UI cells:

```csharp
[Serializable]
record YourCellData
{
    public string name;
    public string icon;
    public string objectGlobalID;
    // Add other properties as needed
}
```

### 6. Setup UI Elements

Configure your MultiColumnTreeView with appropriate columns:

```csharp
void SetupReportContent()
{
    var columns = new Columns()
    {
        new Column()
        {
            name = "Name",
            title = "Name",
            width = Length.Pixels(100),
            makeCell = () => /* Create your cell UI */,
            bindCell = (e, i) => /* Bind data to cell */
        }
        // Add more columns as needed
    };

    m_ReportContent = new MultiColumnTreeView(columns);
    m_ReportContent.selectionChanged += OnSelectionChanged;
}
```

## Key Features Demonstrated

- **Data Source Integration**: Uses `SpriteAtlasDataSource` to access Sprite Atlas data
- **Asynchronous Processing**: Handles large datasets without blocking the UI
- **Multi-Column Tree View**: Displays hierarchical data with custom columns
- **Object Selection**: Integrates with Unity's Inspector for selecting objects
- **Custom Styling**: Uses USS files for custom UI appearance
- **Global Object ID Handling**: Converts between global IDs and Unity objects


## Notes

This sample uses internal APIs from the package which are subject to change in future versions.
