using System;
using System.Threading.Tasks;
using Unity.BossRoom.UnityServices.Lobbies;
using Unity.BossRoom.Utils;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Unity.BossRoom.ConnectionManagement
{
  /// <summary>
  /// ConnectionMethod contains all setup needed to setup NGO to be ready to start a connection, either host or client side.
  /// Please override this abstract class to add a new transport or way of connecting.
  /// </summary>
  /// <summary>
  /// ConnectionMethod는 호스트 또는 클라이언트 측에서 연결을 시작할 준비를 하기 위해 NGO를 설정하는 데 필요한 모든 설정을 포함합니다.
  /// 새로운 전송 방식이나 연결 방식을 추가하려면 이 추상 클래스를 재정의하세요.
  /// </summary>
  public abstract class ConnectionMethodBase
  {
    protected ConnectionManager m_ConnectionManager;
    readonly ProfileManager m_ProfileManager;
    protected readonly string m_PlayerName;
    protected const string k_DtlsConnType = "dtls";

    /// <summary>
    /// Setup the host connection prior to starting the NetworkManager
    /// </summary>
    /// <summary>
    /// NetworkManager를 시작하기 전에 호스트 연결을 설정합니다.
    /// </summary>
    /// <returns></returns>
    public abstract Task SetupHostConnectionAsync();

    /// <summary>
    /// Setup the client connection prior to starting the NetworkManager
    /// </summary>
    /// <summary>
    /// NetworkManager를 시작하기 전에 클라이언트 연결을 설정합니다.
    /// </summary>
    /// <returns></returns>
    public abstract Task SetupClientConnectionAsync();

    /// <summary>
    /// Setup the client for reconnection prior to reconnecting
    /// </summary>
    /// <summary>
    /// 다시 연결하기 전에 클라이언트를 재연결할 준비를 합니다.
    /// </summary>
    /// <returns>
    /// success = true if succeeded in setting up reconnection, false if failed.
    /// shouldTryAgain = true if we should try again after failing, false if not.
    /// </returns>
    /// <returns>
    /// success = 재연결 설정에 성공하면 true, 실패하면 false.
    /// shouldTryAgain = 실패 후 다시 시도해야 하면 true, 그렇지 않으면 false.
    /// </returns>
    public abstract Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync();

    public ConnectionMethodBase(ConnectionManager connectionManager, ProfileManager profileManager, string playerName)
    {
      m_ConnectionManager = connectionManager;
      m_ProfileManager = profileManager;
      m_PlayerName = playerName;
    }

    protected void SetConnectionPayload(string playerId, string playerName)
    {
      var payload = JsonUtility.ToJson(new ConnectionPayload()
      {
        playerId = playerId,
        playerName = playerName,
        isDebug = Debug.isDebugBuild
      });

      var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);

      m_ConnectionManager.NetworkManager.NetworkConfig.ConnectionData = payloadBytes;
    }

    /// Using authentication, this makes sure your session is associated with your account and not your device. This means you could reconnect
    /// from a different device for example. A playerId is also a bit more permanent than player prefs. In a browser for example,
    /// player prefs can be cleared as easily as cookies.
    /// The forked flow here is for debug purposes and to make UGS optional in Boss Room. This way you can study the sample without
    /// setting up a UGS account. It's recommended to investigate your own initialization and IsSigned flows to see if you need
    /// those checks on your own and react accordingly. We offer here the option for offline access for debug purposes, but in your own game you
    /// might want to show an error popup and ask your player to connect to the internet.
    /// 인증을 사용하여 세션이 기기와 연결되지 않고 계정과 연결되도록 보장합니다. 예를 들어, 다른 기기에서 다시 연결할 수 있습니다.
    /// playerId는 player prefs보다 더 영구적입니다. 예를 들어 브라우저에서는 player prefs가 쿠키만큼 쉽게 삭제될 수 있습니다.
    /// 여기서 분기된 흐름은 디버그 목적으로 Boss Room에서 UGS를 선택 사항으로 만듭니다. 이렇게 하면 UGS 계정을 설정하지 않고도 샘플을 학습할 수 있습니다.
    /// 자신의 초기화 및 IsSigned 흐름을 조사하여 이러한 확인이 필요한지 확인하고 이에 따라 반응하는 것이 좋습니다.
    /// 여기서는 디버그 목적으로 오프라인 액세스 옵션을 제공합니다. 하지만 자신의 게임에서는 오류 팝업을 표시하고 플레이어에게 인터넷에 연결하도록 요청할 수 있습니다.
    protected string GetPlayerId()
    {
      if (Services.Core.UnityServices.State != ServicesInitializationState.Initialized)
      {
        return ClientPrefs.GetGuid() + m_ProfileManager.Profile;
      }

      return AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : ClientPrefs.GetGuid() + m_ProfileManager.Profile;
    }
  }

  /// <summary>
  /// Simple IP connection setup with UTP
  /// </summary>
  /// <summary>
  /// UTP를 사용한 간단한 IP 연결 설정
  /// </summary>
  class ConnectionMethodIP : ConnectionMethodBase
  {
    string m_Ipaddress;
    ushort m_Port;

    public ConnectionMethodIP(string ip, ushort port, ConnectionManager connectionManager, ProfileManager profileManager, string playerName)
        : base(connectionManager, profileManager, playerName)
    {
      m_Ipaddress = ip;
      m_Port = port;
      m_ConnectionManager = connectionManager;
    }

#pragma warning disable CS1998 // 이 비동기 메서드에는 'await' 연산자가 없으며 메서드가 동시에 실행됩니다.
    public override async Task SetupClientConnectionAsync()
    {
      SetConnectionPayload(GetPlayerId(), m_PlayerName);
      var utp = (UnityTransport)m_ConnectionManager.NetworkManager.NetworkConfig.NetworkTransport;
      utp.SetConnectionData(m_Ipaddress, m_Port);
    }

    public override async Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync()
    {
      // Nothing to do here
      // 여기서 할 일 없음
      return (true, true);
    }

    public override async Task SetupHostConnectionAsync()
    {
      SetConnectionPayload(GetPlayerId(), m_PlayerName); // Need to set connection payload for host as well, as host is a client too
                                                         // 호스트도 클라이언트이므로 호스트에 대한 연결 페이로드도 설정해야 합니다.
      var utp = (UnityTransport)m_ConnectionManager.NetworkManager.NetworkConfig.NetworkTransport;
      utp.SetConnectionData(m_Ipaddress, m_Port);
    }
  }
