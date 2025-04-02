using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.BossRoom.Navigation
{
    /// <summary>
    /// This system exists to coordinate path finding and navigation functionality in a scene.
    /// The Unity NavMesh is only used to calculate navigation paths. Moving along those paths is done by this system.
    /// </summary>
    /// <summary>
    /// 이 시스템은 씬에서 경로 찾기 및 내비게이션 기능을 조정하기 위해 존재합니다.
    /// Unity NavMesh는 내비게이션 경로를 계산하는 데만 사용됩니다. 해당 경로를 따라 이동하는 것은 이 시스템에서 수행됩니다.
    /// </summary>
    public class NavigationSystem : MonoBehaviour
    {
        public const string NavigationSystemTag = "NavigationSystem";

        /// <summary>
        /// Event that gets invoked when the navigation mesh changed. This happens when dynamic obstacles move or get active
        /// </summary>
        /// <summary>
        /// 내비게이션 메시가 변경될 때 호출되는 이벤트입니다. 동적 장애물이 이동하거나 활성화될 때 발생합니다.
        /// </summary>
        public event System.Action OnNavigationMeshChanged = delegate { };

        /// <summary>
        /// Whether all paths need to be recalculated in the next fixed update.
        /// </summary>
        /// <summary>
        /// 다음 Fixed Update에서 모든 경로를 다시 계산해야 하는지 여부를 나타냅니다.
        /// </summary>
        private bool m_NavMeshChanged;

        public void OnDynamicObstacleDisabled()
        {
            m_NavMeshChanged = true;
        }

        public void OnDynamicObstacleEnabled()
        {
            m_NavMeshChanged = true;
        }

        private void FixedUpdate()
        {
            // This is done in fixed update to make sure that only one expensive global recalculation happens per fixed update.
            // 이는 Fixed Update에서 수행되며, 한 번의 Fixed Update에서만 비용이 많이 드는 전역 재계산이 발생하도록 보장합니다.
            if (m_NavMeshChanged)
            {
                OnNavigationMeshChanged.Invoke();
                m_NavMeshChanged = false;
            }
        }

        private void OnValidate()
        {
            Assert.AreEqual(NavigationSystemTag, tag, $"The GameObject of the {nameof(NavigationSystem)} component has to use the {NavigationSystem.NavigationSystemTag} tag!");
            // {nameof(NavigationSystem)} 컴포넌트가 포함된 GameObject는 반드시 {NavigationSystem.NavigationSystemTag} 태그를 사용해야 합니다!
        }
    }
}
