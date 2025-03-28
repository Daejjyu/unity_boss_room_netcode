using System;
using UnityEngine;
using VContainer.Unity;

namespace Unity.BossRoom.Gameplay.GameState
{
    /// <summary>
    /// 게임의 다양한 상태를 관리하는 열거형입니다.
    /// 각 게임 상태는 하나의 씬과 대응되며, 상태에 따라 게임의 흐름이 달라집니다.
    /// </summary>
    public enum GameState
    {
        MainMenu,
        CharSelect,
        BossRoom,
        PostGame
    }

    /// <summary>
    /// 이 컴포넌트는 개별적인 게임 상태와 그 의존성을 나타냅니다. 이 컴포넌트의 특수한 기능은
    /// 한 번에 하나의 GameState만 실행될 것이라는 보장을 제공합니다.
    /// </summary>
    /// <remarks>
    /// Q: GameState와 Scene 간의 관계는 무엇인가요?
    /// A: 상태와 씬 사이에는 1:다 관계가 있습니다. 즉, 각 씬은 정확히 하나의 상태에 대응하지만,
    ///    하나의 상태는 여러 씬에서 존재할 수 있습니다.
    /// Q: 상태 전환은 어떻게 이루어지나요?
    /// A: 상태 전환은 서버 코드에서 NetworkManager.SceneManager.LoadScene을 호출하여 암시적으로 이루어집니다. 이는
    ///    중요합니다. 왜냐하면 상태 전환이 씬 전환과 별도로 이루어지면, 자신이 실행되는 씬에 대해 신경 쓰는 상태는
    ///    씬 로드를 동기화하는 논리가 필요할 수 있기 때문입니다.
    /// Q: GameStateBehaviour는 몇 개가 있나요?
    /// A: 서버에 하나, 클라이언트에 하나가 있습니다 (호스트에서는 서버와 클라이언트 GameStateBehaviour가 동시에 실행되며,
    ///    네트워크화된 프리팹과 마찬가지로).
    /// Q: MonoBehaviour이지만 여러 씬을 넘나드는 상태가 어떻게 유지될 수 있나요?
    /// A: Persists 속성을 true로 설정하세요. 동일한 게임 상태가 있는 다른 씬으로 전환하면,
    ///    현재 GameState 객체는 계속 살아있고, 새로운 씬에서는 그 객체가 자동으로 제거되어 자리를 비웁니다.
    ///
    /// 중요한 참고 사항: 모든 씬에 GameState 객체가 있다고 가정합니다. 그렇지 않다면, Persisting 상태가
    /// 그 생애를 초과할 수 있습니다 (후속 상태가 없어 이를 정리할 방법이 없기 때문입니다).
    /// </remarks>

    /// <summary>
    /// 게임 상태를 나타내는 특수한 컴포넌트입니다. 이 컴포넌트는 각 게임 상태와 그 의존성을 관리하며,
    /// 중요한 점은 한 번에 하나의 GameState만 활성화되어 실행된다는 보장이 있다는 것입니다.
    /// </summary>
    /// <remarks>
    /// 게임 상태와 씬 간의 관계는 1대 다 관계입니다. 즉, 각 씬은 정확히 하나의 상태에 해당하지만,
    /// 하나의 상태는 여러 씬에서 사용될 수 있습니다. 상태 전환은 씬 전환에 의해 자동으로 이루어집니다.
    /// </remarks>
    public abstract class GameStateBehaviour : LifetimeScope
    {
        /// <summary>
        /// 이 GameState가 여러 씬에 걸쳐 지속되나요?
        /// </summary>
        /// <summary>
        /// 이 상태가 여러 씬을 넘어서서 지속될 것인지 여부를 결정합니다.
        /// 기본적으로는 false로 설정되어 있으며, 상태가 씬을 넘어서 살아남고 싶다면 true로 설정해야 합니다.
        /// </summary>
        public virtual bool Persists
        {
            get { return false; }
        }

        /// <summary>
        /// 이 객체가 나타내는 GameState는 무엇인가요? 서버와 클라이언트에서 상태의 특수화는 항상 동일한 열거형을 반환해야 합니다.
        /// </summary>

        /// <summary>
        /// 이 컴포넌트가 나타내는 게임 상태입니다. 서버와 클라이언트에서 각각의 상태는 같은 열거형을 반환해야 합니다.
        /// </summary>

        public abstract GameState ActiveState { get; }

        /// <summary>
        /// 이 객체는 단 하나의 활성 GameState 객체입니다. 하나만 존재할 수 있습니다.
        /// </summary>
        /// <summary>
        /// 현재 활성화된 GameState 객체를 참조하는 정적 변수입니다.
        /// 각 씬에 대해 하나의 활성 상태만 존재할 수 있도록 보장합니다.
        /// </summary>
        private static GameObject s_ActiveStateGO;

        /// <summary>
        /// MonoBehaviour의 Awake 메서드입니다.
        /// 부모 컨테이너에서 의존성 주입을 수행합니다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (Parent != null)
            {
                Parent.Container.Inject(this);
            }
        }

        // Start는 첫 번째 프레임 업데이트 전에 호출됩니다
        /// <summary>
        /// MonoBehaviour의 Start 메서드입니다.
        /// 게임 상태가 이미 존재하는 상태인지 확인하고, 상태 전환 로직을 처리합니다.
        /// </summary>
        protected virtual void Start()
        {
            // 만약 이미 활성 상태 객체가 있다면, 기존 상태와 비교하여 전환 여부를 결정
            if (s_ActiveStateGO != null)
            {
                // 현재 게임 오브젝트가 이미 활성 상태 객체인 경우
                if (s_ActiveStateGO == gameObject)
                {
                    // 이미 활성 상태 객체이면 아무 것도 할 필요가 없습니다.
                    return;
                }

                // 호스트에서, 이 코드는 클라이언트 또는 서버 버전을 반환할 수 있지만, 어떤 것이든 상관없습니다;
                // 우리는 그 타입과 지속 상태에 대해서만 궁금합니다.
                // 이전 상태 객체를 가져옴
                var previousState = s_ActiveStateGO.GetComponent<GameStateBehaviour>();

                // 이전 상태가 지속 가능한 상태이고, 현재 상태와 동일하면 이 객체는 파괴됨
                if (previousState.Persists && previousState.ActiveState == ActiveState)
                {
                    // 이미 존재하는 DontDestroyOnLoad 상태를 위한 자리를 만들어야 합니다.
                    Destroy(gameObject); // 이미 존재하는 Persist 상태가 있으면 새로운 상태 객체를 파괴
                    return;
                }

                // 그렇지 않으면, 이전 상태가 사라집니다. 지속되는 상태가 아니었거나, 그 상태가
                // 다른 종류의 상태라서 그렇습니다. 어느 쪽이든, 우리는 그것을 대체하려고 합니다.
                // 이전 상태가 사라지고, 새로운 상태가 활성화됨
                Destroy(s_ActiveStateGO); // 이전 상태 객체를 파괴
            }

            // 현재 게임 오브젝트를 활성 상태로 설정
            s_ActiveStateGO = gameObject;

            // 만약 상태가 Persisting이라면 씬 전환 간에도 객체가 파괴되지 않도록 설정
            if (Persists)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// MonoBehaviour의 OnDestroy 메서드입니다.
        /// 상태 객체가 더 이상 필요 없으면 이를 처리합니다.
        /// </summary>
        protected override void OnDestroy()
        {
            // Persisting 상태가 아니라면 활성 상태 객체를 null로 설정
            if (!Persists)
            {
                s_ActiveStateGO = null;
            }
        }
    }
}
