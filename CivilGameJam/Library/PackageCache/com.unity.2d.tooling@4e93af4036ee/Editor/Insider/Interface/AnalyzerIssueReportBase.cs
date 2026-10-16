using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    /// <summary>
    /// Abstract base class for analyzer issue reports that provides common functionality
    /// for data source management, report display, and object inspection.
    /// </summary>
    public abstract class AnalyzerIssueReportBase : IAnalyzerReport
    {
        /// <summary>
        /// Provider for data sources used by this report.
        /// </summary>
        IDataSourceProvider m_DataSourceProvider;

        /// <summary>
        /// Array of data source types that this report is interested in monitoring.
        /// </summary>
        Type[] m_DataSourceInterestedType;

        /// <summary>
        /// List of active data sources that this report is currently monitoring.
        /// </summary>
        List<IReportDataSource> m_DataSourceInterested = new();

        /// <summary>
        /// Event triggered when an object needs to be inspected.
        /// </summary>
        protected event Action<AnalyzerIssueReportBase, Object> m_OnInspectObject;

        /// <summary>
        /// UI list item representing this report in the analyzer interface.
        /// </summary>
        ReportListItem m_ReportListItem;

        /// <summary>
        /// Flag indicating whether the report is currently being filtered.
        /// </summary>
        bool m_IsFilteringReport;

        /// <summary>
        /// String representation of the current report item count.
        /// </summary>
        string m_ReportItemCount = "-";

        /// <summary>
        /// Initializes a new instance of the AnalyzerIssueReportBase class.
        /// </summary>
        /// <param name="dataSourceInterestedType">Array of data source types this report should monitor.</param>
        protected AnalyzerIssueReportBase(Type[] dataSourceInterestedType)
        {
            m_DataSourceInterestedType = dataSourceInterestedType;
            m_ReportListItem = new ReportListItem();
            m_ReportListItem.SetName(reportTitle);
            m_ReportListItem.SetCount(m_ReportItemCount);
        }

        /// <summary>
        /// Gets the visual element representing this report in the list view.
        /// </summary>
        VisualElement IAnalyzerReport.listItem => m_ReportListItem;

        /// <summary>
        /// Gets the main content visual element for this report.
        /// </summary>
        public abstract VisualElement reportContent { get; }

        /// <summary>
        /// Gets the settings content visual element for this report.
        /// </summary>
        public abstract VisualElement settingsContent { get; }

        /// <summary>
        /// Gets the title for this report.
        /// </summary>
        public abstract string reportTitle { get; }

        /// <summary>
        /// Sets the data source provider for this report and initializes data source monitoring.
        /// </summary>
        /// <param name="dataSourceProvider">The data source provider to use.</param>
        void IAnalyzerReport.SetDataSourceProvider(IDataSourceProvider dataSourceProvider)
        {
            if (m_DataSourceProvider != null)
                m_DataSourceProvider.onDataSourceChanged -= OnDataProviderChanged;
            m_DataSourceProvider = dataSourceProvider;
            m_DataSourceProvider.onDataSourceChanged += OnDataProviderChanged;
            InitDataSource();
        }

        /// <summary>
        /// Sets the display name for this report in the list item.
        /// </summary>
        /// <param name="name">The name to display.</param>
        public void SetReportListItemName(string name)
        {
            m_ReportListItem.SetName(name);
        }

        /// <summary>
        /// Sets the item count display for this report and clears filtering state.
        /// </summary>
        /// <param name="count">The count string to display.</param>
        public void SetReportListemCount(string count)
        {
            m_ReportItemCount = count;
            m_IsFilteringReport = false;
            m_ReportListItem.SetCount(count);
        }

        /// <summary>
        /// Triggers the object inspection event for the specified Unity object.
        /// </summary>
        /// <param name="obj">The Unity object to inspect.</param>
        protected void InspectObject(Object obj)
        {
            m_OnInspectObject?.Invoke(this, obj);
        }

        /// <summary>
        /// Handles data provider change events by reinitializing data sources.
        /// </summary>
        void OnDataProviderChanged()
        {
            InitDataSource();
        }

        /// <summary>
        /// Initializes and subscribes to data sources based on the interested types.
        /// Clears existing subscriptions and creates new ones for available data sources.
        /// </summary>
        void InitDataSource()
        {
            for (int i = 0; i < m_DataSourceInterested.Count; ++i)
            {
                m_DataSourceInterested[i].onDataSourceChanged -= OnReportDataSourceChanged;
            }

            m_DataSourceInterested.Clear();
            for (int i = 0; i < m_DataSourceInterestedType.Length; ++i)
            {
                var reportDataSource = m_DataSourceProvider.GetDataSource(m_DataSourceInterestedType[i]);
                if (reportDataSource != null)
                {
                    reportDataSource.onDataSourceChanged += OnReportDataSourceChanged;
                    m_DataSourceInterested.Add(reportDataSource);
                    OnReportDataSourceChanged(reportDataSource);
                }
            }
        }

        /// <summary>
        /// Request capture of data from all interested data sources.
        /// </summary>
        /// <param name="path">The paths to folder or asset to capture.</param>
        protected void RequestCapture(string[] path)
        {
            for(int i = 0; i < m_DataSourceInterested.Count; ++i)
            {
                m_DataSourceInterested[i].Capture(path);
            }
        }

        /// <summary>
        /// Abstract method called when a monitored data source changes.
        /// Derived classes must implement this to handle data source updates.
        /// </summary>
        /// <param name="reportDataSource">The data source that changed.</param>
        protected abstract void OnReportDataSourceChanged(IReportDataSource reportDataSource);

        /// <summary>
        /// Performs cleanup by unsubscribing from events and clearing data sources.
        /// </summary>
        public virtual void Dispose()
        {
            for (int i = 0; i < m_DataSourceInterested.Count; ++i)
            {
                m_DataSourceInterested[i].onDataSourceChanged -= OnReportDataSourceChanged;
            }

            m_DataSourceInterested.Clear();
            m_OnInspectObject = null;
        }

        /// <summary>
        /// Event that is raised when an object should be inspected.
        /// </summary>
        event Action<IAnalyzerReport, Object> IAnalyzerReport.onInspectObject
        {
            add => m_OnInspectObject += value;
            remove => m_OnInspectObject -= value;
        }

        /// <summary>
        /// Gets the report instance for the specified type.
        /// </summary>
        /// <param name="type">The type to get the report for.</param>
        /// <returns>This report instance if the type matches, otherwise null.</returns>
        IAnalyzerReport IAnalyzerReport.GetReportForType(Type type)
        {
            if (type == GetType())
            {
                return this;
            }

            return null;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the report is currently being filtered.
        /// When set to true, shows a loading state in the UI. When set to false, shows the current item count.
        /// </summary>
        public bool isFilteringReport
        {
            get => m_IsFilteringReport;
            protected set
            {
                if (m_IsFilteringReport != value)
                {
                    m_IsFilteringReport = value;
                    if (m_IsFilteringReport)
                    {
                        if (m_ReportListItem != null)
                            m_ReportListItem.SetLoading();
                    }
                    else
                    {
                        m_ReportListItem.SetCount(m_ReportItemCount);
                    }
                }
            }
        }
    }
}
