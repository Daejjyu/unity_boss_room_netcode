
/// <summary>
/// An abstraction layer between the direct calls into the Lobby API and the outcomes you actually want.
/// </summary>
/// <summary>
/// Lobby API의 직접 호출과 실제로 원하는 결과 사이의 추상화 계층입니다.
/// </summary>
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.BossRoom.Infrastructure;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Wire.Internal;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Unity.BossRoom.UnityServices.Lobbies
{
    /// <summary>
    /// Manages the interactions with the Unity Lobby service, including creating, joining, tracking, and leaving lobbies.
    /// </summary>
    /// <summary>
    /// Unity Lobby 서비스와의 상호작용을 관리하며, 프로필 생성, 참여, 추적 및 퇴장 기능을 포함합니다.
    /// </summary>
    public class LobbyServiceFacade : IDisposable, IStartable
    {
        [Inject] LifetimeScope m_ParentScope;
        [Inject] UpdateRunner m_UpdateRunner;
        [Inject] LocalLobby m_LocalLobby;
        [Inject] LocalLobbyUser m_LocalUser;
        [Inject] IPublisher<UnityServiceErrorMessage> m_UnityServiceErrorMessagePub;
        [Inject] IPublisher<LobbyListFetchedMessage> m_LobbyListFetchedPub;

        const float k_HeartbeatPeriod = 8; // The heartbeat must be rate-limited to 5 calls per 30 seconds. We'll aim for longer in case periods don't align.
        /// <summary>
        /// 하트비트는 30초당 5회의 호출로 제한되어야 합니다. 주기가 맞지 않으면 더 긴 주기를 사용할 예정입니다.
        /// </summary>
        float m_HeartbeatTime = 0;

        LifetimeScope m_ServiceScope;
        LobbyAPIInterface m_LobbyApiInterface;

        RateLimitCooldown m_RateLimitQuery;
        RateLimitCooldown m_RateLimitJoin;
        RateLimitCooldown m_RateLimitQuickJoin;
        RateLimitCooldown m_RateLimitHost;

        public Lobby CurrentUnityLobby { get; private set; }

        ILobbyEvents m_LobbyEvents;

        bool m_IsTracking = false;

        LobbyEventConnectionState m_LobbyEventConnectionState = LobbyEventConnectionState.Unknown;

        public void Start()
        {
            m_ServiceScope = m_ParentScope.CreateChild(builder =>
            {
                builder.Register<LobbyAPIInterface>(Lifetime.Singleton);
            });

            m_LobbyApiInterface = m_ServiceScope.Container.Resolve<LobbyAPIInterface>();

            //See https://docs.unity.com/lobby/rate-limits.html
            m_RateLimitQuery = new RateLimitCooldown(1f);
            m_RateLimitJoin = new RateLimitCooldown(3f);
            m_RateLimitQuickJoin = new RateLimitCooldown(10f);
            m_RateLimitHost = new RateLimitCooldown(3f);
        }

        public void Dispose()
        {
            EndTracking();
            if (m_ServiceScope != null)
            {
                m_ServiceScope.Dispose();
            }
        }

        /// <summary>
        /// Sets the current remote lobby and applies remote data to the local lobby.
        /// </summary>
        /// <summary>
        /// 현재 원격 로비를 설정하고 원격 데이터를 로컬 로비에 적용합니다.
        /// </summary>
        public void SetRemoteLobby(Lobby lobby)
        {
            CurrentUnityLobby = lobby;
            m_LocalLobby.ApplyRemoteData(lobby);
        }

        /// <summary>
        /// Initiates tracking of joined lobby's events. The host also starts sending heartbeat pings here.
        /// </summary>
        /// <summary>
        /// 참여한 로비의 이벤트 추적을 시작합니다. 호스트는 이곳에서 하트비트 핑을 보내기 시작합니다.
        /// </summary>
        public void BeginTracking()
        {
            if (!m_IsTracking)
            {
                m_IsTracking = true;
                SubscribeToJoinedLobbyAsync();

                // Only the host sends heartbeat pings to the service to keep the lobby alive
                if (m_LocalUser.IsHost)
                {
                    m_HeartbeatTime = 0;
                    m_UpdateRunner.Subscribe(DoLobbyHeartbeat, 1.5f);
                }
            }
        }

        /// <summary>
        /// Ends tracking of joined lobby's events and leaves or deletes the lobby. The host also stops sending heartbeat pings here.
        /// </summary>
        /// <summary>
        /// 참여한 로비의 이벤트 추적을 종료하고 로비를 나가거나 삭제합니다. 호스트는 이곳에서 하트비트 핑을 중지합니다.
        /// </summary>
        public void EndTracking()
        {
            if (m_IsTracking)
            {
                m_IsTracking = false;
                UnsubscribeToJoinedLobbyAsync();

                // Only the host sends heartbeat pings to the service to keep the lobby alive
                if (m_LocalUser.IsHost)
                {
                    m_UpdateRunner.Unsubscribe(DoLobbyHeartbeat);
                }
            }

            if (CurrentUnityLobby != null)
            {
                if (m_LocalUser.IsHost)
                {
                    DeleteLobbyAsync();
                }
                else
                {
                    LeaveLobbyAsync();
                }
            }
        }

        /// <summary>
        /// Attempt to create a new lobby and then join it.
        /// </summary>
        /// <summary>
        /// 새 로비를 생성하고 이를 참가하려고 시도합니다.
        /// </summary>
        public async Task<(bool Success, Lobby Lobby)> TryCreateLobbyAsync(string lobbyName, int maxPlayers, bool isPrivate)
        {
            if (!m_RateLimitHost.CanCall)
            {
                Debug.LogWarning("Create Lobby hit the rate limit.");
                return (false, null);
            }

            try
            {
                var lobby = await m_LobbyApiInterface.CreateLobby(AuthenticationService.Instance.PlayerId, lobbyName, maxPlayers, isPrivate, m_LocalUser.GetDataForUnityServices(), null);
                return (true, lobby);
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    m_RateLimitHost.PutOnCooldown();
                }
                else
                {
                    PublishError(e);
                }
            }

            return (false, null);
        }

        /// <summary>
        /// Attempt to join an existing lobby. Will try to join via code, if code is null - will try to join via ID.
        /// </summary>
        /// <summary>
        /// 기존 로비에 참여하려고 시도합니다. 코드가 null이면 ID로 참가를 시도합니다.
        /// </summary>
        public async Task<(bool Success, Lobby Lobby)> TryJoinLobbyAsync(string lobbyId, string lobbyCode)
        {
            if (!m_RateLimitJoin.CanCall ||
                (lobbyId == null && lobbyCode == null))
            {
                Debug.LogWarning("Join Lobby hit the rate limit.");
                return (false, null);
            }

            try
            {
                if (!string.IsNullOrEmpty(lobbyCode))
                {
                    var lobby = await m_LobbyApiInterface.JoinLobbyByCode(AuthenticationService.Instance.PlayerId, lobbyCode, m_LocalUser.GetDataForUnityServices());
                    return (true, lobby);
                }
                else
                {
                    var lobby = await m_LobbyApiInterface.JoinLobbyById(AuthenticationService.Instance.PlayerId, lobbyId, m_LocalUser.GetDataForUnityServices());
                    return (true, lobby);
                }
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    m_RateLimitJoin.PutOnCooldown();
                }
                else
                {
                    PublishError(e);
                }
            }

            return (false, null);
        }

        /// <summary>
        /// Attempt to join the first lobby among the available lobbies that match the filtered onlineMode.
        /// </summary>
        /// <summary>
        /// 필터링된 onlineMode와 일치하는 사용 가능한 로비 중 첫 번째 로비에 참가하려고 시도합니다.
        /// </summary>
        public async Task<(bool Success, Lobby Lobby)> TryQuickJoinLobbyAsync()
        {
            if (!m_RateLimitQuickJoin.CanCall)
            {
                Debug.LogWarning("Quick Join Lobby hit the rate limit.");
                return (false, null);
            }

            try
            {
                var lobby = await m_LobbyApiInterface.QuickJoinLobby(AuthenticationService.Instance.PlayerId, m_LocalUser.GetDataForUnityServices());
                return (true, lobby);
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    m_RateLimitQuickJoin.PutOnCooldown();
                }
                else
                {
                    PublishError(e);
                }
            }

            return (false, null);
        }

        /// <summary>
        /// Resets the current lobby and user state, preparing for a new session or disconnection.
        /// </summary>
        /// <summary>
        /// 현재 로비와 사용자 상태를 초기화하여 새로운 세션 또는 연결 해제를 준비합니다.
        /// </summary>
        void ResetLobby()
        {
            CurrentUnityLobby = null;
            if (m_LocalUser != null)
            {
                m_LocalUser.ResetState();
            }
            if (m_LocalLobby != null)
            {
                m_LocalLobby.Reset(m_LocalUser);
            }

            // no need to disconnect Netcode, it should already be handled by Netcode's callback to disconnect
        }

        /// <summary>
        /// Handles the changes to the lobby, such as deletion or updates.
        /// </summary>
        /// <summary>
        /// 로비에 대한 변경 사항(예: 삭제 또는 업데이트)을 처리합니다.
        /// </summary>
        void OnLobbyChanges(ILobbyChanges changes)
        {
            if (changes.LobbyDeleted)
            {
                Debug.Log("Lobby deleted");
                ResetLobby();
                EndTracking();
            }
            else
            {
                Debug.Log("Lobby updated");
                changes.ApplyToLobby(CurrentUnityLobby);
                m_LocalLobby.ApplyRemoteData(CurrentUnityLobby);

                // as client, check if host is still in lobby
                if (!m_LocalUser.IsHost)
                {
                    foreach (var lobbyUser in m_LocalLobby.LobbyUsers)
                    {
                        if (lobbyUser.Value.IsHost)
                        {
                            return;
                        }
                    }

                    m_UnityServiceErrorMessagePub.Publish(new UnityServiceErrorMessage("Host left the lobby", "Disconnecting.", UnityServiceErrorMessage.Service.Lobby));
                    EndTracking();
                    // no need to disconnect Netcode, it should already be handled by Netcode's callback to disconnect
                }
            }
        }

        /// <summary>
        /// Handles the event of being kicked from the lobby.
        /// </summary>
        /// <summary>
        /// 로비에서 강퇴된 이벤트를 처리합니다.
        /// </summary>
        void OnKickedFromLobby()
        {
            Debug.Log("Kicked from Lobby");
            ResetLobby();
            EndTracking();
        }

        /// <summary>
        /// Handles the lobby event connection state changes.
        /// </summary>
        /// <summary>
        /// 로비 이벤트 연결 상태 변경을 처리합니다.
        /// </summary>
        void OnLobbyEventConnectionStateChanged(LobbyEventConnectionState lobbyEventConnectionState)
        {
            m_LobbyEventConnectionState = lobbyEventConnectionState;
            Debug.Log($"LobbyEventConnectionState changed to {lobbyEventConnectionState}");
        }

        /// <summary>
        /// Subscribes to the events of the joined lobby asynchronously.
        /// </summary>
        /// <summary>
        /// 비동기적으로 참여한 로비의 이벤트에 구독합니다.
        /// </summary>
        async void SubscribeToJoinedLobbyAsync()
        {
            var lobbyEventCallbacks = new LobbyEventCallbacks();
            lobbyEventCallbacks.LobbyChanged += OnLobbyChanges;
            lobbyEventCallbacks.KickedFromLobby += OnKickedFromLobby;
            lobbyEventCallbacks.LobbyEventConnectionStateChanged += OnLobbyEventConnectionStateChanged;
            // The LobbyEventCallbacks object created here will now be managed by the Lobby SDK. The callbacks will be
            // unsubscribed from when we call UnsubscribeAsync on the ILobbyEvents object we receive and store here.
            m_LobbyEvents = await m_LobbyApiInterface.SubscribeToLobby(m_LocalLobby.LobbyID, lobbyEventCallbacks);
        }

        /// <summary>
        /// Unsubscribes from the events of the joined lobby asynchronously.
        /// </summary>
        /// <summary>
        /// 비동기적으로 참여한 로비의 이벤트 구독을 취소합니다.
        /// </summary>
        async void UnsubscribeToJoinedLobbyAsync()
        {
            if (m_LobbyEvents != null && m_LobbyEventConnectionState != LobbyEventConnectionState.Unsubscribed)
            {
#if UNITY_EDITOR
                try
                {
                    await m_LobbyEvents.UnsubscribeAsync();
                }
                catch (WebSocketException e)
                {
                    // This exception occurs in the editor when exiting play mode without first leaving the lobby.
                    // This is because Wire closes the websocket internally when exiting playmode in the editor.
                    Debug.Log(e.Message);
                }
#else
        await m_LobbyEvents.UnsubscribeAsync();
#endif
            }
        }

        /// <summary>
        /// Retrieves and publishes the list of all active lobbies without needing full information for each.
        /// </summary>
        /// <summary>
        /// 각 로비에 대한 자세한 정보 없이 모든 활성 로비 목록을 검색하고 게시합니다.
        /// </summary>
        public async Task RetrieveAndPublishLobbyListAsync()
        {
            if (!m_RateLimitQuery.CanCall)
            {
                Debug.LogWarning("Retrieve Lobby list hit the rate limit. Will try again soon...");
                return;
            }

            try
            {
                var response = await m_LobbyApiInterface.QueryAllLobbies();
                m_LobbyListFetchedPub.Publish(new LobbyListFetchedMessage(LocalLobby.CreateLocalLobbies(response)));
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    m_RateLimitQuery.PutOnCooldown();
                }
                else
                {
                    PublishError(e);
                }
            }
        }

        /// <summary>
        /// Attempts to reconnect to a previously joined lobby.
        /// </summary>
        /// <summary>
        /// 이전에 참가한 로비에 재연결을 시도합니다.
        /// </summary>
        public async Task<Lobby> ReconnectToLobbyAsync()
        {
            try
            {
                return await m_LobbyApiInterface.ReconnectToLobby(m_LocalLobby.LobbyID);
            }
            catch (LobbyServiceException e)
            {
                // If Lobby is not found and if we are not the host, it has already been deleted. No need to publish the error here.
                if (e.Reason != LobbyExceptionReason.LobbyNotFound && !m_LocalUser.IsHost)
                {
                    PublishError(e);
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to leave the lobby asynchronously.
        /// </summary>
        /// <summary>
        /// 비동기적으로 로비를 떠나려고 시도합니다.
        /// </summary>
        async void LeaveLobbyAsync()
        {
            string uasId = AuthenticationService.Instance.PlayerId;
            try
            {
                await m_LobbyApiInterface.RemovePlayerFromLobby(uasId, m_LocalLobby.LobbyID);
            }
            catch (LobbyServiceException e)
            {
                // If Lobby is not found and if we are not the host, it has already been deleted. No need to publish the error here.
                if (e.Reason != LobbyExceptionReason.LobbyNotFound && !m_LocalUser.IsHost)
                {
                    PublishError(e);
                }
            }
            finally
            {
                ResetLobby();
            }

        }

        /// <summary>
        /// Removes a player from the lobby asynchronously, only if the current user is the host.
        /// </summary>
        /// <summary>
        /// 현재 사용자가 호스트일 경우에만 비동기적으로 로비에서 선수를 제거합니다.
        /// </summary>
        public async void RemovePlayerFromLobbyAsync(string uasId)
        {
            if (m_LocalUser.IsHost)
            {
                try
                {
                    await m_LobbyApiInterface.RemovePlayerFromLobby(uasId, m_LocalLobby.LobbyID);
                }
                catch (LobbyServiceException e)
                {
                    PublishError(e);
                }
            }
            else
            {
                Debug.LogError("Only the host can remove other players from the lobby.");
            }
        }

        /// <summary>
        /// Attempts to delete the current lobby, but only if the current user is the host.
        /// </summary>
        /// <summary>
        /// 현재 로비를 삭제하려고 시도합니다. 단, 현재 사용자가 호스트일 경우에만 가능합니다.
        /// </summary>
        async void DeleteLobbyAsync()
        {
            if (m_LocalUser.IsHost)
            {
                try
                {
                    await m_LobbyApiInterface.DeleteLobby(m_LocalLobby.LobbyID);
                }
                catch (LobbyServiceException e)
                {
                    PublishError(e);
                }
                finally
                {
                    ResetLobby();
                }
            }
            else
            {
                Debug.LogError("Only the host can delete a lobby.");
            }
        }

        /// <summary>
        /// Attempts to update the set of key-value pairs associated with the local player and handle any relay (or remote allocation) data.
        /// This will overwrite any existing data for the given keys.
        /// </summary>
        /// <summary>
        /// 로컬 플레이어와 관련된 키-값 쌍을 업데이트하려고 시도하며, 이로 인해 기존의 데이터를 덮어씁니다.
        /// 또한 릴레이(또는 원격 할당) 데이터가 포함될 수 있습니다.
        /// </summary>
        /// 
        public async Task UpdatePlayerDataAsync(string allocationId, string connectionInfo)
        {
            if (!m_RateLimitQuery.CanCall)
            {
                return;
            }

            try
            {
                var result = await m_LobbyApiInterface.UpdatePlayer(CurrentUnityLobby.Id, AuthenticationService.Instance.PlayerId, m_LocalUser.GetDataForUnityServices(), allocationId, connectionInfo);

                if (result != null)
                {
                    CurrentUnityLobby = result; // Store the most up-to-date lobby now since we have it, instead of waiting for the next heartbeat.
                }
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    m_RateLimitQuery.PutOnCooldown();
                }
                else if (e.Reason != LobbyExceptionReason.LobbyNotFound && !m_LocalUser.IsHost) // If Lobby is not found and if we are not the host, it has already been deleted. No need to publish the error here.
                {
                    PublishError(e);
                }
            }
        }

        /// <summary>
        /// Attempts to update the set of key-value pairs associated with the current lobby and unlocks it so clients can see it.
        /// </summary>
        /// <summary>
        /// 현재 로비에 연결된 키-값 쌍을 업데이트하고 로비를 잠금 해제하여 클라이언트들이 이를 볼 수 있게 합니다.
        /// </summary>
        public async Task UpdateLobbyDataAndUnlockAsync()
        {
            if (!m_RateLimitQuery.CanCall)
            {
                return;
            }

            var localData = m_LocalLobby.GetDataForUnityServices();

            var dataCurr = CurrentUnityLobby.Data;
            if (dataCurr == null)
            {
                dataCurr = new Dictionary<string, DataObject>();
            }

            foreach (var dataNew in localData)
            {
                if (dataCurr.ContainsKey(dataNew.Key))
                {
                    dataCurr[dataNew.Key] = dataNew.Value;
                }
                else
                {
                    dataCurr.Add(dataNew.Key, dataNew.Value);
                }
            }

            try
            {
                var result = await m_LobbyApiInterface.UpdateLobby(CurrentUnityLobby.Id, dataCurr, shouldLock: false);

                if (result != null)
                {
                    CurrentUnityLobby = result;
                }
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    m_RateLimitQuery.PutOnCooldown();
                }
                else
                {
                    PublishError(e);
                }
            }
        }

        /// <summary>
        /// Sends a periodic "heartbeat" ping to the lobby to keep it active and avoid "zombie" lobbies.
        /// </summary>
        /// <summary>
        /// 로비에 주기적인 "하트비트" 핑을 보내어 로비를 활성 상태로 유지하고 "좀비" 로비를 방지합니다.
        /// </summary>
        void DoLobbyHeartbeat(float dt)
        {
            m_HeartbeatTime += dt;
            if (m_HeartbeatTime > k_HeartbeatPeriod)
            {
                m_HeartbeatTime -= k_HeartbeatPeriod;
                try
                {
                    m_LobbyApiInterface.SendHeartbeatPing(CurrentUnityLobby.Id);
                }
                catch (LobbyServiceException e)
                {
                    // If Lobby is not found and if we are not the host, it has already been deleted. No need to publish the error here.
                    if (e.Reason != LobbyExceptionReason.LobbyNotFound && !m_LocalUser.IsHost)
                    {
                        PublishError(e);
                    }
                }
            }
        }

        /// <summary>
        /// Publishes an error message with the details of the LobbyServiceException.
        /// </summary>
        /// <summary>
        /// LobbyServiceException의 세부 사항을 포함하여 오류 메시지를 게시합니다.
        /// </summary>
        void PublishError(LobbyServiceException e)
        {
            var reason = e.InnerException == null ? e.Message : $"{e.Message} ({e.InnerException.Message})"; // Lobby error type, then HTTP error type.
            m_UnityServiceErrorMessagePub.Publish(new UnityServiceErrorMessage("Lobby Error", reason, UnityServiceErrorMessage.Service.Lobby, e));
        }
    }
}
