using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.BossRoom.Navigation
{
    public sealed class DynamicNavPath : IDisposable
    {
        /// <summary>
        /// The tolerance to decide whether the path needs to be recalculated when the position of a target transform changed.
        /// </summary>
        /// <summary>
        /// 목표 변환의 위치가 변경될 때 경로를 다시 계산해야 하는지 결정하는 허용 오차입니다.
        /// </summary>
        const float k_RepathToleranceSqr = 9f;

        NavMeshAgent m_Agent;

        NavigationSystem m_NavigationSystem;

        /// <summary>
        /// The target position value which was used to calculate the current path.
        /// This get stored to make sure the path gets recalculated if the target
        /// </summary>
        /// <summary>
        /// 현재 경로를 계산할 때 사용된 목표 위치 값입니다.
        /// 이 값이 저장되어 목표가 변경될 경우 경로가 다시 계산되도록 합니다.
        /// </summary>
        Vector3 m_CurrentPathOriginalTarget;

        /// <summary>
        /// This field caches a NavMesh Path so that we don't have to allocate a new one each time.
        /// </summary>
        /// <summary>
        /// 새로운 경로를 매번 할당하지 않도록 NavMesh 경로를 캐시하는 필드입니다.
        /// </summary>
        NavMeshPath m_NavMeshPath;

        /// <summary>
        /// The remaining path points to follow to reach the target position.
        /// </summary>
        /// <summary>
        /// 목표 위치에 도달하기 위해 따라야 할 남은 경로 지점입니다.
        /// </summary>
        List<Vector3> m_Path;

        /// <summary>
        /// The target position of this path.
        /// </summary>
        /// <summary>
        /// 이 경로의 목표 위치입니다.
        /// </summary>
        Vector3 m_PositionTarget;

        /// <summary>
        /// A moving transform target, the path will readjust when the target moves. If this is non-null, it takes precedence over m_PositionTarget.
        /// </summary>
        /// <summary>
        /// 이동하는 변환 목표입니다. 목표가 이동하면 경로가 다시 조정됩니다.  
        /// 이 값이 null이 아니면 m_PositionTarget보다 우선 적용됩니다.
        /// </summary>
        Transform m_TransformTarget;

        /// <summary>
        /// Creates a new instance of the <see cref="DynamicNavPath"/>.
        /// </summary>
        /// <param name="agent">The NavMeshAgent of the object which uses this path.</param>
        /// <param name="navigationSystem">The navigation system which updates this path.</param>
        /// <summary>
        /// <see cref="DynamicNavPath"/>의 새 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="agent">이 경로를 사용하는 객체의 NavMeshAgent입니다.</param>
        /// <param name="navigationSystem">이 경로를 업데이트하는 내비게이션 시스템입니다.</param>
        public DynamicNavPath(NavMeshAgent agent, NavigationSystem navigationSystem)
        {
            m_Agent = agent;
            m_Path = new List<Vector3>();
            m_NavMeshPath = new NavMeshPath();
            m_NavigationSystem = navigationSystem;

            navigationSystem.OnNavigationMeshChanged += OnNavMeshChanged;
        }

        Vector3 TargetPosition => m_TransformTarget != null ? m_TransformTarget.position : m_PositionTarget;

        /// <summary>
        /// Set the target of this path to follow a moving transform.
        /// </summary>
        /// <param name="target">The transform to follow.</param>
        /// <summary>
        /// 이동하는 변환을 따라가도록 경로의 목표를 설정합니다.
        /// </summary>
        /// <param name="target">따라갈 변환입니다.</param>
        public void FollowTransform(Transform target)
        {
            m_TransformTarget = target;
        }

        /// <summary>
        /// Set the target of this path to a static position target.
        /// </summary>
        /// <param name="target">The target position.</param>
        /// <summary>
        /// 정적인 위치를 목표로 경로의 목표를 설정합니다.
        /// </summary>
        /// <param name="target">목표 위치입니다.</param>
        public void SetTargetPosition(Vector3 target)
        {
            // If there is an nav mesh area close to the target use a point inside the nav mesh instead.
            // 목표 근처에 네비게이션 메시 영역이 있으면, 네비게이션 메시 내부의 지점을 대신 사용합니다.
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                target = hit.position;
            }

            m_PositionTarget = target;
            m_TransformTarget = null;
            RecalculatePath();
        }

        /// <summary>
        /// Call this to recalculate the path when the navigation mesh or dynamic obstacles changed.
        /// </summary>
        /// <summary>
        /// 내비게이션 메시나 동적 장애물이 변경될 때 경로를 다시 계산하려면 이 메서드를 호출하세요.
        /// </summary>
        void OnNavMeshChanged()
        {
            RecalculatePath();
        }

        /// <summary>
        /// Clears the path.
        /// </summary>
        /// <summary>
        /// 경로를 초기화합니다.
        /// </summary>
        public void Clear()
        {
            m_Path.Clear();
        }

        /// <summary>
        /// Gets the movement vector for moving this object while following the path.
        /// </summary>
        /// <param name="distance">The distance to move.</param>
        /// <returns>Returns the movement vector.</returns>
        /// <summary>
        /// 경로를 따라 이동할 때 객체를 이동시키는 벡터를 가져옵니다.
        /// </summary>
        /// <param name="distance">이동할 거리입니다.</param>
        /// <returns>이동 벡터를 반환합니다.</returns>
        public Vector3 MoveAlongPath(float distance)
        {
            if (m_TransformTarget != null)
            {
                OnTargetPositionChanged(TargetPosition);
            }

            if (m_Path.Count == 0)
            {
                return Vector3.zero;
            }

            var currentPredictedPosition = m_Agent.transform.position;
            var remainingDistance = distance;

            while (remainingDistance > 0)
            {
                var toNextPathPoint = m_Path[0] - currentPredictedPosition;

                // If end point is closer then distance to move
                // 종료 지점이 이동 거리보다 가까우면
                if (toNextPathPoint.sqrMagnitude < remainingDistance * remainingDistance)
                {
                    currentPredictedPosition = m_Path[0];
                    m_Path.RemoveAt(0);
                    remainingDistance -= toNextPathPoint.magnitude;
                }

                // Move towards point
                // 해당 지점을 향해 이동합니다.
                currentPredictedPosition += toNextPathPoint.normalized * remainingDistance;

                // There is definitely no remaining distance to cover here.
                // 남은 이동 거리가 없습니다.
                break;
            }

            return currentPredictedPosition - m_Agent.transform.position;
        }

        void OnTargetPositionChanged(Vector3 newTarget)
        {
            if (m_Path.Count == 0)
            {
                RecalculatePath();
            }

            if ((newTarget - m_CurrentPathOriginalTarget).sqrMagnitude > k_RepathToleranceSqr)
            {
                RecalculatePath();
            }
        }

        /// <summary>
        /// Recalculates the cached navigationPath
        /// </summary>
        /// <summary>
        /// 캐시된 내비게이션 경로를 다시 계산합니다.
        /// </summary>
        void RecalculatePath()
        {
            m_CurrentPathOriginalTarget = TargetPosition;
            m_Agent.CalculatePath(TargetPosition, m_NavMeshPath);

            m_Path.Clear();

            var corners = m_NavMeshPath.corners;

            for (int i = 1; i < corners.Length; i++) // Skip the first corner because it is the starting point.
            {
                // 첫 번째 코너는 시작 지점이므로 건너뜁니다.
                m_Path.Add(corners[i]);
            }
        }

        public void Dispose()
        {
            m_NavigationSystem.OnNavigationMeshChanged -= OnNavMeshChanged;
        }
    }
}
