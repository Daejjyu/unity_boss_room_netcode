using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.VisualEffects;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    public partial class MeleeAction
    {
        // 실제로 충격을 주었는지 여부. 모든 스윙에서 충격을 주는 것은 아니며, 때로는 공간을 향해 휘두를 수도 있습니다.
        private bool m_ImpactPlayed;

        /// <summary>
        /// 원래 목표가 여전히 있는지 확인할 때, 범위 체크에 약간의 여유를 둡니다.
        /// </summary>
        private const float k_RangePadding = 3f;

        /// <summary>
        /// 목표에 대해 활성화된 특수 그래픽 리스트.
        /// </summary>
        private List<SpecialFXGraphic> m_SpawnedGraphics = null;

        public override bool OnStartClient(ClientCharacter clientCharacter)
        {
            base.OnStartClient(clientCharacter);

            // 목표에 적용할 특수 파티클이 있다면, 충격을 기다리지 말고 즉시 추가합니다.
            // (충격을 기다리지 않음, 왜냐하면 파티클은 더 빨리 시작해야 하기 때문입니다!)
            if (Data.TargetIds != null
                && Data.TargetIds.Length > 0
                && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(Data.TargetIds[0], out var targetNetworkObj)
                && targetNetworkObj != null)
            {
                // 범위에 여유를 둡니다.
                float padRange = Config.Range + k_RangePadding;

                Vector3 targetPosition;
                // 목표의 물리 객체를 가져오는 부분
                if (PhysicsWrapper.TryGetPhysicsWrapper(Data.TargetIds[0], out var physicsWrapper))
                {
                    targetPosition = physicsWrapper.Transform.position;
                }
                else
                {
                    targetPosition = targetNetworkObj.transform.position;
                }

                // 목표가 범위 내에 있다면, 특수 그래픽을 재생합니다.
                if ((clientCharacter.transform.position - targetPosition).sqrMagnitude < (padRange * padRange))
                {
                    m_SpawnedGraphics = InstantiateSpecialFXGraphics(physicsWrapper ? physicsWrapper.Transform : targetNetworkObj.transform, true);
                }
            }

            return true;
        }

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            return ActionConclusion.Continue;
        }

        public override void OnAnimEventClient(ClientCharacter clientCharacter, string id)
        {
            // 애니메이션 이벤트가 "impact"라면, 아직 충격을 주지 않았다면 충격 반응을 실행합니다.
            if (id == "impact" && !m_ImpactPlayed)
            {
                PlayHitReact(clientCharacter);
            }
        }

        public override void EndClient(ClientCharacter clientCharacter)
        {
            // 애니메이션 클립에 "impact" 이벤트가 제대로 설정되지 않은 경우 충격 반응을 놓칠 수 있으므로 종료 시 한 번 더 실행합니다.
            PlayHitReact(clientCharacter);
            base.EndClient(clientCharacter);
        }

        public override void CancelClient(ClientCharacter clientCharacter)
        {
            // 특수 목표 그래픽이 있었다면, 그들에게 작업이 끝났다고 알립니다.
            if (m_SpawnedGraphics != null)
            {
                foreach (var spawnedGraphic in m_SpawnedGraphics)
                {
                    if (spawnedGraphic)
                    {
                        spawnedGraphic.Shutdown();
                    }
                }
            }
        }

        void PlayHitReact(ClientCharacter parent)
        {
            // 이미 충격 반응을 실행했다면, 다시 실행하지 않습니다.
            if (m_ImpactPlayed) { return; }

            m_ImpactPlayed = true;

            // 서버에서 실행 중이면, 반응을 하지 않음
            if (NetworkManager.Singleton.IsServer)
            {
                return;
            }

            // 원래 목표가 여전히 범위 내에 있는지 확인
            if (Data.TargetIds != null &&
                Data.TargetIds.Length > 0 &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(Data.TargetIds[0], out var targetNetworkObj)
                && targetNetworkObj != null)
            {
                float padRange = Config.Range + k_RangePadding;

                Vector3 targetPosition;
                // 목표의 위치를 가져오는 부분
                if (PhysicsWrapper.TryGetPhysicsWrapper(Data.TargetIds[0], out var movementContainer))
                {
                    targetPosition = movementContainer.Transform.position;
                }
                else
                {
                    targetPosition = targetNetworkObj.transform.position;
                }

                // 목표가 범위 내에 있으면, 히트 반응 애니메이션을 실행합니다.
                if ((parent.transform.position - targetPosition).sqrMagnitude < (padRange * padRange))
                {
                    if (targetNetworkObj.NetworkObjectId != parent.NetworkObjectId)
                    {
                        string hitAnim = Config.ReactAnim;
                        if (string.IsNullOrEmpty(hitAnim)) { hitAnim = k_DefaultHitReact; }

                        if (targetNetworkObj.TryGetComponent<ServerCharacter>(out var serverCharacter)
                            && serverCharacter.clientCharacter != null
                            && serverCharacter.clientCharacter.OurAnimator)
                        {
                            serverCharacter.clientCharacter.OurAnimator.SetTrigger(hitAnim);
                        }
                    }
                }
            }

            // 나중에 목표가 "무기 아래로 달려갔다"는 경우를 처리할 수 있도록 추가 물리 체크를 할 수 있습니다.
            // 하지만 현재로서는 원래 목표가 더 이상 존재하지 않으면, 아무것도 반응하지 않습니다.
        }
    }
}
