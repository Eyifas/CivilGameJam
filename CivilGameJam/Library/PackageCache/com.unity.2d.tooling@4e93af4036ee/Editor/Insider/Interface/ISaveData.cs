using System;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    /// <summary>
    /// Marker interface for save data objects that can be serialized and persisted.
    /// This interface serves as a base contract for all data types that can be saved
    /// and loaded by the analyzer system. Implementations should contain only serializable
    /// data and should not hold references to Unity objects or other non-serializable types.
    /// </summary>
    public interface ISaveData
    { }
}
