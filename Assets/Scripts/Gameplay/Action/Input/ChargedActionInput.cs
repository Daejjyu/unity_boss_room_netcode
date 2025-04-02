using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    public class ChargedActionInput : BaseActionInput
    {
        protected float m_StartTime;

        private void Start()
        {
            // get our particle near the right spot!
            // 적절한 위치에 입자를 배치합니다!
            transform.position = m_Origin;

            m_StartTime = Time.time;
            // right now we only support "untargeted" charged attacks.
            // 현재는 "타겟 없는" 차지 공격만 지원합니다.
            // Will need more input (e.g. click position) for fancier types of charged attacks!
            // 더 복잡한 종류의 차지 공격을 위해서는 추가 입력 (예: 클릭 위치)이 필요합니다!
            var data = new ActionRequestData
            {
                Position = transform.position,
                ActionID = m_ActionPrototypeID,
                ShouldQueue = false,
                TargetIds = null
            };
            m_SendInput(data);
        }

        public override void OnReleaseKey()
        {
            m_PlayerOwner.ServerStopChargingUpRpc();
            Destroy(gameObject);
        }

    }
}
