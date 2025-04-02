using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.VisualEffects;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// 캐릭터를 적과 다른 플레이어에게 숨게 만듭니다. 참고 사항:
    /// - Stealth는 ExecTimeSeconds가 지난 후 시작됩니다. Exec 시간이 지나기 전에 공격을 받으면 Stealth가 취소됩니다.
    /// - Stealth는 플레이어가 공격하거나 피해를 입으면 종료됩니다.
    /// </summary>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Stealth Mode Action")]
    public class StealthModeAction : Action
    {
        private bool m_IsStealthStarted = false; // Stealth 시작 여부
        private bool m_IsStealthEnded = false; // Stealth 종료 여부

        /// <summary>
        /// 그래픽 리스트가 null이 아닌 경우, 생성된 모든 그래픽들.
        /// (null인 경우는 실행 시간이 아직 충분히 지나지 않았거나, 이 클라이언트에서 보이지 않게 하기 위해 그래픽을 사용하지 않음)
        /// Description.Spawns 리스트에서 생성된 프리팹이 각각 SpecialFXGraphic 컴포넌트를 가지고 있어야 합니다.
        /// </summary>
        private List<SpecialFXGraphic> m_SpawnedGraphics = null;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            // Stealth 애니메이션 실행
            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);

            // 클라이언트에 Action 데이터를 전송
            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);

            return true;
        }

        public override void Reset()
        {
            base.Reset();
            m_IsStealthEnded = false;
            m_IsStealthStarted = false;
            m_SpawnedGraphics = null;
        }

        public override bool ShouldBecomeNonBlocking()
        {
            // ExecTimeSeconds 시간이 지난 후에는 블로킹 상태에서 벗어날 수 있음
            return TimeRunning >= Config.ExecTimeSeconds;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (TimeRunning >= Config.ExecTimeSeconds && !m_IsStealthStarted && !m_IsStealthEnded)
            {
                // Stealth 모드가 시작됨
                m_IsStealthStarted = true;
                clientCharacter.IsStealthy.Value = true; // Stealth 상태로 설정
            }
            return !m_IsStealthEnded; // Stealth 종료 상태가 아니면 계속 진행
        }

        public override void Cancel(ServerCharacter serverCharacter)
        {
            // Stealth 취소 애니메이션 실행
            if (!string.IsNullOrEmpty(Config.Anim2))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim2);
            }

            // Stealth 종료
            EndStealth(serverCharacter);
        }

        public override void OnGameplayActivity(ServerCharacter serverCharacter, GameplayActivity activityType)
        {
            // 공격을 사용하거나 공격을 받은 경우 Stealth 종료
            if (activityType == GameplayActivity.UsingAttackAction || activityType == GameplayActivity.AttackedByEnemy)
            {
                EndStealth(serverCharacter);
            }
        }

        private void EndStealth(ServerCharacter parent)
        {
            if (!m_IsStealthEnded)
            {
                m_IsStealthEnded = true;
                if (m_IsStealthStarted)
                {
                    parent.IsStealthy.Value = false; // Stealth 종료
                }

                // ActionFX를 여기서 취소합니다. Cancel()에서 취소하지 않는 이유는
                // Stealth 버튼을 두 번 눌렀을 때, "현재 Stealth 액션을 종료하고 새로 시작하는" 경우를 처리하기 위함입니다.
                // Cancel()에서 모든 액션을 취소하면, 새로운 액션이 클라이언트의 ActionFX 큐에 이미 추가되었기 때문에
                // 이전 액션과 새 액션이 모두 취소될 수 있습니다.
                parent.clientCharacter.ClientCancelActionsByPrototypeIDRpc(ActionID);
            }
        }

        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            // ExecTimeSeconds 시간이 지난 후 그래픽을 생성
            if (TimeRunning >= Config.ExecTimeSeconds && m_SpawnedGraphics == null && clientCharacter.IsOwner)
            {
                m_SpawnedGraphics = InstantiateSpecialFXGraphics(clientCharacter.transform, true);
            }

            return ActionConclusion.Continue;
        }

        public override void CancelClient(ClientCharacter clientCharacter)
        {
            // 생성된 그래픽을 제거하고 정리
            if (m_SpawnedGraphics != null)
            {
                foreach (var graphic in m_SpawnedGraphics)
                {
                    if (graphic)
                    {
                        graphic.transform.SetParent(null); // 부모 객체에서 분리
                        graphic.Shutdown(); // 그래픽 정리
                    }
                }
            }
        }
    }
}
