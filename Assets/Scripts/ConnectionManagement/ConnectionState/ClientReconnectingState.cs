using System;
using System.Collections;
using Unity.BossRoom.Infrastructure;
using UnityEngine;
using VContainer;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Connection state corresponding to a client attempting to reconnect to a server. It will try to reconnect a
    /// number of times defined by the ConnectionManager's NbReconnectAttempts property. If it succeeds, it will
    /// transition to the ClientConnected state. If not, it will transition to the Offline state. If given a disconnect
    /// reason first, depending on the reason given, may not try to reconnect again and transition directly to the
    /// Offline state.
    /// </summary>
    /// 
    /// <summary>
    /// 서버에 다시 연결을 시도하는 클라이언트에 해당하는 연결 상태입니다.
    /// ConnectionManager의 NbReconnectAttempts 속성에 정의된 횟수만큼 재연결을 시도합니다.
    /// 재연결에 성공하면 ClientConnected 상태로 전환되며, 실패하면 Offline 상태로 전환됩니다.
    /// 만약 연결 해제 사유가 주어진 경우, 해당 사유에 따라 재연결을 시도하지 않고 
    /// 즉시 Offline 상태로 전환될 수도 있습니다.
    /// </summary>
    class ClientReconnectingState : ClientConnectingState
    {
        [Inject]
        IPublisher<ReconnectMessage> m_ReconnectMessagePublisher;

        Coroutine m_ReconnectCoroutine;
        int m_NbAttempts;

        const float k_TimeBeforeFirstAttempt = 1;
        const float k_TimeBetweenAttempts = 5;

        public override void Enter()
        {
            m_NbAttempts = 0;
            m_ReconnectCoroutine = m_ConnectionManager.StartCoroutine(ReconnectCoroutine());
        }

        public override void Exit()
        {
            if (m_ReconnectCoroutine != null)
            {
                m_ConnectionManager.StopCoroutine(m_ReconnectCoroutine);
                m_ReconnectCoroutine = null;
            }
            m_ReconnectMessagePublisher.Publish(new ReconnectMessage(m_ConnectionManager.NbReconnectAttempts, m_ConnectionManager.NbReconnectAttempts));
        }

        public override void OnClientConnected(ulong _)
        {
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnected);
        }

        public override void OnClientDisconnect(ulong _)
        {
            var disconnectReason = m_ConnectionManager.NetworkManager.DisconnectReason;
            if (m_NbAttempts < m_ConnectionManager.NbReconnectAttempts)
            {
                if (string.IsNullOrEmpty(disconnectReason))
                {
                    m_ReconnectCoroutine = m_ConnectionManager.StartCoroutine(ReconnectCoroutine());
                }
                else
                {
                    var connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
                    m_ConnectStatusPublisher.Publish(connectStatus);
                    switch (connectStatus)
                    {
                        case ConnectStatus.UserRequestedDisconnect:
                        case ConnectStatus.HostEndedSession:
                        case ConnectStatus.ServerFull:
                        case ConnectStatus.IncompatibleBuildType:
                            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
                            break;
                        default:
                            m_ReconnectCoroutine = m_ConnectionManager.StartCoroutine(ReconnectCoroutine());
                            break;
                    }
                }
            }
            else
            {
                if (string.IsNullOrEmpty(disconnectReason))
                {
                    m_ConnectStatusPublisher.Publish(ConnectStatus.GenericDisconnect);
                }
                else
                {
                    var connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
                    m_ConnectStatusPublisher.Publish(connectStatus);
                }

                m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
            }
        }

        IEnumerator ReconnectCoroutine()
        {
            // 첫 번째 시도가 아니라면, 다시 시도하기 전에 잠시 기다립니다. 이는 연결 끊김의 원인이 일시적인 경우,
            // 문제가 해결될 시간을 주기 위함입니다. 여기서는 단순한 고정된 쿨다운 시간을 사용하지만, 
            // 각 실패한 시도 사이에 더 긴 시간을 기다리기 위해 지수 백오프(Exponential Backoff)를 사용할 수도 있습니다.
            // 자세한 내용은 https://en.wikipedia.org/wiki/Exponential_backoff 를 참조하세요.
            if (m_NbAttempts > 0)
            {
                yield return new WaitForSeconds(k_TimeBetweenAttempts);
            }

            Debug.Log("Lost connection to host, trying to reconnect...");

            m_ConnectionManager.NetworkManager.Shutdown();

            yield return new WaitWhile(() => m_ConnectionManager.NetworkManager.ShutdownInProgress); // wait until NetworkManager completes shutting down
            Debug.Log($"Reconnecting attempt {m_NbAttempts + 1}/{m_ConnectionManager.NbReconnectAttempts}...");
            m_ReconnectMessagePublisher.Publish(new ReconnectMessage(m_NbAttempts, m_ConnectionManager.NbReconnectAttempts));

            // 첫 번째 시도라면, 재연결을 시도하기 전에 잠시 기다립니다.
            // (예를 들어, 로비에 있을 때 호스트가 예기치 않게 종료된 경우, 이 시간을 주면 로비가 적절히 삭제되어
            // 빈 로비에 다시 연결되지 않도록 할 수 있습니다.)
            if (m_NbAttempts == 0)
            {
                yield return new WaitForSeconds(k_TimeBeforeFirstAttempt);
            }

            m_NbAttempts++;
            var reconnectingSetupTask = m_ConnectionMethod.SetupClientReconnectionAsync();
            yield return new WaitUntil(() => reconnectingSetupTask.IsCompleted);

            if (!reconnectingSetupTask.IsFaulted && reconnectingSetupTask.Result.success)
            {
                // 만약 이 과정이 실패하면, Netcode에 의해 OnClientDisconnect 콜백이 호출됩니다.
                var connectingTask = ConnectClientAsync();
                yield return new WaitUntil(() => connectingTask.IsCompleted);
            }
            else
            {
                if (!reconnectingSetupTask.Result.shouldTryAgain)
                {
                    // 재시도 횟수를 최대값으로 설정하여 더 이상 새로운 시도가 이루어지지 않도록 합니다.
                    m_NbAttempts = m_ConnectionManager.NbReconnectAttempts;
                }
                // OnClientDisconnect를 호출하여 이번 시도가 실패했음을 표시하고, 
                // 새 시도를 시작하거나 포기하고 Offline 상태로 돌아갑니다.
                OnClientDisconnect(0);
            }
        }
    }
}
