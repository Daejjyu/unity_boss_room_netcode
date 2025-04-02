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
        /// </summary>
        /// <param name="isNPC">true if the attacker is an NPC (and therefore should hit PCs). False for the reverse.</param>
        /// <param name="attacker">The collider of the attacking GameObject.</param>
        /// <param name="range">The range in meters to check for foes.</param>
        /// <param name="results">Place an uninitialized RayCastHit[] ref in here. It will be set to the results array. </param>
        /// <remarks>
        /// This method does not alloc. It returns a maximum of 4 results. Consume the results immediately, as the array will be overwritten with
        /// the next similar query.
        /// </remarks>
        /// <returns>Total number of foes encountered. </returns>
        public static int DetectMeleeFoe(bool isNPC, Collider attacker, float range, out RaycastHit[] results)
        {
            return DetectNearbyEntities(isNPC, !isNPC, attacker, range, out results);
        }

        /// <summary>
        /// Detects friends and/or foes near us.
        /// </summary>
        /// <param name="wantPcs">true if we should detect PCs</param>
        /// <param name="wantNpcs">true if we should detect NPCs</param>
        /// <param name="attacker">The collider of the attacking GameObject.</param>
        /// <param name="range">The range in meters to check.</param>
        /// <param name="results">Place an uninitialized RayCastHit[] ref in here. It will be set to the results array. </param>
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
        /// </summary>
        /// <param name="targetId">the NetId of the target to investigate</param>
        /// <returns>true if this is a valid target</returns>
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
        /// </summary>
        /// <param name="character1Pos">first character's position</param>
        /// <param name="character2Pos">second character's position</param>
        /// <param name="missPos">the point where an obstruction occurred (or if no obstruction, this is just character2Pos)</param>
        /// <returns>true if no obstructions, false if there is a Ground-layer object in the way</returns>
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
        /// </summary>
        /// <param name="stoppedChargingUpTime">The time when we finished charging up, or 0 if we're still charging.</param>
        /// <param name="timeRunning">How long the action has been running. </param>
        /// <param name="timeStarted">when the action started. </param>
        /// <param name="execTime">the total execution time of the action (usually not its duration). </param>
        /// <returns>Percent charge-up, from 0 to 1. </returns>
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
        /// </summary>
        /// <param name="characterTransform">character's transform</param>
        /// <param name="targetSpot">location we want to be next to</param>
        /// <param name="stopAtObstructions">true if we should be blocked by obstructions such as walls</param>
        /// <param name="distanceToUseIfVeryClose">if we should fix up very short teleport destinations, the new location will be this far away (in meters). -1 = don't check for short teleports</param>
        /// <param name="maxDistance">returned location will be no further away from characterTransform than this. -1 = no max distance</param>
        /// <returns>new coordinates that are near the destination (or near the first obstruction)</returns>
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
