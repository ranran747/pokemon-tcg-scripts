using UnityEngine;

namespace PokemonTCG.Core.Architecture
{
    /// <summary>
    /// MonoBehaviourベースのシングルトン基底クラス
    /// Claude拡張でのマネージャー実装を統一化
    /// スレッドセーフ + DontDestroyOnLoad対応
    /// </summary>
    /// <typeparam name="T">シングルトンを実装するクラス</typeparam>
    public abstract class MonoBehaviourSingleton<T> : MonoBehaviour, IManager where T : MonoBehaviourSingleton<T>
    {
        #region Singleton Implementation
        
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isApplicationQuitting = false;

        /// <summary>シングルトンインスタンス</summary>
        public static T Instance
        {
            get
            {
                if (_isApplicationQuitting)
                {
                    Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' already destroyed on application quit. Won't create again.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<T>();

                        if (_instance == null)
                        {
                            GameObject singletonObject = new GameObject(typeof(T).Name);
                            _instance = singletonObject.AddComponent<T>();
                            DontDestroyOnLoad(singletonObject);
                        }
                    }

                    return _instance;
                }
            }
        }

        /// <summary>シングルトンが存在するかチェック</summary>
        public static bool HasInstance => _instance != null;

        #endregion

        #region IManager Implementation

        /// <summary>初期化済みフラグ</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>初期化順序（継承クラスでオーバーライド可能）</summary>
        public virtual int InitializationOrder => 0;

        /// <summary>
        /// 初期化処理
        /// 継承クラスでオーバーライドして具体的な初期化を実装
        /// </summary>
        public virtual void Initialize()
        {
            if (IsInitialized)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Already initialized.");
                return;
            }

            OnInitialize();
            IsInitialized = true;
            Debug.Log($"[{typeof(T).Name}] Initialized successfully.");
        }

        /// <summary>
        /// 破棄処理
        /// 継承クラスでオーバーライドして具体的な終了処理を実装
        /// </summary>
        public virtual void Dispose()
        {
            OnDispose();
            IsInitialized = false;
        }

        /// <summary>
        /// フレーム更新処理
        /// 必要な継承クラスでオーバーライド
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// 固定時間更新処理
        /// 必要な継承クラスでオーバーライド
        /// </summary>
        public virtual void FixedUpdate() { }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            // 重複インスタンスのチェック
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
                
                // 自動初期化（必要に応じて無効化可能）
                if (AutoInitialize)
                {
                    Initialize();
                }
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"[Singleton] Destroying duplicate instance of {typeof(T).Name}");
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                Dispose();
                _instance = null;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        #endregion

        #region Protected Virtual Methods

        /// <summary>
        /// 具体的な初期化処理
        /// 継承クラスでオーバーライドして実装
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// 具体的な破棄処理
        /// 継承クラスでオーバーライドして実装
        /// </summary>
        protected virtual void OnDispose() { }

        /// <summary>
        /// 自動初期化するかどうか
        /// 継承クラスでオーバーライド可能
        /// </summary>
        protected virtual bool AutoInitialize => true;

        #endregion
    }

    /// <summary>
    /// 非MonoBehaviourベースのシングルトン基底クラス
    /// データ管理クラスなどで使用
    /// </summary>
    /// <typeparam name="T">シングルトンを実装するクラス</typeparam>
    public abstract class Singleton<T> : IManager where T : Singleton<T>, new()
    {
        #region Singleton Implementation

        private static T _instance;
        private static readonly object _lock = new object();

        /// <summary>シングルトンインスタンス</summary>
        public static T Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                        _instance.Initialize();
                    }
                    return _instance;
                }
            }
        }

        /// <summary>シングルトンが存在するかチェック</summary>
        public static bool HasInstance => _instance != null;

        #endregion

        #region IManager Implementation

        /// <summary>初期化済みフラグ</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>初期化順序（継承クラスでオーバーライド可能）</summary>
        public virtual int InitializationOrder => 0;

        /// <summary>
        /// 初期化処理
        /// </summary>
        public virtual void Initialize()
        {
            if (IsInitialized)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Already initialized.");
                return;
            }

            OnInitialize();
            IsInitialized = true;
            Debug.Log($"[{typeof(T).Name}] Initialized successfully.");
        }

        /// <summary>
        /// 破棄処理
        /// </summary>
        public virtual void Dispose()
        {
            OnDispose();
            IsInitialized = false;
            _instance = null;
        }

        /// <summary>フレーム更新処理</summary>
        public virtual void Update() { }

        /// <summary>固定時間更新処理</summary>
        public virtual void FixedUpdate() { }

        #endregion

        #region Protected Virtual Methods

        /// <summary>
        /// 具体的な初期化処理
        /// 継承クラスでオーバーライドして実装
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// 具体的な破棄処理
        /// 継承クラスでオーバーライドして実装
        /// </summary>
        protected virtual void OnDispose() { }

        #endregion

        #region Constructor

        /// <summary>
        /// コンストラクタをprotectedにしてnew T()以外での生成を防ぐ
        /// </summary>
        protected Singleton() { }

        #endregion
    }
}