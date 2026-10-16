using System;
using System.Collections.Generic;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    record IssueSaveData
    {
        // The type's assembly full name
        public string typeName;
        // the position of the type in the list
        public int position;
        // not used for now
        public bool inList = true;
    }

    [Serializable]
    record DataSourceData
    {
        public string typeName;
        public bool enabled;
        IReportDataSource m_ReportDataSource;
        public IReportDataSource reportDataSource
        {
            get => m_ReportDataSource;
            set
            {
                m_ReportDataSource = value;
                typeName = value.GetType().FullName;
            }
        }
        public string[] assetSearchPath = { "Assets" };
    }

    [Serializable]
    class AnalyzerWindowSaveData : ISaveData
    {
        public SaveData saveData;
        public List<IssueSaveData> issueSaveData = new ();
        public List<DataSourceData> reportDataSource = new ();

        public List<IAnalyzerReport> OrderReport(List<IAnalyzerReport> reports)
        {
            // Create position lookup dictionary
            Dictionary<string, int> positionLookup = new Dictionary<string, int>();
            for (int i = 0; i < issueSaveData.Count; i++)
            {
                positionLookup[issueSaveData[i].typeName] = issueSaveData[i].position;
            }

            // Separate items with saved positions from those without
            List<(IAnalyzerReport report, int position)> itemsWithSavedPositions = new ();
            List<IAnalyzerReport> itemsWithoutSavedPositions = new ();

            for (int i = 0; i < reports.Count; i++)
            {
                string typeName = reports[i].GetType().FullName;
                if (positionLookup.ContainsKey(typeName))
                {
                    itemsWithSavedPositions.Add(new(reports[i], positionLookup[typeName]));
                }
                else
                {
                    itemsWithoutSavedPositions.Add(reports[i]);
                }
            }

            // Sort items with saved positions
            itemsWithSavedPositions.Sort((x, y) => x.position.CompareTo(y.position));

            // Combine results
            List<IAnalyzerReport> result = new ();
            for (int i = 0; i < itemsWithSavedPositions.Count; i++)
            {
                result.Add(itemsWithSavedPositions[i].report);
            }
            itemsWithoutSavedPositions.Sort((x,y) => x.GetType().FullName.CompareTo(y.GetType().FullName));
            for (int i = 0; i < itemsWithoutSavedPositions.Count; i++)
            {
                result.Add(itemsWithoutSavedPositions[i]);
            }

            return result;
        }

        public void SaveReportPosition(List<IAnalyzerReport> report)
        {
            List<IssueSaveData> newData = new();
            for (int i = 0; i < report.Count; ++i)
            {
                newData.Add(new IssueSaveData
                {
                    typeName = report[i].GetType().FullName,
                    position = i,
                    inList = true
                });
                for(int j = 0; j < issueSaveData.Count; ++j)
                {
                    if (issueSaveData[j].typeName == newData[i].typeName)
                    {
                        newData[i].inList = issueSaveData[j].inList;
                        break;
                    }
                }
            }

            issueSaveData = newData;
        }
    }
}
