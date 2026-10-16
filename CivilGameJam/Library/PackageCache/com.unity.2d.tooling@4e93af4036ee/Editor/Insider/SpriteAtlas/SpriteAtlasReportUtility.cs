using System.Collections.Generic;
using UnityEditor.U2D.Common;
using UnityEngine;
using UnityEngine.U2D;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class SpriteAtlasReportUtility
    {

        public static List<(Sprite sprite, GUID assetGUID)> GetAtlasSpritesToPack(SpriteAtlas atlasToPack)
        {
            var packables = atlasToPack.GetPackables();
            var masterAtlas = atlasToPack.isVariant ? atlasToPack.GetMasterAtlas() : null;
            if (masterAtlas != null)
            {
                packables = masterAtlas.GetPackables();
            }

            var sprites = new List<(Sprite sprite, GUID assetGUID)>();

            for (int i = 0; i < packables.Length; ++i)
            {
                var packable = packables[i];
                if (packable == null)
                    continue;
                if (packable is DefaultAsset folder)
                {
                    var folderPath = AssetDatabase.GetAssetPath(folder);
                    var spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
                    for (int j = 0; j < spriteGuids.Length; ++j)
                    {
                        var spritePath = AssetDatabase.GUIDToAssetPath(spriteGuids[j]);
                        var assets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
                        foreach (var asset in assets)
                        {
                            if (asset is Sprite sprite)
                            {
                                GUID.TryParse(spriteGuids[j], out GUID assetGUID);
                                sprites.Add((sprite, assetGUID));
                            }
                        }
                    }
                }
                else if (packable is Texture2D)
                {
                    var texturePath = AssetDatabase.GetAssetPath(packable);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
                    var assetGUID = AssetDatabase.GUIDFromAssetPath(texturePath);
                    foreach (var asset in assets)
                    {
                        if (asset is Sprite sprite)
                        {
                            sprites.Add((sprite,assetGUID));
                        }
                    }
                }
                else if (packable is Sprite sprite)
                {
                    sprites.Add((sprite, AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(sprite))));
                }
                else
                {
                    Debug.LogError("Packed is " + packable.GetType());
                }
            }

            return sprites;
        }

        public static (SpriteFitDataTask task,List<(Sprite sprite, GUID assetGUID)> sprites)  PackAtlas(SpriteAtlas atlasToPack)
        {
            var sprites = GetAtlasSpritesToPack(atlasToPack);
            return (SpriteAtlasBridge.SpriteAtlasFitDataAsync(atlasToPack, sprites.Count),sprites);
        }
    }
}
