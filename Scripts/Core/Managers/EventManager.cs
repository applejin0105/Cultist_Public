using System;
using System.Collections.Generic;

namespace Core.Managers
{
    public sealed class EventManager
    {
        private static readonly Dictionary<Type, Delegate> Events = new();

        /// <summary>
        ///     이벤트 구독
        ///     제한자 where T : struct를 통해 구조체지만 이벤트로 쓸 수 있게 강제. GC 방지.
        /// </summary>
        public static void AddListener<T>(Action<T> listener) where T : struct
        {
            var eventType = typeof(T);

            if (!Events.TryAdd(eventType, listener)) Events[eventType] = Delegate.Combine(Events[eventType], listener);
        }

        /// <summary>
        ///     이벤트 구독 취소
        ///     메모리 누수 방지용으로 필수임
        /// </summary>
        public static void RemoveListener<T>(Action<T> listener) where T : struct
        {
            var eventType = typeof(T);

            if (!Events.TryGetValue(eventType, out var @event)) return;

            var currentDelegate = Delegate.Remove(@event, listener);

            if (currentDelegate == null)
                Events.Remove(eventType);
            else
                Events[eventType] = currentDelegate;
        }


        /// <summary>
        ///     이벤트 발생
        /// </summary>
        public static void TriggerEvent<T>(T eventStruct) where T : struct
        {
            var eventType = typeof(T);

            if (Events.TryGetValue(eventType, out var action)) (action as Action<T>)?.Invoke(eventStruct);
        }

        /// <summary>
        /// 하드 리셋용: 모든 정적 이벤트 구독을 강제 해제
        /// </summary>
        public static void ClearAllEvents()
        {
            Events.Clear();
        }
    }
}