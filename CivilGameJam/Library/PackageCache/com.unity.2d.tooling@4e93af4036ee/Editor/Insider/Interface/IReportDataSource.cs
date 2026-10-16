using System;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    /// <summary>
    /// Interface for report data sources that can capture, analyze, and provide data for analyzer reports.
    /// Implementations of this interface handle the collection and management of specific types of data
    /// used by analyzer reports, such as asset information, performance metrics, or project statistics.
    /// </summary>
    public interface IReportDataSource
    {
        /// <summary>
        /// Starts capturing data from the specified asset search paths.
        /// This method initiates the data collection process and may run asynchronously.
        /// </summary>
        /// <param name="assetSearchPath">Array of asset paths to search and capture data from.</param>
        void Capture(string[] assetSearchPath);

        /// <summary>
        /// Stops the current data capture operation if one is in progress.
        /// This method should gracefully halt any ongoing capture processes.
        /// </summary>
        void StopCapture();

        /// <summary>
        /// Event raised when the data source's underlying data has changed.
        /// Subscribers can use this to update their reports or UI when new data becomes available.
        /// </summary>
        event Action<IReportDataSource> onDataSourceChanged;

        /// <summary>
        /// Event raised when a data capture operation begins.
        /// This allows subscribers to respond to the start of data collection, such as showing progress indicators.
        /// </summary>
        event Action<IReportDataSource> onCaptureStart;

        /// <summary>
        /// Event raised when a data capture operation completes.
        /// This signals that the capture process has finished, either successfully or due to cancellation.
        /// </summary>
        event Action<IReportDataSource> onCaptureEnd;

        /// <summary>
        /// Gets a value indicating whether a data capture operation is currently in progress.
        /// </summary>
        bool capturing { get; }

        /// <summary>
        /// Performs cleanup operations and releases any resources used by the data source.
        /// This method should be called when the data source is no longer needed.
        /// </summary>
        void Dispose();

        /// <summary>
        /// Saves the current data source state to the specified save file.
        /// This allows the data source's captured data to be persisted for later use.
        /// </summary>
        /// <param name="saveData">The save file interface to write data to.</param>
        void Save(ISaveFile saveData);

        /// <summary>
        /// Loads previously saved data source state from the specified save file.
        /// This restores the data source to a previously captured state without needing to recapture.
        /// </summary>
        /// <param name="saveData">The save file interface to read data from.</param>
        void Load(ISaveFile saveData);

        /// <summary>
        /// Gets the human-readable name of this data source.
        /// This name is typically used for display purposes in the UI and for identification.
        /// </summary>
        public string name { get; }

        /// <summary>
        /// Gets the last time the data source captured data.
        /// </summary>
        public long lastCaptureTime { get; }
    }
}
