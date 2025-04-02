using System;
using System.Collections.Generic;
using Unity.BossRoom.ConnectionManagement;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// Attached to the player-characters' prefab, this maintains a list of active ServerCharacter objects for players.
    /// </summary>
    /// <summary>
    /// 플레이어 캐릭터 프리팹에 부착되어 있으며, 
    /// 플레이어의 활성화된 ServerCharacter 객체 목록을 유지합니다.
    /// </summary>
    /// <remarks>
    /// This is an optimization. In server code you can already get a list of players' ServerCharacters by
    /// iterating over the active connections and calling GetComponent() on their PlayerObject. But we need
    /// to iterate over all players quite often -- the monsters' IdleAIState does so in every Update() --
    /// and all those GetComponent() calls add up! So this optimization lets us iterate without calling
    /// GetComponent(). This will be refactored with a ScriptableObject-based approach on player collection.
    /// </remarks>
    /// <remarks>
    /// 이는 최적화입니다. 서버 코드에서는 활성 연결을 반복(iterate)하고 
    /// 해당 PlayerObject에서 GetComponent()를 호출하여 
    /// 플레이어의 ServerCharacter 목록을 가져올 수 있습니다. 
    /// 하지만 모든 플레이어를 자주 반복해야 합니다. 
    /// 예를 들어, 몬스터의 IdleAIState는 매 Update()마다 이를 수행합니다. 
    /// 이러한 GetComponent() 호출이 누적되면 
    /// 성능에 영향을 줄 수 있습니다. 따라서 이 최적화를 통해 
    /// GetComponent()를 호출하지 않고 반복(iterate)할 수 있도록 합니다.
    /// 이는 플레이어 컬렉션을 ScriptableObject 기반 접근 방식으로 리팩토링할 예정입니다.
    /// </remarks>
    [RequireComponent(typeof(ServerCharacter))]
    public class PlayerServerCharacter : NetworkBehaviour
    {
        static List<ServerCharacter> s_ActivePlayers = new List<ServerCharacter>();

        [SerializeField]
        ServerCharacter m_CachedServerCharacter;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                s_ActivePlayers.Add(m_CachedServerCharacter);
            }
            else
            {
                enabled = false;
            }

        }

        void OnDisable()
        {
            s_ActivePlayers.Remove(m_CachedServerCharacter);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                var movementTransform = m_CachedServerCharacter.Movement.transform;
                SessionPlayerData? sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(OwnerClientId);
                if (sessionPlayerData.HasValue)
                {
                    var playerData = sessionPlayerData.Value;
                    playerData.PlayerPosition = movementTransform.position;
                    playerData.PlayerRotation = movementTransform.rotation;
                    playerData.CurrentHitPoints = m_CachedServerCharacter.HitPoints;
                    playerData.HasCharacterSpawned = true;
                    SessionManager<SessionPlayerData>.Instance.SetPlayerData(OwnerClientId, playerData);
                }
            }
        }

        /// <summary>
        /// Returns a list of all active players' ServerCharacters. Treat the list as read-only!
        /// The list will be empty on the client.
        /// </summary>
        /// <summary>
        /// 모든 활성화된 플레이어의 ServerCharacter 목록을 반환합니다. 
        /// 해당 목록은 읽기 전용으로 취급해야 합니다!
        /// 클라이언트에서는 이 목록이 비어 있습니다.
        /// </summary>
        public static List<ServerCharacter> GetPlayerServerCharacters()
        {
            return s_ActivePlayers;
        }

        /// <summary>
        /// Returns the ServerCharacter owned by a specific client. Always returns null on the client.
        /// </summary>
        /// <param name="ownerClientId"></param>
        /// <returns>The ServerCharacter owned by the client, or null if no ServerCharacter is found</returns>
        /// <summary>
        /// 특정 클라이언트가 소유한 ServerCharacter를 반환합니다. 
        /// 클라이언트에서는 항상 null을 반환합니다.
        /// </summary>
        /// <param name="ownerClientId"></param>
        /// <returns>클라이언트가 소유한 ServerCharacter 객체를 반환하며, 
        /// 찾을 수 없는 경우 null을 반환합니다.</returns>
        public static ServerCharacter GetPlayerServerCharacter(ulong ownerClientId)
        {
            foreach (var playerServerCharacter in s_ActivePlayers)
            {
                if (playerServerCharacter.OwnerClientId == ownerClientId)
                {
                    return playerServerCharacter;
                }
            }
            return null;
        }
    }
}