#pragma warning restore CS1998 // 이 비동기 메서드에는 'await' 연산자가 없으며 메서드가 동시에 실행됩니다.

  /// <summary>
  /// UTP's Relay connection setup using the Lobby integration
  /// </summary>
  /// <summary>
  /// 로비 통합을 사용한 UTP의 릴레이 연결 설정
  /// </summary>
  // class ConnectionMethodRelay : ConnectionMethodBase
  // {
  //   LobbyServiceFacade m_LobbyServiceFacade;
  //   LocalLobby m_LocalLobby;

  //   public ConnectionMethodRelay(LobbyServiceFacade lobbyServiceFacade, LocalLobby localLobby, ConnectionManager connectionManager, ProfileManager profileManager, string playerName)
  //       : base(connectionManager, profileManager, playerName)
  //   {
  //     m_LobbyServiceFacade = lobbyServiceFacade;
  //     m_LocalLobby = localLobby;
  //     m_ConnectionManager = connectionManager;
  //   }

  //   public override async Task SetupClientConnectionAsync()
  //   {
  //     Debug.Log("Setting up Unity Relay client");
  //     // Unity Relay 클라이언트를 설정 중입니다.

  //     SetConnectionPayload(GetPlayerId(), m_PlayerName);

  //     if (m_LobbyServiceFacade.CurrentUnityLobby == null)
  //     {
  //       throw new Exception("Trying to start relay while Lobby isn't set");
  //       // 로비가 설정되지 않은 상태에서 릴레이를 시작하려고 시도 중
  //     }

  //     Debug.Log($"Setting Unity Relay client with join code {m_LocalLobby.RelayJoinCode}");
  //     // 조인 코드 {m_LocalLobby.RelayJoinCode}로 Unity Relay 클라이언트를 설정 중

  //     // Create client joining allocation from join code
  //     // 조인 코드에서 클라이언트 할당 생성
  //     var joinedAllocation = await RelayService.Instance.JoinAllocationAsync(m_LocalLobby.RelayJoinCode);
  //     Debug.Log($"client: {joinedAllocation.ConnectionData[0]} {joinedAllocation.ConnectionData[1]}, " +
  //         $"host: {joinedAllocation.HostConnectionData[0]} {joinedAllocation.HostConnectionData[1]}, " +
  //         $"client: {joinedAllocation.AllocationId}");

  //     await m_LobbyServiceFacade.UpdatePlayerDataAsync(joinedAllocation.AllocationId.ToString(), m_LocalLobby.RelayJoinCode);

  //     // Configure UTP with allocation
  //     // 할당으로 UTP 구성
  //     var utp = (UnityTransport)m_ConnectionManager.NetworkManager.NetworkConfig.NetworkTransport;
  //     utp.SetRelayServerData(new RelayServerData(joinedAllocation, k_DtlsConnType));
  //   }

  //   public override async Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync()
  //   {
  //     if (m_LobbyServiceFacade.CurrentUnityLobby == null)
  //     {
  //       Debug.Log("Lobby does not exist anymore, stopping reconnection attempts.");
  //       // 로비가 더 이상 존재하지 않으므로 재연결 시도를 중단합니다.
  //       return (false, false);
  //     }

  //     // When using Lobby with Relay, if a user is disconnected from the Relay server, the server will notify the
  //     // Lobby service and mark the user as disconnected, but will not remove them from the lobby. They then have
  //     // some time to attempt to reconnect (defined by the "Disconnect removal time" parameter on the dashboard),
  //     // after which they will be removed from the lobby completely.
  //     // See https://docs.unity.com/lobby/reconnect-to-lobby.html
  //     // 릴레이와 함께 로비를 사용할 때 사용자가 릴레이 서버에서 연결이 끊어지면 서버는 로비 서비스에 알리고 사용자를 연결 끊김으로 표시하지만 로비에서 제거하지는 않습니다.
  //     // 그런 다음 다시 연결을 시도할 시간이 주어지며(대시보드의 "Disconnect removal time" 매개변수로 정의됨), 이후에는 로비에서 완전히 제거됩니다.
  //     // https://docs.unity.com/lobby/reconnect-to-lobby.html 참조
  //     var lobby = await m_LobbyServiceFacade.ReconnectToLobbyAsync();
  //     var success = lobby != null;
  //     Debug.Log(success ? "Successfully reconnected to Lobby." : "Failed to reconnect to Lobby.");
  //     // 로비에 성공적으로 다시 연결되었습니다.
  //     // 로비에 다시 연결하지 못했습니다.
  //     return (success, true); // return a success if reconnecting to lobby returns a lobby
  //                             // 로비에 다시 연결하면 로비를 반환하므로 성공을 반환합니다.
  //   }

  //   public override async Task SetupHostConnectionAsync()
  //   {
  //     Debug.Log("Setting up Unity Relay host");
  //     // Unity Relay 호스트를 설정 중입니다.

  //     SetConnectionPayload(GetPlayerId(), m_PlayerName); // Need to set connection payload for host as well, as host is a client too
  //                                                        // 호스트도 클라이언트이므로 호스트에 대한 연결 페이로드도 설정해야 합니다.

  //     // Create relay allocation
  //     // 릴레이 할당 생성
  //     Allocation hostAllocation = await RelayService.Instance.CreateAllocationAsync(m_ConnectionManager.MaxConnectedPlayers, region: null);
  //     var joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);

  //     Debug.Log($"server: connection data: {hostAllocation.ConnectionData[0]} {hostAllocation.ConnectionData[1]}, " +
  //         $"allocation ID:{hostAllocation.AllocationId}, region:{hostAllocation.Region}");
  //     // 서버: 연결 데이터: {hostAllocation.ConnectionData[0]} {hostAllocation.ConnectionData[1]},
  //     // 할당 ID:{hostAllocation.AllocationId}, 지역:{hostAllocation.Region}

  //     m_LocalLobby.RelayJoinCode = joinCode;

  //     // next line enables lobby and relay services integration
  //     // 다음 줄은 로비 및 릴레이 서비스 통합을 활성화합니다.
  //     await m_LobbyServiceFacade.UpdateLobbyDataAndUnlockAsync();
  //     await m_LobbyServiceFacade.UpdatePlayerDataAsync(hostAllocation.AllocationIdBytes.ToString(), joinCode);

  //     // Setup UTP with relay connection info
  //     // 릴레이 연결 정보를 사용하여 UTP 설정
  //     var utp = (UnityTransport)m_ConnectionManager.NetworkManager.NetworkConfig.NetworkTransport;
  //     utp.SetRelayServerData(new RelayServerData(hostAllocation, k_DtlsConnType)); // This is with DTLS enabled for a secure connection
  //                                                                                  // 이는 안전한 연결을 위해 DTLS가 활성화된 상태입니다.

  //     Debug.Log($"Created relay allocation with join code {m_LocalLobby.RelayJoinCode}");
  //     // 조인 코드 {m_LocalLobby.RelayJoinCode}로 릴레이 할당이 생성되었습니다.
  //   }
  // }
}
