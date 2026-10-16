using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    class AtlasSaveData : ISaveData
    {
        public const int k_Version = 1;
        public int version = 0;
        public long lastCaptureTime;
        public EditorAtlasAnalyzerCapture capture;
    }

    class SpriteAtlasDataSource : IReportDataSource
    {
        event Action<IReportDataSource> m_OnDataSourceChange;
        event Action<IReportDataSource> m_OnCaptureStart;
        event Action<IReportDataSource> m_OnCaptureEnd;

        EditorAtlasAnalyzerCapture m_Capture = new ();
        EditorAtlasAnalyzer m_CurrentTask;
        Task<EditorAtlasAnalyzerCapture> m_ResultTask;
        long m_LastCaptureTime;

        public async void Capture(string [] assetSearchPath)
        {
            if (m_CurrentTask != null)
                return;
            m_OnCaptureStart?.Invoke(this);
            m_CurrentTask = new EditorAtlasAnalyzer(m_Capture);
            m_CurrentTask.assetSearchPath = assetSearchPath;
            m_ResultTask = m_CurrentTask.StartTask();
            await m_ResultTask;

            m_LastCaptureTime = DateTime.UtcNow.ToFileTimeUtc();
            m_Capture = m_ResultTask.Result;
            m_CurrentTask = null;
            m_OnCaptureEnd?.Invoke(this);
            m_OnDataSourceChange?.Invoke(this);
        }

        public void StopCapture()
        {
            m_CurrentTask?.CancelTask();
        }

        public event Action<IReportDataSource> onDataSourceChanged
        {
            add => m_OnDataSourceChange += value;
            remove => m_OnDataSourceChange -= value;
        }

        public event Action<IReportDataSource> onCaptureStart
        {
            add => m_OnCaptureStart += value;
            remove => m_OnCaptureStart -= value;
        }

        public event Action<IReportDataSource> onCaptureEnd
        {
            add => m_OnCaptureEnd += value;
            remove => m_OnCaptureEnd -= value;
        }

        public bool capturing => m_CurrentTask != null;

        public void Dispose()
        {
            m_CurrentTask?.CancelTask();
            if(m_ResultTask != null)
            {
                var _ = m_ResultTask.Result;
            }

            m_OnDataSourceChange = null;
            m_OnCaptureStart = null;
            m_OnCaptureEnd = null;
        }

        public void Save(ISaveFile saveData)
        {
            AtlasSaveData atlasSaveData = new AtlasSaveData
            {
                capture = m_Capture,
                lastCaptureTime = m_LastCaptureTime,
                version = AtlasSaveData.k_Version
            };
            saveData.AddSaveData(atlasSaveData);
        }

        public void Load(ISaveFile saveData)
        {
            List<AtlasSaveData> atlasSaveDataList = new List<AtlasSaveData>();
            saveData.GetSaveData(atlasSaveDataList);
            m_Capture = null;
            if (atlasSaveDataList.Count > 0)
            {
                if (atlasSaveDataList[0].version == AtlasSaveData.k_Version)
                {
                    m_LastCaptureTime = atlasSaveDataList[0].lastCaptureTime;
                    m_Capture = atlasSaveDataList[0].capture;
                }
            }
            if (m_Capture == null)
            {
                m_Capture = new EditorAtlasAnalyzerCapture();
                m_LastCaptureTime = DateTime.UtcNow.ToFileTimeUtc();
            }

            m_OnDataSourceChange?.Invoke(this);
        }

        public string name => "Sprite Atlas Report Data Source";
        public virtual long lastCaptureTime => m_LastCaptureTime;

        public virtual List<EditorAtlasInfo> data => m_Capture.atlasInfo;
    }
}
