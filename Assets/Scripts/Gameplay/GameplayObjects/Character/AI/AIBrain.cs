using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.Configuration;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character.AI
{
    /// <summary>
    /// Handles enemy AI. Contains AIStateLogics that handle some of the details,
    /// and has various utility functions that are called by those AIStateLogics
    /// </summary>
    /// <summary>
    /// 적 AI를 처리합니다. 일부 세부 사항을 처리하는 AIStateLogic을 포함하며,  
    /// 이러한 AIStateLogic에서 호출되는 다양한 유틸리티 함수를 가집니다.
    /// </summary>
    public class AIBrain
    {
        private enum AIStateType
        {
            ATTACK, // 공격 상태
            //WANDER, // 방황 상태 (미구현)
            IDLE, // 대기 상태
        }

        static readonly AIStateType[] k_AIStates = (AIStateType[])Enum.GetValues(typeof(AIStateType));

        private ServerCharacter m_ServerCharacter;
        private ServerActionPlayer m_ServerActionPlayer;
        private AIStateType m_CurrentState;
        private Dictionary<AIStateType, AIState> m_Logics;
        private List<ServerCharacter> m_HatedEnemies;

        /// <summary>
        /// If we are created by a spawner, the spawner might override our detection radius
        /// -1 is a sentinel value meaning "no override"
        /// </summary>
        /// <summary>
        /// 스포너에 의해 생성된 경우, 스포너가 탐지 반경을 재정의할 수 있습니다.  
        /// -1은 "재정의 없음"을 의미하는 센티널 값입니다.
        /// </summary>
        private float m_DetectRangeOverride = -1;

        public AIBrain(ServerCharacter me, ServerActionPlayer myServerActionPlayer)
        {
            m_ServerCharacter = me;
            m_ServerActionPlayer = myServerActionPlayer;

            m_Logics = new Dictionary<AIStateType, AIState>
            {
                [AIStateType.IDLE] = new IdleAIState(this),
                //[ AIStateType.WANDER ] = new WanderAIState(this), // not written yet
                [AIStateType.ATTACK] = new AttackAIState(this, m_ServerActionPlayer),
            };
            m_HatedEnemies = new List<ServerCharacter>();
            m_CurrentState = AIStateType.IDLE;
        }

        /// <summary>
        /// Should be called by the AIBrain's owner each Update()
        /// </summary>
        /// <summary>
        /// AI 브레인의 소유자가 매 프레임 Update()에서 호출해야 합니다.
        /// </summary>
        public void Update()
        {
            AIStateType newState = FindBestEligibleAIState();
            if (m_CurrentState != newState)
            {
                m_Logics[newState].Initialize();
            }
            m_CurrentState = newState;
            m_Logics[m_CurrentState].Update();
        }

        /// <summary>
        /// Called when we received some HP. Positive HP is healing, negative is damage.
        /// </summary>
        /// <summary>
        /// HP를 받을 때 호출됩니다. 양수 값은 치유, 음수 값은 피해를 의미합니다.
        /// </summary>
        /// <param name="inflicter">우리를 피해 입히거나 치유한 캐릭터. null일 수 있습니다.</param>
        /// <param name="amount">받은 HP 양. 음수일 경우 피해를 의미합니다.</param>
        public void ReceiveHP(ServerCharacter inflicter, int amount)
        {
            if (inflicter != null && amount < 0)
            {
                Hate(inflicter);
            }
        }

        private AIStateType FindBestEligibleAIState()
        {
            // for now we assume the AI states are in order of appropriateness,
            // which may be nonsensical when there are more states
            // 현재는 AI 상태가 적절한 순서대로 정렬되어 있다고 가정합니다.  
            // 상태가 많아지면 비논리적으로 보일 수도 있습니다.
            foreach (AIStateType aiStateType in k_AIStates)
            {
                if (m_Logics[aiStateType].IsEligible())
                {
                    return aiStateType;
                }
            }

            Debug.LogError("No AI states are valid!?!");
            return AIStateType.IDLE;
        }

        /// <summary>
        /// Returns true if it be appropriate for us to murder this character, starting right now!
        /// </summary>
        /// <summary>
        /// 지금 당장 이 캐릭터를 공격하는 것이 적절한지 여부를 반환합니다.
        /// </summary>
        public bool IsAppropriateFoe(ServerCharacter potentialFoe)
        {
            if (potentialFoe == null ||
                potentialFoe.IsNpc ||
                potentialFoe.LifeState != LifeState.Alive ||
                potentialFoe.IsStealthy.Value)
            {
                return false;
            }

            // Also, we could use NavMesh.Raycast() to see if we have line of sight to foe?
            // 또한 NavMesh.Raycast()를 사용하여 적을 볼 수 있는지 확인할 수도 있습니다.
            return true;
        }

        /// <summary>
        /// Notify the AIBrain that we should consider this character an enemy.
        /// </summary>
        /// <summary>
        /// 이 캐릭터를 적으로 간주해야 함을 AI 브레인에 알립니다.
        /// </summary>
        public void Hate(ServerCharacter character)
        {
            if (!m_HatedEnemies.Contains(character))
            {
                m_HatedEnemies.Add(character);
            }
        }

        /// <summary>
        /// Return the raw list of hated enemies -- treat as read-only!
        /// </summary>
        /// <summary>
        /// 미워하는 적의 목록을 반환합니다. 읽기 전용으로 다뤄야 합니다!
        /// </summary>
        public List<ServerCharacter> GetHatedEnemies()
        {
            // first we clean the list -- remove any enemies that have disappeared (became null), are dead, etc.
            // 먼저 목록을 정리합니다. 사라진( null이 된 ) 적이나 죽은 적 등을 제거합니다.
            for (int i = m_HatedEnemies.Count - 1; i >= 0; i--)
            {
                if (!IsAppropriateFoe(m_HatedEnemies[i]))
                {
                    m_HatedEnemies.RemoveAt(i);
                }
            }
            return m_HatedEnemies;
        }

        /// <summary>
        /// Retrieve info about who we are. Treat as read-only!
        /// </summary>
        /// <summary>
        /// 현재 캐릭터의 정보를 가져옵니다. 읽기 전용으로 다뤄야 합니다!
        /// </summary>
        public ServerCharacter GetMyServerCharacter()
        {
            return m_ServerCharacter;
        }

        /// <summary>
        /// Convenience getter that returns the CharacterData associated with this creature.
        /// </summary>
        /// <summary>
        /// 이 개체와 연결된 CharacterData를 반환하는 편의 속성입니다.
        /// </summary>
        public CharacterClass CharacterData
        {
            get
            {
                return GameDataSource.Instance.CharacterDataByType[m_ServerCharacter.CharacterType];
            }
        }

        /// <summary>
        /// The range at which this character can detect enemies, in meters.
        /// This is usually the same value as is indicated by our game data, but it
        /// can be dynamically overridden.
        /// </summary>
        /// <summary>
        /// 이 캐릭터가 적을 탐지할 수 있는 범위(미터)입니다.  
        /// 일반적으로 게임 데이터에서 제공하는 값과 동일하지만 동적으로 재정의될 수도 있습니다.
        /// </summary>
        public float DetectRange
        {
            get
            {
                return (m_DetectRangeOverride == -1) ? CharacterData.DetectRange : m_DetectRangeOverride;
            }

            set
            {
                m_DetectRangeOverride = value;
            }
        }

    }
}
