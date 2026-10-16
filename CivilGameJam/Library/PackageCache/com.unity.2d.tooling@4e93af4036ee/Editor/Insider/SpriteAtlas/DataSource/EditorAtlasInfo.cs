using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor.U2D.Common;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.U2D;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    class EditorAtlasInfo : EditorResourceUsageInfo<SpriteAtlas>
    {
        [SerializeField]
        List<EditorTextureInfo> m_TextureInfo = new();
        [SerializeField]
        long m_FileModifiedTime;
        [SerializeField]
        long m_MetaFileModifiedTime;
        [SerializeField]
        Hash128 m_AssetHash;
        [SerializeField]
        TextureFormat m_TextureFormat;
        [SerializeField]
        SerializableGuid m_MasterAtlasGuid;
        [SerializeField]
        SerializableGuid m_AtlasGuid;

        public EditorAtlasInfo(EntityId entityId, string assetPath)
            : base(entityId, assetPath) { }

        public async Task CollectAtlasInfo()
        {
            Profiler.BeginSample("CollectAtlasInfo");
            var atlas = GetObject();
            var path = AssetDatabase.GetAssetPath(atlas);
            m_AtlasGuid = new SerializableGuid(AssetDatabase.GUIDFromAssetPath(path));
            m_FileModifiedTime = File.GetLastWriteTimeUtc(path).ToFileTimeUtc();
            m_AssetHash = AssetDatabase.GetAssetDependencyHash(path);
            path = AssetDatabase.GetTextMetaFilePathFromAssetPath(path);
            m_MetaFileModifiedTime = File.GetLastWriteTimeUtc(path).ToFileTimeUtc();
            m_TextureFormat = SpriteAtlasBridge.GetSpriteAtlasTextureFormat(atlas, EditorUserBuildSettings.activeBuildTarget);
            memorySize = 0;
            usedArea = 0;
            totalArea = 0;
            var masterAtlas = atlas.isVariant ? atlas.GetMasterAtlas() : null;
            if (masterAtlas != null)
            {
                // Get package from master atlas since variant atlas does not have them
                var masterAtlasPath = AssetDatabase.GetAssetPath(masterAtlas);
                m_MasterAtlasGuid = new SerializableGuid(AssetDatabase.GUIDFromAssetPath(masterAtlasPath));
            }

            Dictionary<int, EditorTextureInfo> editorTextureInfoDict = new();
            // atlas is not packed. trigger a pack
            var atlastToPack = masterAtlas ?? atlas;

            var pack = SpriteAtlasReportUtility.PackAtlas(atlastToPack);
            var sprites = pack.sprites;
            var t = pack.task;
            await t.WaitForJob();
            for(int i = 0; i < t.Count; ++i)
            {
                var p = t.GetPage(i);
                if (p < 0)
                    continue;

                if (!editorTextureInfoDict.TryGetValue(p, out var existing))
                {
                    var size = t.GetPageSize(i);
                    editorTextureInfoDict[p] = new EditorTextureInfo(EntityId.None, null,
                        size.x, size.y, textureFormat);
                    editorTextureInfoDict[p].name = $"{p}";
                }

                var editorTextureInfo = editorTextureInfoDict[p];
                foreach(var sprite in sprites)
                {
                    var spriteGUID = sprite.sprite.GetSpriteID();
                    var assetGUID = sprite.assetGUID;
                    if(spriteGUID == t.GetSpriteID(i) && assetGUID == t.GetGUID(i))
                    {
                        editorTextureInfo.AddSpriteInfo(sprite.sprite);
                        break;
                    }
                }
            }
            foreach(var textureInfo in editorTextureInfoDict.Values)
            {
                if(textureInfo.spriteInfo.Count > 0)
                    m_TextureInfo.Add(textureInfo);
            }
            t.Dispose();


            // sort the texture name for consistent ordering
            m_TextureInfo.Sort((x,y) => x.name.CompareTo(y.name));
            // for all textures, update total atlas value
            for(int i =0 ; i < m_TextureInfo.Count; ++i)
            {
                var textureInfo = m_TextureInfo[i];
                width += textureInfo.width;
                height += textureInfo.height;
                usedArea += textureInfo.usedArea;
                totalArea += textureInfo.totalArea;
                memorySize += textureInfo.textureMemorySize;
                textureInfo.name = $"MainTex - ({i})";
            }

            Profiler.EndSample();
        }

        static List<Texture2D> GetAtlasMainTextures(SpriteAtlas atlas)
        {
            // find main textures by checking the texture names
            // secondary textures name are suffixed with main texture name
            var textures = SpriteAtlasBridge.GetSpriteAtlasTextures(atlas);
            var mainTextures = new List<Texture2D>();
            for (int i = 0; i < textures.Length; ++i)
            {
                var texName = textures[i].name;
                int j = 0;
                for(; j < textures.Length; ++j)
                {
                    if (j == i)
                        continue;
                    if (texName.StartsWith(textures[j].name))
                        break;
                }
                if(j >= textures.Length)
                    mainTextures.Add(textures[i]);
            }
            return mainTextures;
        }

        public virtual List<EditorTextureInfo> textureInfo => m_TextureInfo;
        public TextureFormat textureFormat => m_TextureFormat;
        public long metaFileModifiedTime => m_MetaFileModifiedTime;
        public Hash128 assetHash => m_AssetHash;

        public long fileModifiedTime => m_FileModifiedTime;

        public bool isVariant => masterAtlasGuid != null && masterAtlasGuid.isValid;
        public virtual SerializableGuid masterAtlasGuid => m_MasterAtlasGuid;
        public virtual SerializableGuid atlasGuid => m_AtlasGuid;

        public List<EditorSpriteInfo> spriteInfo
        {
            get
            {
                List<EditorSpriteInfo> sprites = new();
                foreach(var textureInfo in m_TextureInfo)
                {
                    sprites.AddRange(textureInfo.spriteInfo);
                }

                return sprites;
            }
        }

        public static bool HasAtlasChange(EditorAtlasInfo prevCaptureAtlasInfo, string atlasPath)
        {
            var hash = AssetDatabase.GetAssetDependencyHash(atlasPath);
            var fileTime = File.GetLastWriteTimeUtc(atlasPath).ToFileTimeUtc();
            var metaPath = AssetDatabase.GetTextMetaFilePathFromAssetPath(atlasPath);
            var metaTime = File.GetLastWriteTimeUtc(metaPath).ToFileTimeUtc();
            return prevCaptureAtlasInfo?.fileModifiedTime != fileTime || prevCaptureAtlasInfo?.metaFileModifiedTime != metaTime || prevCaptureAtlasInfo?.assetHash != hash;
        }
    }
}
