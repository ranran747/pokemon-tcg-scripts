using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokemonTCG.Core.Architecture
{
    /// <summary>
    /// イベントバスシステム
    /// 疎結合なイベント通信とClaude拡張での動的イベント処理を提供
    /// Publisher-Subscriberパターンの実装
    /// </summary>
    public class EventBus : MonoBehaviourSingleton<EventBus>
    {
        #region Fields

        /// <summary>イベントリスナーのマップ</summary>
        private readonly Dictionary<Type, List<object>> _eventListeners = new Dictionary<Type, List<object>>();
        
        /// <summary>一回限りのイベントリスナー</summary>
        private readonly Dictionary<Type, List<object>> _onceListeners = new Dictionary<Type, List<object>>();
        
        /// <summary>遅延実行イベントキュー</summary>
        private readonly Queue<Action> _delayedEvents = new Queue<Action>();
        
        /// <summary>統計情報</summary>
        private readonly Dictionary<Type, int> _eventStats = new Dictionary<Type, int>();

        /// <summary>初期化順序（早期に初期化）</summary>
        public override int InitializationOrder => -900;

        #endregion

        #region Event Registration

        /// <summary>
        /// イベントリスナーを登録
        /// </summary>
        /// <typeparam name="T">イベント型</typeparam>
        /// <param name="listener">リスナー関数</param>
        public void Subscribe<T>(Action<T> listener) where T : class
        {
            Type eventType = typeof(T);
            
            if (!_eventListeners.ContainsKey(eventType))
            {
                _eventListeners[eventType] = new List<object>();
            }
            
            _eventListeners[eventType].Add(listener);
            
            Debug.Log($"[EventBus] Subscribed to event: {eventType.Name}");
        }

        /// <summary>
        /// 一回限りのイベントリスナーを登録
        /// </summary>
        /// <typeparam name="T">イベント型</typeparam>
        /// <param name="listener">リスナー関数</param>
        public void SubscribeOnce<T>(Action<T> listener) where T : class
        {
            Type eventType = typeof(T);
            
            if (!_onceListeners.ContainsKey(eventType))
            {
                _onceListeners[eventType] = new List<object>();
            }
            
            _onceListeners[eventType].Add(listener);
            
            Debug.Log($"[EventBus] Subscribed once to event: {eventType.Name}");
        }

        /// <summary>
        /// イベントリスナーの登録を解除
        /// </summary>
        /// <typeparam name="T">イベント型</typeparam>
        /// <param name="listener">リスナー関数</param>
        public void Unsubscribe<T>(Action<T> listener) where T : class
        {
            Type eventType = typeof(T);
            
            if (_eventListeners.TryGetValue(eventType, out var listeners))
            {
                listeners.Remove(listener);
                
                if (listeners.Count == 0)
                {
                    _eventListeners.Remove(eventType);
                }
            }
            
            Debug.Log($"[EventBus] Unsubscribed from event: {eventType.Name}");
        }

        /// <summary>
        /// 指定型のすべてのリスナーを解除
        /// </summary>
        /// <typeparam name="T">イベント型</typeparam>
        public void UnsubscribeAll<T>() where T : class
        {
            Type eventType = typeof(T);
            
            _eventListeners.Remove(eventType);
            _onceListeners.Remove(eventType);
            
            Debug.Log($"[EventBus] Unsubscribed all listeners for event: {eventType.Name}");
        }

        #endregion

        #region Event Publishing

        /// <summary>
        /// イベントを即座に発行
        /// </summary>
        /// <typeparam name="T">イベント型</typeparam>
        /// <param name="eventData">イベントデータ</param>
        public void Publish<T>(T eventData) where T : class
        {
            PublishInternal(eventData, immediate: true);
        }

        /// <summary>
        /// イベントを次フレームで発行
        /// </summary>
        /// <typeparam name="T">イベント型</typeparam>
        /// <param name="eventData">イベントデータ</param>
        public void PublishDelayed<T>(T eventData) where T : class
        {
            _delayedEvents.Enqueue(() => PublishInternal(eventData, immediate: true));
        }

        /// <summary>
        /// 内部イベント発行処理
        /// </summary>
        /// <typeparam name="T">イベント型</typeparam>
        /// <param name="eventData">イベントデータ</param>
        /// <param name="immediate">即座に実行するか</param>
        private void PublishInternal<T>(T eventData, bool immediate) where T : class
        {
            Type eventType = typeof(T);
            
            // 統計更新
            if (!_eventStats.ContainsKey(eventType))
            {
                _eventStats[eventType] = 0;
            }
            _eventStats[eventType]++;

            int totalListeners = 0;

            // 通常リスナーの実行
            if (_eventListeners.TryGetValue(eventType, out var listeners))
            {
                // リストのコピーを作成（実行中の変更に対応）
                var listenersToExecute = new List<object>(listeners);
                
                foreach (var listener in listenersToExecute)
                {
                    try
                    {
                        if (listener is Action<T> action)
                        {
                            action.Invoke(eventData);
                            totalListeners++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EventBus] Error executing listener for {eventType.Name}: {ex.Message}");
                    }
                }
            }

            // 一回限りリスナーの実行と削除
            if (_onceListeners.TryGetValue(eventType, out var onceListeners))
            {
                var listenersToExecute = new List<object>(onceListeners);
                onceListeners.Clear(); // 先にクリア
                
                foreach (var listener in listenersToExecute)
                {
                    try
                    {
                        if (listener is Action<T> action)
                        {
                            action.Invoke(eventData);
                            totalListeners++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EventBus] Error executing once listener for {eventType.Name}: {ex.Message}");
                    }
                }
                
                if (onceListeners.Count == 0)
                {
                    _onceListeners.Remove(eventType);
                }
            }

            Debug.Log($"[EventBus] Published event {eventType.Name} to {totalListeners} listeners");
        }

        #endregion

        #region Static Convenience Methods

        /// <summary>静的メソッド: イベント購読</summary>
        public static void On<T>(Action<T> listener) where T : class
        {
            Instance?.Subscribe(listener);
        }

        /// <summary>静的メソッド: 一回限りイベント購読</summary>
        public static void Once<T>(Action<T> listener) where T : class
        {
            Instance?.SubscribeOnce(listener);
        }

        /// <summary>静的メソッド: イベント購読解除</summary>
        public static void Off<T>(Action<T> listener) where T : class
        {
            Instance?.Unsubscribe(listener);
        }

        /// <summary>静的メソッド: イベント発行</summary>
        public static void Emit<T>(T eventData) where T : class
        {
            Instance?.Publish(eventData);
        }

        /// <summary>静的メソッド: 遅延イベント発行</summary>
        public static void EmitDelayed<T>(T eventData) where T : class
        {
            Instance?.PublishDelayed(eventData);
        }

        #endregion

        #region Unity Lifecycle

        protected override void OnInitialize()
        {
            Debug.Log("[EventBus] EventBus initialized.");
        }

        protected override void OnDispose()
        {
            _eventListeners.Clear();
            _onceListeners.Clear();
            _delayedEvents.Clear();
            _eventStats.Clear();
            Debug.Log("[EventBus] EventBus disposed.");
        }

        void Update()
        {
            // 遅延イベントの処理
            ProcessDelayedEvents();
        }

        #endregion

        #region Delayed Events Processing

        /// <summary>
        /// 遅延イベントを処理
        /// </summary>
        private void ProcessDelayedEvents()
        {
            int processedCount = 0;
            
            while (_delayedEvents.Count > 0 && processedCount < 10) // フレームあたり最大10イベント
            {
                try
                {
                    var eventAction = _delayedEvents.Dequeue();
                    eventAction?.Invoke();
                    processedCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventBus] Error processing delayed event: {ex.Message}");
                }
            }
        }

        #endregion

        #region Debug Information

        /// <summary>
        /// 登録されているイベントリスナー情報をログ出力
        /// </summary>
        [ContextMenu("Log Event Listeners")]
        public void LogEventListeners()
        {
            Debug.Log($"[EventBus] Registered Event Listeners:");
            
            foreach (var kvp in _eventListeners)
            {
                Debug.Log($"  - {kvp.Key.Name}: {kvp.Value.Count} listeners");
            }
            
            foreach (var kvp in _onceListeners)
            {
                Debug.Log($"  - {kvp.Key.Name}: {kvp.Value.Count} once listeners");
            }
        }

        /// <summary>
        /// イベント統計情報をログ出力
        /// </summary>
        [ContextMenu("Log Event Stats")]
        public void LogEventStats()
        {
            Debug.Log($"[EventBus] Event Statistics:");
            
            foreach (var kvp in _eventStats)
            {
                Debug.Log($"  - {kvp.Key.Name}: {kvp.Value} times published");
            }
        }

        #endregion
    }
}