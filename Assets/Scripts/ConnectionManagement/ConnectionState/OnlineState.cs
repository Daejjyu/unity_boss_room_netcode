namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Base class representing an online connection state.
    /// </summary>
    abstract class OnlineState : ConnectionState
    {
        public override void OnUserRequestedShutdown()
        {
            // This behaviour will be the same for every online state
            // 이 동작은 모든 온라인 상태에서 동일하게 발생합니다.
            m_ConnectStatusPublisher.Publish(ConnectStatus.UserRequestedDisconnect);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }

        public override void OnTransportFailure()
        {
            // This behaviour will be the same for every online state
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }
    }
}
