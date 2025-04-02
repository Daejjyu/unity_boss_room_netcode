using System;
using Unity.BossRoom.Infrastructure;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    /// <summary>
    /// A runtime list of <see cref="PersistentPlayer"/> objects that is populated both on clients and server.
    /// </summary>
    /// <summary>
    /// 클라이언트와 서버에서 모두 채워지는 <see cref="PersistentPlayer"/> 객체의 런타임 리스트입니다.
    /// </summary>
    [CreateAssetMenu]
    public class PersistentPlayerRuntimeCollection : RuntimeCollection<PersistentPlayer>
    {
        public bool TryGetPlayer(ulong clientID, out PersistentPlayer persistentPlayer)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (clientID == Items[i].OwnerClientId)
                {
                    persistentPlayer = Items[i];
                    return true;
                }
            }

            persistentPlayer = null;
            return false;
        }
    }
}
