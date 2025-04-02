using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.Actions;
using UnityEngine;
using Action = Unity.BossRoom.Gameplay.Actions.Action;
using Random = UnityEngine.Random;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character.AI
{
    /// <summary>
    /// AI state that handles attack behavior.
    /// </summary>
    /// <summary>
    /// 공격 행동을 처리하는 AI 상태입니다.
    /// </summary>
    public class AttackAIState : AIState
    {
        private AIBrain m_Brain;
        private ServerActionPlayer m_ServerActionPlayer;
        private ServerCharacter m_Foe;
        private Action m_CurAttackAction;

        List<Action> m_AttackActions;

        public AttackAIState(AIBrain brain, ServerActionPlayer serverActionPlayer)
        {
            m_Brain = brain;
            m_ServerActionPlayer = serverActionPlayer;
        }

        /// <summary>
        /// Determines if this AI state is eligible to run.
        /// </summary>
        /// <summary>
        /// 이 AI 상태가 실행될 수 있는지 결정합니다.
        /// </summary>
        public override bool IsEligible()
        {
            return m_Foe != null || ChooseFoe() != null;
        }

        /// <summary>
        /// Initializes the attack state.
        /// </summary>
        /// <summary>
        /// 공격 상태를 초기화합니다.
        /// </summary>
        public override void Initialize()
        {
            m_AttackActions = new List<Action>();
            if (m_Brain.CharacterData.Skill1 != null)
            {
                m_AttackActions.Add(m_Brain.CharacterData.Skill1);
            }
            if (m_Brain.CharacterData.Skill2 != null)
            {
                m_AttackActions.Add(m_Brain.CharacterData.Skill2);
            }
            if (m_Brain.CharacterData.Skill3 != null)
            {
                m_AttackActions.Add(m_Brain.CharacterData.Skill3);
            }

            // pick a starting attack action from the possible
            // 가능한 공격 액션 중 하나를 선택합니다.
            m_CurAttackAction = m_AttackActions[Random.Range(0, m_AttackActions.Count)];

            // clear any old foe info; we'll choose a new one in Update()
            // 이전의 적 정보를 초기화합니다. 새로운 적을 Update()에서 선택할 것입니다.
            m_Foe = null;
        }

        /// <summary>
        /// Updates the attack state every frame.
        /// </summary>
        /// <summary>
        /// 매 프레임마다 공격 상태를 업데이트합니다.
        /// </summary>
        public override void Update()
        {
            if (!m_Brain.IsAppropriateFoe(m_Foe))
            {
                // time for a new foe!
                // 새로운 적을 찾을 시간입니다!
                m_Foe = ChooseFoe();
                // whatever we used to be doing, stop that. New plan is coming!
                // 기존의 행동을 멈춥니다. 새로운 계획이 진행될 것입니다!
                m_ServerActionPlayer.ClearActions(true);
            }

            // if we're out of foes, stop! IsEligible() will now return false so we'll soon switch to a new state
            // 적이 없다면 중지합니다! IsEligible()이 이제 false를 반환하므로 곧 새로운 상태로 전환될 것입니다.
            if (!m_Foe)
            {
                return;
            }

            // see if we're already chasing or attacking our active foe!
            // 현재 적을 추격 중이거나 공격 중인지 확인합니다!
            if (m_ServerActionPlayer.GetActiveActionInfo(out var info))
            {
                if (GameDataSource.Instance.GetActionPrototypeByID(info.ActionID).IsChaseAction)
                {
                    if (info.TargetIds != null && info.TargetIds[0] == m_Foe.NetworkObjectId)
                    {
                        // yep we're chasing our foe; all set! (The attack is enqueued after it)
                        // 적을 추격 중입니다. 준비 완료! (공격은 그 후에 진행됩니다.)
                        return;
                    }
                }
                else if (info.ActionID == m_CurAttackAction.ActionID)
                {
                    if (info.TargetIds != null && info.TargetIds[0] == m_Foe.NetworkObjectId)
                    {
                        // yep we're attacking our foe; all set!
                        // 적을 공격 중입니다. 준비 완료!
                        return;
                    }
                }
                else if (GameDataSource.Instance.GetActionPrototypeByID(info.ActionID).IsStunAction)
                {
                    // we can't do anything right now. We're stunned!
                    // 현재 아무것도 할 수 없습니다. 기절 상태입니다!
                    return;
                }
            }

            // choose the attack to use
            // 사용할 공격을 선택합니다.
            m_CurAttackAction = ChooseAttack();
            if (m_CurAttackAction == null)
            {
                // no actions are usable right now
                // 현재 사용할 수 있는 공격이 없습니다.
                return;
            }

            // attack!
            // 공격 실행!
            var attackData = new ActionRequestData
            {
                ActionID = m_CurAttackAction.ActionID,
                TargetIds = new ulong[] { m_Foe.NetworkObjectId },
                ShouldClose = true,
                Direction = m_Brain.GetMyServerCharacter().physicsWrapper.Transform.forward
            };
            m_ServerActionPlayer.PlayAction(ref attackData);
        }

        /// <summary>
        /// Picks the most appropriate foe for us to attack right now, or null if none are appropriate.
        /// (Currently just chooses the foe closest to us in distance)
        /// </summary>
        /// <summary>
        /// 현재 공격할 가장 적절한 적을 선택하거나, 적절한 적이 없으면 null을 반환합니다.
        /// (현재는 가장 가까운 적을 선택합니다.)
        /// </summary>
        /// <returns>가장 적절한 적 또는 null</returns>
        private ServerCharacter ChooseFoe()
        {
            Vector3 myPosition = m_Brain.GetMyServerCharacter().physicsWrapper.Transform.position;

            float closestDistanceSqr = int.MaxValue;
            ServerCharacter closestFoe = null;
            foreach (var foe in m_Brain.GetHatedEnemies())
            {
                float distanceSqr = (myPosition - foe.physicsWrapper.Transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestFoe = foe;
                }
            }
            return closestFoe;
        }

        /// <summary>
        /// Randomly picks a usable attack. If no actions are usable right now, returns null.
        /// </summary>
        /// <summary>
        /// 사용 가능한 공격을 무작위로 선택합니다. 현재 사용할 수 있는 공격이 없으면 null을 반환합니다.
        /// </summary>
        /// <returns>사용할 공격 액션 또는 null</returns>
        private Action ChooseAttack()
        {
            // make a random choice
            // 무작위로 선택합니다.
            int idx = Random.Range(0, m_AttackActions.Count);

            // now iterate through our options to find one that's currently usable
            // 현재 사용할 수 있는 공격을 찾기 위해 반복합니다.
            bool anyUsable;
            do
            {
                anyUsable = false;
                foreach (var attack in m_AttackActions)
                {
                    if (m_ServerActionPlayer.IsReuseTimeElapsed(attack.ActionID))
                    {
                        anyUsable = true;
                        if (idx == 0)
                        {
                            return attack;
                        }
                        --idx;
                    }
                }
            } while (anyUsable);

            // none of our actions are available now
            // 현재 사용할 수 있는 공격이 없습니다.
            return null;
        }
    }
}
