using System;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
  public enum MovementState
  {
    Idle = 0,
    PathFollowing = 1,
    Charging = 2,
    Knockback = 3,
  }

  /// <summary>
  /// Component responsible for moving a character on the server side based on inputs.
  /// </summary>
  /// <summary>
  /// 입력을 기반으로 서버 측에서 캐릭터를 이동시키는 컴포넌트입니다.
  /// </summary>
  /*[RequireComponent(typeof(NetworkCharacterState), typeof(NavMeshAgent), typeof(ServerCharacter)), RequireComponent(typeof(Rigidbody))]*/
  public class ServerCharacterMovement : NetworkBehaviour
  {
    [SerializeField]
    NavMeshAgent m_NavMeshAgent;

    [SerializeField]
    Rigidbody m_Rigidbody;

    private NavigationSystem m_NavigationSystem;

    private DynamicNavPath m_NavPath;

    private MovementState m_MovementState;

    MovementStatus m_PreviousState;

    [SerializeField]
    private ServerCharacter m_CharLogic;

    // When we are in charging and knockback mode, we use these additional variables  
    // 캐릭터가 차징 또는 넉백 모드일 때 사용하는 추가 변수들  
    private float m_ForcedSpeed;
    private float m_SpecialModeDurationRemaining;

    // This one is specific to knockback mode  
    // 이 변수는 넉백 모드에서만 사용됩니다.  
    private Vector3 m_KnockbackVector;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool TeleportModeActivated { get; set; }

    const float k_CheatSpeed = 20;

    public bool SpeedCheatActivated { get; set; }
#endif

    void Awake()
    {
      // Disable this NetworkBehavior until it is spawned
      // 이 NetworkBehavior가 스폰될 때까지 비활성화합니다.
      enabled = false;
    }

    public override void OnNetworkSpawn()
    {
      if (IsServer)
      {
        // Only enable server component on servers
        // 서버에서만 서버 컴포넌트를 활성화합니다.
        enabled = true;

        // On the server enable navMeshAgent and initialize
        // 서버에서 navMeshAgent를 활성화하고 초기화합니다.
        m_NavMeshAgent.enabled = true;
        m_NavigationSystem = GameObject.FindGameObjectWithTag(NavigationSystem.NavigationSystemTag).GetComponent<NavigationSystem>();
        m_NavPath = new DynamicNavPath(m_NavMeshAgent, m_NavigationSystem);
      }
    }

    /// <summary>
    /// Sets a movement target. We will path to this position, avoiding static obstacles.
    /// 이동 목표를 설정합니다. 정적 장애물을 피하면서 이 위치로 이동 경로를 생성합니다.
    /// </summary>
    /// <param name="position">경로를 생성할 월드 좌표</param>
    public void SetMovementTarget(Vector3 position)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
      if (TeleportModeActivated)
      {
        Teleport(position);
        return;
      }
#endif
      m_MovementState = MovementState.PathFollowing;
      m_NavPath.SetTargetPosition(position);
    }

    public void StartForwardCharge(float speed, float duration)
    {
      m_NavPath.Clear();
      m_MovementState = MovementState.Charging;
      m_ForcedSpeed = speed;
      m_SpecialModeDurationRemaining = duration;
    }

    public void StartKnockback(Vector3 knocker, float speed, float duration)
    {
      m_NavPath.Clear();
      m_MovementState = MovementState.Knockback;
      m_KnockbackVector = transform.position - knocker;
      m_ForcedSpeed = speed;
      m_SpecialModeDurationRemaining = duration;
    }

    /// <summary>
    /// Follow the given transform until it is reached.
    /// 주어진 트랜스폼을 목표 지점으로 삼아 따라갑니다.
    /// </summary>
    /// <param name="followTransform">따라갈 트랜스폼</param>
    public void FollowTransform(Transform followTransform)
    {
      m_MovementState = MovementState.PathFollowing;
      m_NavPath.FollowTransform(followTransform);
    }

    /// <summary>
    /// Returns true if the current movement-mode is unabortable (e.g. a knockback effect)
    /// 현재 이동 모드가 취소할 수 없는 상태인지 여부를 반환합니다 (예: 넉백 효과).
    /// </summary>
    /// <returns>취소할 수 없는 이동 상태인 경우 true, 그렇지 않으면 false</returns>
    public bool IsPerformingForcedMovement()
    {
      return m_MovementState == MovementState.Knockback || m_MovementState == MovementState.Charging;
    }

    /// <summary>
    /// Returns true if the character is actively moving, false otherwise.
    /// 캐릭터가 현재 활발하게 이동 중이면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
    /// </summary>
    /// <returns>캐릭터가 이동 중이면 true, 아니면 false</returns>
    public bool IsMoving()
    {
      return m_MovementState != MovementState.Idle;
    }

    /// <summary>
    /// Cancels any moves that are currently in progress.
    /// 현재 진행 중인 모든 이동을 취소합니다.
    /// </summary>
    public void CancelMove()
    {
      if (m_NavPath != null)
      {
        m_NavPath.Clear();
      }
      m_MovementState = MovementState.Idle;
    }

    /// <summary>
    /// Instantly moves the character to a new position. NOTE: this cancels any active movement operation!
    /// This does not notify the client that the movement occurred due to teleportation, so that needs to
    /// happen in some other way, such as with the custom action visualization in DashAttackActionFX. (Without
    /// this, the clients will animate the character moving to the new destination spot, rather than instantly
    /// appearing in the new spot.)
    /// 즉시 캐릭터를 새로운 위치로 이동시킵니다. 참고: 이 작업은 현재 진행 중인 모든 이동 작업을 취소합니다!
    /// 이 함수는 이동이 텔레포트로 인해 발생했음을 클라이언트에 알리지 않으므로, DashAttackActionFX의 
    /// 사용자 지정 액션 시각화와 같은 다른 방식으로 처리해야 합니다. (이 처리가 없으면 클라이언트는 
    /// 캐릭터가 새 목적지까지 애니메이션을 통해 이동하는 것으로 보이며, 즉시 나타나지 않습니다.)
    /// </summary>
    /// <param name="newPosition">new coordinates the character should be at</param>
    /// <param name="newPosition">캐릭터가 있어야 할 새로운 좌표</param>
    public void Teleport(Vector3 newPosition)
    {
      CancelMove();
      if (!m_NavMeshAgent.Warp(newPosition))
      {
        // Warping failed! We're off the navmesh somehow. Weird... but we can still teleport  
        // 워프에 실패했습니다! 어쩐 일인지 내비메시에서 벗어났습니다. 이상하지만, 그래도 텔레포트할 수 있습니다.
        Debug.LogWarning($"NavMeshAgent.Warp({newPosition}) failed!", gameObject);
        transform.position = newPosition;
      }

      m_Rigidbody.position = transform.position;
      m_Rigidbody.rotation = transform.rotation;
    }

    private void FixedUpdate()
    {
      PerformMovement();

      var currentState = GetMovementStatus(m_MovementState);
      if (m_PreviousState != currentState)
      {
        m_CharLogic.MovementStatus.Value = currentState;
        m_PreviousState = currentState;
      }
    }

    public override void OnNetworkDespawn()
    {
      if (m_NavPath != null)
      {
        m_NavPath.Dispose();
      }
      if (IsServer)
      {
        // Disable server components when despawning  
        // 처분할 때 서버 구성 요소 비활성화
        enabled = false;
        m_NavMeshAgent.enabled = false;
      }
    }

    private void PerformMovement()
    {
      if (m_MovementState == MovementState.Idle)
        return;

      Vector3 movementVector;

      if (m_MovementState == MovementState.Charging)
      {
        // if we're done charging, stop moving
        // 충전이 끝나면 움직임을 멈춘다
        m_SpecialModeDurationRemaining -= Time.fixedDeltaTime;
        if (m_SpecialModeDurationRemaining <= 0)
        {
          m_MovementState = MovementState.Idle;
          return;
        }

        var desiredMovementAmount = m_ForcedSpeed * Time.fixedDeltaTime;
        movementVector = transform.forward * desiredMovementAmount;
      }
      else if (m_MovementState == MovementState.Knockback)
      {
        m_SpecialModeDurationRemaining -= Time.fixedDeltaTime;
        if (m_SpecialModeDurationRemaining <= 0)
        {
          m_MovementState = MovementState.Idle;
          return;
        }

        var desiredMovementAmount = m_ForcedSpeed * Time.fixedDeltaTime;
        movementVector = m_KnockbackVector * desiredMovementAmount;
      }
      else
      {
        var desiredMovementAmount = GetBaseMovementSpeed() * Time.fixedDeltaTime;
        movementVector = m_NavPath.MoveAlongPath(desiredMovementAmount);

        // If we didn't move stop moving.
        // 이동하지 않았다면, 움직임을 멈춘다.
        if (movementVector == Vector3.zero)
        {
          m_MovementState = MovementState.Idle;
          return;
        }
      }

      m_NavMeshAgent.Move(movementVector);
      transform.rotation = Quaternion.LookRotation(movementVector);

      // After moving adjust the position of the dynamic rigidbody.
      // 이동 후 동적 리지드바디의 위치를 조정한다.
      m_Rigidbody.position = transform.position;
      m_Rigidbody.rotation = transform.rotation;
    }

    /// <summary>
    /// Retrieves the speed for this character's class.
    /// </summary>
    /// <summary>
    /// 이 캐릭터 클래스의 속도를 가져온다.
    /// </summary>
    private float GetBaseMovementSpeed()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
      if (SpeedCheatActivated)
      {
        return k_CheatSpeed;
      }
#endif
      CharacterClass characterClass = GameDataSource.Instance.CharacterDataByType[m_CharLogic.CharacterType];
      Assert.IsNotNull(characterClass, $"No CharacterClass data for character type {m_CharLogic.CharacterType}");
      return characterClass.Speed;
    }

    /// <summary>
    /// Determines the appropriate MovementStatus for the character. The
    /// MovementStatus is used by the client code when animating the character.
    /// </summary>
    /// <summary>
    /// 캐릭터에 적절한 MovementStatus를 결정한다. MovementStatus는 캐릭터 애니메이션을 적용할 때 클라이언트 코드에서 사용된다.
    /// </summary>
    private MovementStatus GetMovementStatus(MovementState movementState)
    {
      switch (movementState)
      {
        case MovementState.Idle:
          return MovementStatus.Idle;
        case MovementState.Knockback:
          return MovementStatus.Uncontrolled;
        default:
          return MovementStatus.Normal;
      }
    }
  }
}
