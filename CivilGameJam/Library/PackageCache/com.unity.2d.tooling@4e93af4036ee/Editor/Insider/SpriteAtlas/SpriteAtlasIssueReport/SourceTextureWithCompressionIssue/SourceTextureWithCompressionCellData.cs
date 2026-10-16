using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    class SourceTextureWithCompressionCellData
    {
        public string name;
        public string textureFormat;
        public LazyLoadReference<Object> asset;
        public EditorAtlasInfo atlasInfo;
        public string icon;

        public static int Compare(SourceTextureWithCompressionCellData a, SourceTextureWithCompressionCellData b, string propertyToCompare)
        {
            switch (propertyToCompare)
            {
                case "name":
                case null:
                    return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                case "textureFormat":
                    return string.Compare(a.textureFormat, b.textureFormat, StringComparison.Ordinal);
                default:
                    return 0;
            }
        }
    }
}
