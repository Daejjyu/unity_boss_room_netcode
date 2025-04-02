using System;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Gameplay.UserInput;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.BossRoom.Gameplay.Actions
{
    public partial class TargetAction
    {
        private GameObject m_TargetReticule; // 타겟 리티클
        private ulong m_CurrentTarget; // 현재 타겟 ID
        private ulong m_NewTarget; // 새 타겟 ID

        private const float k_ReticuleGroundHeight = 0.2f; // 리티클의 높이

        // 클라이언트에서 액션 시작 시 호출되는 메서드
        public override bool OnStartClient(ClientCharacter clientCharacter)
        {
            base.OnStartClient(clientCharacter);

            // 타겟이 변경될 때마다 호출되는 이벤트 핸들러 등록
            clientCharacter.serverCharacter.TargetId.OnValueChanged += OnTargetChanged;

            // 사용자 입력 이벤트 처리기 등록
            clientCharacter.serverCharacter.GetComponent<ClientInputSender>().ActionInputEvent += OnActionInput;

            return true;
        }

        // 타겟이 변경되었을 때 호출되는 메서드
        private void OnTargetChanged(ulong oldTarget, ulong newTarget)
        {
            m_NewTarget = newTarget; // 새 타겟 ID 업데이트
        }

        // 클라이언트에서 매 프레임마다 호출되는 업데이트 메서드
        public override bool OnUpdateClient(ClientCharacter clientCharacter)
        {
            // 현재 타겟과 새 타겟이 다를 경우에만 리티클을 업데이트
            if (m_CurrentTarget != m_NewTarget)
            {
                m_CurrentTarget = m_NewTarget;

                // 타겟 객체를 찾고, 타겟이 유효한지 체크
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(m_CurrentTarget, out NetworkObject targetObject))
                {
                    var targetEntity = targetObject != null ? targetObject.GetComponent<ITargetable>() : null;
                    if (targetEntity != null)
                    {
                        // 리티클 유효성 검사 및 활성화
                        ValidateReticule(clientCharacter, targetObject);
                        m_TargetReticule.SetActive(true);

                        var parentTransform = targetObject.transform;
                        if (targetObject.TryGetComponent(out ServerCharacter serverCharacter) && serverCharacter.clientCharacter)
                        {
                            // 캐릭터일 경우, 리티클을 자식 그래픽 객체에 붙임
                            parentTransform = serverCharacter.clientCharacter.transform;
                        }

                        // 리티클을 타겟 객체에 맞게 위치시키기
                        m_TargetReticule.transform.parent = parentTransform;
                        m_TargetReticule.transform.localPosition = new Vector3(0, k_ReticuleGroundHeight, 0);
                    }
                }
                else
                {
                    // 타겟이 없거나 타겟이 삭제된 경우 리티클 비활성화
                    if (m_TargetReticule != null)
                    {
                        m_TargetReticule.transform.parent = null;
                        m_TargetReticule.SetActive(false);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 타겟 리티클 GameObject가 존재하는지 확인하고 없으면 생성합니다.
        /// 리티클은 부모가 삭제될 경우 "실수로" 삭제될 수 있기 때문에 이를 방지하기 위해 확인합니다.
        /// </summary>
        void ValidateReticule(ClientCharacter parent, NetworkObject targetObject)
        {
            // 리티클이 없다면 새로 생성
            if (m_TargetReticule == null)
            {
                m_TargetReticule = Object.Instantiate(parent.TargetReticulePrefab);
            }

            // 타겟이 NPC인지, 자신이 NPC인지 체크
            bool target_isnpc = targetObject.GetComponent<ITargetable>().IsNpc;
            bool myself_isnpc = parent.serverCharacter.CharacterClass.IsNpc;
            bool hostile = target_isnpc != myself_isnpc; // 타겟이 적대적인지 확인

            // 타겟 리티클의 색상을 적대적/우호적에 맞게 설정
            m_TargetReticule.GetComponent<MeshRenderer>().material = hostile ? parent.ReticuleHostileMat : parent.ReticuleFriendlyMat;
        }

        // 클라이언트에서 액션을 취소할 때 호출되는 메서드
        public override void CancelClient(ClientCharacter clientCharacter)
        {
            // 리티클을 삭제
            GameObject.Destroy(m_TargetReticule);

            // 타겟 변경 이벤트 핸들러를 해제
            clientCharacter.serverCharacter.TargetId.OnValueChanged -= OnTargetChanged;

            // 입력 이벤트 처리기 해제
            if (clientCharacter.TryGetComponent(out ClientInputSender inputSender))
            {
                inputSender.ActionInputEvent -= OnActionInput;
            }
        }

        // 입력 이벤트가 발생했을 때 호출되는 메서드
        private void OnActionInput(ActionRequestData data)
        {
            // 이 메서드는 소유한 클라이언트에서 실행되며, 새로운 타겟을 예상하기 위해 호출됩니다.
            if (GameDataSource.Instance.GetActionPrototypeByID(data.ActionID).IsGeneralTargetAction)
            {
                // 일반 타겟 액션인 경우 새 타겟 ID 업데이트
                m_NewTarget = data.TargetIds[0];
            }
        }
    }
}
