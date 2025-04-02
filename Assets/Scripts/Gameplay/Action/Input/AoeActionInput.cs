using Unity.BossRoom.Gameplay.GameplayObjects;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// 이 클래스는 AoE(범위 공격) 능력의 첫 번째 단계입니다. 초기 입력 시각화의 위치를 업데이트하고 사용자 입력을 추적하는 역할을 합니다.
    /// 능력이 확인되고 마우스가 클릭되면, 서버에 적절한 RPC를 보내 AoE 서버 측 게임 플레이 로직을 트리거합니다.
    /// 서버 측 게임 플레이 액션은 이후 클라이언트 측 결과적인 FX를 트리거합니다.
    /// 이 액션의 흐름은 다음과 같습니다: (클라이언트) AoEActionInput --> (서버) AoEAction --> (클라이언트) AoEActionFX
    /// </summary>
    public class AoeActionInput : BaseActionInput
    {
        [SerializeField]
        GameObject m_InRangeVisualization;

        [SerializeField]
        GameObject m_OutOfRangeVisualization;

        Camera m_Camera;

        // 일반적인 액션 시스템은 마우스 다운 이벤트를 기반으로 작동합니다 (충전된 액션을 지원하기 위해),
        // 하지만 내부적으로 마우스 업 이벤트만 기다리면 사용자가 액션 입력을 시작한 UI 클릭과 동일한 마우스 클릭으로 발생하기 때문에
        // 마우스 다운과 마우스 업 주기를 추적하면 사용자가 액션을 시작한 버튼에서 NavMesh로 마우스를 클릭할 수 있습니다.
        bool m_ReceivedMouseDownEvent;

        NavMeshHit m_NavMeshHit;

        // y 방향으로 위를 향한 노말이 있는 평면, 원점에서 0의 거리 단위로 이동된 평면
        // 이는 게임 내 NavMesh와 동일한 높이입니다
        static readonly Plane k_Plane = new Plane(Vector3.up, 0f);

        void Start()
        {
            var radius = GameDataSource.Instance.GetActionPrototypeByID(m_ActionPrototypeID).Config.Radius;

            transform.localScale = new Vector3(radius * 2, radius * 2, radius * 2);
            m_Camera = Camera.main;
        }

        void Update()
        {
            if (PlaneRaycast(k_Plane, m_Camera.ScreenPointToRay(Input.mousePosition), out Vector3 pointOnPlane) &&
                NavMesh.SamplePosition(pointOnPlane, out m_NavMeshHit, 2f, NavMesh.AllAreas))
            {
                transform.position = m_NavMeshHit.position;
            }

            float range = GameDataSource.Instance.GetActionPrototypeByID(m_ActionPrototypeID).Config.Range;
            bool isInRange = (m_Origin - transform.position).sqrMagnitude <= range * range;
            m_InRangeVisualization.SetActive(isInRange);
            m_OutOfRangeVisualization.SetActive(!isInRange);

            // 플레이어가 클릭하고 마우스 버튼을 놓은 후 입력을 받습니다
            if (Input.GetMouseButtonDown(0))
            {
                m_ReceivedMouseDownEvent = true;
            }

            if (Input.GetMouseButtonUp(0) && m_ReceivedMouseDownEvent)
            {
                if (isInRange)
                {
                    var data = new ActionRequestData
                    {
                        Position = transform.position,
                        ActionID = m_ActionPrototypeID,
                        ShouldQueue = false,
                        TargetIds = null
                    };
                    m_SendInput(data);
                }
                Destroy(gameObject);
                return;
            }
        }

        /// <summary>
        /// 주어진 평면에 대해 레이캐스트를 시뮬레이션하는 유틸리티 메서드입니다. 물리 기반 레이캐스트를 사용하지 않습니다.
        /// </summary>
        /// <remarks> 문서화된 예제를 기반으로 합니다: https://docs.unity3d.com/ScriptReference/Plane.Raycast.html
        /// </remarks>
        /// <param name="plane"></param>
        /// <param name="ray"></param>
        /// <param name="pointOnPlane"></param>
        /// <returns> NavMesh 내에 교차점이 있으면 true, 아니면 false </returns>
        static bool PlaneRaycast(Plane plane, Ray ray, out Vector3 pointOnPlane)
        {
            // 이 레이가 평면과 교차하는지 확인
            if (plane.Raycast(ray, out var enter))
            {
                // 교차점의 지점을 얻음
                pointOnPlane = ray.GetPoint(enter);
                return true;
            }
            else
            {
                pointOnPlane = Vector3.zero;
                return false;
            }
        }
    }
}
