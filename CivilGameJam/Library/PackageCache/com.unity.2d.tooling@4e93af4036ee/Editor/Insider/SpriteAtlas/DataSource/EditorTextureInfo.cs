using System;
using System.Collections.Generic;
using UnityEditor.U2D.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    class EditorTextureInfo : EditorResourceUsageInfo<Texture2D>
    {
        [SerializeField]
        TextureFormat m_TextureFormat;

        [SerializeField]
        List<EditorSpriteInfo> m_SpriteInfo = new();

        public EditorTextureInfo(EntityId entityId, string assetPath)
            : base(entityId, assetPath) { }

        public EditorTextureInfo(EntityId entityId, string assetPath, int width, int height, TextureFormat textureFormat)
            : base(entityId, assetPath)
        {
            m_TextureFormat = textureFormat;
            this.width = width;
            this.height = height;
            if(width == 0 || height == 0)
            {
                memorySize = 0;
                return;
            }
            var t = new Texture2D(width, height, textureFormat, false) { hideFlags = HideFlags.HideAndDontSave };
            memorySize = (ulong)InternalEditorBridge.GetTextureStorageMemorySizeLong(t);
            Object.DestroyImmediate(t);
        }

        public void CollectInfo(Texture2D texture)
        {
            memorySize = (ulong)InternalEditorBridge.GetTextureStorageMemorySizeLong(texture);
            m_TextureFormat = texture.format;
            width = texture.width;
            height = texture.height;
        }

        public void AddSpriteInfo(Sprite sprite)
        {
            var spritePath = AssetDatabase.GetAssetPath(sprite);
            var spriteInfo = new EditorSpriteInfo(sprite.GetEntityId(), spritePath);
            spriteInfo.CollectSpriteInfo();
            m_SpriteInfo.Add(spriteInfo);
            usedArea += spriteInfo.usedArea;
        }

        public TextureFormat textureFormat => m_TextureFormat;
        public virtual List<EditorSpriteInfo> spriteInfo => m_SpriteInfo;
        public ulong textureMemorySize => memorySize;
    }
}
