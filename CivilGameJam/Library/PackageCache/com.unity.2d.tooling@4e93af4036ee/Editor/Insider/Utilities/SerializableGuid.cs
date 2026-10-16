using System;
using UnityEngine;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    [Serializable]
    class SerializableGuid : IEquatable<SerializableGuid>
    {
        [SerializeField]
        string m_StringGUID = "";
        GUID m_Guid;

        public SerializableGuid()
        {
            InitGUID();
        }

        public SerializableGuid(GUID guid)
        {
            m_Guid = guid;
            m_StringGUID = guid.ToString();
        }

        public SerializableGuid(string guid)
        {
            m_Guid = new GUID(guid);
            m_StringGUID = guid;
        }

        public GUID guid
        {
            get
            {
                InitGUID();
                return m_Guid;
            }
        }

        public bool isValid => !string.IsNullOrEmpty(m_StringGUID);
        void InitGUID()
        {
            if (!string.IsNullOrEmpty(m_StringGUID))
                m_Guid = new GUID(m_StringGUID);
            if (m_Guid == null)
            {
                m_Guid = GUID.Generate();
                m_StringGUID = m_Guid.ToString();
            }
        }

        public bool Equals(SerializableGuid other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return guid == other.guid;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SerializableGuid)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(m_StringGUID, m_Guid);
        }

        public static bool operator == (SerializableGuid me, SerializableGuid other) => me?.guid == other?.guid;
        public static bool operator != (SerializableGuid me, SerializableGuid other) => me?.guid != other?.guid;
        public static bool operator == (GUID me, SerializableGuid other) => me == other?.guid;
        public static bool operator != (GUID me, SerializableGuid other) => me != other?.guid;
        public static implicit operator GUID(SerializableGuid me) => me?.guid ?? new GUID();
    }
}
