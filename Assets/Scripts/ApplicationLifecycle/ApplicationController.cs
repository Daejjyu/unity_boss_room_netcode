using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Infrastructure;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Unity.BossRoom.ApplicationLifecycle
{
  public class ApplicationController : LifetimeScope
  {
    [SerializeField]
    UpdateRunner m_UpdateRunner;
    [SerializeField]
    ConnectionManager m_connectionManager;
  }
}
