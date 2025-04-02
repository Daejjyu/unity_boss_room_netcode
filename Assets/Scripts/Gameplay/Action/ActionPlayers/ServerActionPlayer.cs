using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;
using UnityEngine.Pool;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Class responsible for playing back action inputs from user.
    /// </summary>
    /// <summary>
    /// 사용자로부터 입력된 액션을 재생하는 역할을 하는 클래스입니다.
    /// </summary>
    public class ServerActionPlayer
    {
        private ServerCharacter m_ServerCharacter;

        private ServerCharacterMovement m_Movement;

        private List<Action> m_Queue;

        private List<Action> m_NonBlockingActions;

        private Dictionary<ActionID, float> m_LastUsedTimestamps;

        /// <summary>
        /// To prevent the action queue from growing without bound, we cap its play time to this number of seconds. We can only ever estimate
        /// the time-length of the queue, since actions are allowed to block indefinitely. But this is still a useful estimate that prevents
        /// us from piling up a large number of small actions.
        /// </summary>
        /// <summary>
        /// 액션 큐가 무한히 커지는 것을 방지하기 위해, 액션의 실행 시간을 이 초로 제한합니다. 액션은 무한히 블록될 수 있기 때문에 큐의 시간 길이는 추정할 수밖에 없습니다.
        /// 그래도 이 추정치는 작은 액션들이 쌓이는 것을 방지하는 데 유용합니다.
        /// </summary>
        private const float k_MaxQueueTimeDepth = 1.6f;

        private ActionRequestData m_PendingSynthesizedAction = new ActionRequestData();
        private bool m_HasPendingSynthesizedAction;

        public ServerActionPlayer(ServerCharacter serverCharacter)
        {
            m_ServerCharacter = serverCharacter;
            m_Movement = serverCharacter.Movement;
            m_Queue = new List<Action>();
            m_NonBlockingActions = new List<Action>();
            m_LastUsedTimestamps = new Dictionary<ActionID, float>();
        }

        /// <summary>
        /// Perform a sequence of actions.
        /// </summary>
        /// <summary>
        /// 일련의 액션을 실행합니다.
        /// </summary>
        public void PlayAction(ref ActionRequestData action)
        {
            if (!action.ShouldQueue && m_Queue.Count > 0 &&
                (m_Queue[0].Config.ActionInterruptible ||
                    m_Queue[0].Config.CanBeInterruptedBy(action.ActionID)))
            {
                ClearActions(false);
            }

            if (GetQueueTimeDepth() >= k_MaxQueueTimeDepth)
            {
                //the queue is too big (in execution seconds) to accommodate any more actions, so this action must be discarded.
                //큐가 너무 커서 더 이상 액션을 처리할 수 없으므로 이 액션은 버려야 합니다.
                return;
            }

            var newAction = ActionFactory.CreateActionFromData(ref action);
            m_Queue.Add(newAction);
            if (m_Queue.Count == 1) { StartAction(); }
        }

        public void ClearActions(bool cancelNonBlocking)
        {
            if (m_Queue.Count > 0)
            {
                // Since this action was canceled, we don't want the player to have to wait Description.ReuseTimeSeconds
                // to be able to start it again. It should be restartable immediately!
                // 이 액션이 취소되었기 때문에, 플레이어가 다시 시작하기 위해 대기하지 않도록 해야 합니다. 즉시 다시 시작할 수 있어야 합니다.
                m_LastUsedTimestamps.Remove(m_Queue[0].ActionID);
                m_Queue[0].Cancel(m_ServerCharacter);
            }

            //clear the action queue
            //액션 큐를 초기화합니다.
            {
                var removedActions = ListPool<Action>.Get();

                foreach (var action in m_Queue)
                {
                    removedActions.Add(action);
                }

                m_Queue.Clear();

                foreach (var action in removedActions)
                {
                    TryReturnAction(action);
                }

                ListPool<Action>.Release(removedActions);
            }


            if (cancelNonBlocking)
            {
                var removedActions = ListPool<Action>.Get();

                foreach (var action in m_NonBlockingActions)
                {
                    action.Cancel(m_ServerCharacter);
                    removedActions.Add(action);
                }
                m_NonBlockingActions.Clear();

                foreach (var action in removedActions)
                {
                    TryReturnAction(action);
                }

                ListPool<Action>.Release(removedActions);
            }
        }

        /// <summary>
        /// If an Action is active, fills out 'data' param and returns true. If no Action is active, returns false.
        /// This only refers to the blocking action! (multiple non-blocking actions can be running in the background, and
        /// this will still return false).
        /// </summary>
        /// <summary>
        /// 액션이 활성화되어 있으면 'data' 매개변수를 채우고 true를 반환합니다. 액션이 활성화되지 않았으면 false를 반환합니다.
        /// 이것은 오직 블록킹 액션만을 의미합니다! (여러 개의 비차단 액션이 백그라운드에서 실행되고 있을 수 있으며, 이 경우 여전히 false를 반환합니다.)
        /// </summary>
        public bool GetActiveActionInfo(out ActionRequestData data)
        {
            if (m_Queue.Count > 0)
            {
                data = m_Queue[0].Data;
                return true;
            }
            else
            {
                data = new ActionRequestData();
                return false;
            }
        }

        /// <summary>
        /// Figures out if an action can be played now, or if it would automatically fail because it was
        /// used too recently. (Meaning that its ReuseTimeSeconds hasn't elapsed since the last use.)
        /// </summary>
        /// <param name="actionID">the action we want to run</param>
        /// <returns>true if the action can be run now, false if more time must elapse before this action can be run</returns>
        /// <summary>
        /// 액션을 지금 실행할 수 있는지, 아니면 너무 최근에 사용되어 자동으로 실패하는지 확인합니다.
        /// (즉, ReuseTimeSeconds가 마지막 사용 이후 경과하지 않은 경우입니다.)
        /// </summary>
        /// <param name="actionID">실행하려는 액션</param>
        /// <returns>액션을 지금 실행할 수 있으면 true, 더 많은 시간이 경과해야 실행할 수 있으면 false</returns>
        public bool IsReuseTimeElapsed(ActionID actionID)
        {
            if (m_LastUsedTimestamps.TryGetValue(actionID, out float lastTimeUsed))
            {
                var abilityConfig = GameDataSource.Instance.GetActionPrototypeByID(actionID).Config;

                float reuseTime = abilityConfig.ReuseTimeSeconds;
                if (reuseTime > 0 && Time.time - lastTimeUsed < reuseTime)
                {
                    // still needs more time!
                    // 아직 더 시간이 필요합니다!
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns how many actions are actively running. This includes all non-blocking actions,
        /// and the one blocking action at the head of the queue (if present).
        /// </summary>
        /// <summary>
        /// 현재 실행 중인 액션의 수를 반환합니다. 여기에는 모든 비차단 액션과
        /// 큐의 맨 앞에 있는 하나의 블록킹 액션이 포함됩니다(존재하는 경우).
        /// </summary>
        public int RunningActionCount
        {
            get
            {
                return m_NonBlockingActions.Count + (m_Queue.Count > 0 ? 1 : 0);
            }
        }

        /// <summary>
        /// Starts the action at the head of the queue, if any.
        /// </summary>
        /// <summary>
        /// 큐의 맨 앞에 있는 액션을 시작합니다.
        /// </summary>
        private void StartAction()
        {
            if (m_Queue.Count > 0)
            {
                float reuseTime = m_Queue[0].Config.ReuseTimeSeconds;
                if (reuseTime > 0
                    && m_LastUsedTimestamps.TryGetValue(m_Queue[0].ActionID, out float lastTimeUsed)
                    && Time.time - lastTimeUsed < reuseTime)
                {
                    // we've already started one of these too recently
                    // 이미 너무 최근에 시작한 액션이 있습니다.
                    AdvanceQueue(false); // note: this will call StartAction() recursively if there's more stuff in the queue ...
                    return;              // ... so it's important not to try to do anything more here
                }

                int index = SynthesizeTargetIfNecessary(0);
                SynthesizeChaseIfNecessary(index);

                m_Queue[0].TimeStarted = Time.time;
                bool play = m_Queue[0].OnStart(m_ServerCharacter);
                if (!play)
                {
                    //actions that exited out in the "Start" method will not have their End method called, by design.
                    // "Start" 메서드에서 종료된 액션은 "End" 메서드가 호출되지 않습니다. (설계상)
                    AdvanceQueue(false); // note: this will call StartAction() recursively if there's more stuff in the queue ...
                    return;              // ... so it's important not to try to do anything more here
                }

                // if this Action is interruptible, that means movement should interrupt it... character needs to be stationary for this!
                // So stop any movement that's already happening before we begin
                if (m_Queue[0].Config.ActionInterruptible && !m_Movement.IsPerformingForcedMovement())
                {
                    m_Movement.CancelMove();
                }

                // remember the moment when we successfully used this Action!
                m_LastUsedTimestamps[m_Queue[0].ActionID] = Time.time;

                if (m_Queue[0].Config.ExecTimeSeconds == 0 && m_Queue[0].Config.BlockingMode == BlockingModeType.OnlyDuringExecTime)
                {
                    //this is a non-blocking action with no exec time. It should never be hanging out at the front of the queue (not even for a frame),
                    //because it could get cleared if a new Action came in in that interval.
                    m_NonBlockingActions.Add(m_Queue[0]);
                    AdvanceQueue(false); // note: this will call StartAction() recursively if there's more stuff in the queue ...
                    return;              // ... so it's important not to try to do anything more here
                }
            }
        }

        /// <summary>
        /// Synthesizes a Chase Action for the action at the Head of the queue, if necessary (the base action must have a target,
        /// and must have the ShouldClose flag set). This method must not be called when the queue is empty.
        /// </summary>
        /// <returns>The new index of the Action being operated on.</returns>
        /// <summary>
        /// 큐의 맨 앞에 있는 액션에 대해 필요한 경우 추격 액션을 합성합니다. (기본 액션은 타겟이 있어야 하며, ShouldClose 플래그가 설정되어야 합니다.)
        /// 큐가 비어 있을 때는 이 메서드를 호출해서는 안 됩니다.
        /// </summary>
        /// <returns>작동 중인 액션의 새로운 인덱스를 반환합니다.</returns>
        private int SynthesizeChaseIfNecessary(int baseIndex)
        {
            Action baseAction = m_Queue[baseIndex];

            if (baseAction.Data.ShouldClose && baseAction.Data.TargetIds != null)
            {
                ActionRequestData data = new ActionRequestData
                {
                    ActionID = GameDataSource.Instance.GeneralChaseActionPrototype.ActionID,
                    TargetIds = baseAction.Data.TargetIds,
                    Amount = baseAction.Config.Range
                };
                baseAction.Data.ShouldClose = false; //you only get to do this once!
                Action chaseAction = ActionFactory.CreateActionFromData(ref data);
                m_Queue.Insert(baseIndex, chaseAction);
                return baseIndex + 1;
            }
            return baseIndex;
        }


        // <summary>
        // This class handles the action queue system, managing both blocking and non-blocking actions in a gameplay context.
        // It ensures actions are executed in the correct order and allows for action cancellation, target synthesis, and action updates.
        // </summary>
        // <summary>
        // 이 클래스는 게임플레이 컨텍스트에서 블로킹 및 논블로킹 액션을 관리하는 액션 큐 시스템을 처리합니다.
        // 액션들이 올바른 순서로 실행되도록 보장하고, 액션 취소, 목표 합성, 액션 업데이트를 허용합니다.
        // </summary>

        /// <summary>
        /// Targeted skills should implicitly set the active target of the character, if not already set.
        /// </summary>
        /// <summary>
        /// 목표 스킬은 이미 설정되지 않은 경우 캐릭터의 활성 목표를 암시적으로 설정해야 합니다.
        /// </summary>
        private int SynthesizeTargetIfNecessary(int baseIndex)
        {
            Action baseAction = m_Queue[baseIndex];
            var targets = baseAction.Data.TargetIds;

            if (targets != null &&
                targets.Length == 1 &&
                targets[0] != m_ServerCharacter.TargetId.Value)
            {
                //if this is a targeted skill (with a single requested target), and it is different from our
                //active target, then we synthesize a TargetAction to change  our target over.

                ActionRequestData data = new ActionRequestData
                {
                    ActionID = GameDataSource.Instance.GeneralTargetActionPrototype.ActionID,
                    TargetIds = baseAction.Data.TargetIds
                };

                //this shouldn't run redundantly, because the next time the base Action comes up to play, its Target
                //and the active target in our NetState should match.
                Action targetAction = ActionFactory.CreateActionFromData(ref data);
                m_Queue.Insert(baseIndex, targetAction);
                return baseIndex + 1;
            }

            return baseIndex;
        }

        /// <summary>
        /// Optionally end the currently playing action, and advance to the next Action that wants to play.
        /// </summary>
        /// <summary>
        /// 현재 실행 중인 액션을 종료하고, 다음 실행을 원하는 액션으로 이동합니다.
        /// </summary>
        private void AdvanceQueue(bool endRemoved)
        {
            if (m_Queue.Count > 0)
            {
                if (endRemoved)
                {
                    m_Queue[0].End(m_ServerCharacter);
                    if (m_Queue[0].ChainIntoNewAction(ref m_PendingSynthesizedAction))
                    {
                        m_HasPendingSynthesizedAction = true;
                    }
                }
                var action = m_Queue[0];
                m_Queue.RemoveAt(0);
                TryReturnAction(action);
            }

            // now start the new Action! ... unless we now have a pending Action that will supercede it
            if (!m_HasPendingSynthesizedAction || m_PendingSynthesizedAction.ShouldQueue)
            {
                StartAction();
            }
        }

        /// <summary>
        /// Tries to return the action to the pool if it is no longer needed.
        /// </summary>
        /// <summary>
        /// 더 이상 필요하지 않은 액션을 풀에 반환하려고 시도합니다.
        /// </summary>
        private void TryReturnAction(Action action)
        {
            if (m_Queue.Contains(action))
            {
                return;
            }

            if (m_NonBlockingActions.Contains(action))
            {
                return;
            }

            ActionFactory.ReturnAction(action);
        }

        public void OnUpdate()
        {
            if (m_HasPendingSynthesizedAction)
            {
                m_HasPendingSynthesizedAction = false;
                PlayAction(ref m_PendingSynthesizedAction);
            }

            if (m_Queue.Count > 0 && m_Queue[0].ShouldBecomeNonBlocking())
            {
                // the active action is no longer blocking, meaning it should be moved out of the blocking queue and into the
                // non-blocking one. (We use this for e.g. projectile attacks, so the projectiles can keep flying, but
                // the player can enqueue other actions in the meantime.)
                m_NonBlockingActions.Add(m_Queue[0]);
                AdvanceQueue(false);
            }

            // if there's a blocking action, update it
            if (m_Queue.Count > 0)
            {
                if (!UpdateAction(m_Queue[0]))
                {
                    AdvanceQueue(true);
                }
            }

            // if there's non-blocking actions, update them! We do this in reverse-order so we can easily remove expired actions.
            for (int i = m_NonBlockingActions.Count - 1; i >= 0; --i)
            {
                Action runningAction = m_NonBlockingActions[i];
                if (!UpdateAction(runningAction))
                {
                    // it's dead!
                    runningAction.End(m_ServerCharacter);
                    m_NonBlockingActions.RemoveAt(i);
                    TryReturnAction(runningAction);
                }
            }
        }

        /// <summary>
        /// Calls a given Action's Update() and decides if the action is still alive.
        /// </summary>
        /// <returns>true if the action is still active, false if it's dead</returns>
        /// <summary>
        /// 주어진 액션의 Update()를 호출하고, 액션이 여전히 활성 상태인지 결정합니다.
        /// </summary>
        /// <returns>액션이 여전히 활성 상태이면 true, 아니면 false</returns>
        private bool UpdateAction(Action action)
        {
            bool keepGoing = action.OnUpdate(m_ServerCharacter);
            bool expirable = action.Config.DurationSeconds > 0f; //non-positive value is a sentinel indicating the duration is indefinite.
            var timeElapsed = Time.time - action.TimeStarted;
            bool timeExpired = expirable && timeElapsed >= action.Config.DurationSeconds;
            return keepGoing && !timeExpired;
        }

        /// <summary>
        /// How much time will it take all remaining Actions in the queue to play out? This sums up all the time each Action is blocking,
        /// which is different from each Action's duration. Note that this is an ESTIMATE. An action may block the queue indefinitely if it wishes.
        /// </summary>
        /// <returns>The total "time depth" of the queue, or how long it would take to play in seconds, if no more actions were added. </returns>
        /// <summary>
        /// 큐에 남아 있는 모든 액션이 실행되는 데 얼마나 걸릴지 계산합니다. 각 액션이 차지하는 블로킹 시간을 합산하며, 이는 각 액션의 지속 시간과 다릅니다.
        /// 참고로 이것은 추정값입니다. 액션은 원하면 큐를 무한정 차단할 수 있습니다.
        /// </summary>
        /// <returns>큐의 총 "시간 깊이", 즉 더 이상 액션이 추가되지 않는다면 실행되는 데 걸리는 시간 (초)</returns>
        private float GetQueueTimeDepth()
        {
            if (m_Queue.Count == 0) { return 0; }

            float totalTime = 0;
            foreach (var action in m_Queue)
            {
                var info = action.Config;
                float actionTime = info.BlockingMode == BlockingModeType.OnlyDuringExecTime ? info.ExecTimeSeconds :
                                    info.BlockingMode == BlockingModeType.EntireDuration ? info.DurationSeconds :
                                    throw new System.Exception($"Unrecognized blocking mode: {info.BlockingMode}");
                totalTime += actionTime;
            }

            return totalTime - m_Queue[0].TimeRunning;
        }

        public void CollisionEntered(Collision collision)
        {
            if (m_Queue.Count > 0)
            {
                m_Queue[0].CollisionEntered(m_ServerCharacter, collision);
            }
        }

        /// <summary>
        /// Gives all active Actions a chance to alter a gameplay variable.
        /// </summary>
        /// <remarks>
        /// Note that this handles both positive alterations (commonly called "buffs")
        /// AND negative ones ("debuffs").
        ///
        /// </remarks>
        /// <param name="buffType">Which gameplay variable is being calculated</param>
        /// <returns>The final ("buffed") value of the variable</returns>
        /// <summary>
        /// 모든 활성 액션에 게임플레이 변수를 변경할 기회를 제공합니다.
        /// </summary>
        /// <remarks>
        /// 이 코드는 긍정적인 변경 (일반적으로 "버프"라고 부릅니다)과
        /// 부정적인 변경 ("디버프")을 모두 처리합니다.
        /// </remarks>
        /// <param name="buffType">계산 중인 게임플레이 변수</param>
        /// <returns>변수의 최종 ("버프된") 값</returns>
        public float GetBuffedValue(Action.BuffableValue buffType)
        {
            float buffedValue = Action.GetUnbuffedValue(buffType);
            if (m_Queue.Count > 0)
            {
                m_Queue[0].BuffValue(buffType, ref buffedValue);
            }
            foreach (var action in m_NonBlockingActions)
            {
                action.BuffValue(buffType, ref buffedValue);
            }
            return buffedValue;
        }

        /// <summary>
        /// Tells all active Actions that a particular gameplay event happened, such as being hit,
        /// getting healed, dying, etc. Actions can change their behavior as a result.
        /// </summary>
        /// <param name="activityThatOccurred">The type of event that has occurred</param>
        /// <summary>
        /// 특정 게임플레이 이벤트가 발생했음을 모든 활성 액션에 알립니다. 예를 들면 맞았거나, 치료되었거나, 죽었을 때 등입니다.
        /// 액션은 그에 따라 행동을 변경할 수 있습니다.
        /// </summary>
        /// <param name="activityThatOccurred">발생한 이벤트 유형</param>
        public virtual void OnGameplayActivity(Action.GameplayActivity activityThatOccurred)
        {
            if (m_Queue.Count > 0)
            {
                m_Queue[0].OnGameplayActivity(m_ServerCharacter, activityThatOccurred);
            }
            foreach (var action in m_NonBlockingActions)
            {
                action.OnGameplayActivity(m_ServerCharacter, activityThatOccurred);
            }
        }

        /// <summary>
        /// Cancels the first instance of the given ActionLogic that is currently running, or all instances if cancelAll is set to true.
        /// Searches actively running actions first, then looks at the head action in the queue.
        /// </summary>
        /// <param name="logic">The ActionLogic to cancel</param>
        /// <param name="cancelAll">If true will cancel all instances; if false will just cancel the first running instance.</param>
        /// <param name="exceptThis">If set, will skip this action (useful for actions canceling other instances of themselves).</param>
        /// <summary>
        /// 주어진 ActionLogic의 첫 번째 실행 중인 인스턴스를 취소하거나, cancelAll이 true로 설정된 경우 모든 인스턴스를 취소합니다.
        /// 우선 실행 중인 액션을 찾고, 그런 다음 큐의 헤드 액션을 확인합니다.
        /// </summary>
        /// <param name="logic">취소할 ActionLogic</param>
        /// <param name="cancelAll">true이면 모든 인스턴스를 취소하고, false이면 첫 번째 실행 중인 인스턴스만 취소합니다.</param>
        /// <param name="exceptThis">설정된 경우 이 액션을 건너뜁니다 (자기 자신을 취소하는 액션에 유용합니다).</param>
        public void CancelRunningActionsByLogic(ActionLogic logic, bool cancelAll, Action exceptThis = null)
        {
            for (int i = m_NonBlockingActions.Count - 1; i >= 0; --i)
            {
                var action = m_NonBlockingActions[i];
                if (action.Config.Logic == logic && action != exceptThis)
                {
                    action.Cancel(m_ServerCharacter);
                    m_NonBlockingActions.RemoveAt(i);
                    TryReturnAction(action);
                    if (!cancelAll) { return; }
                }
            }

            if (m_Queue.Count > 0)
            {
                var action = m_Queue[0];
                if (action.Config.Logic == logic && action != exceptThis)
                {
                    action.Cancel(m_ServerCharacter);
                    m_Queue.RemoveAt(0);
                    TryReturnAction(action);
                }
            }
        }
    }
}