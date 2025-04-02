using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace Unity.BossRoom.Infrastructure
{
    public class MessageChannel<T> : IMessageChannel<T>
    {
        readonly List<Action<T>> m_MessageHandlers = new List<Action<T>>();

        /// This dictionary of handlers to be either added or removed is used to prevent problems from immediate
        /// modification of the list of subscribers. It could happen if one decides to unsubscribe in a message handler
        /// etc.A true value means this handler should be added, and a false one means it should be removed
        /// 이 핸들러의 사전(Dictionary)은 구독자 목록을 즉시 수정함으로써 발생할 수 있는 문제를 방지하는 데 사용됩니다.
        /// 예를 들어, 메시지 핸들러 내에서 구독을 취소하려는 경우 등이 이에 해당합니다.
        /// `true` 값은 이 핸들러가 추가되어야 함을 의미하며, `false` 값은 이 핸들러가 제거되어야 함을 의미합니다.
        readonly Dictionary<Action<T>, bool> m_PendingHandlers = new Dictionary<Action<T>, bool>();

        public bool IsDisposed { get; private set; } = false;

        public virtual void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                m_MessageHandlers.Clear();
                m_PendingHandlers.Clear();
            }
        }

        public virtual void Publish(T message)
        {
            foreach (var handler in m_PendingHandlers.Keys)
            {
                if (m_PendingHandlers[handler])
                {
                    m_MessageHandlers.Add(handler);
                }
                else
                {
                    m_MessageHandlers.Remove(handler);
                }
            }
            m_PendingHandlers.Clear();

            foreach (var messageHandler in m_MessageHandlers)
            {
                if (messageHandler != null)
                {
                    messageHandler.Invoke(message);
                }
            }
        }

        public virtual IDisposable Subscribe(Action<T> handler)
        {
            Assert.IsTrue(!IsSubscribed(handler), "Attempting to subscribe with the same handler more than once");

            if (m_PendingHandlers.ContainsKey(handler))
            {
                if (!m_PendingHandlers[handler])
                {
                    m_PendingHandlers.Remove(handler);
                }
            }
            else
            {
                m_PendingHandlers[handler] = true;
            }

            var subscription = new DisposableSubscription<T>(this, handler);
            return subscription;
        }

        public void Unsubscribe(Action<T> handler)
        {
            if (IsSubscribed(handler))
            {
                if (m_PendingHandlers.ContainsKey(handler))
                {
                    if (m_PendingHandlers[handler])
                    {
                        m_PendingHandlers.Remove(handler);
                    }
                }
                else
                {
                    m_PendingHandlers[handler] = false;
                }
            }
        }

        bool IsSubscribed(Action<T> handler)
        {
            var isPendingRemoval = m_PendingHandlers.ContainsKey(handler) && !m_PendingHandlers[handler];
            var isPendingAdding = m_PendingHandlers.ContainsKey(handler) && m_PendingHandlers[handler];
            return m_MessageHandlers.Contains(handler) && !isPendingRemoval || isPendingAdding;
        }
    }
}
