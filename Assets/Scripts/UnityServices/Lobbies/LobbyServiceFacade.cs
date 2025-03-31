// using System;
// using Unity.BossRoom.Infrastructure;
// using VContainer;
// using VContainer.Unity;

// namespace Unity.BossRoom.UnityServices.Lobbies
// {
//   public class LobbyServiceFacade : IDisposable, IStartable
//   {
//     [Inject] LifetimeScope m_ParentScope;
//     [Inject] UpdateRunner m_UpdateRunner;
//     [Inject] LocalLobby m_LocalLobby;
//     [Inject] LocalLobbyUser m_LocalLobbyUser;
//     [Inject] IPublisher<UnityServiceErrorMessage> m_UnityServiceErrorMessagePub;
//     [Inject] IPublisher<LobbyListFetchedMessage> m_LobbyListFetchedMessagePub;

//   }

// }