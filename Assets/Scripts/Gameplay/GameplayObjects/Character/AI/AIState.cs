using System;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character.AI
{

    /// <summary>
    /// Base class for all AIStates
    /// </summary>
    /// <summary>
    /// 모든 AI 상태의 기본 클래스입니다.
    /// </summary>
    public abstract class AIState
    {
        /// <summary>
        /// Indicates whether this state thinks it can become/continue to be the active state.
        /// </summary>
        /// <summary>
        /// 이 상태가 활성 상태가 될 수 있는지 또는 계속 활성 상태로 유지될 수 있는지를 나타냅니다.
        /// </summary>
        /// <returns></returns>
        public abstract bool IsEligible();

        /// <summary>
        /// Called once each time this state becomes the active state.
        /// (This will only happen if IsEligible() has returned true for this state)
        /// </summary>
        /// <summary>
        /// 이 상태가 활성 상태가 될 때마다 한 번 호출됩니다.
        /// (이 상태에 대해 IsEligible()이 true를 반환한 경우에만 발생합니다)
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Called once per frame while this is the active state. Initialize() will have
        /// already been called prior to Update() being called
        /// </summary>
        /// <summary>
        /// 이 상태가 활성 상태인 동안 프레임마다 한 번 호출됩니다.
        /// Update()가 호출되기 전에 Initialize()가 이미 호출된 상태입니다.
        /// </summary>
        public abstract void Update();

    }
}
