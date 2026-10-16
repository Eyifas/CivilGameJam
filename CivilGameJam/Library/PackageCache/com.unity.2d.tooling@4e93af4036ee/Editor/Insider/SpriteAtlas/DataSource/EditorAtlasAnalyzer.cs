using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.U2D;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    class EditorAtlasAnalyzerCapture
    {
        [SerializeField]
        public List<EditorAtlasInfo> atlasInfo = new ();
    }

    class EditorAtlasAnalyzer
    {
        AnalyzerProgress m_AnalyzerProgress;
        EditorAtlasAnalyzerCapture m_PreviousCapture;
        bool m_CancelTask;

        public EditorAtlasAnalyzer(EditorAtlasAnalyzerCapture prevCapture)
        {
            m_PreviousCapture = prevCapture;
            m_AnalyzerProgress = new();
        }

        public void CancelTask()
        {
            m_CancelTask = true;
        }

        public async Task<EditorAtlasAnalyzerCapture> StartTask()
        {
            m_CancelTask = false;
            var capture = new EditorAtlasAnalyzerCapture();
            List<string> fileGuid = new();
            for(int i = 0; i< assetSearchPath.Length; i++)
            {
                if (File.Exists(assetSearchPath[i]))
                {
                    fileGuid.Add(assetSearchPath[i]);
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:SpriteAtlas", assetSearchPath);
            var assetCount = guids.Length + fileGuid.Count;
            m_AnalyzerProgress.StartProgressTrack("Atlas Analyzer");
            for (int a = 0; a < assetCount && !m_CancelTask; a++)
            {
                m_AnalyzerProgress.UpdateProgressTrack(a, assetCount, "Analyzing atlases...");
                await Task.Delay(10);
                string atlasPath = "";
                if (a < guids.Length)
                {
                    atlasPath = AssetDatabase.GUIDToAssetPath(guids[a]);
                }
                else
                {
                    atlasPath = fileGuid[a - guids.Length];
                }

                EditorAtlasInfo atlasInfo = null;
                int prevAtlasIndex = 0;
                for(; prevAtlasIndex < m_PreviousCapture.atlasInfo.Count; prevAtlasIndex++)
                {
                    if (m_PreviousCapture.atlasInfo[prevAtlasIndex].assetPath == atlasPath)
                    {
                        atlasInfo = m_PreviousCapture.atlasInfo[prevAtlasIndex];
                        break;
                    }
                }
                if (EditorAtlasInfo.HasAtlasChange(atlasInfo, atlasPath))
                {
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                    if (atlas != null)
                    {
                        atlasInfo = new EditorAtlasInfo(atlas.GetEntityId(), atlasPath);
                        await atlasInfo.CollectAtlasInfo();
                    }
                }

                if (atlasInfo != null)
                {
                    capture.atlasInfo.Add(atlasInfo);
                    if(prevAtlasIndex < m_PreviousCapture.atlasInfo.Count)
                        m_PreviousCapture.atlasInfo[prevAtlasIndex] = atlasInfo;
                    else
                        m_PreviousCapture.atlasInfo.Add(atlasInfo);
                }

            }
            m_AnalyzerProgress.EndProgressTrack();
            if (guids.Length == 0 && fileGuid.Count != 0)
            {
                // we are checking individual files, so we don't want to lose any previous capture
                return m_PreviousCapture;
            }

            return capture;
        }

        internal string[] assetSearchPath { get; set; }
    }
}
