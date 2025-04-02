using System;
using System.Collections.Generic;
using Unity.BossRoom.Utils;
using Unity.Netcode;
using UnityEngine;
using UUnity.BossRoom.ConnectionManagement;
using VContainer;

namespace Unity.BossRoom.ConnectionManagement
{
    // 연결 상태를 나타내는 열거형
    public enum ConnectStatus
    {
        Undefined,                // 상태 정의되지 않음
        Success,                  // 클라이언트가 성공적으로 연결됨. 재연결도 포함됨.
        ServerFull,               // 서버가 이미 최대 용량을 초과하여 접속 불가
        LoggedInAgain,            // 다른 클라이언트에서 로그인하여 해당 클라이언트는 연결 끊김
        UserRequestedDisconnect,  // 사용자가 의도적으로 연결을 끊음
        GenericDisconnect,        // 서버가 연결을 끊었으나 구체적인 사유는 없음
        Reconnecting,             // 클라이언트가 연결이 끊어져 재연결 시도 중
        IncompatibleBuildType,    // 클라이언트 빌드 타입이 서버와 호환되지 않음
        HostEndedSession,         // 호스트가 의도적으로 세션 종료
        StartHostFailed,          // 서버가 바인딩 실패
        StartClientFailed         // 서버에 연결 실패 또는 잘못된 네트워크 엔드포인트
    }

    // 재연결 메시지를 나타내는 구조체
    public struct ReconnectMessage
    {
        public int CurrentAttempt;  // 현재 시도 횟수
        public int MaxAttempt;      // 최대 시도 횟수

        public ReconnectMessage(int currentAttempt, int maxAttempt)
        {
            CurrentAttempt = currentAttempt;
            MaxAttempt = maxAttempt;
        }
    }

    // 네트워크 직렬화를 위한 연결 이벤트 메시지 구조체
    public struct ConnectionEventMessage : INetworkSerializeByMemcpy
    {
        public ConnectStatus ConnectStatus;  // 연결 상태
        public FixedPlayerName PlayerName;   // 플레이어 이름
    }

    // 연결 페이로드를 나타내는 클래스
    [Serializable]
    public class ConnectionPayload
    {
        public string playerId;   // 플레이어 ID
        public string playerName; // 플레이어 이름
        public bool isDebug;      // 디버그 모드 여부
    }

    /// <summary>
    /// 이 상태 기계는 NetworkManager를 통해 연결을 처리합니다. NetworkManager의 콜백 및 외부 호출을 
    /// 수신하고 현재 ConnectionState 객체로 리다이렉트하는 역할을 합니다.
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        ConnectionState m_CurrentState;  // 현재 연결 상태

        [Inject]
        NetworkManager m_NetworkManager;  // 네트워크 매니저
        public NetworkManager NetworkManager => m_NetworkManager;

        [SerializeField]
        int m_NbReconnectAttempts = 2;  // 재연결 시도 횟수

        public int NbReconnectAttempts => m_NbReconnectAttempts;

        [Inject]
        IObjectResolver m_Resolver;  // 객체 해석기

        public int MaxConnectedPlayers = 8;  // 최대 연결 가능한 플레이어 수

        // 각 상태 객체
        internal readonly OfflineState m_Offline = new OfflineState();
        internal readonly ClientConnectingState m_ClientConnecting = new ClientConnectingState();
        internal readonly ClientConnectedState m_ClientConnected = new ClientConnectedState();
        internal readonly ClientReconnectingState m_ClientReconnecting = new ClientReconnectingState();
        internal readonly StartingHostState m_StartingHost = new StartingHostState();
        internal readonly HostingState m_Hosting = new HostingState();

        void Awake()
        {
            DontDestroyOnLoad(gameObject);  // 씬 로드 간에도 객체가 삭제되지 않도록 설정
        }

        void Start()
        {
            List<ConnectionState> states = new() { m_Offline, m_ClientConnecting, m_ClientConnected, m_ClientReconnecting, m_StartingHost, m_Hosting };
            foreach (var connectionState in states)
            {
                // 이 방식은 특정 객체의 생명 주기를 수동으로 관리하면서도, 의존성 주입의 편리함을 유지하는 전략
                // VContainer로 Register vs Inject를 사용하여 의존성 주입을 수행
                m_Resolver.Inject(connectionState);  // 각 상태 객체에 의존성 주입
            }

            m_CurrentState = m_Offline;  // 초기 상태 설정

            // 네트워크 이벤트 콜백 설정
            NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
            NetworkManager.OnServerStarted += OnServerStarted;
            NetworkManager.ConnectionApprovalCallback += ApprovalCheck;
            NetworkManager.OnTransportFailure += OnTransportFailure;
            NetworkManager.OnServerStopped += OnServerStopped;
        }

        void OnDestroy()
        {
            // 네트워크 이벤트 콜백 해제
            NetworkManager.OnClientConnectedCallback -= OnClientConnectedCallback;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
            NetworkManager.OnServerStarted -= OnServerStarted;
            NetworkManager.ConnectionApprovalCallback -= ApprovalCheck;
            NetworkManager.OnTransportFailure -= OnTransportFailure;
            NetworkManager.OnServerStopped -= OnServerStopped;
        }

        // 연결 상태를 변경하는 메서드
        internal void ChangeState(ConnectionState nextState)
        {
            Debug.Log($"{name}: Changed connection state from {m_CurrentState.GetType().Name} to {nextState.GetType().Name}.");

            if (m_CurrentState != null)
            {
                m_CurrentState.Exit();  // 기존 상태 종료
            }
            m_CurrentState = nextState;  // 새 상태로 변경
            m_CurrentState.Enter();  // 새 상태 시작
        }

        void OnClientDisconnectCallback(ulong clientId)
        {
            m_CurrentState.OnClientDisconnect(clientId);  // 클라이언트 연결 해제 처리
        }

        void OnClientConnectedCallback(ulong clientId)
        {
            m_CurrentState.OnClientConnected(clientId);  // 클라이언트 연결 처리
        }

        void OnServerStarted()
        {
            m_CurrentState.OnServerStarted();  // 서버 시작 처리
        }

        // 연결 승인 검사
        void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            m_CurrentState.ApprovalCheck(request, response);
        }

        void OnTransportFailure()
        {
            m_CurrentState.OnTransportFailure();  // 전송 실패 처리
        }

        // 서버가 중지되었을 때의 처리
        void OnServerStopped(bool _)
        {
            m_CurrentState.OnServerStopped();  // 서버 중지 처리
        }

        // 클라이언트 로비 시작
        public void StartClientLobby(string playerName)
        {
            m_CurrentState.StartClientLobby(playerName);  // 클라이언트 로비 시작
        }

        // IP로 클라이언트 시작
        public void StartClientIp(string playerName, string ipaddress, int port)
        {
            m_CurrentState.StartClientIP(playerName, ipaddress, port);  // IP로 클라이언트 시작
        }

        // 호스트 로비 시작
        public void StartHostLobby(string playerName)
        {
            m_CurrentState.StartHostLobby(playerName);  // 호스트 로비 시작
        }

        // IP로 호스트 시작
        public void StartHostIp(string playerName, string ipaddress, int port)
        {
            m_CurrentState.StartHostIP(playerName, ipaddress, port);  // IP로 호스트 시작
        }

        // 사용자 요청에 의한 종료
        public void RequestShutdown()
        {
            m_CurrentState.OnUserRequestedShutdown();  // 사용자 요청에 의한 종료 처리
        }
    }
}
