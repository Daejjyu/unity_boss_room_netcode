using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.VisualEffects;
using Unity.Netcode;
using UnityEngine;
using BlockingMode = Unity.BossRoom.Gameplay.Actions.BlockingModeType;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// The abstract parent class that all Actions derive from.
    /// </summary>
    /// <summary>
    /// 모든 액션이 상속하는 추상 부모 클래스입니다.
    /// </summary>
    /// <remarks>
    /// The Action System is a generalized mechanism for Characters to "do stuff" in a networked way. Actions
    /// include everything from your basic character attack, to a fancy skill like the Archer's Volley Shot, but also
    /// include more mundane things like pulling a lever.
    /// For every ActionLogic enum, there will be one specialization of this class.
    /// There is only ever one active Action (also called the "blocking" action) at a time on a character, but multiple
    /// Actions may exist at once, with subsequent Actions pending behind the currently active one, and possibly
    /// "non-blocking" actions running in the background. See ActionPlayer.cs
    ///
    /// The flow for Actions is:
    /// Initially: Start()
    /// Every frame: ShouldBecomeNonBlocking() (only if Action is blocking), then Update()
    /// On shutdown: End() or Cancel()
    /// After shutdown: ChainIntoNewAction()    (only if Action was blocking, and only if End() was called, not Cancel())
    ///
    /// Note also that if Start() returns false, no other functions are called on the Action, not even End().
    ///
    /// This Action system has not been designed to be generic and extractable to be reused in other projects - keep that in mind when reading through this code.
    /// A better action system would need to be more accessible and customizable by game designers and allow more design emergence. It'd have ways to define smaller atomic action steps and have a generic way to define and access character data. It would also need to be more performant, as actions would scale with your number of characters and concurrent actions.
    /// </remarks>
    /// <remarks>
    /// 액션 시스템은 캐릭터가 네트워크를 통해 "작업을 수행"할 수 있도록 하는 일반화된 메커니즘입니다.
    /// 액션에는 기본적인 캐릭터 공격부터 아처의 화살비와 같은 화려한 기술, 그리고 레버를 당기는 것과 같은 더 평범한 작업까지 포함됩니다.
    /// 각 ActionLogic 열거형마다 이 클래스의 특수화가 존재합니다.
    /// 한 번에 캐릭터에는 항상 하나의 활성화된 액션(또는 "차단" 액션)만 존재하지만, 여러 액션이 동시에 존재할 수 있으며, 현재 활성화된 액션 뒤에 대기 중인 액션이 있고, "비차단" 액션은 백그라운드에서 실행될 수 있습니다. ActionPlayer.cs를 참조하세요.
    ///
    /// 액션 흐름은 다음과 같습니다:
    /// 처음에: Start()
    /// 매 프레임: ShouldBecomeNonBlocking() (액션이 차단 중일 경우에만), 그 다음에 Update()
    /// 종료 시: End() 또는 Cancel()
    /// 종료 후: ChainIntoNewAction() (단, 액션이 차단 중일 경우에만, End()가 호출되었을 때만, Cancel()이 아닌 경우)
    ///
    /// 또한 Start()가 false를 반환하면, 액션에 대해 다른 함수는 호출되지 않으며, End()조차도 호출되지 않습니다.
    ///
    /// 이 액션 시스템은 다른 프로젝트에서 재사용할 수 있도록 일반화되거나 추출될 수 있도록 설계되지 않았습니다. 이 코드를 읽을 때 이를 염두에 두세요.
    /// 더 나은 액션 시스템은 게임 디자이너들이 더 접근 가능하고 맞춤화할 수 있어야 하며, 더 많은 디자인 창출을 허용해야 합니다. 더 작은 원자적 액션 단계를 정의하고, 캐릭터 데이터를 정의하고 접근하는 일반적인 방법이 필요할 것입니다. 또한, 액션은 캐릭터 수와 동시 액션 수에 따라 확장되므로 성능이 더 중요해져야 합니다.
    /// </remarks>

    public abstract class Action : ScriptableObject
    {
        /// <summary>
        /// An index into the GameDataSource array of action prototypes. Set at runtime by GameDataSource class.  If action is not itself a prototype - will contain the action id of the prototype reference.
        /// This field is used to identify actions in a way that can be sent over the network.
        /// </summary>
        /// <summary>
        /// GameDataSource의 액션 프로토타입 배열에서의 인덱스입니다. 실행 시간에 GameDataSource 클래스에 의해 설정됩니다. 액션 자체가 프로토타입이 아니면 프로토타입 참조의 액션 ID가 포함됩니다.
        /// 이 필드는 네트워크를 통해 전송될 수 있도록 액션을 식별하는 데 사용됩니다.
        /// </summary>
        [NonSerialized]
        public ActionID ActionID;

        /// <summary>
        /// The default hit react animation; several different ActionFXs make use of this.
        /// </summary>
        /// <summary>
        /// 기본적인 피격 반응 애니메이션; 여러 ActionFX가 이를 사용합니다.
        /// </summary>
        public const string k_DefaultHitReact = "HitReact1";


        protected ActionRequestData m_Data;

        /// <summary>
        /// Time when this Action was started (from Time.time) in seconds. Set by the ActionPlayer or ActionVisualization.
        /// </summary>
        /// <summary>
        /// 이 액션이 시작된 시간 (Time.time 기준으로 초 단위). ActionPlayer 또는 ActionVisualization에 의해 설정됩니다.
        /// </summary>
        public float TimeStarted { get; set; }

        /// <summary>
        /// How long the Action has been running (since its Start was called)--in seconds, measured via Time.time.
        /// </summary>
        /// <summary>
        /// 이 액션이 얼마나 오래 실행되었는지 (Start가 호출된 이후) -- 초 단위로, Time.time을 사용하여 측정됩니다.
        /// </summary>
        public float TimeRunning { get { return (Time.time - TimeStarted); } }

        /// <summary>
        /// RequestData we were instantiated with. Value should be treated as readonly.
        /// </summary>
        /// <summary>
        /// 우리가 초기화할 때 사용된 RequestData. 값은 읽기 전용으로 처리되어야 합니다.
        /// </summary>
        public ref ActionRequestData Data => ref m_Data;

        /// <summary>
        /// Data Description for this action.
        /// </summary>
        /// <summary>
        /// 이 액션에 대한 데이터 설명.
        /// </summary>
        public ActionConfig Config;

        public bool IsChaseAction => ActionID == GameDataSource.Instance.GeneralChaseActionPrototype.ActionID;
        public bool IsStunAction => ActionID == GameDataSource.Instance.StunnedActionPrototype.ActionID;
        public bool IsGeneralTargetAction => ActionID == GameDataSource.Instance.GeneralTargetActionPrototype.ActionID;

        /// <summary>
        /// Constructor. The "data" parameter should not be retained after passing in to this method, because we take ownership of its internal memory.
        /// Needs to be called by the ActionFactory.
        /// </summary>
        /// <summary>
        /// 생성자. "data" 파라미터는 이 메서드에 전달된 후 보관되지 않아야 합니다. 우리는 그 내부 메모리에 대한 소유권을 가집니다.
        /// ActionFactory에 의해 호출되어야 합니다.
        /// </summary>
        public void Initialize(ref ActionRequestData data)
        {
            m_Data = data;
            ActionID = data.ActionID;
        }

        /// <summary>
        /// This function resets the action before returning it to the pool
        /// </summary>
        /// <summary>
        /// 이 함수는 액션을 풀에 반환하기 전에 리셋합니다.
        /// </summary>
        public virtual void Reset()
        {
            m_Data = default;
            ActionID = default;
            TimeStarted = 0;
        }

        /// <summary>
        /// Called when the Action starts actually playing (which may be after it is created, because of queueing).
        /// </summary>
        /// <returns>false if the action decided it doesn't want to run after all, true otherwise. </returns>
        /// <summary>
        /// 액션이 실제로 실행되기 시작할 때 호출됩니다 (이는 생성된 후, 큐에 의해 실행될 수 있습니다).
        /// </summary>
        public abstract bool OnStart(ServerCharacter serverCharacter);

        /// <summary>
        /// Called each frame while the action is running.
        /// </summary>
        /// <returns>true to keep running, false to stop. The Action will stop by default when its duration expires, if it has a duration set. </returns>
        /// <summary>
        /// 액션이 실행되는 동안 매 프레임마다 호출됩니다.
        /// </summary>
        public abstract bool OnUpdate(ServerCharacter clientCharacter);

        /// <summary>
        /// Called each frame (before OnUpdate()) for the active ("blocking") Action, asking if it should become a background Action.
        /// </summary>
        /// <returns>true to become a non-blocking Action, false to remain a blocking Action</returns>
        /// <summary>
        /// 활성("블로킹") 액션에 대해 매 프레임마다 호출됩니다. 이 액션이 백그라운드 액션으로 바뀌어야 하는지 묻습니다.
        /// </summary>
        public virtual bool ShouldBecomeNonBlocking()
        {
            return Config.BlockingMode == BlockingModeType.OnlyDuringExecTime ? TimeRunning >= Config.ExecTimeSeconds : false;
        }

        /// <summary>
        /// Called when the Action ends naturally. By default just calls Cancel()
        /// </summary>
        /// <summary>
        /// 액션이 자연스럽게 끝날 때 호출됩니다. 기본적으로는 Cancel()을 호출합니다.
        /// </summary>
        public virtual void End(ServerCharacter serverCharacter)
        {
            Cancel(serverCharacter);
        }

        /// <summary>
        /// This will get called when the Action gets canceled. The Action should clean up any ongoing effects at this point.
        /// (e.g. an Action that involves moving should cancel the current active move).
        /// </summary>
        /// <summary>
        /// 액션이 취소될 때 호출됩니다. 이 시점에서 액션은 진행 중인 효과를 정리해야 합니다.
        /// (예: 이동이 포함된 액션은 현재 활성 이동을 취소해야 합니다).
        /// </summary>
        public virtual void Cancel(ServerCharacter serverCharacter) { }

        /// <summary>
        /// Called *AFTER* End(). At this point, the Action has ended, meaning its Update() etc. functions will never be
        /// called again. If the Action wants to immediately segue into a different Action, it can do so here. The new
        /// Action will take effect in the next Update().
        ///
        /// Note that this is not called on prematurely cancelled Actions, only on ones that have their End() called.
        /// </summary>
        /// <param name="newAction">the new Action to immediately transition to</param>
        /// <returns>true if there's a new action, false otherwise</returns>
        /// <summary>
        /// End() 이후에 호출됩니다. 이 시점에서 액션은 종료되었으므로 Update() 등의 함수가 다시 호출되지 않습니다.
        /// 만약 액션이 즉시 다른 액션으로 전환되기를 원하면 이곳에서 그렇게 할 수 있습니다. 새 액션은 다음 Update()에서 적용됩니다.
        /// </summary>
        public virtual bool ChainIntoNewAction(ref ActionRequestData newAction) { return false; }

        /// <summary>
        /// Called on the active ("blocking") Action when this character collides with another.
        /// </summary>
        /// <param name="serverCharacter"></param>
        /// <param name="collision"></param>
        /// <summary>
        /// 이 캐릭터가 다른 캐릭터와 충돌할 때 활성("블로킹") 액션에서 호출됩니다.
        /// </summary>
        public virtual void CollisionEntered(ServerCharacter serverCharacter, Collision collision) { }

        public enum BuffableValue
        {
            PercentHealingReceived, // unbuffed value is 1.0. Reducing to 0 would mean "no healing". 2 would mean "double healing"
            PercentDamageReceived,  // unbuffed value is 1.0. Reducing to 0 would mean "no damage". 2 would mean "double damage"
            ChanceToStunTramplers,  // unbuffed value is 0. If > 0, is the chance that someone trampling this character becomes stunned
        }

        /// <summary>
        /// Called on all active Actions to give them a chance to alter the outcome of a gameplay calculation. Note
        /// that this is used for both "buffs" (positive gameplay benefits) and "debuffs" (gameplay penalties).
        /// </summary>
        /// <remarks>
        /// In a more complex game with lots of buffs and debuffs, this function might be replaced by a separate
        /// BuffRegistry component. This would let you add fancier features, such as defining which effects
        /// "stack" with other ones, and could provide a UI that lists which are affecting each character
        /// and for how long.
        /// </remarks>
        /// <param name="buffType">Which gameplay variable being calculated</param>
        /// <param name="orgValue">The original ("un-buffed") value</param>
        /// <param name="buffedValue">The final ("buffed") value</param>
        /// <summary>
        /// 모든 활성 액션에서 호출되어 게임 플레이 계산 결과를 변경할 기회를 제공합니다. 
        /// 이것은 "버프"(긍정적인 게임 플레이 혜택)와 "디버프"(게임 플레이 페널티) 모두에 사용됩니다.
        /// </summary>
        public virtual void BuffValue(BuffableValue buffType, ref float buffedValue) { }

        /// <summary>
        /// Static utility function that returns the default ("un-buffed") value for a BuffableValue.
        /// (This just ensures that there's one place for all these constants.)
        /// </summary>
        /// <summary>
        /// BuffableValue에 대한 기본("버프되지 않은") 값을 반환하는 정적 유틸리티 함수입니다.
        /// (이것은 모든 상수에 대해 하나의 위치에서 관리되도록 보장합니다.)
        /// </summary>
        public static float GetUnbuffedValue(Action.BuffableValue buffType)
        {
            switch (buffType)
            {
                case BuffableValue.PercentDamageReceived: return 1;
                case BuffableValue.PercentHealingReceived: return 1;
                case BuffableValue.ChanceToStunTramplers: return 0;
                default: throw new System.Exception($"Unknown buff type {buffType}");
            }
        }

        public enum GameplayActivity
        {
            AttackedByEnemy,
            Healed,
            StoppedChargingUp,
            UsingAttackAction, // called immediately before we perform the attack Action
        }

        /// <summary>
        /// Called on active Actions to let them know when a notable gameplay event happens.
        /// </summary>
        /// <remarks>
        /// When a GameplayActivity of AttackedByEnemy or Healed happens, OnGameplayAction() is called BEFORE BuffValue() is called.
        /// </remarks>
        /// <param name="serverCharacter"></param>
        /// <param name="activityType"></param>
        /// <summary>
        /// 활성 액션에 대해 중요한 게임 플레이 이벤트가 발생했을 때 이를 알려주는 함수입니다.
        /// </summary>
        public virtual void OnGameplayActivity(ServerCharacter serverCharacter, GameplayActivity activityType) { }

        /// <summary>
        /// True if this actionFX began running immediately, prior to getting a confirmation from the server.
        /// </summary>
        /// <summary>
        /// 이 actionFX가 서버로부터 확인을 받기 전에 즉시 실행되었으면 true입니다.
        /// </summary>
        public bool AnticipatedClient { get; protected set; }

        /// <summary>
        /// Starts the ActionFX. Derived classes may return false if they wish to end immediately without their Update being called.
        /// </summary>
        /// <remarks>
        /// Derived class should be sure to call base.OnStart() in their implementation, but note that this resets "Anticipated" to false.
        /// </remarks>
        /// <returns>true to play, false to be immediately cleaned up.</returns>
        /// <summary>
        /// ActionFX를 시작합니다. 파생 클래스는 업데이트가 호출되지 않고 즉시 끝내기를 원할 경우 false를 반환할 수 있습니다.
        /// </summary>
        public virtual bool OnStartClient(ClientCharacter clientCharacter)
        {
            AnticipatedClient = false; // 실제로 시작되면 더 이상 예상된 액션이 아닙니다.
            TimeStarted = UnityEngine.Time.time;
            return true;
        }

        public virtual bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            return ActionConclusion.Continue;
        }
        /// <summary>
        /// End는 ActionFX가 완료될 때 항상 호출됩니다. 이는 파생 클래스가 종료 로직을 추가하기 좋은 위치입니다.
        /// </summary>
        public virtual void EndClient(ClientCharacter clientCharacter)
        {
            CancelClient(clientCharacter);
        }

        /// <summary>
        /// ActionFX가 예기치 않게 중단되었을 때 호출됩니다. 
        /// </summary>
        public virtual void CancelClient(ClientCharacter clientCharacter) { }

        /// <summary>
        /// 이 ActionFX는 클라이언트에서 예상하여 생성해야 하는지 여부를 반환합니다.
        /// </summary>
        /// <summary>
        /// Should this ActionFX be created anticipatively on the owning client?
        /// </summary>
        /// <param name="clientCharacter">The ActionVisualization that would be playing this ActionFX.</param>
        /// <param name="clientCharacter">이 ActionFX를 실행하는 ActionVisualization입니다.</param>
        /// <param name="data">The request being sent to the server</param>
        /// <param name="data">서버로 전송되는 요청</param>
        /// <returns>If true ActionVisualization should pre-emptively create the ActionFX on the owning client, before hearing back from the server.</returns>
        /// <returns>만약 true라면, ActionVisualization은 서버의 응답을 받기 전에 소유 클라이언트에서 ActionFX를 미리 생성해야 합니다.</returns>
        public static bool ShouldClientAnticipate(ClientCharacter clientCharacter, ref ActionRequestData data)
        {
            if (!clientCharacter.CanPerformActions) { return false; }

            var actionDescription = GameDataSource.Instance.GetActionPrototypeByID(data.ActionID).Config;

            //for actions with ShouldClose set, we check our range locally. If we are out of range, we shouldn't anticipate, as we will
            //need to execute a ChaseAction (synthesized on the server) prior to actually playing the skill.
            //ShouldClose가 설정된 액션의 경우, 우리는 범위를 로컬에서 확인합니다. 범위를 벗어나면 예상하지 않아야 합니다. 실제 스킬을 실행하기 전에 서버에서 합성된 ChaseAction을 실행해야 합니다.
            bool isTargetEligible = true;
            if (data.ShouldClose == true)
            {
                ulong targetId = (data.TargetIds != null && data.TargetIds.Length > 0) ? data.TargetIds[0] : 0;
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject networkObject))
                {
                    float rangeSquared = actionDescription.Range * actionDescription.Range;
                    isTargetEligible = (networkObject.transform.position - clientCharacter.transform.position).sqrMagnitude < rangeSquared;
                }
            }

            //at present all Actionts anticipate except for the Target action, which runs a single instance on the client and is
            //responsible for action anticipation on its own.
            //현재 모든 액션은 예상되지만, Target 액션은 클라이언트에서 하나의 인스턴스를 실행하며, 그 자체로 액션 예측을 담당합니다.
            return isTargetEligible && actionDescription.Logic != ActionLogic.Target;
        }

        /// <summary>
        /// Called when the visualization receives an animation event.
        /// </summary>
        /// <summary>
        /// 시각화가 애니메이션 이벤트를 받을 때 호출됩니다.
        /// </summary>
        public virtual void OnAnimEventClient(ClientCharacter clientCharacter, string id) { }

        /// <summary>
        /// Called when this action has finished "charging up". (Which is only meaningful for a
        /// few types of actions -- it is not called for other actions.)
        /// </summary>
        /// <param name="finalChargeUpPercentage"></param>
        /// <param name="finalChargeUpPercentage">최종 충전 비율</param>
        public virtual void OnStoppedChargingUpClient(ClientCharacter clientCharacter, float finalChargeUpPercentage) { }

        /// <summary>
        /// Utility function that instantiates all the graphics in the Spawns list.
        /// If parentToOrigin is true, the new graphics are parented to the origin Transform.
        /// If false, they are positioned/oriented the same way but are not parented.
        /// </summary>
        /// <summary>
        /// Spawns 목록에 있는 모든 그래픽을 인스턴스화하는 유틸리티 함수입니다.
        /// parentToOrigin이 true이면 새 그래픽이 원본 Transform에 부모로 설정됩니다.
        /// false이면 동일한 방식으로 위치/방향이 설정되지만 부모가 설정되지 않습니다.
        /// </summary>
        protected List<SpecialFXGraphic> InstantiateSpecialFXGraphics(Transform origin, bool parentToOrigin)
        {
            var returnList = new List<SpecialFXGraphic>();
            foreach (var prefab in Config.Spawns)
            {
                if (!prefab) { continue; } // skip blank entries in our prefab list
                returnList.Add(InstantiateSpecialFXGraphic(prefab, origin, parentToOrigin));
            }
            return returnList;
        }

        /// <summary>
        /// Utility function that instantiates one of the graphics from the Spawns list.
        /// If parentToOrigin is true, the new graphics are parented to the origin Transform.
        /// If false, they are positioned/oriented the same way but are not parented.
        /// </summary>
        /// <summary>
        /// Spawns 목록에서 하나의 그래픽을 인스턴스화하는 유틸리티 함수입니다.
        /// parentToOrigin이 true이면 새 그래픽이 원본 Transform에 부모로 설정됩니다.
        /// false이면 동일한 방식으로 위치/방향이 설정되지만 부모가 설정되지 않습니다.
        /// </summary>
        protected SpecialFXGraphic InstantiateSpecialFXGraphic(GameObject prefab, Transform origin, bool parentToOrigin)
        {
            if (prefab.GetComponent<SpecialFXGraphic>() == null)
            {
                throw new System.Exception($"One of the Spawns on action {this.name} does not have a SpecialFXGraphic component and can't be instantiated!");
            }
            var graphicsGO = GameObject.Instantiate(prefab, origin.transform.position, origin.transform.rotation, (parentToOrigin ? origin.transform : null));
            return graphicsGO.GetComponent<SpecialFXGraphic>();
        }

        /// <summary>
        /// Called when the action is being "anticipated" on the client. For example, if you are the owner of a tank and you swing your hammer,
        /// you get this call immediately on the client, before the server round-trip.
        /// Overriders should always call the base class in their implementation!
        /// </summary>
        /// <summary>
        /// 클라이언트에서 액션이 "예상"될 때 호출됩니다. 예를 들어, 당신이 탱크의 소유자이고 망치를 휘두를 때,
        /// 서버 왕복 전 바로 클라이언트에서 이 호출이 이루어집니다.
        /// 오버라이더는 항상 자신의 구현에서 기본 클래스를 호출해야 합니다!
        /// </summary>
        public virtual void AnticipateActionClient(ClientCharacter clientCharacter)
        {
            AnticipatedClient = true;
            TimeStarted = UnityEngine.Time.time;

            if (!string.IsNullOrEmpty(Config.AnimAnticipation))
            {
                clientCharacter.OurAnimator.SetTrigger(Config.AnimAnticipation);
            }
        }
    }
}