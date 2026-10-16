using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor.U2D.Common;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    [BurstCompile]
    class EditorSpriteInfo : EditorResourceUsageInfo<Sprite>
    {
        [SerializeField]
        float m_SpriteAtlasTextureScale;
        [SerializeField]
        SpritePackingMode m_SpritePackingMode;
        [SerializeField]
        TextureFormat m_TextureFormat;
        [SerializeField]
        string m_SpriteID;

        public EditorSpriteInfo(EntityId entityId, string assetPath)
            : base(entityId, assetPath) { }

        public void CollectSpriteInfo()
        {
            Profiler.BeginSample("CollectSpriteInfo");
            var sprite = GetObject();
            m_SpriteID = sprite.GetSpriteID().ToString();
            memorySize = (ulong)Profiler.GetRuntimeMemorySizeLong(sprite);
            m_SpriteAtlasTextureScale = sprite.spriteAtlasTextureScale;
            width = Mathf.Ceil(sprite.textureRect.width * m_SpriteAtlasTextureScale);
            height = Mathf.Ceil(sprite.textureRect.height * m_SpriteAtlasTextureScale);

            if (sprite.vertices.Length == 4 && sprite.rect.size == sprite.textureRect.size)
            {
                m_SpritePackingMode = SpritePackingMode.Rectangle;
                usedArea = width * height;
            }
            else
            {
                 m_SpritePackingMode = SpritePackingMode.Tight;
                 Profiler.BeginSample("CalculateMeshArea");
                 var pos = sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position);
                 var idx = sprite.GetIndices();
                 var posArray = new NativeArray<Vector3>(pos.Length, Allocator.Temp);
                 pos.CopyTo(posArray);
                 var area = CalculateMeshArea(ref posArray, ref idx);

                Profiler.EndSample();
                //usedArea = area * 0.5f * sprite.pixelsPerUnit * sprite.pixelsPerUnit;
                usedArea = area * sprite.pixelsPerUnit * sprite.pixelsPerUnit;
            }

            var t = SpriteAtlasBridge.GetSpriteTexture(sprite, false);
            m_TextureFormat = GraphicsFormatUtility.GetTextureFormat(t.graphicsFormat);
            Profiler.EndSample();
            if (width * height < usedArea)
                usedArea = width * height;
        }

        [BurstCompile]
        public static float CalculateMeshArea(ref NativeArray<Vector3> position, ref NativeArray<ushort> triangles)
        {
            float totalArea = 0.0f;
            // Iterate through all triangles
            for (int i = 0; i < triangles.Length; )
            {
                // Get the three vertices of the current triangle
                float3 v1 = new (position[triangles[i++]]);
                float3 v2 = new (position[triangles[i++]]);
                float3 v3 = new (position[triangles[i++]]);

                float3 edge1 = v2 - v1;
                float3 edge2 = v3 - v1;

                // Calculate the cross product
                var crossProduct = math.cross(edge1, edge2);
                // The area is half the magnitude of the cross product
                totalArea += math.length(crossProduct) * 0.5f;
            }

            return totalArea;
        }

        public virtual Texture2D GetSpriteTexture(bool fromAtlas)
        {
            var sprite = GetObject();
            if (!sprite)
                return null;
            return SpriteAtlasBridge.GetSpriteTexture(sprite, fromAtlas);
        }

        public SpritePackingMode spritePackingMode => m_SpritePackingMode;
        public TextureFormat textureFormat => m_TextureFormat;
    }
}
