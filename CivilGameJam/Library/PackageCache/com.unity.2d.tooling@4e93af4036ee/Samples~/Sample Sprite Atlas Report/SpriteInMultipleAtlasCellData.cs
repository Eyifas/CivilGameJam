using System;

namespace SampleReport.Reports
{
    /// <summary>
    /// Represents cell data for a Sprite that is included in multiple Sprite Atlases, used in the report UI.
    /// </summary>
    [Serializable]
    record SpriteInMultipleAtlasCellData
    {
    /// <summary>
    /// The display name of the Sprite or Sprite Atlas.
    /// </summary>
    public string name;

    /// <summary>
    /// The icon class name for the UI representation (e.g., "sprite-icon" or "spriteatlas-icon").
    /// </summary>
    public string icon;

    /// <summary>
    /// The global object ID string for the Sprite or Sprite Atlas.
    /// </summary>
    public string objectGlobalID;

    /// <summary>
    /// The number of child items (e.g., the count of Sprite Atlases containing the Sprite).
    /// </summary>
    public string childCount;
    }
}
