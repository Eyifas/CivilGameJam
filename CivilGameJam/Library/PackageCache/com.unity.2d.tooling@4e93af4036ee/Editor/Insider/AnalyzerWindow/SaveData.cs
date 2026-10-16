using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    class SaveData : ISaveFile
    {
        const int k_Version = 1;
        [Serializable]
        struct SaveDataContent
        {
            public string className;
            public string assemblyName;
            public List<string> content;
        }
        [SerializeField]
        int m_Version = k_Version;
        [SerializeField]
        List<SaveDataContent> m_Content = new();


        public void AddSaveData(ISaveData saveData)
        {
            var typeName = saveData.GetType().FullName;
            var assemblyName = saveData.GetType().Assembly.GetName().Name;
            for(int i = 0; i < m_Content.Count; i++)
            {
                if (m_Content[i].className == typeName &&
                    m_Content[i].assemblyName == assemblyName)
                {
                    m_Content[i].content.Add(JsonUtility.ToJson(saveData));
                    return;
                }
            }

            m_Content.Add(new SaveDataContent
            {
                className = typeName,
                assemblyName = assemblyName,
                content = new List<string> { JsonUtility.ToJson(saveData) }
            });
        }

        public void GetSaveData<T>(List<T> saveDataList) where T : ISaveData
        {
            if (m_Version == k_Version)
            {
                var typeName = typeof(T).FullName;
                var assemblyName = typeof(T).Assembly.GetName().Name;
                for(int i = 0; i < m_Content.Count; i++)
                {
                    if (m_Content[i].className == typeName &&
                        m_Content[i].assemblyName == assemblyName)
                    {
                        var existingData = m_Content[i].content;
                        for(int j = 0; j < existingData.Count; j++)
                        {
                            T data = JsonUtility.FromJson<T>(existingData[j]);
                            saveDataList.Add(data);
                        }
                        return;
                    }
                }
            }
        }
    }
}
