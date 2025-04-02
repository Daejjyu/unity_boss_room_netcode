using System;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character.AI
{
    /// <summary>
    /// AI가 대기(IDLE) 상태일 때의 행동을 정의하는 클래스
    /// </summary>
    public class IdleAIState : AIState
    {
        private AIBrain m_Brain;

        public IdleAIState(AIBrain brain)
        {
            m_Brain = brain;
        }

        /// <summary>
        /// 현재 상태가 유효한지 확인합니다.
        /// 적을 미워하는 리스트가 비어 있으면 대기 상태가 유효합니다.
        /// </summary>
        public override bool IsEligible()
        {
            return m_Brain.GetHatedEnemies().Count == 0;
        }

        /// <summary>
        /// 상태 초기화 함수 (현재 특별한 초기화 로직 없음)
        /// </summary>
        public override void Initialize()
        {
        }

        /// <summary>
        /// 매 프레임 호출되며, 주변을 스캔하여 적을 탐지합니다.
        /// </summary>
        public override void Update()
        {
            // 대기 중에는 주변을 스캔하여 공격할 대상을 찾습니다.
            DetectFoes();
        }

        /// <summary>
        /// 주변을 탐색하여 적이 있으면 '미워하는 적 목록'에 추가합니다.
        /// </summary>
        protected void DetectFoes()
        {
            float detectionRange = m_Brain.DetectRange;
            // 매 프레임마다 이 체크를 수행하기 때문에, 성능을 위해 sqrt 연산이 포함된 Vector3.magnitude 대신 제곱 거리(sqrMagnitude)를 사용합니다.
            float detectionRangeSqr = detectionRange * detectionRange;
            Vector3 position = m_Brain.GetMyServerCharacter().physicsWrapper.Transform.position;

            // 이 게임에서는 NPC가 플레이어만 공격하고, 다른 NPC를 공격하지 않기 때문에  
            // 플레이어 목록을 순회하며 범위 내에 있는지 확인합니다.
            foreach (var character in PlayerServerCharacter.GetPlayerServerCharacters())
            {
                if (m_Brain.IsAppropriateFoe(character) && (character.physicsWrapper.Transform.position - position).sqrMagnitude <= detectionRangeSqr)
                {
                    m_Brain.Hate(character);
                }
            }
        }
    }
}
