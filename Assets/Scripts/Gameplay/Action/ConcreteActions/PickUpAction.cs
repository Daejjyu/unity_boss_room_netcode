using System;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// "Heavy" 아이템을 집는 액션을 담당합니다. 이 클래스는 네트워크 오브젝트를 부모로 바꾸는 작업과,
    /// 아이템을 내려놓는 작업을 처리합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Pick Up Action")]
    public class PickUpAction : Action
    {
        const string k_HeavyTag = "Heavy"; // "Heavy" 태그
        const string k_NpcLayer = "NPCs"; // NPC 레이어
        const string k_FailedPickupTrigger = "PickUpFailed"; // 아이템 집기 실패 애니메이션 트리거

        static RaycastHitComparer s_RaycastHitComparer = new RaycastHitComparer(); // 레이캐스트 히트 비교기

        RaycastHit[] m_RaycastHits = new RaycastHit[8]; // 레이캐스트 결과 저장
        float m_ActionStartTime; // 액션 시작 시간
        bool m_AttemptedPickup; // 아이템 집기를 시도했는지 여부

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            m_ActionStartTime = Time.time; // 액션 시작 시간 기록

            // 만약 이미 무거운 아이템을 들고 있지 않다면 애니메이션을 실행
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    serverCharacter.HeldNetworkObject.Value, out var heldObject))
            {
                // 설정된 애니메이션 트리거가 있다면 애니메이션 실행
                if (!string.IsNullOrEmpty(Config.Anim))
                {
                    serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
                }
            }

            return true;
        }

        public override void Reset()
        {
            base.Reset();
            m_ActionStartTime = 0; // 액션 시작 시간 초기화
            m_AttemptedPickup = false; // 아이템 집기 시도 여부 초기화
        }

        bool TryPickUp(ServerCharacter parent)
        {
            // 부모 캐릭터 앞 방향으로 레이캐스트 수행 (NPC 레이어에 대해서만)
            var numResults = Physics.RaycastNonAlloc(parent.physicsWrapper.Transform.position,
                parent.physicsWrapper.Transform.forward,
                m_RaycastHits,
                Config.Range,
                1 << LayerMask.NameToLayer(k_NpcLayer));

            // 레이캐스트 결과 정렬
            Array.Sort(m_RaycastHits, 0, numResults, s_RaycastHitComparer);

            // "Heavy" 태그가 붙어있고, 부모가 없는 오브젝트를 찾고, 부모로 설정이 성공했는지 체크
            if (numResults == 0 || !m_RaycastHits[0].collider.TryGetComponent(out NetworkObject heavyNetworkObject) ||
                !m_RaycastHits[0].collider.gameObject.CompareTag(k_HeavyTag) ||
                (heavyNetworkObject.transform.parent != null &&
                    heavyNetworkObject.transform.parent.TryGetComponent(out NetworkObject parentNetworkObject)) ||
                !heavyNetworkObject.TrySetParent(parent.transform)) // 부모 설정 실패 시
            {
                // 집기 실패 애니메이션 실행
                parent.serverAnimationHandler.NetworkAnimator.SetTrigger(k_FailedPickupTrigger);
                return false;
            }

            // 아이템이 성공적으로 부모가 되었으므로, 네트워크 오브젝트 ID를 기록
            parent.HeldNetworkObject.Value = heavyNetworkObject.NetworkObjectId;

            Data.TargetIds = new ulong[] { heavyNetworkObject.NetworkObjectId };

            // 부모가 목표를 잃었으므로 목표 ID를 0으로 초기화
            parent.TargetId.Value = 0;

            // 부모가 목표를 향하도록 회전
            if (Data.Direction != Vector3.zero)
            {
                parent.transform.forward = Data.Direction;
            }

            // 아이템이 손에 맞게 따라가도록 PositionConstraint 컴포넌트를 설정
            var positionConstraint = heavyNetworkObject.GetComponent<PositionConstraint>();
            if (positionConstraint)
            {
                if (parent.TryGetComponent(out ServerCharacter serverCharacter))
                {
                    var constraintSource = new ConstraintSource()
                    {
                        sourceTransform = serverCharacter.clientCharacter.CharacterSwap.CharacterModel.handSocket.transform,
                        weight = 1
                    };
                    positionConstraint.AddSource(constraintSource);
                    positionConstraint.constraintActive = true; // 제약 활성화
                }
            }

            return true;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            // 아이템 집기 시도가 아직 안되었고, 액션 시간이 초과되면 집기 시도
            if (!m_AttemptedPickup && Time.time > m_ActionStartTime + Config.ExecTimeSeconds)
            {
                m_AttemptedPickup = true;
                if (!TryPickUp(clientCharacter))
                {
                    // 집기 시도가 실패했다면 액션 종료
                    return ActionConclusion.Stop;
                }
            }

            return ActionConclusion.Continue;
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            // 캐릭터가 기절 상태이면, 들고 있는 아이템을 내려놓음
            if (serverCharacter.LifeState == LifeState.Fainted)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(serverCharacter.HeldNetworkObject.Value, out var heavyNetworkObject))
                {
                    heavyNetworkObject.transform.SetParent(null); // 부모 객체에서 분리
                }
                serverCharacter.HeldNetworkObject.Value = 0; // 들고 있는 오브젝트 ID 초기화
            }
        }
    }
}
