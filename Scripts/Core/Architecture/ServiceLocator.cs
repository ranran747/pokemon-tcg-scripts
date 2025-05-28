using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PokemonTCG.Core.Architecture
{
    /// <summary>
    /// サービスロケーターパターン実装
    /// 依存性注入とサービス管理の中核システム
    /// Claude拡張クラスの動的登録・取得を可能にする
    /// </summary>
    public class ServiceLocator : MonoBehaviourSingleton<ServiceLocator>
    {
        #region Fields

        /// <summary>登録されたサービスのコンテナ</summary>
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        
        /// <summary>登録されたマネージャーのリスト（初期化順序管理用）</summary>
        private readonly List<IManager> _managers = new List<IManager>();
        
        /// <summary>サービス登録時のコールバック</summary>
        private readonly Dictionary<Type, List<Action<object>>> _serviceCallbacks = new Dictionary<Type, List<Action<object>>>();

        /// <summary>初期化順序</summary>
        public override int InitializationOrder => -1000; // 最優先で初期化

        #endregion

        #region Service Registration

        /// <summary>
        /// サービスを登録
        /// </summary>
        /// <typeparam name="TInterface">サービスのインターフェース型</typeparam>
        /// <typeparam name="TImplementation">サービスの実装型</typeparam>
        /// <param name="implementation">実装インスタンス</param>
        public void RegisterService<TInterface, TImplementation>(TImplementation implementation) 
            where TInterface : class
            where TImplementation : class, TInterface
        {
            RegisterService<TInterface>(implementation);
        }

        /// <summary>
        /// サービスを登録
        /// </summary>
        /// <typeparam name="T">サービス型</typeparam>
        /// <param name="service">サービスインスタンス</param>
        public void RegisterService<T>(T service) where T : class
        {
            Type serviceType = typeof(T);
            
            if (_services.ContainsKey(serviceType))
            {
                Debug.LogWarning($"[ServiceLocator] Service of type {serviceType.Name} is already registered. Overwriting...");
            }

            _services[serviceType] = service;

            // マネージャーの場合は管理リストに追加
            if (service is IManager manager)
            {
                if (!_managers.Contains(manager))
                {
                    _managers.Add(manager);
                    // 初期化順序でソート
                    _managers.Sort((m1, m2) => m1.InitializationOrder.CompareTo(m2.InitializationOrder));
                }
            }

            // コールバック実行
            if (_serviceCallbacks.TryGetValue(serviceType, out var callbacks))
            {
                foreach (var callback in callbacks)
                {
                    callback?.Invoke(service);
                }
                _serviceCallbacks.Remove(serviceType);
            }

            Debug.Log($"[ServiceLocator] Registered service: {serviceType.Name}");
        }

        /// <summary>
        /// サービスの登録を解除
        /// </summary>
        /// <typeparam name="T">サービス型</typeparam>
        public void UnregisterService<T>() where T : class
        {
            Type serviceType = typeof(T);
            
            if (_services.TryGetValue(serviceType, out var service))
            {
                _services.Remove(serviceType);
                
                // マネージャーの場合は管理リストからも削除
                if (service is IManager manager)
                {
                    _managers.Remove(manager);
                }
                
                Debug.Log($"[ServiceLocator] Unregistered service: {serviceType.Name}");
            }
        }

        #endregion

        #region Service Retrieval

        /// <summary>
        /// サービスを取得
        /// </summary>
        /// <typeparam name="T">サービス型</typeparam>
        /// <returns>サービスインスタンス</returns>
        public T GetService<T>() where T : class
        {
            Type serviceType = typeof(T);
            
            if (_services.TryGetValue(serviceType, out var service))
            {
                return service as T;
            }

            Debug.LogWarning($"[ServiceLocator] Service of type {serviceType.Name} not found.");
            return null;
        }

        /// <summary>
        /// サービスを取得（非ジェネリック版）
        /// </summary>
        /// <param name="serviceType">サービス型</param>
        /// <returns>サービスインスタンス</returns>
        public object GetService(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out var service))
            {
                return service;
            }

            Debug.LogWarning($"[ServiceLocator] Service of type {serviceType.Name} not found.");
            return null;
        }

        /// <summary>
        /// サービスが登録されているかチェック
        /// </summary>
        /// <typeparam name="T">サービス型</typeparam>
        /// <returns>登録されている場合true</returns>
        public bool HasService<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// サービスが利用可能になったときのコールバックを登録
        /// </summary>
        /// <typeparam name="T">サービス型</typeparam>
        /// <param name="callback">コールバック</param>
        public void OnServiceAvailable<T>(Action<T> callback) where T : class
        {
            Type serviceType = typeof(T);
            
            // 既に登録されている場合は即座に実行
            if (_services.TryGetValue(serviceType, out var service))
            {
                callback?.Invoke(service as T);
                return;
            }

            // 未登録の場合はコールバックリストに追加
            if (!_serviceCallbacks.ContainsKey(serviceType))
            {
                _serviceCallbacks[serviceType] = new List<Action<object>>();
            }
            
            _serviceCallbacks[serviceType].Add(obj => callback?.Invoke(obj as T));
        }

        #endregion

        #region Manager Management

        /// <summary>
        /// 全マネージャーを初期化順序に従って初期化
        /// </summary>
        public void InitializeAllManagers()
        {
            Debug.Log($"[ServiceLocator] Initializing {_managers.Count} managers...");
            
            foreach (var manager in _managers)
            {
                if (!manager.IsInitialized)
                {
                    try
                    {
                        manager.Initialize();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ServiceLocator] Failed to initialize manager {manager.GetType().Name}: {ex.Message}");
                    }
                }
            }
            
            Debug.Log("[ServiceLocator] All managers initialized.");
        }

        /// <summary>
        /// 全マネージャーを破棄
        /// </summary>
        public void DisposeAllManagers()
        {
            Debug.Log($"[ServiceLocator] Disposing {_managers.Count} managers...");
            
            // 初期化の逆順で破棄
            for (int i = _managers.Count - 1; i >= 0; i--)
            {
                try
                {
                    _managers[i].Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ServiceLocator] Failed to dispose manager {_managers[i].GetType().Name}: {ex.Message}");
                }
            }
            
            _managers.Clear();
            Debug.Log("[ServiceLocator] All managers disposed.");
        }

        #endregion

        #region Static Convenience Methods

        /// <summary>静的メソッド: サービス登録</summary>
        public static void Register<T>(T service) where T : class
        {
            Instance?.RegisterService(service);
        }

        /// <summary>静的メソッド: サービス取得</summary>
        public static T Get<T>() where T : class
        {
            return Instance?.GetService<T>();
        }

        /// <summary>静的メソッド: サービス存在チェック</summary>
        public static bool Has<T>() where T : class
        {
            return Instance?.HasService<T>() ?? false;
        }

        #endregion

        #region Unity Lifecycle

        protected override void OnInitialize()
        {
            Debug.Log("[ServiceLocator] ServiceLocator initialized.");
        }

        protected override void OnDispose()
        {
            DisposeAllManagers();
            _services.Clear();
            _serviceCallbacks.Clear();
            Debug.Log("[ServiceLocator] ServiceLocator disposed.");
        }

        #endregion

        #region Debug Information

        /// <summary>
        /// 登録されているサービスの情報をログ出力
        /// </summary>
        [ContextMenu("Log Registered Services")]
        public void LogRegisteredServices()
        {
            Debug.Log($"[ServiceLocator] Registered Services ({_services.Count}):");
            foreach (var kvp in _services)
            {
                Debug.Log($"  - {kvp.Key.Name}: {kvp.Value.GetType().Name}");
            }
        }

        #endregion
    }
}