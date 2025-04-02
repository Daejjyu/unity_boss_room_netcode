using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// This is a companion class to ClientCharacter that is specifically responsible for visualizing Actions. Action visualizations have lifetimes
    /// and ongoing state, making this class closely analogous in spirit to the Unity.Multiplayer.Samples.BossRoom.Actions.ServerActionPlayer class.
    /// </summary>
    /// <summary>
    /// 이 클래스는 ClientCharacter의 동반자로, Actions의 시각화를 담당합니다. Action 시각화는 수명이 있고 지속적인 상태를 가지므로,
    /// 이 클래스는 Unity.Multiplayer.Samples.BossRoom.Actions.ServerActionPlayer 클래스와 유사한 역할을 합니다.
    /// </summary>
    public sealed class ClientActionPlayer
    {
        private List<Action> m_PlayingActions = new List<Action>();

        /// <summary>
        /// Don't let anticipated actionFXs persist longer than this. This is a safeguard against scenarios
        /// where we never get a confirmed action for an action we anticipated.
        /// </summary>
        /// <summary>
        /// 예상된 actionFX가 이보다 더 오래 지속되지 않도록 합니다. 이는 예상했던 액션에 대해 확인된 액션을 얻지 못하는 시나리오를
        /// 방지하기 위한 안전장치입니다.
        /// </summary>
        private const float k_AnticipationTimeoutSeconds = 1;

        public ClientCharacter ClientCharacter { get; private set; }

        public ClientActionPlayer(ClientCharacter clientCharacter)
        {
            ClientCharacter = clientCharacter;
        }

        public void OnUpdate()
        {
            //do a reverse-walk so we can safely remove inside the loop.
            // 루프 내에서 안전하게 제거할 수 있도록 역순으로 순회합니다.
            for (int i = m_PlayingActions.Count - 1; i >= 0; --i)
            {
                var action = m_PlayingActions[i];
                bool keepGoing = action.AnticipatedClient || action.OnUpdateClient(ClientCharacter); // only call OnUpdate() on actions that are past anticipation
                bool expirable = action.Config.DurationSeconds > 0f; //non-positive value is a sentinel indicating the duration is indefinite.
                bool timeExpired = expirable && action.TimeRunning >= action.Config.DurationSeconds;
                bool timedOut = action.AnticipatedClient && action.TimeRunning >= k_AnticipationTimeoutSeconds;
                if (!keepGoing || timeExpired || timedOut)
                {
                    if (timedOut) { action.CancelClient(ClientCharacter); } //an anticipated action that timed out shouldn't get its End called. It is canceled instead.
                    else { action.EndClient(ClientCharacter); }

                    m_PlayingActions.RemoveAt(i);
                    ActionFactory.ReturnAction(action);
                }
            }
        }

        //helper wrapper for a FindIndex call on m_PlayingActions.
        // m_PlayingActions에서 FindIndex 호출을 위한 도우미 래퍼.
        private int FindAction(ActionID actionID, bool anticipatedOnly)
        {
            return m_PlayingActions.FindIndex(a => a.ActionID == actionID && (!anticipatedOnly || a.AnticipatedClient));
        }

        public void OnAnimEvent(string id)
        {
            foreach (var actionFX in m_PlayingActions)
            {
                actionFX.OnAnimEventClient(ClientCharacter, id);
            }
        }

        public void OnStoppedChargingUp(float finalChargeUpPercentage)
        {
            foreach (var actionFX in m_PlayingActions)
            {
                actionFX.OnStoppedChargingUpClient(ClientCharacter, finalChargeUpPercentage);
            }
        }

        /// <summary>
        /// Called on the client that owns the Character when the player triggers an action. This allows actions to immediately start playing feedback.
        /// </summary>
        /// <remarks>
        ///
        /// What is Action Anticipation and what problem does it solve? In short, it lets Actions run logic the moment the input event that triggers them
        /// is detected on the local client. The purpose of this is to help mask latency. Because this demo is server authoritative, the default behavior is
        /// to only see feedback for your input after a server-client roundtrip. Somewhere over 200ms of round-trip latency, this starts to feel oppressively sluggish.
        /// To combat this, you can play visual effects immediately. For example, MeleeActionFX plays both its weapon swing and applies a hit react to the target,
        /// without waiting to hear from the server. This can lead to discrepancies when the server doesn't think the target was hit, but on the net, will feel
        /// more responsive.
        ///
        /// An important concept of Action Anticipation is that it is opportunistic--it doesn't make any strong guarantees. You don't get an anticipated
        /// action animation if you are already animating in some way, as one example. Another complexity is that you don't know if the server will actually
        /// let you play all the actions that you've requested--some may get thrown away, e.g. because you have too many actions in your queue. What this means
        /// is that Anticipated Actions (actions that have been constructed but not started) won't match up perfectly with actual approved delivered actions from
        /// the server. For that reason, it must always be fine to receive PlayAction and not have an anticipated action already started (this is true for playback
        /// Characters belonging to the server and other characters anyway). It also means we need to handle the case where we created an Anticipated Action, but
        /// never got a confirmation--actions like that need to eventually get discarded.
        ///
        /// Another important aspect of Anticipated Actions is that they are an "opt-in" system. You must call base.Start in your Start implementation, but other than
        /// that, if you don't have a good way to implement an Anticipation for your action, you don't have to do anything. In this case, that action will play
        /// "normally" (with visual feedback starting when the server's action broadcast reaches the client). Every action type will have its own particular set of
        /// problems to solve to sell the anticipation effect. For example, in this demo, the mage base attack (FXProjectileTargetedActionFX) just plays the attack animation
        /// anticipatively, but it could be revised to create and drive the mage bolt effect as well--leaving only damage to arrive in true server time.
        ///
        /// How to implement your own Anticipation logic:
        ///   1. Isolate the visual feedback you want play anticipatively in a private helper method on your ActionFX, like "PlayAttackAnim".
        ///   2. Override ActionFX.AnticipateAction. Be sure to call base.AnticipateAction, as well as play your visual logic (like PlayAttackAnim).
        ///   3. In your Start method, be sure to call base.Start (note that this will reset the "Anticipated" field to false).
        ///   4. In Start, check if the action was Anticipated. If NOT, then play call your PlayAttackAnim method.
        ///
        /// </remarks>
        /// <remarks>
        /// 액션 예상(Anticipation)이란 무엇이며 어떤 문제를 해결하는가? 간단히 말하면, 
        /// 액션을 실행시키는 입력 이벤트가 로컬 클라이언트에서
        /// 감지되는 순간 액션의 로직이 실행되도록 합니다. 이는 지연(latency)을 마스킹하는 데 도움을 주기 위한 것입니다. 
        /// 이 데모는 서버가 권한을 가지므로,
        /// 기본 동작은 서버-클라이언트 왕복 후에야 입력에 대한 피드백을 보는 것입니다. 
        /// 왕복 지연이 200ms를 넘으면, 이는 매우 느리게 느껴집니다.
        /// 이를 해결하기 위해, 시각 효과를 즉시 재생할 수 있습니다. 
        /// 예를 들어, MeleeActionFX는 서버의 응답을 기다리지 않고 무기 휘두르기와 타격 반응을
        /// 모두 실행합니다. 이로 인해 서버가 목표물이 맞지 않았다고 생각할 수 있지만,
        ///  네트워크 상에서는 더 반응성이 좋아 보일 수 있습니다.
        ///
        /// 액션 예상은 기회주의적입니다—강력한 보장을 하지 않습니다. 예를 들어, 
        /// 이미 애니메이션이 실행 중이면 예상된 액션 애니메이션을 얻을 수 없습니다.
        /// 또 다른 복잡성은 서버가 실제로 요청된 모든 액션을 실행하게 할지 알 수 없다는 점입니다. 
        /// 일부 액션은 버려질 수 있습니다(예: 대기열에 너무 많은 액션이 있으면).
        /// 예상된 액션(구성되었지만 시작되지 않은 액션)은 서버에서 승인된 실제 액션과 
        /// 완벽하게 일치하지 않을 수 있습니다. 따라서 PlayAction을 수신하고
        /// 예상된 액션이 이미 시작되지 않은 상태여도 괜찮습니다(서버와 다른 캐릭터에 대해 해당됩니다).
        ///
        /// 예상된 액션의 또 다른 중요한 측면은 "선택적(opt-in)" 시스템이라는 것입니다. 
        /// Start 구현에서 base.Start를 호출해야 하지만, 
        /// 예상 효과를 구현할 방법이 없다면 아무 것도 할 필요는 없습니다. 
        /// 이 경우 해당 액션은 "정상적으로" 재생됩니다(서버의 액션 방송이
        /// 클라이언트에 도달할 때 시각적 피드백이 시작됩니다). 각 액션 유형은 고유한 문제 세트를 해결해야 합니다.
        ///
        /// 자신의 예상 로직을 구현하는 방법:
        ///   1. ActionFX에서 "PlayAttackAnim"과 같은 private helper 메서드로 예상되는 시각적 피드백을 분리합니다.
        ///   2. ActionFX.AnticipateAction을 재정의합니다. 
        /// base.AnticipateAction을 호출하고, 시각적 로직을 실행합니다(PlayAttackAnim과 같은).
        ///   3. Start 메서드에서 base.Start를 호출합니다(이렇게 하면 "Anticipated" 필드가 false로 리셋됩니다).
        ///   4. Start에서 액션이 예상되었는지 확인합니다. 만약 그렇지 않다면, PlayAttackAnim 메서드를 호출합니다.
        ///
        /// </remarks>
        /// <param name="data">The Action that is being requested.</param>
        public void AnticipateAction(ref ActionRequestData data)
        {
            if (!ClientCharacter.IsAnimating() && Action.ShouldClientAnticipate(ClientCharacter, ref data))
            {
                var actionFX = ActionFactory.CreateActionFromData(ref data);
                actionFX.AnticipateActionClient(ClientCharacter);
                m_PlayingActions.Add(actionFX);
            }
        }

        public void PlayAction(ref ActionRequestData data)
        {
            var anticipatedActionIndex = FindAction(data.ActionID, true);

            var actionFX = anticipatedActionIndex >= 0 ? m_PlayingActions[anticipatedActionIndex] : ActionFactory.CreateActionFromData(ref data);
            if (actionFX.OnStartClient(ClientCharacter))
            {
                if (anticipatedActionIndex < 0)
                {
                    m_PlayingActions.Add(actionFX);
                }
                //otherwise just let the action sit in it's existing slot
            }
            else if (anticipatedActionIndex >= 0)
            {
                var removedAction = m_PlayingActions[anticipatedActionIndex];
                m_PlayingActions.RemoveAt(anticipatedActionIndex);
                ActionFactory.ReturnAction(removedAction);
            }
        }

        /// <summary>
        /// Cancels all playing ActionFX.
        /// </summary>
        /// <summary>
        /// 실행 중인 모든 ActionFX를 취소합니다.
        /// </summary>
        public void CancelAllActions()
        {
            foreach (var action in m_PlayingActions)
            {
                action.CancelClient(ClientCharacter);
                ActionFactory.ReturnAction(action);
            }
            m_PlayingActions.Clear();
        }

        public void CancelAllActionsWithSamePrototypeID(ActionID actionID)
        {
            for (int i = m_PlayingActions.Count - 1; i >= 0; --i)
            {
                if (m_PlayingActions[i].ActionID == actionID)
                {
                    var action = m_PlayingActions[i];
                    action.CancelClient(ClientCharacter);
                    m_PlayingActions.RemoveAt(i);
                    ActionFactory.ReturnAction(action);
                }
            }
        }
    }
}
