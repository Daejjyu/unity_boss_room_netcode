using System;
using Unity.Netcode;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// This struct is used by Action system (and GameDataSource) to refer to a specific action in runtime.
    /// It wraps a simple integer.
    /// </summary>
    /// <summary>
    /// 이 구조체는 실행 중 특정 액션을 참조하기 위해 액션 시스템(및 GameDataSource)에서 사용됩니다.
    /// 간단한 정수를 감쌉니다.
    /// </summary>
    public struct ActionID : INetworkSerializeByMemcpy, IEquatable<ActionID>
    {
        public int ID;

        public bool Equals(ActionID other)
        {
            return ID == other.ID;
        }

        public override bool Equals(object obj)
        {
            return obj is ActionID other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ID;
        }

        public static bool operator ==(ActionID x, ActionID y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(ActionID x, ActionID y)
        {
            return !(x == y);
        }

        public override string ToString()
        {
            return $"ActionID({ID})";
        }
    }
}