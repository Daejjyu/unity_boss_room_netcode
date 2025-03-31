namespace Unity.BossRoom.ConnectionManagement
{
  abstract class OnlineState : ConnectionState
  {
    public override void OnUserRequestedShutdown()
    {
      m_ConnectStatusPublisher.Publish(ConnectStatus.UserRequestedDisconnect);
      m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
    }

    public override void OnTransportFailure()
    {
      m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
    }
  }
}