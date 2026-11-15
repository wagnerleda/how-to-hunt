#nullable enable
using System;

namespace HowToHunt.Foundation
{
    /// <summary>Handler for a typed event. Events are structs passed by readonly reference: no boxing, no garbage.</summary>
    public delegate void EventHandlerIn<T>(in T evt) where T : struct;

    /// <summary>
    /// Typed, allocation-free event bus for cross-feature communication.
    /// Publishing never allocates. Subscribing copies the handler array (rare, at init time).
    /// </summary>
    public static class EventBus
    {
        private static Action[] _resetters = new Action[16];
        private static int _resetterCount;

        public static void Publish<T>(in T evt) where T : struct => Channel<T>.Publish(in evt);

        public static void Subscribe<T>(EventHandlerIn<T> handler) where T : struct => Channel<T>.Subscribe(handler);

        public static void Unsubscribe<T>(EventHandlerIn<T> handler) where T : struct => Channel<T>.Unsubscribe(handler);

        public static int SubscriberCount<T>() where T : struct => Channel<T>.Count;

        /// <summary>Removes every subscriber of every channel. Used on shutdown and between tests.</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < _resetterCount; i++) _resetters[i]();
        }

        private static void RegisterResetter(Action reset)
        {
            if (_resetterCount == _resetters.Length) Array.Resize(ref _resetters, _resetters.Length * 2);
            _resetters[_resetterCount++] = reset;
        }

        private static class Channel<T> where T : struct
        {
            // Copy-on-write: Publish iterates a stable snapshot even if handlers (un)subscribe mid-dispatch.
            private static EventHandlerIn<T>[] _handlers = Array.Empty<EventHandlerIn<T>>();

            static Channel() => RegisterResetter(() => _handlers = Array.Empty<EventHandlerIn<T>>());

            public static int Count => _handlers.Length;

            public static void Publish(in T evt)
            {
                EventHandlerIn<T>[] snapshot = _handlers;
                for (int i = 0; i < snapshot.Length; i++) snapshot[i](in evt);
            }

            public static void Subscribe(EventHandlerIn<T> handler)
            {
                EventHandlerIn<T>[] next = new EventHandlerIn<T>[_handlers.Length + 1];
                Array.Copy(_handlers, next, _handlers.Length);
                next[_handlers.Length] = handler;
                _handlers = next;
            }

            public static void Unsubscribe(EventHandlerIn<T> handler)
            {
                int index = Array.IndexOf(_handlers, handler);
                if (index < 0) return;
                EventHandlerIn<T>[] next = new EventHandlerIn<T>[_handlers.Length - 1];
                Array.Copy(_handlers, 0, next, 0, index);
                Array.Copy(_handlers, index + 1, next, index, _handlers.Length - index - 1);
                _handlers = next;
            }
        }
    }
}
