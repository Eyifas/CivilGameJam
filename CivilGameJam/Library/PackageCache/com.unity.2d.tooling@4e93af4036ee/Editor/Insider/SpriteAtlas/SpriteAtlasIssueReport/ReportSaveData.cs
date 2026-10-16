using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    class ReportSaveData<TCellData>
    {
        [SerializeReference]
        public TCellData data;
        public int id;
        [SerializeReference]
        public List<ReportSaveData<TCellData>> children = new();
    }

    [Serializable]
    class ReportSaveDataRoot<TCellData>
    {
        public long lastCaptureTime;
        [SerializeReference]
        public List<ReportSaveData<TCellData>> root = new();
        public int version;

        public static List<TreeViewItemData<TCellData>> ConvertToTreeViewItemData(List<ReportSaveData<TCellData>> toConvert)
        {
            List<TreeViewItemData<TCellData>> items = new();
            for (int i = 0; i < toConvert.Count; ++i)
            {
                var cellData = toConvert[i];
                List<TreeViewItemData<TCellData>> children = null;
                if (cellData.children != null)
                {
                    children = ConvertToTreeViewItemData(cellData.children);
                }
                items.Add(new TreeViewItemData<TCellData>(cellData.id, cellData.data, children));
            }
            return items;
        }

        public static  List<ReportSaveData<TCellData>> CovertToSaveFormat(IEnumerable<TreeViewItemData<TCellData>> data)
        {
            List<ReportSaveData<TCellData>> items = new();
            foreach (var item in data)
            {
                List<ReportSaveData<TCellData>> children = null;
                if (item.hasChildren)
                {
                    children = CovertToSaveFormat(item.children);
                }

                var saveItem = new ReportSaveData<TCellData>() { children = children, data = item.data, id = item.id };
                items.Add(saveItem);
            }
            return items;
        }
    }
}
