using System;
using UnityEngine;

namespace SampleReport.Reports
{
    /// <summary>
    /// Represents filtered data for a Sprite's mesh, including its name, vertex and triangle counts, and associated Sprite data.
    /// </summary>
    [Serializable]
    record SpriteMeshSizeFilteredData
    {
    /// <summary>
    /// The name of the Sprite mesh.
    /// </summary>
    public string name;

    /// <summary>
    /// The number of vertices in the Sprite mesh.
    /// </summary>
    public int vertices;

    /// <summary>
    /// The number of triangles in the Sprite mesh.
    /// </summary>
    public int triangles;

    /// <summary>
    /// The associated Sprite data.
    /// </summary>
    public SpriteData spriteData;
    }
}
