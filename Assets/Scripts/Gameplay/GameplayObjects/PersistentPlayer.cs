using System;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Utils;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
  [RequireComponent(typeof(NetworkObject))]
  public class PersistentPlayer : NetworkBehaviour
  {
    [SerializeField]
    PersistentPlayerRuntimeCollection m_PersistentPlayerRuntimeCollection;

    [SerializeField]
    NetworkNameState m_NetworkNameState;

    [SerializeField]
    NetworkAvatarGuidState m_NetworkAvatarGuidState;



  }

}