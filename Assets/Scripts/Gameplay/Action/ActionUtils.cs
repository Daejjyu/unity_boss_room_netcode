using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
  public static class ActionUtils
  {
    //cache Physics Cast hits, to minimize allocs.
    //물리 캐스트 히트를 캐시하여 할당을 최소화합니다.
    static RaycastHit[] s_Hits = new RaycastHit[4];
    // cache layer IDs (after first use). -1 is a sentinel value meaning "uninitialized"
    // 첫 번째 사용 후 레이어 ID를 캐시합니다. -1은 "초기화되지 않음"을 의미하는 센티넬 값입니다.
    static int s_PCLayer = -1;
    static int s_NpcLayer = -1;
    static int s_EnvironmentLayer = -1;

    /// <summary>
    /// When doing line-of-sight checks we assume the characters' "eyes" are at this height above their transform
    /// </summary>
    /// <summary>
    /// 시야 검사 시 캐릭터의 "눈"이 그들의 트랜스폼 위에 이 높이에 있다고 가정합니다.
    /// </summary>
    static readonly Vector3 k_CharacterEyelineOffset = new Vector3(0, 1, 0);

    /// <summary>
    /// When teleporting to a destination, this is how far away from the destination spot to arrive
    /// </summary>
    /// <summary>
    /// 목표 지점으로 텔레포트할 때, 목표 지점에서 얼마나 멀리 떨어져 도착할지를 정의합니다.
    /// </summary>
    const float k_CloseDistanceOffset = 1;

    /// <summary>
    /// When checking if a teleport-destination is "too close" to the starting spot, anything less than this is too close
    /// </summary>
    /// <summary>
    /// 텔레포트 목적지가 시작 지점에 "너무 가까운지" 확인할 때, 이 값보다 가까운 경우 너무 가깝습니다.
    /// </summary>
    const float k_VeryCloseTeleportRange = k_CloseDistanceOffset + 1;

    /// <summary>
    /// Does a melee foe hit detect.
    /// 근접 적중 탐지를 수행합니다.
    /// </summary>
    /// <param name="isNPC">true if the attacker is an NPC (and therefore should hit PCs). False for the reverse.
    /// 공격자가 NPC인 경우 true(즉, 플레이어 캐릭터(PC)를 공격할 수 있음). 반대의 경우 false.</param>
    /// <param name="attacker">The collider of the attacking GameObject.
    /// 공격하는 게임 오브젝트의 콜라이더.</param>
    /// <param name="range">The range in meters to check for foes.
    /// 적을 탐지할 거리(미터 단위).</param>
    /// <param name="results">Place an uninitialized RayCastHit[] ref in here. It will be set to the results array.
    /// 초기화되지 않은 RayCastHit[] 참조를 전달하세요. 결과 배열로 설정됩니다.</param>
    /// <remarks>
    /// This method does not alloc. It returns a maximum of 4 results. Consume the results immediately, as the array will be overwritten with
    /// the next similar query.
    /// 이 메서드는 추가 메모리를 할당하지 않습니다. 최대 4개의 결과를 반환합니다.
    /// 배열은 다음 유사한 쿼리로 덮어쓰이므로 결과를 즉시 사용하세요.
    /// </remarks>
    /// <returns>Total number of foes encountered.
    /// 탐지된 적의 총 수.</returns>
    public static int DetectMeleeFoe(bool isNPC, Collider attacker, float range, out RaycastHit[] results)
    {
      return DetectNearbyEntities(isNPC, !isNPC, attacker, range, out results);
    }

    /// <summary>
    /// Detects friends and/or foes near us.
    /// 주변의 친구 또는 적을 탐지합니다.
    /// </summary>
    /// <param name="wantPcs">true if we should detect PCs
    /// 플레이어 캐릭터(PC)를 탐지해야 하면 true.</param>
    /// <param name="wantNpcs">true if we should detect NPCs
    /// NPC를 탐지해야 하면 true.</param>
    /// <param name="attacker">The collider of the attacking GameObject.
    /// 공격하는 게임 오브젝트의 콜라이더.</param>
    /// <param name="range">The range in meters to check.
    /// 탐지할 거리(미터 단위).</param>
    /// <param name="results">Place an uninitialized RayCastHit[] ref in here. It will be set to the results array.
    /// 초기화되지 않은 RayCastHit[] 참조를 전달하세요. 결과 배열로 설정됩니다.</param>
    /// <returns></returns>
    public static int DetectNearbyEntities(bool wantPcs, bool wantNpcs, Collider attacker, float range, out RaycastHit[] results)
    {
      //this simple detect just does a boxcast out from our position in the direction we're facing, out to the range of the attack.
      //이 간단한 감지는 우리의 위치에서 우리가 향하고 있는 방향으로 범위까지 박스 캐스트를 수행합니다.

      var myBounds = attacker.bounds;

      if (s_PCLayer == -1)
        s_PCLayer = LayerMask.NameToLayer("PCs");
      if (s_NpcLayer == -1)
        s_NpcLayer = LayerMask.NameToLayer("NPCs");

      int mask = 0;
      if (wantPcs)
        mask |= (1 << s_PCLayer);
      if (wantNpcs)
        mask |= (1 << s_NpcLayer);

      int numResults = Physics.BoxCastNonAlloc(attacker.transform.position, myBounds.extents,
          attacker.transform.forward, s_Hits, Quaternion.identity, range, mask);

      results = s_Hits;
      return numResults;
    }

    /// <summary>
    /// Does this NetId represent a valid target? Used by Target Action. The target needs to exist, be a
    /// NetworkCharacterState, and be alive. In the future, it will be any non-dead IDamageable.
    /// 이 NetId가 유효한 대상인지 확인합니다. Target Action에서 사용됩니다.
    /// 대상은 존재해야 하며, NetworkCharacterState여야 하고 살아 있어야 합니다.
    /// 향후에는 죽지 않은 IDamageable이면 유효한 대상으로 간주될 것입니다.
    /// </summary>
    /// <param name="targetId">the NetId of the target to investigate
    /// 조사할 대상의 NetId.</param>
    /// <returns>true if this is a valid target
    /// 유효한 대상이면 true.</returns>
    public static bool IsValidTarget(ulong targetId)
    {
      //note that we DON'T check if you're an ally. It's perfectly valid to target friends,
      //because there are friendly skills, such as Heal.
      //우리는 동맹 여부를 확인하지 않음을 주의하세요. 친구를 대상으로 삼는 것은 완전히 유효합니다,
      //예를 들어 치유와 같은 우호적인 스킬이 있기 때문입니다.

      if (NetworkManager.Singleton.SpawnManager == null || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var targetChar))
      {
        return false;
      }

      var targetable = targetChar.GetComponent<ITargetable>();
      return targetable != null && targetable.IsValidTarget;
    }

    /// <summary>
    /// Given the coordinates of two entities, checks to see if there is an obstacle between them.
    /// (Since character coordinates are beneath the feet of the visual avatar, we add a small amount of height to
    /// these coordinates to simulate their eye-line.)
    /// 두 개체의 좌표를 받아, 그들 사이에 장애물이 있는지 확인합니다.
    /// (캐릭터 좌표는 시각적 아바타의 발 아래에 위치하므로, 이 좌표에 약간의 높이를 추가하여 
    /// 시선 높이를 시뮬레이션합니다.)
    /// </summary>
    /// <param name="character1Pos">first character's position
    /// 첫 번째 캐릭터의 위치.</param>
    /// <param name="character2Pos">second character's position
    /// 두 번째 캐릭터의 위치.</param>
    /// <param name="missPos">the point where an obstruction occurred (or if no obstruction, this is just character2Pos)
    /// 장애물이 발생한 지점(또는 장애물이 없으면 character2Pos 값과 동일).</param>
    /// <returns>true if no obstructions, false if there is a Ground-layer object in the way
    /// 장애물이 없으면 true, Ground 레이어의 객체가 경로를 막고 있으면 false.</returns>
    public static bool HasLineOfSight(Vector3 character1Pos, Vector3 character2Pos, out Vector3 missPos)
    {
      if (s_EnvironmentLayer == -1)
      {
        s_EnvironmentLayer = LayerMask.NameToLayer("Environment");
      }

      int mask = 1 << s_EnvironmentLayer;

      character1Pos += k_CharacterEyelineOffset;
      character2Pos += k_CharacterEyelineOffset;
      var rayDirection = character2Pos - character1Pos;
      var distance = rayDirection.magnitude;

      var numHits = Physics.RaycastNonAlloc(new Ray(character1Pos, rayDirection), s_Hits, distance, mask);
      if (numHits == 0)
      {
        missPos = character2Pos;
        return true;
      }
      else
      {
        missPos = s_Hits[0].point;
        return false;
      }
    }

    /// <summary>
    /// Helper method that calculates the percent a charge-up action is charged, based on how long it has run, returning a value
    /// from 0-1.
    /// 실행된 시간에 따라 차지 업(Charge-up) 액션이 얼마나 충전되었는지를 계산하는 보조 메서드로, 
    /// 0에서 1 사이의 값을 반환합니다.
    /// </summary>
    /// <param name="stoppedChargingUpTime">The time when we finished charging up, or 0 if we're still charging.
    /// 충전이 완료된 시간. 아직 충전 중이라면 0.</param>
    /// <param name="timeRunning">How long the action has been running.
    /// 액션이 실행된 시간.</param>
    /// <param name="timeStarted">when the action started.
    /// 액션이 시작된 시간.</param>
    /// <param name="execTime">the total execution time of the action (usually not its duration).
    /// 액션의 총 실행 시간(일반적으로 지속 시간이 아님).</param>
    /// <returns>Percent charge-up, from 0 to 1.
    /// 충전 비율(0에서 1 사이).</returns>
    public static float GetPercentChargedUp(float stoppedChargingUpTime, float timeRunning, float timeStarted, float execTime)
    {
      float timeSpentChargingUp;
      if (stoppedChargingUpTime == 0)
      {
        timeSpentChargingUp = timeRunning; // we're still charging up, so all of our runtime has been charge-up time
                                           //우리는 아직 충전 중이므로 모든 실행 시간이 충전 시간입니다.
      }
      else
      {
        timeSpentChargingUp = stoppedChargingUpTime - timeStarted;
      }
      return Mathf.Clamp01(timeSpentChargingUp / execTime);
    }

    /// <summary>
    /// Determines a spot very near a chosen location, so that we can teleport next to the target (rather
    /// than teleporting literally on top of the target). Can optionally perform a bunch of additional checks:
    /// - can do a line-of-sight check and stop at the first obstruction.
    /// - can make sure that the chosen spot is a meaningful distance away from the starting spot.
    /// - can make sure that the chosen spot is no further than a specified distance away.
    /// 선택한 위치 근처의 지점을 결정하여 대상 바로 위가 아닌 옆으로 순간이동할 수 있도록 합니다.
    /// 추가적인 여러 가지 검사를 수행할 수 있습니다:
    /// - 시야(Line-of-sight) 검사를 수행하고 첫 번째 장애물에서 멈출 수 있습니다.
    /// - 선택한 위치가 시작 위치로부터 일정한 거리 이상 떨어져 있도록 할 수 있습니다.
    /// - 선택한 위치가 지정된 거리보다 멀어지지 않도록 할 수 있습니다.
    /// </summary>
    /// <param name="characterTransform">character's transform
    /// 캐릭터의 트랜스폼.</param>
    /// <param name="targetSpot">location we want to be next to
    /// 이동하려는 목표 지점.</param>
    /// <param name="stopAtObstructions">true if we should be blocked by obstructions such as walls
    /// 벽과 같은 장애물에 의해 차단되어야 하면 true.</param>
    /// <param name="distanceToUseIfVeryClose">if we should fix up very short teleport destinations, the new location will be this far away (in meters). -1 = don't check for short teleports
    /// 순간이동 거리가 너무 짧을 경우 보정해야 하면, 새 위치는 이 거리만큼 떨어지게 됩니다(미터 단위).
    /// -1을 입력하면 짧은 순간이동을 검사하지 않습니다.</param>
    /// <param name="maxDistance">returned location will be no further away from characterTransform than this. -1 = no max distance
    /// 반환된 위치는 characterTransform에서 이 거리보다 멀어지지 않습니다.
    /// -1을 입력하면 최대 거리를 제한하지 않습니다.</param>
    /// <returns>new coordinates that are near the destination (or near the first obstruction)
    /// 목표 지점 근처(또는 첫 번째 장애물 근처)의 새로운 좌표.</returns>
    public static Vector3 GetDashDestination(Transform characterTransform, Vector3 targetSpot, bool stopAtObstructions, float distanceToUseIfVeryClose = -1, float maxDistance = -1)
    {
      Vector3 destinationSpot = targetSpot;

      if (distanceToUseIfVeryClose != -1)
      {
        // make sure our stopping point is a meaningful distance away!
        //정지 지점이 의미 있는 거리가 되도록 합니다!
        if (destinationSpot == Vector3.zero || Vector3.Distance(characterTransform.position, destinationSpot) <= k_VeryCloseTeleportRange)
        {
          // we don't have a meaningful stopping spot. Find a new one based on the character's current direction
          // 의미 있는 정지 지점이 없습니다. 캐릭터의 현재 방향을 기준으로 새로운 지점을 찾습니다.
          destinationSpot = characterTransform.position + characterTransform.forward * distanceToUseIfVeryClose;
        }
      }

      if (maxDistance != -1)
      {
        // make sure our stopping point isn't too far away!
        // 정지 지점이 너무 멀지 않도록 합니다!
        float distance = Vector3.Distance(characterTransform.position, destinationSpot);
        if (distance > maxDistance)
        {
          destinationSpot = Vector3.MoveTowards(destinationSpot, characterTransform.position, distance - maxDistance);
        }
      }

      if (stopAtObstructions)
      {
        // if we're going to hit an obstruction, stop at the obstruction
        // 만약 장애물에 부딪힐 예정이라면, 장애물에서 멈춥니다.
        if (!HasLineOfSight(characterTransform.position, destinationSpot, out Vector3 collidePos))
        {
          destinationSpot = collidePos;
        }
      }

      // now get a spot "near" the end point
      // 이제 목적지 근처의 지점을 찾습니다.
      destinationSpot = Vector3.MoveTowards(destinationSpot, characterTransform.position, k_CloseDistanceOffset);

      return destinationSpot;
    }
  }

  /// <summary>
  /// Small utility to better understand action start and stop conclusion
  /// </summary>
  /// <summary>
  /// 액션의 시작 및 종료 결론을 더 잘 이해하기 위한 작은 유틸리티입니다.
  /// </summary>
  public static class ActionConclusion
  {
    public const bool Stop = false;
    public const bool Continue = true;
  }

  /// <summary>
  /// Utility comparer to sort through RaycastHits by distance.
  /// </summary>
  /// <summary>
  /// RaycastHits를 거리별로 정렬하는 유틸리티 비교기입니다.
  /// </summary>
  public class RaycastHitComparer : IComparer<RaycastHit>
  {
    public int Compare(RaycastHit x, RaycastHit y)
    {
      return x.distance.CompareTo(y.distance);
    }
  }
}
