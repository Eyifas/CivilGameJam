using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.U2D.Tooling.Analyzer;
using UnityEngine;
using UnityEngine.U2D;

namespace SampleReport
{
    /// <summary>
    /// Provides functionality to capture, save, and load Sprite asset data for reporting purposes.
    /// </summary>
    class SpriteDataSource : IReportDataSource
    {
        /// Stores the current Sprite capture data.
        SpriteCaptureData m_Capture = new();

        /// Indicates whether the capture process should be cancelled.
        bool m_Cancel;

        /// The task responsible for capturing Sprite data asynchronously.
        Task<SpriteCaptureData> m_CaptureTask;

        /// <summary>
        /// Starts capturing Sprite data from the specified asset search paths.
        /// </summary>
        /// <param name="assetSearchPath">Array of asset search paths.</param>
        public async void Capture(string[] assetSearchPath)
        {
            m_Cancel = false;
            capturing = true;
            onCaptureStart?.Invoke(this);
            m_CaptureTask = GetSpriteData(m_Capture, assetSearchPath);
            await m_CaptureTask;
            m_Capture = m_CaptureTask.Result;
            capturing = false;
            onCaptureEnd?.Invoke(this);
            onDataSourceChanged?.Invoke(this);
        }

        /// <summary>
        /// Requests to stop the ongoing capture process.
        /// </summary>
        public void StopCapture()
        {
            m_Cancel = true;
        }

        /// <summary>
        /// Event triggered when the data source changes.
        /// </summary>
        public event Action<IReportDataSource> onDataSourceChanged;

        /// <summary>
        /// Event triggered when capture starts.
        /// </summary>
        public event Action<IReportDataSource> onCaptureStart;

        /// <summary>
        /// Event triggered when capture ends.
        /// </summary>
        public event Action<IReportDataSource> onCaptureEnd;

        /// <summary>
        /// Gets a value indicating whether a capture is in progress.
        /// </summary>
        public bool capturing { get; private set; }

        /// <summary>
        /// Disposes the data source and cancels any ongoing capture.
        /// </summary>
        public void Dispose()
        {
            m_Cancel = true;
            if (m_CaptureTask != null)
            {
                var _ = m_CaptureTask.Result;
            }
        }

        /// <summary>
        /// Saves the current capture data to the specified save file.
        /// </summary>
        /// <param name="saveData">The save file to store data in.</param>
        public void Save(ISaveFile saveData)
        {
            saveData.AddSaveData(m_Capture);
        }

        /// <summary>
        /// Loads capture data from the specified save file.
        /// </summary>
        /// <param name="saveData">The save file to load data from.</param>
        public void Load(ISaveFile saveData)
        {
            List<SpriteCaptureData> saveDataList = new ();
            saveData.GetSaveData(saveDataList);
            if (saveDataList.Count > 0)
                m_Capture = saveDataList[0];
            else
                m_Capture = new ();
            onDataSourceChanged?.Invoke(this);
        }

        /// <summary>
        /// Gets the name of the data source.
        /// </summary>
        public string name => "Sprite Report Data Source";

        /// <summary>
        /// Gets the last time the data source captured data.
        /// </summary>
        public long lastCaptureTime => m_Capture.lastCaptureTime;

        /// <summary>
        /// Gets the list of captured sprite assets.
        /// </summary>
        public List<SpriteAssets> data => m_Capture.spriteData;

        /// <summary>
        /// Asynchronously captures sprite data from the specified asset search paths.
        /// </summary>
        /// <param name="prevCapture">The previous capture data for comparison.</param>
        /// <param name="assetSearchPath">Array of asset search paths.</param>
        /// <returns>A task representing the asynchronous operation, with the captured data as result.</returns>
        async Task<SpriteCaptureData> GetSpriteData(SpriteCaptureData prevCapture, string[] assetSearchPath)
        {
            int id = Progress.Start("Sprite Data Capture");
            var capture = new SpriteCaptureData();
            string[] guids = AssetDatabase.FindAssets("t:Sprite", assetSearchPath);
            HashSet<string> pathVisited = new ();

            for (int i = 0; i < guids.Length && !m_Cancel; ++i)
            {
                Progress.Report(id, i, guids.Length, "Capturing sprite data");
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!pathVisited.Add(path))
                    continue;
                SpriteAssets spriteAssets = null;
                for(int j = 0; j < prevCapture.spriteData.Count; j++)
                {
                    if (prevCapture.spriteData[j].assetPathGuid == guids[i])
                    {
                        spriteAssets = prevCapture.spriteData[j];
                        break;
                    }
                }

                if (!HasAssetChanged(spriteAssets, path))
                {
                    capture.spriteData.Add(spriteAssets);
                    continue;
                }

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path);
                spriteAssets = new SpriteAssets()
                {
                    assetPathGuid = guids[i],
                    fileModifiedTime = File.GetLastWriteTimeUtc(path).ToFileTimeUtc(),
                    metaFileModifiedTime = File.GetLastWriteTimeUtc(AssetDatabase.GetTextMetaFilePathFromAssetPath(path)).ToFileTimeUtc()
                };
                for (int j = 0; j < sprites.Length; ++j)
                {
                    if (sprites[j] is Sprite sprite)
                    {
                        var indices = sprite.triangles.Length;
                        var vertexCount = sprite.GetVertexCount();
                        spriteAssets.spriteData.Add(new SpriteData()
                        {
                            name = sprite.name,
                            vertexCount = vertexCount,
                            triangleCount = indices/3,
                            spriteGlobalID = GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString()
                        });
                    }
                }
                capture.spriteData.Add(spriteAssets);
                await Task.Delay(10);
            }

            Progress.Remove(id);
            capture.lastCaptureTime = DateTime.UtcNow.ToFileTimeUtc();
            return capture;
        }

        /// <summary>
        /// Determines whether the asset or its meta file has changed since the previous capture.
        /// </summary>
        /// <param name="prevCapture">The previous asset capture data.</param>
        /// <param name="path">The asset path.</param>
        /// <returns>True if the asset or meta file has changed; otherwise, false.</returns>
        public static bool HasAssetChanged(SpriteAssets prevCapture, string path)
        {
            var fileTime = File.GetLastWriteTimeUtc(path).ToFileTimeUtc();
            var metaPath = AssetDatabase.GetTextMetaFilePathFromAssetPath(path);
            var metaTime = File.GetLastWriteTimeUtc(metaPath).ToFileTimeUtc();
            return prevCapture?.fileModifiedTime != fileTime || prevCapture?.metaFileModifiedTime != metaTime;
        }
    }
}
