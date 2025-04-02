using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Utils
{
    /// <summary>
    /// NetworkBehaviour containing only one NetworkVariableString which represents this object's name.
    /// </summary>
    /// <summary>
    /// 이 객체의 이름을 나타내는 하나의 NetworkVariableString만 포함하는 NetworkBehaviour입니다.
    /// </summary>
    public class NetworkNameState : NetworkBehaviour
    {
        [HideInInspector]
        public NetworkVariable<FixedPlayerName> Name = new NetworkVariable<FixedPlayerName>();
    }

    /// <summary>
    /// Wrapping FixedString so that if we want to change player name max size in the future, we only do it once here
    /// </summary>
    /// <summary>
    /// FixedString을 래핑하여, 나중에 플레이어 이름 최대 크기를 변경하고 싶을 때 여기서 한 번만 수정할 수 있도록 합니다.
    /// </summary>
    public struct FixedPlayerName : INetworkSerializable
    {
        FixedString32Bytes m_Name;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_Name);
        }

        public override string ToString()
        {
            return m_Name.Value.ToString();
        }

        public static implicit operator string(FixedPlayerName s) => s.ToString();
        public static implicit operator FixedPlayerName(string s) => new FixedPlayerName() { m_Name = new FixedString32Bytes(s) };
    }
}
