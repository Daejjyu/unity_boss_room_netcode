using System;
using Unity.BossRoom.CameraUtils;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.UserInput;
using Unity.BossRoom.Utils;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// <see cref="ClientCharacter"/> is responsible for displaying a character on the client's screen based on state information sent by the server.
    /// </summary>
    /// <summary>
    /// <see cref="ClientCharacter"/>는 서버에서 전송된 상태 정보를 기반으로 클라이언트 화면에 캐릭터를 표시하는 역할을 합니다.
    /// </summary>
    public class ClientCharacter : NetworkBehaviour
    {
        [SerializeField]
        Animator m_ClientVisualsAnimator;

        [SerializeField]
        VisualizationConfiguration m_VisualizationConfiguration;

        public Animator OurAnimator => m_ClientVisualsAnimator;
        public GameObject TargetReticulePrefab => m_VisualizationConfiguration.TargetReticule;
        public Material ReticuleHostileMat => m_VisualizationConfiguration.ReticuleHostileMat;
        public Material ReticuleFriendlyMat => m_VisualizationConfiguration.ReticuleFriendlyMat;

        CharacterSwap m_CharacterSwapper;
        public CharacterSwap CharacterSwap => m_CharacterSwapper;
        public bool CanPerformActions => m_ServerCharacter.CanPerformActions;

        ServerCharacter m_ServerCharacter;
        public ServerCharacter serverCharacter => m_ServerCharacter;

        ClientActionPlayer m_ClientActionViz;
        PositionLerper m_PositionLerper;
        RotationLerper m_RotationLerper;
        const float k_LerpTime = 0.08f;

        Vector3 m_LerpedPosition;
        Quaternion m_LerpedRotation;
        float m_CurrentSpeed;

        [Rpc(SendTo.ClientsAndHost)]
        public void ClientPlayActionRpc(ActionRequestData data)
        {
            ActionRequestData data1 = data;
            m_ClientActionViz.PlayAction(ref data1);
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void ClientCancelAllActionsRpc()
        {
            m_ClientActionViz.CancelAllActions();
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void ClientCancelActionsByPrototypeIDRpc(ActionID actionPrototypeID)
        {
            m_ClientActionViz.CancelAllActionsWithSamePrototypeID(actionPrototypeID);
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void ClientStopChargingUpRpc(float percentCharged)
        {
            m_ClientActionViz.OnStoppedChargingUp(percentCharged);
        }

        void Awake()
        {
            enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsClient || transform.parent == null)
            {
                return;
            }

            enabled = true;
            m_ClientActionViz = new ClientActionPlayer(this);
            m_ServerCharacter = GetComponentInParent<ServerCharacter>();

            m_ServerCharacter.IsStealthy.OnValueChanged += OnStealthyChanged;
            m_ServerCharacter.MovementStatus.OnValueChanged += OnMovementStatusChanged;
            OnMovementStatusChanged(MovementStatus.Normal, m_ServerCharacter.MovementStatus.Value);

            transform.SetPositionAndRotation(serverCharacter.physicsWrapper.Transform.position,
                serverCharacter.physicsWrapper.Transform.rotation);
            m_LerpedPosition = transform.position;
            m_LerpedRotation = transform.rotation;

            m_PositionLerper = new PositionLerper(serverCharacter.physicsWrapper.Transform.position, k_LerpTime);
            m_RotationLerper = new RotationLerper(serverCharacter.physicsWrapper.Transform.rotation, k_LerpTime);

            if (!m_ServerCharacter.IsNpc)
            {
                name = "AvatarGraphics" + m_ServerCharacter.OwnerClientId;

                if (m_ServerCharacter.TryGetComponent(out ClientPlayerAvatarNetworkAnimator characterNetworkAnimator))
                {
                    m_ClientVisualsAnimator = characterNetworkAnimator.Animator;
                }

                m_CharacterSwapper = GetComponentInChildren<CharacterSwap>();
                SetAppearanceSwap();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (m_ServerCharacter)
            {
                m_ServerCharacter.IsStealthy.OnValueChanged -= OnStealthyChanged;
            }
            enabled = false;
        }

        void OnStealthyChanged(bool oldValue, bool newValue)
        {
            SetAppearanceSwap();
        }

        void SetAppearanceSwap()
        {
            if (m_CharacterSwapper)
            {
                var specialMaterialMode = CharacterSwap.SpecialMaterialMode.None;
                if (m_ServerCharacter.IsStealthy.Value)
                {
                    specialMaterialMode = m_ServerCharacter.IsOwner
                        ? CharacterSwap.SpecialMaterialMode.StealthySelf
                        : CharacterSwap.SpecialMaterialMode.StealthyOther;
                }
                m_CharacterSwapper.SwapToModel(specialMaterialMode);
            }
        }

        /// <summary>
        /// Returns the value we should set the Animator's "Speed" variable, given current gameplay conditions.
        /// </summary>
        /// <summary>
        /// 현재 게임 플레이 상태에 따라 애니메이터의 "Speed" 변수를 설정해야 하는 값을 반환합니다.
        /// </summary>
        float GetVisualMovementSpeed(MovementStatus movementStatus)
        {
            if (m_ServerCharacter.NetLifeState.LifeState.Value != LifeState.Alive)
            {
                return m_VisualizationConfiguration.SpeedDead;
            }

            switch (movementStatus)
            {
                case MovementStatus.Idle:
                    return m_VisualizationConfiguration.SpeedIdle;
                case MovementStatus.Normal:
                    return m_VisualizationConfiguration.SpeedNormal;
                case MovementStatus.Uncontrolled:
                    return m_VisualizationConfiguration.SpeedUncontrolled;
                case MovementStatus.Slowed:
                    return m_VisualizationConfiguration.SpeedSlowed;
                case MovementStatus.Hasted:
                    return m_VisualizationConfiguration.SpeedHasted;
                case MovementStatus.Walking:
                    return m_VisualizationConfiguration.SpeedWalking;
                default:
                    throw new Exception($"Unknown MovementStatus {movementStatus}");
            }
        }

        void OnMovementStatusChanged(MovementStatus previousValue, MovementStatus newValue)
        {
            m_CurrentSpeed = GetVisualMovementSpeed(newValue);
        }

        void Update()
        {
            // On the host, Characters are translated via ServerCharacterMovement's FixedUpdate method. To ensure that
            // the game camera tracks a GameObject moving in the Update loop and therefore eliminate any camera jitter,
            // this graphics GameObject's position is smoothed over time on the host. Clients do not need to perform any
            // positional smoothing since NetworkTransform will interpolate position updates on the root GameObject.
            //
            // 호스트에서는 ServerCharacterMovement의 FixedUpdate 메서드를 통해 캐릭터가 이동합니다.
            // 게임 카메라가 Update 루프에서 이동하는 게임 오브젝트를 추적하도록 하여 카메라 떨림을 방지하기 위해
            // 이 그래픽 게임 오브젝트의 위치를 시간이 지나면서 부드럽게 조정합니다.
            // 클라이언트는 NetworkTransform이 루트 게임 오브젝트의 위치 업데이트를 보간하기 때문에 위치 보정을 수행할 필요가 없습니다.
            if (IsHost)
            {
                // Note: a cached position (m_LerpedPosition) and rotation (m_LerpedRotation) are created and used as
                // the starting point for each interpolation since the root's position and rotation are modified in
                // FixedUpdate, thus altering this transform (being a child) in the process.
                //
                // 참고: 캐시된 위치(m_LerpedPosition)와 회전(m_LerpedRotation)은 각 보간의 시작점으로 사용됩니다.
                // 이는 루트의 위치와 회전이 FixedUpdate에서 변경되므로, 이에 따라 이 트랜스폼(자식 오브젝트)도 변경되기 때문입니다.
                m_LerpedPosition = m_PositionLerper.LerpPosition(m_LerpedPosition,
                    serverCharacter.physicsWrapper.Transform.position);
                m_LerpedRotation = m_RotationLerper.LerpRotation(m_LerpedRotation,
                    serverCharacter.physicsWrapper.Transform.rotation);
                transform.SetPositionAndRotation(m_LerpedPosition, m_LerpedRotation);
            }

            if (m_ClientVisualsAnimator)
            {
                // set Animator variables here
                // 애니메이터 변수 설정
                OurAnimator.SetFloat(m_VisualizationConfiguration.SpeedVariableID, m_CurrentSpeed);
            }

            m_ClientActionViz.OnUpdate();
        }

        void OnAnimEvent(string id)
        {
            //if you are trying to figure out who calls this method, it's "magic". The Unity Animation Event system takes method names as strings,
            //and calls a method of the same name on a component on the same GameObject as the Animator. See the "attack1" Animation Clip as one
            //example of where this is configured.
            //
            // 이 메서드가 어디에서 호출되는지 궁금하다면, 이것은 "매직"입니다. Unity 애니메이션 이벤트 시스템은 메서드 이름을 문자열로 사용하여
            // 애니메이터와 동일한 게임 오브젝트에 있는 컴포넌트에서 동일한 이름의 메서드를 호출합니다.
            // "attack1" 애니메이션 클립에서 이러한 설정의 예를 확인할 수 있습니다.

            m_ClientActionViz.OnAnimEvent(id);
        }

        public bool IsAnimating()
        {
            if (OurAnimator.GetFloat(m_VisualizationConfiguration.SpeedVariableID) > 0.0) { return true; }

            for (int i = 0; i < OurAnimator.layerCount; i++)
            {
                if (OurAnimator.GetCurrentAnimatorStateInfo(i).tagHash != m_VisualizationConfiguration.BaseNodeTagID)
                {
                    //we are in an active node, not the default "nothing" node.
                    // 우리는 기본 "nothing" 노드가 아닌 활성 노드에 있습니다.
                    return true;
                }
            }

            return false;
        }
    }
}
