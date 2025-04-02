using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Multiplayer.Samples.BossRoom
{
    public interface ISessionPlayerData
    {
        bool IsConnected { get; set; }
        ulong ClientID { get; set; }
        void Reinitialize();
    }

    /// <summary>
    /// This class uses a unique player ID to bind a player to a session. Once that player connects to a host, the host
    /// associates the current ClientID to the player's unique ID. If the player disconnects and reconnects to the same
    /// host, the session is preserved.
    /// </summary>
    /// <summary>
    /// 이 클래스는 고유한 플레이어 ID를 사용하여 플레이어를 세션에 바인딩합니다. 플레이어가 호스트에 연결되면, 
    /// 호스트는 현재 ClientID를 플레이어의 고유 ID와 연결합니다. 
    /// 플레이어가 연결을 끊고 동일한 호스트에 다시 연결하면 세션이 유지됩니다.
    /// </summary>
    /// <remarks>
    /// Using a client-generated player ID and sending it directly could be problematic, as a malicious user could
    /// intercept it and reuse it to impersonate the original user. We are currently investigating this to offer a
    /// solution that handles security better.
    /// </remarks>
    /// <remarks>
    /// 클라이언트에서 생성한 플레이어 ID를 직접 전송하는 것은 보안상 문제가 될 수 있습니다. 
    /// 악의적인 사용자가 이를 가로채 원래 사용자인 척할 가능성이 있기 때문입니다. 
    /// 현재 보안을 보다 강화할 수 있는 해결책을 조사 중입니다.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public class SessionManager<T> where T : struct, ISessionPlayerData
    {
        SessionManager()
        {
            m_ClientData = new Dictionary<string, T>();
            m_ClientIDToPlayerId = new Dictionary<ulong, string>();
        }

        public static SessionManager<T> Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new SessionManager<T>();
                }

                return s_Instance;
            }
        }

        static SessionManager<T> s_Instance;

        /// <summary>
        /// Maps a given client player id to the data for a given client player.
        /// </summary>
        /// <summary>
        /// 주어진 클라이언트 플레이어 ID를 해당 클라이언트 플레이어의 데이터에 매핑합니다.
        /// </summary>
        Dictionary<string, T> m_ClientData;

        /// <summary>
        /// Map to allow us to cheaply map from player id to player data.
        /// </summary>
        /// <summary>
        /// 플레이어 ID에서 플레이어 데이터로 효율적으로 매핑할 수 있도록 하는 맵입니다.
        /// </summary>
        Dictionary<ulong, string> m_ClientIDToPlayerId;

        bool m_HasSessionStarted;

        /// <summary>
        /// Handles client disconnect.
        /// </summary>
        /// <summary>
        /// 클라이언트 연결 해제를 처리합니다.
        /// </summary>
        public void DisconnectClient(ulong clientId)

        {
            if (m_HasSessionStarted)
            {
                // Mark client as disconnected, but keep their data so they can reconnect.  
                // 클라이언트를 연결 해제 상태로 표시하지만, 데이터를 유지하여 다시 연결할 수 있도록 합니다.
                if (m_ClientIDToPlayerId.TryGetValue(clientId, out var playerId))
                {
                    var playerData = GetPlayerData(playerId);
                    if (playerData != null && playerData.Value.ClientID == clientId)
                    {
                        var clientData = m_ClientData[playerId];
                        clientData.IsConnected = false;
                        m_ClientData[playerId] = clientData;
                    }
                }
            }
            else
            {
                // Session has not started, no need to keep their data  
                // 세션이 시작되지 않았으므로 데이터를 유지할 필요가 없습니다.
                if (m_ClientIDToPlayerId.TryGetValue(clientId, out var playerId))
                {
                    m_ClientIDToPlayerId.Remove(clientId);
                    var playerData = GetPlayerData(playerId);
                    if (playerData != null && playerData.Value.ClientID == clientId)
                    {
                        m_ClientData.Remove(playerId);
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <summary>
        /// </summary>
        /// <param name="playerId">This is the playerId that is unique to this client and persists across multiple logins from the same client</param>
        /// <param name="playerId">이 값은 특정 클라이언트에 고유한 playerId이며, 동일한 클라이언트에서 여러 번 로그인해도 유지됩니다.</param>
        /// <returns>True if a player with this ID is already connected.</returns>
        /// <returns>이 ID를 가진 플레이어가 이미 연결되어 있으면 True를 반환합니다.</returns>
        public bool IsDuplicateConnection(string playerId)
        {
            return m_ClientData.ContainsKey(playerId) && m_ClientData[playerId].IsConnected;
        }

        /// <summary>
        /// Adds a connecting player's session data if it is a new connection, or updates their session data in case of a reconnection.
        /// </summary>
        /// <summary>
        /// 새로운 연결일 경우 플레이어의 세션 데이터를 추가하고, 재연결의 경우 기존 세션 데이터를 업데이트합니다.
        /// </summary>
        /// <param name="clientId">This is the clientId that Netcode assigned us on login. It does not persist across multiple logins from the same client.</param>
        /// <param name="clientId">이 값은 Netcode가 로그인 시 할당한 clientId입니다. 동일한 클라이언트에서 여러 번 로그인해도 유지되지 않습니다.</param>
        /// <param name="playerId">This is the playerId that is unique to this client and persists across multiple logins from the same client</param>
        /// <param name="playerId">이 값은 특정 클라이언트에 고유한 playerId이며, 동일한 클라이언트에서 여러 번 로그인해도 유지됩니다.</param>
        /// <param name="sessionPlayerData">The player's initial data</param>
        /// <param name="sessionPlayerData">플레이어의 초기 데이터</param>
        public void SetupConnectingPlayerSessionData(ulong clientId, string playerId, T sessionPlayerData)
        {
            var isReconnecting = false;

            // Test for duplicate connection
            // 중복 연결 여부 확인
            if (IsDuplicateConnection(playerId))
            {
                Debug.LogError($"Player ID {playerId} already exists. This is a duplicate connection. Rejecting this session data.");
                Debug.LogError($"플레이어 ID {playerId}가 이미 존재합니다. 중복 연결입니다. 이 세션 데이터를 거부합니다.");
                return;
            }

            // If another client exists with the same playerId
            // 동일한 playerId를 가진 다른 클라이언트가 있는 경우
            if (m_ClientData.ContainsKey(playerId))
            {
                if (!m_ClientData[playerId].IsConnected)
                {
                    // If this connecting client has the same player Id as a disconnected client, this is a reconnection.
                    // 현재 연결을 시도하는 클라이언트가 이전에 연결이 끊어진 동일한 playerId를 가지고 있다면, 이는 재연결입니다.
                    isReconnecting = true;
                }
            }

            // Reconnecting. Give data from old player to new player
            // 재연결 중. 기존 플레이어 데이터를 새 플레이어에게 할당
            if (isReconnecting)
            {
                // Update player session data
                // 플레이어 세션 데이터 업데이트
                sessionPlayerData = m_ClientData[playerId];
                sessionPlayerData.ClientID = clientId;
                sessionPlayerData.IsConnected = true;
            }

            // Populate our dictionaries with the SessionPlayerData
            // 세션 플레이어 데이터를 딕셔너리에 저장
            m_ClientIDToPlayerId[clientId] = playerId;
            m_ClientData[playerId] = sessionPlayerData;
        }

        /// <summary>
        ///
        /// </summary>
        /// <summary>
        /// </summary>
        /// <param name="clientId">id of the client whose data is requested</param>
        /// <param name="clientId">데이터를 요청하는 클라이언트의 ID</param>
        /// <returns>The Player ID matching the given client ID</returns>
        /// <returns>주어진 클라이언트 ID와 일치하는 플레이어 ID</returns>
        public string GetPlayerId(ulong clientId)
        {
            if (m_ClientIDToPlayerId.TryGetValue(clientId, out string playerId))
            {
                return playerId;
            }

            Debug.Log($"No client player ID found mapped to the given client ID: {clientId}");
            Debug.Log($"주어진 클라이언트 ID에 매핑된 플레이어 ID를 찾을 수 없습니다: {clientId}");
            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <summary>
        /// </summary>
        /// <param name="clientId">id of the client whose data is requested</param>
        /// <param name="clientId">데이터를 요청하는 클라이언트의 ID</param>
        /// <returns>Player data struct matching the given ID</returns>
        /// <returns>주어진 ID와 일치하는 플레이어 데이터 구조체</returns>
        public T? GetPlayerData(ulong clientId)
        {
            // First see if we have a playerId matching the clientID given.
            // 먼저 주어진 클라이언트 ID와 일치하는 playerId가 있는지 확인
            var playerId = GetPlayerId(clientId);
            if (playerId != null)
            {
                return GetPlayerData(playerId);
            }

            Debug.Log($"No client player ID found mapped to the given client ID: {clientId}");
            Debug.Log($"주어진 클라이언트 ID에 매핑된 플레이어 ID를 찾을 수 없습니다: {clientId}");
            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <summary>
        /// </summary>
        /// <param name="playerId">Player ID of the client whose data is requested</param>
        /// <param name="playerId">데이터를 요청하는 클라이언트의 플레이어 ID</param>
        /// <returns>Player data struct matching the given ID</returns>
        /// <returns>주어진 ID와 일치하는 플레이어 데이터 구조체</returns>
        public T? GetPlayerData(string playerId)
        {
            if (m_ClientData.TryGetValue(playerId, out T data))
            {
                return data;
            }

            Debug.Log($"No PlayerData of matching player ID found: {playerId}");
            Debug.Log($"일치하는 플레이어 ID의 PlayerData를 찾을 수 없습니다: {playerId}");
            return null;
        }

        /// <summary>
        /// Updates player data
        /// </summary>
        /// <summary>
        /// 플레이어 데이터를 업데이트합니다.
        /// </summary>
        /// <param name="clientId">id of the client whose data will be updated</param>
        /// <param name="clientId">데이터가 업데이트될 클라이언트의 ID</param>
        /// <param name="sessionPlayerData">new data to overwrite the old</param>
        /// <param name="sessionPlayerData">기존 데이터를 덮어쓸 새로운 데이터</param>
        public void SetPlayerData(ulong clientId, T sessionPlayerData)
        {
            if (m_ClientIDToPlayerId.TryGetValue(clientId, out string playerId))
            {
                m_ClientData[playerId] = sessionPlayerData;
            }
            else
            {
                Debug.LogError($"No client player ID found mapped to the given client ID: {clientId}");
                Debug.LogError($"주어진 클라이언트 ID에 매핑된 플레이어 ID를 찾을 수 없습니다: {clientId}");
            }
        }

        /// <summary>
        /// Marks the current session as started, so from now on we keep the data of disconnected players.
        /// </summary>
        /// <summary>
        /// 현재 세션을 시작된 상태로 표시하여, 이후부터는 연결이 끊긴 플레이어의 데이터를 유지합니다.
        /// </summary>
        public void OnSessionStarted()
        {
            m_HasSessionStarted = true;
        }

        /// <summary>
        /// Reinitializes session data from connected players, and clears data from disconnected players, 
        /// so that if they reconnect in the next game, they will be treated as new players.
        /// </summary>
        /// <summary>
        /// 연결된 플레이어의 세션 데이터를 다시 초기화하고, 연결이 끊긴 플레이어의 데이터를 삭제하여, 
        /// 다음 게임에서 다시 연결할 경우 새로운 플레이어로 취급되도록 합니다.
        /// </summary>
        public void OnSessionEnded()
        {
            ClearDisconnectedPlayersData();
            ReinitializePlayersData();
            m_HasSessionStarted = false;
        }

        /// <summary>
        /// Resets all our runtime state, so it is ready to be reinitialized when starting a new server.
        /// </summary>
        /// <summary>
        /// 모든 런타임 상태를 초기화하여, 새 서버를 시작할 때 다시 초기화할 수 있도록 준비합니다.
        /// </summary>
        public void OnServerEnded()
        {
            m_ClientData.Clear();
            m_ClientIDToPlayerId.Clear();
            m_HasSessionStarted = false;
        }

        void ReinitializePlayersData()
        {
            foreach (var id in m_ClientIDToPlayerId.Keys)
            {
                string playerId = m_ClientIDToPlayerId[id];
                T sessionPlayerData = m_ClientData[playerId];
                sessionPlayerData.Reinitialize();
                m_ClientData[playerId] = sessionPlayerData;
            }
        }

        void ClearDisconnectedPlayersData()
        {
            List<ulong> idsToClear = new List<ulong>();
            foreach (var id in m_ClientIDToPlayerId.Keys)
            {
                var data = GetPlayerData(id);
                if (data is { IsConnected: false })
                {
                    idsToClear.Add(id);
                }
            }

            foreach (var id in idsToClear)
            {
                string playerId = m_ClientIDToPlayerId[id];
                var playerData = GetPlayerData(playerId);
                if (playerData != null && playerData.Value.ClientID == id)
                {
                    m_ClientData.Remove(playerId);
                }

                m_ClientIDToPlayerId.Remove(id);
            }
        }
    }
}
