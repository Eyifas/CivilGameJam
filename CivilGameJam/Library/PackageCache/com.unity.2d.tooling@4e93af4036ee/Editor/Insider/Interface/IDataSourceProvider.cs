using System;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    /// <summary>
    /// Interface for providing access to report data sources and notifying when data sources change.
    /// Implementations of this interface serve as a centralized registry for managing and retrieving
    /// different types of data sources used by analyzer reports.
    /// </summary>
    interface IDataSourceProvider
    {
        /// <summary>
        /// Gets a data source of the specified generic type.
        /// </summary>
        /// <typeparam name="T">The type of data source to retrieve. Must implement IReportDataSource.</typeparam>
        /// <returns>The data source of type T if available, otherwise null.</returns>
        T GetDataSource<T>() where T : class, IReportDataSource;

        /// <summary>
        /// Gets a data source for the specified type.
        /// </summary>
        /// <param name="t">The type of data source to retrieve.</param>
        /// <returns>The data source instance if available, otherwise null.</returns>
        IReportDataSource GetDataSource(Type t);

        /// <summary>
        /// Event raised when any data source managed by this provider changes.
        /// Subscribers can use this to be notified of data source updates without
        /// having to monitor individual data sources directly.
        /// </summary>
        event Action onDataSourceChanged;
    }
}
