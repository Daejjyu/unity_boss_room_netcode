using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Utils;
using Unity.Multiplayer.Samples.Utilities;
using UnityEngine.SceneManagement;
using VContainer;

namespace UUnity.BossRoom.ConnectionManagement
{
  class OfflineState : ConnectionState
  {
    // [Inject]
    // LobbyServiceFacade m_LobbyServiceFacade;
    [Inject]
    ProfileManager m_ProfileManager;
    // [Inject]
    // LocalLobby m_LocalLobby;

    const string k_MainMenuSceneName = "MainMenu";


    public override void Enter()
    {
      // m_LobbyServiceFacade.EndTracking();
      m_ConnectionManager.NetworkManager.Shutdown();
      if (SceneManager.GetActiveScene().name != k_MainMenuSceneName)
      {
        SceneLoaderWrapper.Instance.LoadScene(k_MainMenuSceneName, useNetworkSceneManager: false);
      }
    }

    public override void Exit() { }

    public override void StartClientIP(string playerName, string ipaddress, int port)
    {
      var connectionMethod = new ConnectionMethodIP(ipaddress, (ushort)port, m_ConnectionManager, m_ProfileManager, playerName);
      m_ConnectionManager.m_ClientReconnecting.Configure(connectionMethod);
      m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnecting.Configure(connectionMethod));
    }

    // public override void StartClientLobby(string playerName)
    // {
    //   var connectionMethod = new ConnectionMethodRelay(m_LobbyServiceFacade, m_LocalLobby, m_ConnectionManager, m_ProfileManager, playerName);
    //   m_ConnectionManager.m_ClientReconnecting.Configure(connectionMethod);
    //   m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnecting.Configure(connectionMethod));
    // }

    public override void StartHostIP(string playerName, string ipaddress, int port)
    {
      var connectionMethod = new ConnectionMethodIP(ipaddress, (ushort)port, m_ConnectionManager, m_ProfileManager, playerName);
      m_ConnectionManager.ChangeState(m_ConnectionManager.m_StartingHost.Configure(connectionMethod));
    }

    // public override void StartHostLobby(string playerName)
    // {
    //   var connectionMethod = new ConnectionMethodRelay(m_LobbyServiceFacade, m_LocalLobby, m_ConnectionManager, m_ProfileManager, playerName);
    //   m_ConnectionManager.ChangeState(m_ConnectionManager.m_StartingHost.Configure(connectionMethod));
    // }

  }
}