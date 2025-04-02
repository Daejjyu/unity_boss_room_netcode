using System;
using System.Collections.Generic;
using System.Data;
using Unity.BossRoom.Utils;
using Unity.Netcode;
using UnityEngine;
using UUnity.BossRoom.ConnectionManagement;
using VContainer;

namespace Unity.BossRoom.ConnectionManagement
{

  public enum ConnectStatus
  {
    Undefined,
    Success,
    ServerFull,
    LoggedInAgain,
    UserRequestedDisconnect,
    GenericDisconnect,
    Reconnecting,
    IncompatibleBuildType,
    HostEndedSession,
    StartHostFailed,
    StartClientFailed
  }

  public struct ReconnectMessage
  {
    public int CurrentAttempts;
    public int MaxAttempt;

    public ReconnectMessage(int currentAttempts, int maxAttempts)
    {
      CurrentAttempts = currentAttempts;
      MaxAttempt = maxAttempts;
    }
  }

  public struct ConnectionEventMessage : INetworkSerializeByMemcpy
  {
    public ConnectStatus ConnectStatus;
    public FixedPlayerName PlayerName;
  }

  [Serializable]
  public class ConnectionPayload
  {
    public string playerId;
    public string playerName;
    public bool isDebug;
  }


  public class ConnectionManager : MonoBehaviour
  {
    ConnectionState m_CurrentState;

    [Inject]
    NetworkManager m_NetworkManager;
    //public으로 getter만 제공
    public NetworkManager NetworkManager => m_NetworkManager;

    [SerializeField]
    int m_NbReconnectAttempts = 2; // Nb = Number

    public int NbReconnectAttempts => m_NbReconnectAttempts;

    [Inject]
    IObjectResolver m_Resolver;

    public int MaxConnectedPlayers = 8;

    internal readonly OfflineState m_Offline = new OfflineState();
    internal readonly ClientConnectingState m_ClientConnecting = new ClientConnectingState();
    internal readonly ClientConnectedState m_ClientConnected = new ClientConnectedState();
    internal readonly ClientReconnectingState m_ClientReconnecting = new ClientReconnectingState();
    internal readonly StartingHostState m_StartingHost = new StartingHostState();
    internal readonly HostingState m_Hosting = new HostingState();

    void Awake()
    {
      DontDestroyOnLoad(gameObject);
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

    // // 클라이언트 로비 시작
    // public void StartClientLobby(string playerName)
    // {
    //   m_CurrentState.StartClientLobby(playerName);  // 클라이언트 로비 시작
    // }

    // IP로 클라이언트 시작
    public void StartClientIp(string playerName, string ipaddress, int port)
    {
      m_CurrentState.StartClientIP(playerName, ipaddress, port);  // IP로 클라이언트 시작
    }

    // // 호스트 로비 시작
    // public void StartHostLobby(string playerName)
    // {
    //   m_CurrentState.StartHostLobby(playerName);  // 호스트 로비 시작
    // }

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
