using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Connection state corresponding to when a client is attempting to connect to a server. Starts the client when
    /// entering. If successful, transitions to the ClientConnected state. If not, transitions to the Offline state.
    /// </summary>
    /// <summary>
    /// 클라이언트가 서버에 연결을 시도하는 상태에 해당하는 연결 상태입니다. 
    /// 이 상태에 진입하면 클라이언트를 시작합니다. 
    /// 연결에 성공하면 ClientConnected 상태로 전환되며, 실패하면 Offline 상태로 전환됩니다.
    /// </summary>
    class ClientConnectingState : OnlineState
    {
        protected ConnectionMethodBase m_ConnectionMethod;

        public ClientConnectingState Configure(ConnectionMethodBase baseConnectionMethod)
        {
            m_ConnectionMethod = baseConnectionMethod;
            return this;
        }

        public override void Enter()
        {
#pragma warning disable 4014
            ConnectClientAsync();
#pragma warning restore 4014
        }

        public override void Exit() { }

        public override void OnClientConnected(ulong _)
        {
            m_ConnectStatusPublisher.Publish(ConnectStatus.Success);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnected);
        }

        public override void OnClientDisconnect(ulong _)
        {
            // client ID is for sure ours here
            // 여기서 클라이언트 ID는 확실히 우리의 ID입니다.
            // 이 주석이 강조하는 부분은 "우리는 다른 클라이언트의 연결 해제를 고려할 필요 없이, 
            // 즉시 연결 실패 처리를 해도 된다"**는 점입니다.
            // 즉, 특정한 clientId를 확인하는 추가적인 로직이 필요 없이, 
            // StartingClientFailed(); 를 바로 호출해도 문제가 없다는 뜻입니다.
            StartingClientFailed();
        }

        void StartingClientFailed()
        {
            var disconnectReason = m_ConnectionManager.NetworkManager.DisconnectReason;
            if (string.IsNullOrEmpty(disconnectReason))
            {
                m_ConnectStatusPublisher.Publish(ConnectStatus.StartClientFailed);
            }
            else
            {
                var connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
                m_ConnectStatusPublisher.Publish(connectStatus);
            }
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }


        internal async Task ConnectClientAsync()
        {
            try
            {
                // 현재 연결 방법을 사용하여 NGO(Networking for Game Objects) 설정
                await m_ConnectionMethod.SetupClientConnectionAsync();

                // NGO의 StartClient가 모든 것을 실행함
                if (!m_ConnectionManager.NetworkManager.StartClient())
                {
                    throw new Exception("NetworkManager StartClient failed");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error connecting client, see following exception");
                Debug.LogException(e);
                StartingClientFailed();
                throw;
            }
        }
    }
}
