using System.Collections;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using Action = Unity.BossRoom.Gameplay.Actions.Action;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{

  [RequireComponent(typeof(NetworkHealthState),
            typeof(NetworkLifeState),
            typeof(NetworkAvatarGuidState))]
  public class ServerCharacter : NetworkBehaviour, ITargetable
  {
    [FormerlySerializedAs("m_ClientVisualization")]
    [SerializeField]
    ClientCharacter m_ClientCharacter;


    public bool IsNpc => throw new System.NotImplementedException();

    public bool IsValidTarget => throw new System.NotImplementedException();
  }
}