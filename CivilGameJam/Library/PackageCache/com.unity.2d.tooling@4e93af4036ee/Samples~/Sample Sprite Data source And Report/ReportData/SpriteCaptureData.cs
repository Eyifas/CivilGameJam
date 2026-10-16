using System;
using System.Collections.Generic;
using UnityEditor.U2D.Tooling.Analyzer;
using UnityEngine;

namespace SampleReport
{
    /// <summary>
    /// Represents the root data structure for capturing Sprite asset information.
    /// </summary>
    [Serializable]
    class SpriteCaptureData : ISaveData
    {
        /// <summary>
        /// List of sprite asset data entries.
        /// </summary>
        public List<SpriteAssets> spriteData = new();
        /// <summary>
        /// Last time the capture was performed.
        /// </summary>
        public long lastCaptureTime;
    }


    /// <summary>
    /// Contains metadata and Sprite data for a specific sprite asset.
    /// </summary>
    [Serializable]
    class SpriteAssets
    {
        /// <summary>
        /// The GUID of the asset path.
        /// </summary>
        public string assetPathGuid;

        /// <summary>
        /// The last modified time of the asset file.
        /// </summary>
        public long fileModifiedTime;

        /// <summary>
        /// The last modified time of the asset's meta file.
        /// </summary>
        public long metaFileModifiedTime;

        /// <summary>
        /// List of individual sprite data within the asset.
        /// </summary>
        public List<SpriteData> spriteData = new();
    }

    /// <summary>
    /// Stores information about an individual Sprite.
    /// </summary>
    [Serializable]
    class SpriteData
    {
        /// <summary>
        /// The name of the Sprite.
        /// </summary>
        public string name;

        /// <summary>
        /// The global identifier for the Sprite.
        /// </summary>
        public string spriteGlobalID;

        /// <summary>
        /// The number of triangles in the Sprite mesh.
        /// </summary>
        public int triangleCount;

        /// <summary>
        /// The number of vertices in the Sprite mesh.
        /// </summary>
        public int vertexCount;
    }
}
