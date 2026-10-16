using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    interface IAnalyzerReport
    {
        /// <summary>
        /// The VisualElement that represents the report in Analyzer Window's list view.
        /// </summary>
        VisualElement listItem { get; }
        /// <summary>
        /// The VisualElement that represents the report in Report Window's view.
        /// </summary>
        VisualElement reportContent { get; }
        /// <summary>
        /// The VisualElement that represents the report in Report Window's view.
        /// </summary>
        VisualElement settingsContent { get; }
        /// <summary>
        /// The title of the report.
        /// </summary>
        string reportTitle { get; }
        /// <summary>
        /// The method is called to provide the report with the data source provider.
        /// </summary>
        /// <param name="dataSourceProvider">Data source provider avaiable currently.</param>
        void SetDataSourceProvider(IDataSourceProvider dataSourceProvider);
        /// <summary>
        /// Called when the report is disposed for any clean up.
        /// </summary>
        void Dispose();
        /// <summary>
        /// Registered to be informed when the report wants inspects an object.
        /// </summary>
        event Action<IAnalyzerReport, Object> onInspectObject;

        /// <summary>
        /// Returns the report given the type. This is used when a IAnalyzerReport have nested reports.
        /// </summary>
        /// <param name="type">The report type.</param>
        /// <returns>The IAnalyzerReport for the report type.</returns>
        IAnalyzerReport GetReportForType(Type type);

        bool isFilteringReport { get; }
    }
}
