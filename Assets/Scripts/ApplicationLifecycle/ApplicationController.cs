using System;
using System.Collections;
using Unity.BossRoom.ApplicationLifecycle.Messages;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameState;
using Unity.BossRoom.Gameplay.Messages;
using Unity.BossRoom.Infrastructure;
using Unity.BossRoom.UnityServices.Lobbies;
using Unity.BossRoom.Utils;
using Unity.Netcode;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField]
    NetworkManager m_NetworkManager;

    // LocalLobby m_LocalLobby;
    // LobbyServiceFacade m_LobbyServiceFacade;

    IDisposable m_Subscriptions;

    protected override void Configure(IContainerBuilder builder)
    {
      base.Configure(builder);
      builder.RegisterComponent(m_UpdateRunner);
      builder.RegisterComponent(m_connectionManager);
      builder.RegisterComponent(m_NetworkManager);

      // builder.Register<LocalLobbyUser>(Lifetime.Singleton);
      // builder.Register<LocalLobby>(Lifetime.Singleton);

      builder.Register<ProfileManager>(Lifetime.Singleton);
      builder.Register<PersistentGameState>(Lifetime.Singleton);

      builder.RegisterInstance(new MessageChannel<QuitApplicationMessage>()).AsImplementedInterfaces();
      // builder.RegisterInstance(new MessageChannel<UnityServiceErrorMessage>()).AsImplementedInterfaces();
      builder.RegisterInstance(new MessageChannel<ConnectStatus>()).AsImplementedInterfaces();
      builder.RegisterInstance(new MessageChannel<DoorStateChangedEventMessage>()).AsImplementedInterfaces();

      builder.RegisterComponent(new NetworkedMessageChannel<LifeStateChangedEventMessage>()).AsImplementedInterfaces();
      builder.RegisterComponent(new NetworkedMessageChannel<ConnectionEventMessage>()).AsImplementedInterfaces();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
      builder.RegisterComponent(new NetworkedMessageChannel<CheatUsedMessage>()).AsImplementedInterfaces();
#endif

      builder.RegisterInstance(new MessageChannel<ReconnectMessage>()).AsImplementedInterfaces();

      // builder.RegisterInstance(new BufferedMessageChannel<LobbyListFetchedMessage>()).AsImplementedInterfaces();
      // builder.Register<AuthenticationServiceFacade>(Lifetime.Singleton); //a manager entity that allows us to do anonymous authentication with unity services
      // builder.RegisterEntryPoint<LobbyServiceFacade>(Lifetime.Singleton).AsSelf();
    }

    private void Start()
    {
      // m_LocalLobby = Container.Resolve<LocalLobby>();
      // m_LobbyServiceFacade = Container.Resolve<LobbyServiceFacade>();

      var quitApplicationSub = Container.Resolve<ISubscriber<QuitApplicationMessage>>();
      var subHandles = new DisposableGroup();
      subHandles.Add(quitApplicationSub.Subscribe(QuitGame));
      m_Subscriptions = subHandles;

      Application.wantsToQuit += OnWantToQuit;
      DontDestroyOnLoad(gameObject);
      DontDestroyOnLoad(m_UpdateRunner.gameObject);
      Application.targetFrameRate = 120;
      // SceneManager.LoadScene("MainMenu");
    }

    protected override void OnDestroy()
    {
      if (m_Subscriptions != null)
      {
        m_Subscriptions.Dispose();
      }

      // if (m_LobbyServiceFacade != null)
      // {
      //   m_LobbyServiceFacade.EndTracking();
      // }

      base.OnDestroy();
    }

    private IEnumerator LeaveBeforeQuit()
    {
      // try
      // {
      //   m_LobbyServiceFacade.EndTracking();
      // }
      // catch (Exception e)
      // {
      //   Debug.LogError(e.Message);
      // }

      yield return null;
      Application.Quit();
    }

    private bool OnWantToQuit()
    {
      Application.wantsToQuit -= OnWantToQuit;

      // var canQuit = m_LocalLobby != null && string.IsNullOrEmpty(m_LocalLobby.LobbyID);
      var canQuit = true;
      if (!canQuit)
      {
        StartCoroutine(LeaveBeforeQuit());
      }

      return canQuit;
    }

    private void QuitGame(QuitApplicationMessage msg)
    {
#if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
    }
  }
}