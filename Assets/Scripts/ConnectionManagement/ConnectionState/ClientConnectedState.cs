using Unity.BossRoom.UnityServices.Lobbies;
using UnityEngine;
using VContainer;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Connection state corresponding to a connected client. When being disconnected, transitions to the
    /// ClientReconnecting state if no reason is given, or to the Offline state.
    /// </summary>
    /// <summary>
    /// 연결된 클라이언트에 해당하는 연결 상태입니다. 
    /// 클라이언트가 연결 해제될 경우, 이유가 제공되지 않으면 ClientReconnecting 상태로 전환되며, 
    /// 이유가 제공된 경우 Offline 상태로 전환됩니다.
    /// </summary>
    class ClientConnectedState : OnlineState
    {
        [Inject]
        protected LobbyServiceFacade m_LobbyServiceFacade;

        public override void Enter()
        {
            if (m_LobbyServiceFacade.CurrentUnityLobby != null)
            {
                m_LobbyServiceFacade.BeginTracking();
            }
        }

        public override void Exit() { }

        public override void OnClientDisconnect(ulong _)
        {
            var disconnectReason = m_ConnectionManager.NetworkManager.DisconnectReason;
            if (string.IsNullOrEmpty(disconnectReason) ||
                disconnectReason == "Disconnected due to host shutting down.")
            {
                m_ConnectStatusPublisher.Publish(ConnectStatus.Reconnecting);
                m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientReconnecting);
            }
            else
            {
                var connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
                m_ConnectStatusPublisher.Publish(connectStatus);
                m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
            }
        }
    }
}
