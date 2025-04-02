using System.Collections;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.GameplayObjects.Character.AI;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using Action = Unity.BossRoom.Gameplay.Actions.Action;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// Contains all NetworkVariables, RPCs and server-side logic of a character.
    /// This class was separated in two to keep client and server context self contained. This way you don't have to continuously ask yourself if code is running client or server side.
    /// </summary>
    /// <summary>
    /// 캐릭터의 모든 NetworkVariables, RPC 및 서버 측 로직을 포함합니다.
    /// 이 클래스는 클라이언트와 서버 컨텍스트를 자체적으로 유지하기 위해 두 개로 분리되었습니다. 
    /// 이렇게 하면 코드가 클라이언트 측인지 서버 측인지 계속 묻지 않아도 됩니다.
    /// </summary>
    [RequireComponent(typeof(NetworkHealthState),
            typeof(NetworkLifeState),
            typeof(NetworkAvatarGuidState))]
    public class ServerCharacter : NetworkBehaviour, ITargetable
    {
        [FormerlySerializedAs("m_ClientVisualization")]
        [SerializeField]
        ClientCharacter m_ClientCharacter;

        public ClientCharacter clientCharacter => m_ClientCharacter;

        [SerializeField]
        CharacterClass m_CharacterClass;

        public CharacterClass CharacterClass
        {
            get
            {
                if (m_CharacterClass == null)
                {
                    m_CharacterClass = m_State.RegisteredAvatar.CharacterClass;
                }

                return m_CharacterClass;
            }

            set => m_CharacterClass = value;
        }

        /// Indicates how the character's movement should be depicted.
        public NetworkVariable<MovementStatus> MovementStatus { get; } = new NetworkVariable<MovementStatus>();

        public NetworkVariable<ulong> HeldNetworkObject { get; } = new NetworkVariable<ulong>();

        /// <summary>
        /// Indicates whether this character is in "stealth mode" (invisible to monsters and other players).
        /// </summary>
        /// <summary>
        /// 이 캐릭터가 "은폐 모드"(몬스터와 다른 플레이어에게 보이지 않음) 상태인지를 나타냅니다.
        /// </summary>
        public NetworkVariable<bool> IsStealthy { get; } = new NetworkVariable<bool>();

        public NetworkHealthState NetHealthState { get; private set; }

        /// <summary>
        /// The active target of this character.
        /// </summary>
        /// <summary>
        /// 이 캐릭터의 활성 타겟입니다.
        /// </summary>
        public NetworkVariable<ulong> TargetId { get; } = new NetworkVariable<ulong>();

        /// <summary>
        /// Current HP. This value is populated at startup time from CharacterClass data.
        /// </summary>
        /// <summary>
        /// 현재 HP. 이 값은 CharacterClass 데이터에서 시작 시 채워집니다.
        /// </summary>
        public int HitPoints
        {
            get => NetHealthState.HitPoints.Value;
            private set => NetHealthState.HitPoints.Value = value;
        }

        public NetworkLifeState NetLifeState { get; private set; }

        /// <summary>
        /// Current LifeState. Only Players should enter the FAINTED state.
        /// </summary>
        /// <summary>
        /// 현재 LifeState. FAINTED 상태에는 오직 플레이어만 들어가야 합니다.
        /// </summary>
        public LifeState LifeState
        {
            get => NetLifeState.LifeState.Value;
            private set => NetLifeState.LifeState.Value = value;
        }

        /// <summary>
        /// Returns true if this Character is an NPC.
        /// </summary>
        /// <summary>
        /// 이 캐릭터가 NPC이면 true를 반환합니다.
        /// </summary>
        public bool IsNpc => CharacterClass.IsNpc;

        public bool IsValidTarget => LifeState != LifeState.Dead;

        /// <summary>
        /// Returns true if the Character is currently in a state where it can play actions, false otherwise.
        /// </summary>
        /// <summary>
        /// 이 캐릭터가 현재 행동을 수행할 수 있는 상태라면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
        /// </summary>
        public bool CanPerformActions => LifeState == LifeState.Alive;

        /// <summary>
        /// Character Type. This value is populated during character selection.
        /// </summary>
        /// <summary>
        /// 캐릭터 유형. 이 값은 캐릭터 선택 중에 채워집니다.
        /// </summary>
        public CharacterTypeEnum CharacterType => CharacterClass.CharacterType;

        private ServerActionPlayer m_ServerActionPlayer;

        /// <summary>
        /// The Character's ActionPlayer. This is mainly exposed for use by other Actions. In particular, users are discouraged from
        /// calling 'PlayAction' directly on this, as the ServerCharacter has certain game-level checks it performs in its own wrapper.
        /// </summary>
        /// <summary>
        /// 캐릭터의 ActionPlayer입니다. 주로 다른 행동에서 사용하기 위해 노출됩니다. 특히, 사용자는 'PlayAction'을 이 객체에서 직접 호출하지 않는 것이 좋습니다.
        /// ServerCharacter는 자체 래퍼에서 특정 게임 레벨 검사를 수행하기 때문입니다.
        /// </summary>
        public ServerActionPlayer ActionPlayer => m_ServerActionPlayer;

        [SerializeField]
        [Tooltip("If set to false, an NPC character will be denied its brain (won't attack or chase players)")]
        private bool m_BrainEnabled = true;

        [SerializeField]
        [Tooltip("Setting negative value disables destroying object after it is killed.")]
        private float m_KilledDestroyDelaySeconds = 3.0f;

        [SerializeField]
        [Tooltip("If set, the ServerCharacter will automatically play the StartingAction when it is created. ")]
        private Action m_StartingAction;


        [SerializeField]
        DamageReceiver m_DamageReceiver;

        [SerializeField]
        ServerCharacterMovement m_Movement;

        public ServerCharacterMovement Movement => m_Movement;

        [SerializeField]
        PhysicsWrapper m_PhysicsWrapper;

        public PhysicsWrapper physicsWrapper => m_PhysicsWrapper;

        [SerializeField]
        ServerAnimationHandler m_ServerAnimationHandler;

        public ServerAnimationHandler serverAnimationHandler => m_ServerAnimationHandler;

        private AIBrain m_AIBrain;
        NetworkAvatarGuidState m_State;

        void Awake()
        {
            m_ServerActionPlayer = new ServerActionPlayer(this);
            NetLifeState = GetComponent<NetworkLifeState>();
            NetHealthState = GetComponent<NetworkHealthState>();
            m_State = GetComponent<NetworkAvatarGuidState>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) { enabled = false; }
            else
            {
                NetLifeState.LifeState.OnValueChanged += OnLifeStateChanged;
                m_DamageReceiver.DamageReceived += ReceiveHP;
                m_DamageReceiver.CollisionEntered += CollisionEntered;

                if (IsNpc)
                {
                    m_AIBrain = new AIBrain(this, m_ServerActionPlayer);
                }

                if (m_StartingAction != null)
                {
                    var startingAction = new ActionRequestData() { ActionID = m_StartingAction.ActionID };
                    PlayAction(ref startingAction);
                }
                InitializeHitPoints();
            }
        }

        /// <summary>
        /// Called when the object is despawned from the network.
        /// </summary>
        /// <summary>
        /// 네트워크에서 객체가 제거될 때 호출됩니다.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            NetLifeState.LifeState.OnValueChanged -= OnLifeStateChanged;

            if (m_DamageReceiver)
            {
                m_DamageReceiver.DamageReceived -= ReceiveHP;
                m_DamageReceiver.CollisionEntered -= CollisionEntered;
            }
        }

        /// <summary>
        /// RPC to send inputs for this character from a client to a server.
        /// </summary>
        /// <param name="movementTarget">The position which this character should move towards.</param>
        /// <summary>
        /// 이 캐릭터의 입력을 클라이언트에서 서버로 보내는 RPC입니다.
        /// </summary>
        /// <param name="movementTarget">이 캐릭터가 이동해야 할 위치입니다.</param>
        [Rpc(SendTo.Server)]
        public void ServerSendCharacterInputRpc(Vector3 movementTarget)
        {
            if (LifeState == LifeState.Alive && !m_Movement.IsPerformingForcedMovement())
            {
                // if we're currently playing an interruptible action, interrupt it!
                if (m_ServerActionPlayer.GetActiveActionInfo(out ActionRequestData data))
                {
                    if (GameDataSource.Instance.GetActionPrototypeByID(data.ActionID).Config.ActionInterruptible)
                    {
                        m_ServerActionPlayer.ClearActions(false);
                    }
                }

                m_ServerActionPlayer.CancelRunningActionsByLogic(ActionLogic.Target, true); //clear target on move.
                m_Movement.SetMovementTarget(movementTarget);
            }
        }

        /// <summary>
        /// Client->Server RPC that sends a request to play an action.
        /// </summary>
        /// <param name="data">Data about which action to play and its associated details.</param>
        /// <summary>
        /// 클라이언트에서 서버로 액션을 실행하라는 요청을 보내는 RPC입니다.
        /// </summary>
        /// <param name="data">실행할 액션과 관련된 데이터입니다.</param>
        [Rpc(SendTo.Server)]
        public void ServerPlayActionRpc(ActionRequestData data)
        {
            ActionRequestData data1 = data;
            if (!GameDataSource.Instance.GetActionPrototypeByID(data1.ActionID).Config.IsFriendly)
            {
                // notify running actions that we're using a new attack. (e.g. so Stealth can cancel itself)
                ActionPlayer.OnGameplayActivity(Action.GameplayActivity.UsingAttackAction);
            }

            PlayAction(ref data1);
        }

        /// <summary>
        /// Called on server when the character's client decides they have stopped "charging up" an attack.
        /// </summary>
        /// <summary>
        /// 캐릭터의 클라이언트가 "충전 중"인 공격을 멈췄다고 결정했을 때 서버에서 호출됩니다.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ServerStopChargingUpRpc()
        {
            m_ServerActionPlayer.OnGameplayActivity(Action.GameplayActivity.StoppedChargingUp);
        }

        void InitializeHitPoints()
        {
            HitPoints = CharacterClass.BaseHP.Value;

            if (!IsNpc)
            {
                SessionPlayerData? sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(OwnerClientId);
                if (sessionPlayerData is { HasCharacterSpawned: true })
                {
                    HitPoints = sessionPlayerData.Value.CurrentHitPoints;
                    if (HitPoints <= 0)
                    {
                        LifeState = LifeState.Fainted;
                    }
                }
            }
        }

        /// <summary>
        /// Play a sequence of actions!
        /// </summary>
        /// <summary>
        /// 일련의 액션을 실행합니다!
        /// </summary>
        public void PlayAction(ref ActionRequestData action)
        {
            // the character needs to be alive in order to be able to play actions
            if (LifeState == LifeState.Alive && !m_Movement.IsPerformingForcedMovement())
            {
                if (action.CancelMovement)
                {
                    m_Movement.CancelMove();
                }

                m_ServerActionPlayer.PlayAction(ref action);
            }
        }

        void OnLifeStateChanged(LifeState prevLifeState, LifeState lifeState)
        {
            if (lifeState != LifeState.Alive)
            {
                m_ServerActionPlayer.ClearActions(true);
                m_Movement.CancelMove();
            }
        }

        IEnumerator KilledDestroyProcess()
        {
            yield return new WaitForSeconds(m_KilledDestroyDelaySeconds);

            if (NetworkObject != null)
            {
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>
        /// Receive an HP change from somewhere. Could be healing or damage.
        /// </summary>
        /// <param name="inflicter">Person dishing out this damage/healing. Can be null.</param>
        /// <param name="HP">The HP to receive. Positive value is healing. Negative is damage.</param>
        /// <summary>
        /// 어딘가에서 HP 변경을 수신합니다. 치유 또는 피해일 수 있습니다.
        /// </summary>
        /// <param name="inflicter">이 피해/치유를 주는 사람입니다. null일 수도 있습니다.</param>
        /// <param name="HP">수신할 HP입니다. 양수는 치유, 음수는 피해입니다.</param>
        void ReceiveHP(ServerCharacter inflicter, int HP)
        {
            // to our own effects, and modify the damage or healing as appropriate. But in this game, we just take it straight.
            if (HP > 0)
            {
                m_ServerActionPlayer.OnGameplayActivity(Action.GameplayActivity.Healed);
                float healingMod = m_ServerActionPlayer.GetBuffedValue(Action.BuffableValue.PercentHealingReceived);
                HP = (int)(HP * healingMod);
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Don't apply damage if god mode is on
                if (NetLifeState.IsGodMode.Value)
                {
                    return;
                }
#endif

                m_ServerActionPlayer.OnGameplayActivity(Action.GameplayActivity.AttackedByEnemy);
                float damageMod = m_ServerActionPlayer.GetBuffedValue(Action.BuffableValue.PercentDamageReceived);
                HP = (int)(HP * damageMod);

                serverAnimationHandler.NetworkAnimator.SetTrigger("HitReact1");
            }

            HitPoints = Mathf.Clamp(HitPoints + HP, 0, CharacterClass.BaseHP.Value);

            if (m_AIBrain != null)
            {
                // let the brain know about the modified amount of damage we received.
                m_AIBrain.ReceiveHP(inflicter, HP);
            }

            // we can't currently heal a dead character back to Alive state.
            // that's handled by a separate function.
            if (HitPoints <= 0)
            {
                if (IsNpc)
                {
                    if (m_KilledDestroyDelaySeconds >= 0.0f && LifeState != LifeState.Dead)
                    {
                        StartCoroutine(KilledDestroyProcess());
                    }

                    LifeState = LifeState.Dead;
                }
                else
                {
                    LifeState = LifeState.Fainted;
                }

                m_ServerActionPlayer.ClearActions(false);
            }
        }

        /// <summary>
        /// Determines a gameplay variable for this character. The value is determined
        /// by the character's active Actions.
        /// </summary>
        /// <param name="buffType"></param>
        /// <returns></returns>
        /// <summary>
        /// 이 캐릭터의 게임 플레이 변수 값을 결정합니다. 이 값은 캐릭터의 활성 액션에 의해 결정됩니다.
        /// </summary>
        /// <param name="buffType"></param>
        /// <returns></returns>
        public float GetBuffedValue(Action.BuffableValue buffType)
        {
            return m_ServerActionPlayer.GetBuffedValue(buffType);
        }

        /// <summary>
        /// Receive a Life State change that brings Fainted characters back to Alive state.
        /// </summary>
        /// <param name="inflicter">Person reviving the character.</param>
        /// <param name="HP">The HP to set to a newly revived character.</param>
        /// <summary>
        /// 기절한 캐릭터를 다시 살아나게 만드는 생명 상태 변경을 수신합니다.
        /// </summary>
        /// <param name="inflicter">캐릭터를 부활시키는 사람입니다.</param>
        /// <param name="HP">새로 부활한 캐릭터의 HP를 설정합니다.</param>
        public void Revive(ServerCharacter inflicter, int HP)
        {
            if (LifeState == LifeState.Fainted)
            {
                HitPoints = Mathf.Clamp(HP, 0, CharacterClass.BaseHP.Value);
                NetLifeState.LifeState.Value = LifeState.Alive;
            }
        }

        void Update()
        {
            m_ServerActionPlayer.OnUpdate();
            if (m_AIBrain != null && LifeState == LifeState.Alive && m_BrainEnabled)
            {
                m_AIBrain.Update();
            }
        }

        void CollisionEntered(Collision collision)
        {
            if (m_ServerActionPlayer != null)
            {
                m_ServerActionPlayer.CollisionEntered(collision);
            }
        }

        /// <summary>
        /// This character's AIBrain. Will be null if this is not an NPC.
        /// </summary>
        /// <summary>
        /// 이 캐릭터의 AIBrain입니다. NPC가 아닌 경우 null이 됩니다.
        /// </summary>
        public AIBrain AIBrain { get { return m_AIBrain; } }
    }
}