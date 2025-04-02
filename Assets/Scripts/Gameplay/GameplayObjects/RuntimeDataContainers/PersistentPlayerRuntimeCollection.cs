using System;
using Unity.BossRoom.Infrastructure;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
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