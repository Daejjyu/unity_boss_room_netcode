using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// Some objects might need to be on a slower update loop than the usual MonoBehaviour Update and without precise timing, e.g. to refresh data from services.
    /// Some might also not want to be coupled to a Unity object at all but still need an update loop.
    /// </summary>
    /// <summary>
    /// 일부 객체는 일반적인 MonoBehaviour Update보다 더 느린 업데이트 루프가 필요하거나 정확한 타이밍 없이 업데이트가 필요할 수 있습니다. 예를 들어 서비스에서 데이터를 새로 고치는 용도입니다.
    /// 일부 객체는 Unity 오브젝트와 전혀 연결되지 않길 원할 수도 있지만 여전히 업데이트 루프가 필요합니다.
    /// </summary>
    public class UpdateRunner : MonoBehaviour
    {
        // 구독자 데이터 클래스: 각 구독자마다 주기, 마지막 호출 시간, 다음 호출 시간을 저장
        class SubscriberData
        {
            public float Period;            // 구독자의 업데이트 주기 (초 단위)
            public float NextCallTime;      // 다음 호출 시간을 추적
            public float LastCallTime;      // 마지막 호출 시간을 추적
        }

        // 대기 중인 핸들러를 처리할 큐
        readonly Queue<Action> m_PendingHandlers = new Queue<Action>();

        // 구독자들을 추적할 해시셋 (구독자마다 주기적인 업데이트 호출)
        readonly HashSet<Action<float>> m_Subscribers = new HashSet<Action<float>>();

        // 각 구독자에 대한 데이터(주기, 호출 시간 등)를 저장할 딕셔너리
        readonly Dictionary<Action<float>, SubscriberData> m_SubscriberData = new Dictionary<Action<float>, SubscriberData>();

        // 객체가 파괴될 때 호출, 구독자 및 데이터 구조를 정리
        public void OnDestroy()
        {
            m_PendingHandlers.Clear();
            m_Subscribers.Clear();
            m_SubscriberData.Clear();
        }

        /// <summary>
        /// Subscribe in order to have onUpdate called approximately every period seconds (or every frame, if period <= 0).
        /// Don't assume that onUpdate will be called in any particular order compared to other subscribers.
        /// </summary>
        /// <summary>
        /// onUpdate가 약 period 초마다 호출되도록 구독합니다. (period <= 0이면 매 프레임 호출)
        /// 다른 구독자들과 비교하여 onUpdate가 반드시 특정 순서로 호출된다고 가정하지 마세요.
        /// </summary>
        public void Subscribe(Action<float> onUpdate, float updatePeriod)
        {
            if (onUpdate == null)
            {
                return;
            }

            // local function은 구독할 수 없음을 체크 (구독 해제가 불가능함)
            if (onUpdate.Target == null)
            {
                Debug.LogError("Can't subscribe to a local function that can go out of scope and can't be unsubscribed from");
                return;
            }

            // 익명 함수로 구독하는 것을 방지 (구독 해제가 불가능함)
            if (onUpdate.Method.ToString().Contains("<"))
            {
                Debug.LogError("Can't subscribe with an anonymous function that cannot be Unsubscribed, by checking for a character that can't exist in a declared method name.");
                return;
            }

            // 구독자가 이미 구독 목록에 없다면 구독을 추가
            if (!m_Subscribers.Contains(onUpdate))
            {
                m_PendingHandlers.Enqueue(() =>
                {
                    // 구독이 추가되면 해당 구독자에 대한 데이터를 딕셔너리에 저장
                    if (m_Subscribers.Add(onUpdate))
                    {
                        m_SubscriberData.Add(onUpdate, new SubscriberData() { Period = updatePeriod, NextCallTime = 0, LastCallTime = Time.time });
                    }
                });
            }
        }

        /// <summary>
        /// Safe to call even if onUpdate was not previously Subscribed.
        /// </summary>
        /// <summary>
        /// onUpdate가 이전에 구독되지 않았더라도 안전하게 호출할 수 있습니다.
        /// </summary>
        public void Unsubscribe(Action<float> onUpdate)
        {
            m_PendingHandlers.Enqueue(() =>
            {
                // 구독자와 해당 데이터를 제거
                m_Subscribers.Remove(onUpdate);
                m_SubscriberData.Remove(onUpdate);
            });
        }

        /// <summary>
        /// Each frame, advance all subscribers. Any that have hit their period should then act, though if they take too long they could be removed.
        /// </summary>
        /// <summary>
        /// 매 프레임마다 모든 구독자를 진행시킵니다. 만약 구독자가 지정된 주기를 초과하면 그에 맞는 동작을 취합니다. 그러나 시간이 너무 오래 걸리면 제거될 수 있습니다.
        /// </summary>
        void Update()
        {
            // 대기 중인 핸들러들이 있다면 모두 실행
            while (m_PendingHandlers.Count > 0)
            {
                m_PendingHandlers.Dequeue()?.Invoke();
            }

            // 구독자들을 순회하면서 주기가 끝났는지 확인하고, 끝났으면 콜백을 호출
            foreach (var subscriber in m_Subscribers)
            {
                var subscriberData = m_SubscriberData[subscriber];

                // 구독자의 주기가 끝났으면 onUpdate 호출
                if (Time.time >= subscriberData.NextCallTime)
                {
                    subscriber.Invoke(Time.time - subscriberData.LastCallTime);  // 지난 시간 간격을 전달
                    subscriberData.LastCallTime = Time.time;                       // 마지막 호출 시간 갱신
                    subscriberData.NextCallTime = Time.time + subscriberData.Period; // 다음 호출 시간 갱신
                }
            }
        }
    }
}
