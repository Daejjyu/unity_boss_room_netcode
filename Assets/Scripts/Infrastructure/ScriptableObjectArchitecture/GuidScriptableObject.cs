using System;
using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// ScriptableObject that stores a GUID for unique identification. The population of this field is implemented
    /// inside an Editor script.
    /// </summary>
    /// <summary>
    /// 고유 식별을 위한 GUID를 저장하는 ScriptableObject입니다. 
    /// 이 필드의 값은 Editor 스크립트 내에서 설정됩니다.
    /// </summary>
    [Serializable]
    public abstract class GuidScriptableObject : ScriptableObject
    {
        [HideInInspector]
        [SerializeField]
        byte[] m_Guid;

        public Guid Guid => new Guid(m_Guid);

        void OnValidate()
        {
            if (m_Guid.Length == 0)
            {
                m_Guid = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
