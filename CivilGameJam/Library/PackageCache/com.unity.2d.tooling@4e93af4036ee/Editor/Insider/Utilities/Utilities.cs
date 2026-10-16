using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    static class Utilities
    {
        internal static string[] RemoveChildDirectories(string[] assetSearchPath)
        {
            if (assetSearchPath == null || assetSearchPath.Length == 0)
                return new[] { "Assets" };

            // reorder by length
            for (int i = 0; i < assetSearchPath.Length - 1; i++)
            {
                for (int j = 0; j < assetSearchPath.Length - 1 - i; j++)
                {
                    if (assetSearchPath[j]?.Length > assetSearchPath[j + 1]?.Length)
                    {
                        (assetSearchPath[j], assetSearchPath[j + 1]) = (assetSearchPath[j + 1], assetSearchPath[j]);
                    }
                }
            }

            var result = new List<string>();
            foreach (var path in assetSearchPath)
            {
                if (!string.IsNullOrEmpty(path) && !result.Contains(path))
                {
                    int i = 0;
                    for(; i < result.Count; ++i)
                    {
                        if (path.StartsWith(result[i]))
                            break;
                    }
                    if(i < result.Count)
                        continue; // already contained in the result
                    result.Add(path);
                }

            }
            return result.ToArray();
        }

        public static void WriteSaveDataToFile<T>(string path, T data)
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            if(File.Exists(path))
                File.Delete(path);
            var jsonText = JsonUtility.ToJson(data);
            File.WriteAllText(path, jsonText);
        }

        public static T LoadSaveDataFromFile<T>(string path)
        {
            if (File.Exists(path))
            {
                var jsonText = File.ReadAllText(path);
                return JsonUtility.FromJson<T>(jsonText);
            }

            return default(T);
        }
    }
}
