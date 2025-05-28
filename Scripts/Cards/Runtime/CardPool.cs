using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.Cards.Runtime
{
    /// <summary>
    /// カードオブジェクトプーリングシステム
    /// カードインスタンスの効率的な管理と再利用
    /// メモリ使用量削減とパフォーマンス向上を実現
    /// </summary>
    public class CardPool : MonoBehaviourSingleton<CardPool>
    {
        #region Fields

        [Header("プール設定")]
        [SerializeField] private int _initialPoolSize = 100;
        [SerializeField] private int _maxPoolSize = 500;
        [SerializeField] private bool _allowDynamicExpansion = true;
        [SerializeField] private float _cleanupInterval = 60f; // 秒

        [Header("プリファブ")]
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private Transform _poolContainer;

        [Header("統計情報")]
        [SerializeField] private bool _enableStatistics = true;
        [SerializeField] private bool _logStatistics = false;

        // プールデータ
        private Queue<Card> _availableCards = new Queue<Card>();
        private HashSet<Card> _activeCards = new HashSet<Card>();
        private Dictionary<string, Queue<Card>> _typeSpecificPools = new Dictionary<string, Queue<Card>>();
        
        // 統計情報
        private PoolStatistics _statistics = new PoolStatistics();
        private float _lastCleanupTime = 0f;
        private int _nextInstanceId = 1000;

        // イベント
        public event Action<Card> OnCardRetrieved;
        public event Action<Card> OnCardReturned;
        public event Action<CardPool> OnPoolExpanded;
        public event Action<CardPool> OnPoolCleaned;

        #endregion

        #region Properties

        /// <summary>初期化順序</summary>
        public override int InitializationOrder => -800;

        /// <summary>利用可能カード数</summary>
        public int AvailableCount => _availableCards.Count;

        /// <summary>アクティブカード数</summary>
        public int ActiveCount => _activeCards.Count;

        /// <summary>プール総サイズ</summary>
        public int TotalPoolSize => _availableCards.Count + _activeCards.Count;

        /// <summary>最大プールサイズ</summary>
        public int MaxPoolSize => _maxPoolSize;

        /// <summary>統計情報</summary>
        public PoolStatistics Statistics => _statistics;

        /// <summary>動的拡張が有効か</summary>
        public bool AllowDynamicExpansion => _allowDynamicExpansion;

        #endregion

        #region Unity Lifecycle

        protected override void OnInitialize()
        {
            InitializePool();
            
            if (_cleanupInterval > 0)
            {
                InvokeRepeating(nameof(PeriodicCleanup), _cleanupInterval, _cleanupInterval);
            }
            
            Debug.Log($"[CardPool] Initialized with {_initialPoolSize} cards");
        }

        protected override void OnDispose()
        {
            ClearPool();
            CancelInvoke();
            Debug.Log("[CardPool] CardPool disposed");
        }

        private void Update()
        {
            if (_enableStatistics && _logStatistics && Time.time - _lastCleanupTime > 10f)
            {
                LogStatistics();
                _lastCleanupTime = Time.time;
            }
        }

        #endregion

        #region Pool Initialization

        /// <summary>
        /// プールを初期化
        /// </summary>
        private void InitializePool()
        {
            // プールコンテナを設定
            SetupPoolContainer();
            
            // カードプリファブを設定
            SetupCardPrefab();
            
            // 初期プールを作成
            CreateInitialPool();
            
            // 統計情報をリセット
            _statistics = new PoolStatistics();
        }

        /// <summary>
        /// プールコンテナを設定
        /// </summary>
        private void SetupPoolContainer()
        {
            if (_poolContainer == null)
            {
                var containerGO = new GameObject("CardPoolContainer");
                containerGO.transform.SetParent(transform);
                _poolContainer = containerGO.transform;
            }
        }

        /// <summary>
        /// カードプリファブを設定
        /// </summary>
        private void SetupCardPrefab()
        {
            if (_cardPrefab == null)
            {
                // デフォルトプリファブを作成
                _cardPrefab = CreateDefaultCardPrefab();
            }
        }

        /// <summary>
        /// デフォルトカードプリファブを作成
        /// </summary>
        /// <returns>作成されたプリファブ</returns>
        private GameObject CreateDefaultCardPrefab()
        {
            var prefab = new GameObject("DefaultCardPrefab");
            
            // 必要なコンポーネントを追加
            var card = prefab.AddComponent<Card>();
            var spriteRenderer = prefab.AddComponent<SpriteRenderer>();
            var collider = prefab.AddComponent<BoxCollider2D>();
            
            // デフォルト設定
            spriteRenderer.sortingLayerName = "Cards";
            collider.size = Vector2.one;
            
            return prefab;
        }

        /// <summary>
        /// 初期プールを作成
        /// </summary>
        private void CreateInitialPool()
        {
            for (int i = 0; i < _initialPoolSize; i++)
            {
                var cardInstance = CreateNewCardInstance();
                _availableCards.Enqueue(cardInstance);
            }
            
            _statistics.TotalCreated = _initialPoolSize;
            _statistics.CurrentPoolSize = _initialPoolSize;
        }

        #endregion

        #region Card Management

        /// <summary>
        /// カードをプールから取得
        /// </summary>
        /// <param name="cardData">カードデータ</param>
        /// <param name="ownerId">所有者ID</param>
        /// <returns>取得されたカード</returns>
        public Card GetCard(BaseCardData cardData, string ownerId)
        {
            Card card = GetAvailableCard();
            
            if (card == null)
            {
                if (_allowDynamicExpansion && TotalPoolSize < _maxPoolSize)
                {
                    card = CreateNewCardInstance();
                    _statistics.TotalCreated++;
                    
                    if (_enableStatistics)
                        Debug.Log($"[CardPool] Dynamically expanded pool (Size: {TotalPoolSize})");
                }
                else
                {
                    Debug.LogWarning("[CardPool] Cannot get card: pool exhausted and expansion disabled/limited");
                    return null;
                }
            }

            // カードを初期化
            int instanceId = GenerateInstanceId();
            card.Initialize(cardData, ownerId, instanceId);
            
            // アクティブセットに追加
            _activeCards.Add(card);
            
            // 統計更新
            if (_enableStatistics)
            {
                _statistics.TotalRetrieved++;
                _statistics.CurrentActiveCards = _activeCards.Count;
                _statistics.CurrentPoolSize = TotalPoolSize;
            }
            
            // イベント発火
            OnCardRetrieved?.Invoke(card);
            
            return card;
        }

        /// <summary>
        /// カードをプールに返却
        /// </summary>
        /// <param name="card">返却するカード</param>
        /// <returns>返却成功した場合true</returns>
        public bool ReturnCard(Card card)
        {
            if (card == null)
                return false;

            if (!_activeCards.Contains(card))
            {
                Debug.LogWarning($"[CardPool] Cannot return card: not from this pool");
                return false;
            }

            // アクティブセットから削除
            _activeCards.Remove(card);
            
            // カードをリセット
            ResetCard(card);
            
            // プールサイズチェック
            if (_availableCards.Count < _maxPoolSize)
            {
                _availableCards.Enqueue(card);
            }
            else
            {
                // プールが満杯の場合は破棄
                DestroyCardInstance(card);
                _statistics.TotalDestroyed++;
            }
            
            // 統計更新
            if (_enableStatistics)
            {
                _statistics.TotalReturned++;
                _statistics.CurrentActiveCards = _activeCards.Count;
                _statistics.CurrentPoolSize = TotalPoolSize;
            }
            
            // イベント発火
            OnCardReturned?.Invoke(card);
            
            Debug.Log($"[CardPool] Returned card to pool (Available: {AvailableCount})");
            return true;
        }

        /// <summary>
        /// 複数のカードを一括返却
        /// </summary>
        /// <param name="cards">返却するカードリスト</param>
        /// <returns>返却されたカード数</returns>
        public int ReturnCards(IEnumerable<Card> cards)
        {
            int returnedCount = 0;
            
            foreach (var card in cards.ToList()) // ToList()でコレクション変更エラーを回避
            {
                if (ReturnCard(card))
                {
                    returnedCount++;
                }
            }
            
            return returnedCount;
        }

        /// <summary>
        /// 特定タイプのカードを取得（最適化版）
        /// </summary>
        /// <param name="cardData">カードデータ</param>
        /// <param name="ownerId">所有者ID</param>
        /// <param name="cardType">カードタイプ</param>
        /// <returns>取得されたカード</returns>
        public Card GetCardByType(BaseCardData cardData, string ownerId, CardType cardType)
        {
            string typeKey = cardType.ToString();
            
            // タイプ固有プールから取得を試行
            if (_typeSpecificPools.ContainsKey(typeKey) && _typeSpecificPools[typeKey].Count > 0)
            {
                var card = _typeSpecificPools[typeKey].Dequeue();
                card.Initialize(cardData, ownerId, GenerateInstanceId());
                _activeCards.Add(card);
                
                OnCardRetrieved?.Invoke(card);
                return card;
            }
            
            // 通常のプールから取得
            return GetCard(cardData, ownerId);
        }

        #endregion

        #region Internal Pool Management

        /// <summary>
        /// 利用可能なカードを取得
        /// </summary>
        /// <returns>利用可能なカード（なければnull）</returns>
        private Card GetAvailableCard()
        {
            if (_availableCards.Count > 0)
            {
                return _availableCards.Dequeue();
            }
            
            return null;
        }

        /// <summary>
        /// 新しいカードインスタンスを作成
        /// </summary>
        /// <returns>作成されたカード</returns>
        private Card CreateNewCardInstance()
        {
            var cardGO = Instantiate(_cardPrefab, _poolContainer);
            cardGO.SetActive(false);
            
            var card = cardGO.GetComponent<Card>();
            if (card == null)
            {
                card = cardGO.AddComponent<Card>();
            }
            
            return card;
        }

        /// <summary>
        /// カードをリセット
        /// </summary>
        /// <param name="card">リセットするカード</param>
        private void ResetCard(Card card)
        {
            if (card == null)
                return;

            // 基本状態にリセット
            card.SetSelected(false);
            card.SetHighlighted(false);
            card.SetState(CardState.Normal);
            card.SetZone(CardZone.Unknown);
            
            // トランスフォームをリセット
            card.transform.SetParent(_poolContainer);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one;
            
            // ゲームオブジェクトを無効化
            card.gameObject.SetActive(false);
        }

        /// <summary>
        /// カードインスタンスを破棄
        /// </summary>
        /// <param name="card">破棄するカード</param>
        private void DestroyCardInstance(Card card)
        {
            if (card != null && card.gameObject != null)
            {
                Destroy(card.gameObject);
            }
        }

        /// <summary>
        /// インスタンスIDを生成
        /// </summary>
        /// <returns>一意のインスタンスID</returns>
        private int GenerateInstanceId()
        {
            return _nextInstanceId++;
        }

        #endregion

        #region Pool Maintenance

        /// <summary>
        /// 定期的なクリーンアップ
        /// </summary>
        private void PeriodicCleanup()
        {
            CleanupPool();
            
            if (_logStatistics)
            {
                LogStatistics();
            }
        }

        /// <summary>
        /// プールをクリーンアップ
        /// </summary>
        public void CleanupPool()
        {
            // 破棄されたカードをアクティブセットから削除
            var destroyedCards = _activeCards.Where(card => card == null || card.gameObject == null).ToList();
            foreach (var destroyedCard in destroyedCards)
            {
                _activeCards.Remove(destroyedCard);
            }
            
            // 統計更新
            _statistics.CurrentActiveCards = _activeCards.Count;
            _statistics.CurrentPoolSize = TotalPoolSize;
            _statistics.LastCleanupTime = Time.time;
            
            OnPoolCleaned?.Invoke(this);
            
            if (destroyedCards.Count > 0)
            {
                Debug.Log($"[CardPool] Cleaned up {destroyedCards.Count} destroyed cards");
            }
        }

        /// <summary>
        /// プールを拡張
        /// </summary>
        /// <param name="additionalSize">追加サイズ</param>
        public void ExpandPool(int additionalSize)
        {
            if (!_allowDynamicExpansion)
            {
                Debug.LogWarning("[CardPool] Dynamic expansion is disabled");
                return;
            }
            
            if (TotalPoolSize + additionalSize > _maxPoolSize)
            {
                additionalSize = _maxPoolSize - TotalPoolSize;
                if (additionalSize <= 0)
                {
                    Debug.LogWarning("[CardPool] Cannot expand: already at maximum size");
                    return;
                }
            }
            
            for (int i = 0; i < additionalSize; i++)
            {
                var card = CreateNewCardInstance();
                _availableCards.Enqueue(card);
            }
            
            _statistics.TotalCreated += additionalSize;
            _statistics.CurrentPoolSize = TotalPoolSize;
            
            OnPoolExpanded?.Invoke(this);
            Debug.Log($"[CardPool] Expanded pool by {additionalSize} (Total: {TotalPoolSize})");
        }

        /// <summary>
        /// プールサイズを縮小
        /// </summary>
        /// <param name="targetSize">目標サイズ</param>
        public void ShrinkPool(int targetSize)
        {
            targetSize = Mathf.Max(0, targetSize);
            
            while (_availableCards.Count > targetSize)
            {
                var card = _availableCards.Dequeue();
                DestroyCardInstance(card);
                _statistics.TotalDestroyed++;
            }
            
            _statistics.CurrentPoolSize = TotalPoolSize;
            Debug.Log($"[CardPool] Shrunk pool to {targetSize} available cards");
        }

        /// <summary>
        /// プールを完全にクリア
        /// </summary>
        public void ClearPool()
        {
            // 利用可能なカードを破棄
            while (_availableCards.Count > 0)
            {
                var card = _availableCards.Dequeue();
                DestroyCardInstance(card);
            }
            
            // アクティブカードの参照をクリア（実際の破棄は外部で行う）
            _activeCards.Clear();
            
            // タイプ固有プールをクリア
            _typeSpecificPools.Clear();
            
            // 統計リセット
            _statistics = new PoolStatistics();
            
            Debug.Log("[CardPool] Cleared all pools");
        }

        #endregion

        #region Statistics and Debug

        /// <summary>
        /// 統計情報をログ出力
        /// </summary>
        [ContextMenu("Log Statistics")]
        public void LogStatistics()
        {
            Debug.Log($"=== CardPool Statistics ===");
            Debug.Log($"Pool Size: {AvailableCount}/{TotalPoolSize} (Max: {_maxPoolSize})");
            Debug.Log($"Active Cards: {ActiveCount}");
            Debug.Log($"Total Created: {_statistics.TotalCreated}");
            Debug.Log($"Total Retrieved: {_statistics.TotalRetrieved}");
            Debug.Log($"Total Returned: {_statistics.TotalReturned}");
            Debug.Log($"Total Destroyed: {_statistics.TotalDestroyed}");
            Debug.Log($"Efficiency: {_statistics.PoolEfficiency:P2}");
        }

        /// <summary>
        /// 統計情報をリセット
        /// </summary>
        [ContextMenu("Reset Statistics")]
        public void ResetStatistics()
        {
            _statistics = new PoolStatistics
            {
                CurrentPoolSize = TotalPoolSize,
                CurrentActiveCards = ActiveCount
            };
            Debug.Log("[CardPool] Statistics reset");
        }

        /// <summary>
        /// メモリ使用量を取得
        /// </summary>
        /// <returns>推定メモリ使用量（バイト）</returns>
        public long GetEstimatedMemoryUsage()
        {
            // 概算値（カード1枚あたり約1KB）
            return TotalPoolSize * 1024;
        }

        #endregion

        #region Static Access Methods

        /// <summary>
        /// 静的メソッド: カード取得
        /// </summary>
        public static Card Get(BaseCardData cardData, string ownerId)
        {
            return Instance?.GetCard(cardData, ownerId);
        }

        /// <summary>
        /// 静的メソッド: カード返却
        /// </summary>
        public static bool Return(Card card)
        {
            return Instance?.ReturnCard(card) ?? false;
        }

        /// <summary>
        /// 静的メソッド: 複数カード返却
        /// </summary>
        public static int ReturnMultiple(IEnumerable<Card> cards)
        {
            return Instance?.ReturnCards(cards) ?? 0;
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// プール統計情報
    /// </summary>
    [System.Serializable]
    public class PoolStatistics
    {
        public int TotalCreated = 0;
        public int TotalRetrieved = 0;
        public int TotalReturned = 0;
        public int TotalDestroyed = 0;
        public int CurrentPoolSize = 0;
        public int CurrentActiveCards = 0;
        public float LastCleanupTime = 0f;

        /// <summary>プール効率（返却率）</summary>
        public float PoolEfficiency => TotalRetrieved > 0 ? (float)TotalReturned / TotalRetrieved : 0f;

        /// <summary>メモリ節約量（推定）</summary>
        public int MemorySaved => TotalReturned * 1024; // 1KB per card

        /// <summary>作成vs再利用の比率</summary>
        public float ReuseRatio => TotalCreated > 0 ? (float)TotalReturned / TotalCreated : 0f;
    }

    #endregion
}