using Unity.BossRoom.Infrastructure;
using Unity.Netcode;
using VContainer;

namespace Unity.BossRoom.ConnectionManagement
{
  abstract class ConnectionState
  {
    [Inject]
    protected ConnectionManager m_ConnectionManager;

    [Inject]
    protected IPublisher<ConnectStatus> m_ConnectStatusPublisher;

    public abstract void Enter();

    public abstract void Exit();

    public virtual void OnClientConnected(ulong clientID) { }
    public virtual void OnClientDisconnect(ulong clientID) { }

    public virtual void OnServerStarted() { }

    public virtual void StartClientIP(string playerName, string ipaddress, int port) { }
    // public virtual void StartClientLobby(string playerName) { }

    public virtual void StartHostIP(string playerName, string ipaddress, int port) { }
    // public virtual void StartHostLobby(string playerName) { }

    public virtual void OnUserRequestedShutdown() { }
    public virtual void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response) { }
    public virtual void OnTransportFailure() { }
    public virtual void OnServerStopped() { }
  }

}