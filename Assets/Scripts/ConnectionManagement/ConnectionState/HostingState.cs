using System;
using Unity.BossRoom.Infrastructure;
using Unity.BossRoom.UnityServices.Lobbies;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Multiplayer.Samples.Utilities;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Connection state corresponding to a listening host. Handles incoming client connections. When shutting down or
    /// being timed out, transitions to the Offline state.
    /// </summary>
    /// 
    /// <summary>
    /// 클라이언트의 연결을 처리하는 리스닝 호스트에 해당하는 연결 상태입니다.
    /// 클라이언트의 접속을 관리합니다.
    /// 호스트가 종료되거나 타임아웃이 발생하면 Offline 상태로 전환됩니다.
    /// </summary>
    class HostingState : OnlineState
    {
        [Inject]
        LobbyServiceFacade m_LobbyServiceFacade;
        [Inject]
        IPublisher<ConnectionEventMessage> m_ConnectionEventPublisher;

        // used in ApprovalCheck. This is intended as a bit of light protection against DOS attacks that rely on sending silly big buffers of garbage.
        // ApprovalCheck에서 사용됩니다. 이는 쓸데없이 큰 무의미한 데이터를 보내는 
        // DOS 공격에 대한 가벼운 보호를 의도한 것입니다.
        const int k_MaxConnectPayload = 1024;

        public override void Enter()
        {
            //The "BossRoom" server always advances to CharSelect immediately on start. Different games
            //may do this differently.
            // "BossRoom" 서버는 시작 시 항상 즉시 CharSelect로 진행됩니다. 
            // 다른 게임들은 이 과정을 다르게 할 수 있습니다.
            SceneLoaderWrapper.Instance.LoadScene("CharSelect", useNetworkSceneManager: true);

            if (m_LobbyServiceFacade.CurrentUnityLobby != null)
            {
                m_LobbyServiceFacade.BeginTracking();
            }
        }

        public override void Exit()
        {
            SessionManager<SessionPlayerData>.Instance.OnServerEnded();
        }

        public override void OnClientConnected(ulong clientId)
        {
            var playerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
            if (playerData != null)
            {
                m_ConnectionEventPublisher.Publish(new ConnectionEventMessage() { ConnectStatus = ConnectStatus.Success, PlayerName = playerData.Value.PlayerName });
            }
            else
            {
                // This should not happen since player data is assigned during connection approval
                // 연결 승인을 통해 플레이어 데이터가 할당되므로 이는 발생하지 않아야 합니다.
                Debug.LogError($"No player data associated with client {clientId}");
                var reason = JsonUtility.ToJson(ConnectStatus.GenericDisconnect);
                m_ConnectionManager.NetworkManager.DisconnectClient(clientId, reason);
            }

        }

        public override void OnClientDisconnect(ulong clientId)
        {
            if (clientId != m_ConnectionManager.NetworkManager.LocalClientId)
            {
                var playerId = SessionManager<SessionPlayerData>.Instance.GetPlayerId(clientId);
                if (playerId != null)
                {
                    var sessionData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(playerId);
                    if (sessionData.HasValue)
                    {
                        m_ConnectionEventPublisher.Publish(new ConnectionEventMessage() { ConnectStatus = ConnectStatus.GenericDisconnect, PlayerName = sessionData.Value.PlayerName });
                    }
                    SessionManager<SessionPlayerData>.Instance.DisconnectClient(clientId);
                }
            }
        }

        public override void OnUserRequestedShutdown()
        {
            var reason = JsonUtility.ToJson(ConnectStatus.HostEndedSession);
            for (var i = m_ConnectionManager.NetworkManager.ConnectedClientsIds.Count - 1; i >= 0; i--)
            {
                var id = m_ConnectionManager.NetworkManager.ConnectedClientsIds[i];
                if (id != m_ConnectionManager.NetworkManager.LocalClientId)
                {
                    m_ConnectionManager.NetworkManager.DisconnectClient(id, reason);
                }
            }
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }

        public override void OnServerStopped()
        {
            m_ConnectStatusPublisher.Publish(ConnectStatus.GenericDisconnect);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }

        /// <summary>
        /// This logic plugs into the "ConnectionApprovalResponse" exposed by Netcode.NetworkManager. It is run every time a client connects to us.
        /// The complementary logic that runs when the client starts its connection can be found in ClientConnectingState.
        /// </summary>
        /// <remarks>
        /// Multiple things can be done here, some asynchronously. For example, it could authenticate your user against an auth service like UGS' auth service. It can
        /// also send custom messages to connecting users before they receive their connection result (this is useful to set status messages client side
        /// when connection is refused, for example).
        /// Note on authentication: It's usually harder to justify having authentication in a client hosted game's connection approval. Since the host can't be trusted,
        /// clients shouldn't send it private authentication tokens you'd usually send to a dedicated server.
        /// </remarks>
        /// <param name="request"> The initial request contains, among other things, binary data passed into StartClient. In our case, this is the client's GUID,
        /// which is a unique identifier for their install of the game that persists across app restarts.
        ///  <param name="response"> Our response to the approval process. In case of connection refusal with custom return message, we delay using the Pending field.
        /// 
        /// <summary>
        /// 이 로직은 Netcode.NetworkManager가 노출한 "ConnectionApprovalResponse"에 연결됩니다. 클라이언트가 우리에게 연결할 때마다 실행됩니다.
        /// 클라이언트가 연결을 시작할 때 실행되는 보완적인 로직은 ClientConnectingState에서 찾을 수 있습니다.
        /// </summary>
        /// <remarks>
        /// 여기서 여러 가지 작업을 할 수 있으며, 일부는 비동기적으로 실행될 수 있습니다. 예를 들어, 사용자를 인증 서비스(예: UGS의 인증 서비스)에 대해 인증할 수 있습니다. 
        /// 또한 연결 결과를 받기 전에 연결 중인 사용자에게 사용자 지정 메시지를 보낼 수도 있습니다(예: 연결이 거부될 때 클라이언트 측에서 상태 메시지를 설정할 수 있음).
        /// 인증에 대한 참고: 클라이언트 호스트 게임에서 연결 승인을 통한 인증을 하는 것은 일반적으로 어렵습니다. 호스트는 신뢰할 수 없으므로 클라이언트는 일반적으로 전용 서버에 보내는 개인 인증 토큰을 호스트에 보내지 않아야 합니다.
        /// </remarks>
        /// <param name="request"> 초기 요청은 StartClient에 전달된 이진 데이터를 포함합니다. 이 경우, 이는 게임 설치의 고유 식별자인 클라이언트의 GUID로, 앱을 재시작해도 유지됩니다.
        /// <param name="response"> 연결 승인 프로세스에 대한 우리의 응답입니다. 연결 거부 시 사용자 지정 메시지와 함께 응답을 지연하려면 Pending 필드를 사용합니다.

        public override void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var connectionData = request.Payload;
            var clientId = request.ClientNetworkId;
            if (connectionData.Length > k_MaxConnectPayload)
            {
                // If connectionData too high, deny immediately to avoid wasting time on the server. This is intended as
                // a bit of light protection against DOS attacks that rely on sending silly big buffers of garbage.
                // connectionData가 너무 크면, 서버에서 시간을 낭비하지 않도록 즉시 거부합니다. 
                // 이는 불필요하게 큰 데이터 버퍼를 보내는 DOS 공격에 대한 경미한 
                // 보호책으로 의도된 것입니다.
                response.Approved = false;
                return;
            }

            var payload = System.Text.Encoding.UTF8.GetString(connectionData);
            var connectionPayload = JsonUtility.FromJson<ConnectionPayload>(payload); // https://docs.unity3d.com/2020.2/Documentation/Manual/JSONSerialization.html
            var gameReturnStatus = GetConnectStatus(connectionPayload);

            if (gameReturnStatus == ConnectStatus.Success)
            {
                SessionManager<SessionPlayerData>.Instance.SetupConnectingPlayerSessionData(clientId, connectionPayload.playerId,
                    new SessionPlayerData(clientId, connectionPayload.playerName, new NetworkGuid(), 0, true));

                // connection approval will create a player object for you
                // 연결 승인은 플레이어 객체를 자동으로 생성합니다.
                response.Approved = true;
                response.CreatePlayerObject = true;
                response.Position = Vector3.zero;
                response.Rotation = Quaternion.identity;
                return;
            }

            response.Approved = false;
            response.Reason = JsonUtility.ToJson(gameReturnStatus);
            if (m_LobbyServiceFacade.CurrentUnityLobby != null)
            {
                m_LobbyServiceFacade.RemovePlayerFromLobbyAsync(connectionPayload.playerId);
            }
        }

        ConnectStatus GetConnectStatus(ConnectionPayload connectionPayload)
        {
            if (m_ConnectionManager.NetworkManager.ConnectedClientsIds.Count >= m_ConnectionManager.MaxConnectedPlayers)
            {
                return ConnectStatus.ServerFull;
            }

            if (connectionPayload.isDebug != Debug.isDebugBuild)
            {
                return ConnectStatus.IncompatibleBuildType;
            }

            return SessionManager<SessionPlayerData>.Instance.IsDuplicateConnection(connectionPayload.playerId) ?
                ConnectStatus.LoggedInAgain : ConnectStatus.Success;
        }
    }
}
