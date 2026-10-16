using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    class EditorResourceUsageInfo<T> : EditorAssetInfo<T> where T : Object
    {
        [SerializeField]
        int m_RefCount;
        [SerializeField]
        int m_RefCountActive;
        [SerializeField]
        ulong m_MemorySize;
        [SerializeField]
        float m_Width;
        [SerializeField]
        float m_Height;
        [SerializeField]
        float m_UsedArea;
        [SerializeField]
        float m_TotalArea;

        public EditorResourceUsageInfo(EntityId entityId, string assetPath)
            : base(entityId, assetPath)
        { }

        public int refCount
        {
            get => m_RefCount;
            set => m_RefCount = value;
        }

        public int refCountActive
        {
            get => m_RefCountActive;
            set => m_RefCountActive = value;
        }

        public ulong memorySize
        {
            get => m_MemorySize;
            set => m_MemorySize = value;
        }

        public float width
        {
            get => m_Width;
            set => m_Width = value;
        }

        public float height
        {
            get => m_Height;
            set => m_Height = value;
        }

        public float usedArea
        {
            get => m_UsedArea;
            set => m_UsedArea = value;
        }

        public float totalArea
        {
            get => m_TotalArea > 0 ? m_TotalArea : width * height;
            set => m_TotalArea = value;
        }

        public float unusedArea => totalArea - usedArea;
        public float usedRatio =>  (totalArea != 0 ? usedArea / totalArea : 0);
        public float unUsedRation => 1 - usedRatio;
        public float usedMemory => memorySize * usedRatio;
        public virtual float unusedMemory => memorySize * unUsedRation;
    }
}
