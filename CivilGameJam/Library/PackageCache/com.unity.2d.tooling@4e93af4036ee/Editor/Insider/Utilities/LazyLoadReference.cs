using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    struct LazyLoadReference<T> where T : Object
    {
        GlobalObjectId m_GlobalObjectID;
        [SerializeField]
        SerializableGuid m_GUID;
        [SerializeField]
        long m_LocalFileID;
        [SerializeField]
        string m_GlobalObjectIDString;
        EntityId m_EntityId;

        public LazyLoadReference(T obj)
        {
            m_GlobalObjectID = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            m_GlobalObjectIDString = m_GlobalObjectID.ToString();
            if (obj)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out m_LocalFileID);
                m_GUID = new SerializableGuid(guid);
                m_EntityId = obj.GetEntityId();
            }
            else
            {
                m_EntityId = EntityId.None;
                m_LocalFileID = 0;
                m_GUID = new SerializableGuid(new GUID());
            }
        }

        public LazyLoadReference(EntityId instanceID)
        {
            m_GlobalObjectID = GlobalObjectId.GetGlobalObjectIdSlow(instanceID);
            m_GlobalObjectIDString = m_GlobalObjectID.ToString();
            m_EntityId = instanceID;
            var obj = EditorUtility.EntityIdToObject(m_EntityId);
            if (obj != null)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out m_LocalFileID);
                m_GUID = new SerializableGuid(guid);
            }
            else
            {
                m_EntityId = EntityId.None;
                m_LocalFileID = 0;
                m_GUID = new SerializableGuid(new GUID());
            }
        }

        public static implicit operator LazyLoadReference<T>(T asset)
        {
            return new LazyLoadReference<T>(asset);
        }

        public static implicit operator LazyLoadReference<T>(EntityId entityId)
        {
            return new LazyLoadReference<T>(entityId);
        }

        public T GetAsset()
        {
            if (m_EntityId == EntityId.None)
            {
                if (m_GlobalObjectID.assetGUID == new GUID())
                {
                    GlobalObjectId.TryParse(m_GlobalObjectIDString, out m_GlobalObjectID);
                }
                m_EntityId = GlobalObjectId.GlobalObjectIdentifierToEntityIdSlow(m_GlobalObjectID);

            }
            return EditorUtility.EntityIdToObject(m_EntityId) as T;
        }

        public EntityId entityId => m_EntityId;
        public string globalObjectIDString => m_GlobalObjectIDString;
    }
}
