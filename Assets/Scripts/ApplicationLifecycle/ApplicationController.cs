// Unity 라이브러리 문법 및 사용된 문법
// LifetimeScope: VContainer 라이프타임 범위 관리
// IContainerBuilder: VContainer의 의존성 주입 컨테이너 빌더
// Singleton: VContainer에서 오브젝트를 싱글톤으로 등록
// RegisterComponent: Unity에서 컴포넌트를 의존성 주입 컨테이너에 등록
// RegisterInstance: 특정 인스턴스를 컨테이너에 등록
// IDisposable: 메모리 해제를 위한 인터페이스
// Subscribe: 메시지 구독
// Coroutine: Unity의 비동기 처리

// 이 코드는 Unity 게임 애플리케이션의 핵심 기능을 관리하는 컨트롤러로,
// DI(Dependency Injection)을 통해 필수적인 컴포넌트들과 서비스들을 설정하고 관리합니다. 
// 게임 상태, 네트워크 연결 및 로비 서비스 등 주요 서비스들을 관리하고, 종료 시 적절히 처리합니다.

using System;
using System.Collections;
using Unity.BossRoom.ApplicationLifecycle.Messages;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameState;
using Unity.BossRoom.Gameplay.Messages;
using Unity.BossRoom.Infrastructure;
using Unity.BossRoom.UnityServices;
using Unity.BossRoom.UnityServices.Auth;
using Unity.BossRoom.UnityServices.Lobbies;
using Unity.BossRoom.Utils;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Unity.BossRoom.ApplicationLifecycle
{
    /// <summary>
    /// An entry point to the application, where we bind all the common dependencies to the root DI scope.
    /// 애플리케이션의 진입점으로, 공통 의존성을 루트 DI 범위에 바인딩합니다.
    /// </summary>
    public class ApplicationController : LifetimeScope
    {
        [SerializeField]
        UpdateRunner m_UpdateRunner;
        [SerializeField]
        ConnectionManager m_ConnectionManager;
        [SerializeField]
        NetworkManager m_NetworkManager;

        LocalLobby m_LocalLobby;
        LobbyServiceFacade m_LobbyServiceFacade;

        IDisposable m_Subscriptions;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(m_UpdateRunner);
            builder.RegisterComponent(m_ConnectionManager);
            builder.RegisterComponent(m_NetworkManager);

            //the following singletons represent the local representations of the lobby that we're in and the user that we are
            //they can persist longer than the lifetime of the UI in MainMenu where we set up the lobby that we create or join
            //다음 싱글톤들은 우리가 있는 로비와 사용자의 로컬 표현을 나타냅니다.
            //이들은 우리가 로비를 설정하거나 참가하는 MainMenu UI의 생애 주기보다 오래 지속될 수 있습니다.
            builder.Register<LocalLobbyUser>(Lifetime.Singleton);
            builder.Register<LocalLobby>(Lifetime.Singleton);

            builder.Register<ProfileManager>(Lifetime.Singleton);

            builder.Register<PersistentGameState>(Lifetime.Singleton);

            //these message channels are essential and persist for the lifetime of the lobby and relay services
            // Registering as instance to prevent code stripping on iOS
            //이 메시지 채널들은 로비와 릴레이 서비스의 생애 주기 동안 지속되며 필수적입니다.
            //iOS에서 코드 스트리핑을 방지하기 위해 인스턴스로 등록됩니다.
            builder.RegisterInstance(new MessageChannel<QuitApplicationMessage>()).AsImplementedInterfaces();
            builder.RegisterInstance(new MessageChannel<UnityServiceErrorMessage>()).AsImplementedInterfaces();
            builder.RegisterInstance(new MessageChannel<ConnectStatus>()).AsImplementedInterfaces();
            builder.RegisterInstance(new MessageChannel<DoorStateChangedEventMessage>()).AsImplementedInterfaces();

            //these message channels are essential and persist for the lifetime of the lobby and relay services
            //they are networked so that the clients can subscribe to those messages that are published by the server
            //이 메시지 채널들은 로비와 릴레이 서비스의 생애 주기 동안 지속되며 필수적입니다.
            //이들은 네트워크화되어 클라이언트들이 서버에서 발행하는 메시지에 구독할 수 있도록 합니다.
            builder.RegisterComponent(new NetworkedMessageChannel<LifeStateChangedEventMessage>()).AsImplementedInterfaces();
            builder.RegisterComponent(new NetworkedMessageChannel<ConnectionEventMessage>()).AsImplementedInterfaces();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterComponent(new NetworkedMessageChannel<CheatUsedMessage>()).AsImplementedInterfaces();
#endif

            //this message channel is essential and persists for the lifetime of the lobby and relay services
            //이 메시지 채널은 로비와 릴레이 서비스의 생애 주기 동안 지속되며 필수적입니다.
            builder.RegisterInstance(new MessageChannel<ReconnectMessage>()).AsImplementedInterfaces();

            //buffered message channels hold the latest received message in buffer and pass to any new subscribers
            //버퍼링된 메시지 채널은 최신 수신 메시지를 버퍼에 저장하고 새 구독자에게 전달합니다.
            builder.RegisterInstance(new BufferedMessageChannel<LobbyListFetchedMessage>()).AsImplementedInterfaces();

            //all the lobby service stuff, bound here so that it persists through scene loads
            //모든 로비 서비스 관련 기능을 이곳에 바인딩하여 씬 전환 시에도 지속될 수 있도록 합니다.
            builder.Register<AuthenticationServiceFacade>(Lifetime.Singleton); //a manager entity that allows us to do anonymous authentication with unity services
            builder.RegisterEntryPoint<LobbyServiceFacade>(Lifetime.Singleton).AsSelf();
        }

        private void Start()
        {
            m_LocalLobby = Container.Resolve<LocalLobby>();
            m_LobbyServiceFacade = Container.Resolve<LobbyServiceFacade>();

            var quitApplicationSub = Container.Resolve<ISubscriber<QuitApplicationMessage>>();

            var subHandles = new DisposableGroup();
            subHandles.Add(quitApplicationSub.Subscribe(QuitGame));
            m_Subscriptions = subHandles;

            Application.wantsToQuit += OnWantToQuit;
            DontDestroyOnLoad(gameObject);
            DontDestroyOnLoad(m_UpdateRunner.gameObject);
            Application.targetFrameRate = 120;
            SceneManager.LoadScene("MainMenu");

            // 애플리케이션이 시작될 때, 로컬 로비와 로비 서비스 파사드를 해결하고, 종료 메시지를 구독합니다.
        }

        protected override void OnDestroy()
        {
            if (m_Subscriptions != null)
            {
                m_Subscriptions.Dispose();
            }

            if (m_LobbyServiceFacade != null)
            {
                m_LobbyServiceFacade.EndTracking();
            }

            base.OnDestroy();

            // 자원이 해제되기 전에 구독을 해제하고, 로비 서비스 파사드를 종료합니다.
        }

        /// <summary>
        ///     In builds, if we are in a lobby and try to send a Leave request on application quit, it won't go through if we're quitting on the same frame.
        ///     So, we need to delay just briefly to let the request happen (though we don't need to wait for the result).
        ///     빌드에서, 로비에 있을 때 애플리케이션 종료 시 Leave 요청을 보내면, 같은 프레임에서 종료하면 요청이 처리되지 않습니다.
        ///     따라서 요청을 처리할 수 있도록 잠시 지연시켜야 합니다 (결과를 기다릴 필요는 없습니다).
        /// </summary>
        private IEnumerator LeaveBeforeQuit()
        {
            // We want to quit anyways, so if anything happens while trying to leave the Lobby, log the exception then carry on
            // 우리는 어차피 종료하고 싶으므로, 로비를 떠나려고 시도할 때 문제가 발생하면 예외를 기록하고 계속 진행합니다.
            try
            {
                m_LobbyServiceFacade.EndTracking();
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

            yield return null;
            Application.Quit();
        }

        private bool OnWantToQuit()
        {
            Application.wantsToQuit -= OnWantToQuit;

            var canQuit = m_LocalLobby != null && string.IsNullOrEmpty(m_LocalLobby.LobbyID);
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
