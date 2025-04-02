using System;

namespace Unity.BossRoom.Infrastructure
{
    public class BufferedMessageChannel<T> : MessageChannel<T>, IBufferedMessageChannel<T>
    {
        public override void Publish(T message)
        {
            HasBufferedMessage = true;
            BufferedMessage = message;
            //미리 전송한다
            base.Publish(message);
        }

        public override IDisposable Subscribe(Action<T> handler)
        {
            var subscription = base.Subscribe(handler);

            if (HasBufferedMessage)
            {
                // 신규 구독자에게 이전 메시지를 전송해준다
                handler?.Invoke(BufferedMessage);
            }

            return subscription;
        }

        public bool HasBufferedMessage { get; private set; } = false;
        public T BufferedMessage { get; private set; }
    }
}
