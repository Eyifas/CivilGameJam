using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    class EditorAssetInfo<T> where T : Object
    {
        [SerializeField]
        LazyLoadReference<Object> m_Object;
        [SerializeField]
        string m_AssetPath;
        [SerializeField]
        string m_Name;

        public EditorAssetInfo(EntityId entityId, string assetPath)
        {
            m_Object = entityId;
            m_AssetPath = assetPath;
            m_Name =m_Object.GetAsset()?.name ?? $"{m_Object.entityId}_No_Name";
        }

        public virtual string assetPath => m_AssetPath;

        public virtual string name
        {
            get => m_Name;
            set => m_Name = value;
        }

        //public T unityObject => m_InstanceID.asset as T;

        public T GetObject()
        {
            return m_Object.GetAsset() as T;
        }

        public EntityId entityId => m_Object.entityId;
        public string globalObjectIDString => m_Object.globalObjectIDString;
    }
}
