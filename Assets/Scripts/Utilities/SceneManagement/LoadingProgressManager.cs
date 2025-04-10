using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Multiplayer.Samples.Utilities
{
    /// <summary>
    /// Contains data on scene loading progress for the local instance and remote instances.
    /// </summary>
    public class LoadingProgressManager : NetworkBehaviour
    {
        [SerializeField]
        GameObject m_ProgressTrackerPrefab;

        /// <summary>
        /// Dictionary containing references to the NetworkedLoadingProgessTrackers that contain the loading progress of
        /// each client. Keys are ClientIds.
        /// </summary>
        public Dictionary<ulong, NetworkedLoadingProgressTracker> ProgressTrackers { get; } = new Dictionary<ulong, NetworkedLoadingProgressTracker>();

        /// <summary>
        /// This is the AsyncOperation of the current load operation. This property should be set each time a new
        /// loading operation begins.
        /// </summary>
        public AsyncOperation LocalLoadOperation
        {
            set
            {
                m_IsLoading = true;
                LocalProgress = 0;
                m_LocalLoadOperation = value;
            }
        }

        AsyncOperation m_LocalLoadOperation;

        float m_LocalProgress;

        bool m_IsLoading;

        /// <summary>
        /// This event is invoked each time the dictionary of progress trackers is updated (if one is removed or added, for example.)
        /// </summary>
        public event Action onTrackersUpdated;

        /// <summary>
        /// The current loading progress for the local client. Handled by a local field if not in a networked session,
        /// or by a progress tracker from the dictionary.
        /// </summary>
        public float LocalProgress
        {
            get => IsSpawned && ProgressTrackers.ContainsKey(NetworkManager.LocalClientId) ?
                ProgressTrackers[NetworkManager.LocalClientId].Progress.Value : m_LocalProgress;
            private set
            {
                if (IsSpawned && ProgressTrackers.ContainsKey(NetworkManager.LocalClientId) && ProgressTrackers[NetworkManager.LocalClientId].IsSpawned)
                {
                    ProgressTrackers[NetworkManager.LocalClientId].Progress.Value = value;
                }
                else
                {
                    m_LocalProgress = value;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += AddTracker;
                NetworkManager.OnClientDisconnectCallback += RemoveTracker;
                AddTracker(NetworkManager.LocalClientId);
            }
        }
        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= AddTracker;
                NetworkManager.OnClientDisconnectCallback -= RemoveTracker;
            }
            ProgressTrackers.Clear();
            onTrackersUpdated?.Invoke();
        }

        void Update()
        {
            if (m_LocalLoadOperation != null && m_IsLoading)
            {
                if (m_LocalLoadOperation.isDone)
                {
                    m_IsLoading = false;
                    LocalProgress = 1;
                }
                else
                {
                    LocalProgress = m_LocalLoadOperation.progress;
                }
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        void ClientUpdateTrackersRpc()
        {
            if (!IsHost)
            {
                ProgressTrackers.Clear();
#pragma warning disable CS0618 // 형식 또는 멤버는 사용되지 않습니다.
                foreach (var tracker in FindObjectsOfType<NetworkedLoadingProgressTracker>())
                {
                    // If a tracker is despawned but not destroyed yet, don't add it
                    if (tracker.IsSpawned)
                    {
                        ProgressTrackers[tracker.OwnerClientId] = tracker;
                        if (tracker.OwnerClientId == NetworkManager.LocalClientId)
                        {
                            LocalProgress = Mathf.Max(m_LocalProgress, LocalProgress);
                        }
                    }
                }
#pragma warning restore CS0618 // 형식 또는 멤버는 사용되지 않습니다.
            }
            onTrackersUpdated?.Invoke();
        }

        void AddTracker(ulong clientId)
        {
            if (IsServer)
            {
                var tracker = Instantiate(m_ProgressTrackerPrefab);
                var networkObject = tracker.GetComponent<NetworkObject>();
                networkObject.SpawnWithOwnership(clientId);
                ProgressTrackers[clientId] = tracker.GetComponent<NetworkedLoadingProgressTracker>();
                ClientUpdateTrackersRpc();
            }
        }

        void RemoveTracker(ulong clientId)
        {
            if (IsServer)
            {
                if (ProgressTrackers.ContainsKey(clientId))
                {
                    var tracker = ProgressTrackers[clientId];
                    ProgressTrackers.Remove(clientId);
                    tracker.NetworkObject.Despawn();
                    ClientUpdateTrackersRpc();
                }
            }
        }
    }
}
