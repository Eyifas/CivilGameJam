using System;
using System.Collections.Generic;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    /// <summary>
    /// Interface for managing save file operations that handle the storage and retrieval
    /// of serializable data objects. Implementations of this interface provide a unified
    /// way to persist and load analyzer data across different storage formats and locations.
    /// </summary>
    public interface ISaveFile
    {
        /// <summary>
        /// Adds a save data object to the save file for persistence.
        /// The data will be stored and can be retrieved later using GetSaveData.
        /// </summary>
        /// <param name="saveData">The save data object to add. Must implement ISaveData interface.</param>
        /// <remarks>
        /// Multiple save data objects of the same type can be added to the same save file.
        /// The implementation should handle type organization and ensure data integrity.
        /// </remarks>
        void AddSaveData(ISaveData saveData);

        /// <summary>
        /// Retrieves all save data objects of the specified type from the save file.
        /// The retrieved objects are added to the provided list.
        /// </summary>
        /// <typeparam name="T">The type of save data to retrieve. Must implement ISaveData interface.</typeparam>
        /// <param name="saveDataList">The list to populate with retrieved save data objects.
        /// Existing items in the list will be preserved, and new items will be added.</param>
        /// <remarks>
        /// If no save data of the specified type exists in the file, the list remains unchanged.
        /// The method does not clear the provided list before adding items, allowing for
        /// accumulation of data from multiple sources.
        /// </remarks>
        void GetSaveData<T>(List<T> saveDataList) where T : ISaveData;
    }
}
