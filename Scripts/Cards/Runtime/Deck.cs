using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.Cards.Runtime
{
    /// <summary>
    /// デッキ管理システム
    /// カードの管理、シャッフル、ドロー、検索機能を提供
    /// ゲームルールに基づくデッキ構築検証も実装
    /// </summary>
    [System.Serializable]
    public class Deck
    {
        #region Fields

        [Header("デッキ情報")]
        [SerializeField] private string _deckName = "New Deck";
        [SerializeField] private string _deckId;
        [SerializeField] private string _ownerId;
        [SerializeField] private DeckFormat _format = DeckFormat.Standard60;

        [Header("カードリスト")]
        [SerializeField] private List<DeckCard> _deckCards = new List<DeckCard>();
        [SerializeField] private List<Card> _runtimeCards = new List<Card>();

        [Header("設定")]
        [SerializeField] private bool _isShuffled = false;
        [SerializeField] private bool _allowDuplicates = true;
        [SerializeField] private int _maxDeckSize = 60;
        [SerializeField] private int _minDeckSize = 60;

        // イベント
        public event Action<Deck> OnDeckShuffled;
        public event Action<Deck, Card> OnCardDrawn;
        public event Action<Deck, Card> OnCardAdded;
        public event Action<Deck, Card> OnCardRemoved;
        public event Action<Deck> OnDeckValidated;

        // 統計情報
        private DeckStatistics _statistics;
        private bool _statisticsNeedUpdate = true;

        #endregion

        #region Properties

        /// <summary>デッキ名</summary>
        public string DeckName => _deckName;

        /// <summary>デッキID</summary>
        public string DeckId => _deckId;

        /// <summary>オーナーID</summary>
        public string OwnerId => _ownerId;

        /// <summary>デッキフォーマット</summary>
        public DeckFormat Format => _format;

        /// <summary>デッキカードリスト（データ）</summary>
        public List<DeckCard> DeckCards => _deckCards;

        /// <summary>実行時カードリスト</summary>
        public List<Card> RuntimeCards => _runtimeCards;

        /// <summary>デッキサイズ</summary>
        public int Count => _runtimeCards.Count;

        /// <summary>空かどうか</summary>
        public bool IsEmpty => _runtimeCards.Count == 0;

        /// <summary>シャッフル済みか</summary>
        public bool IsShuffled => _isShuffled;

        /// <summary>有効なデッキか</summary>
        public bool IsValid => ValidateDeck().IsValid;

        /// <summary>デッキ統計情報</summary>
        public DeckStatistics Statistics
        {
            get
            {
                if (_statisticsNeedUpdate)
                {
                    UpdateStatistics();
                }
                return _statistics;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// デフォルトコンストラクタ
        /// </summary>
        public Deck()
        {
            _deckId = Guid.NewGuid().ToString();
            _statistics = new DeckStatistics();
        }

        /// <summary>
        /// パラメータ付きコンストラクタ
        /// </summary>
        /// <param name="deckName">デッキ名</param>
        /// <param name="ownerId">オーナーID</param>
        /// <param name="format">デッキフォーマット</param>
        public Deck(string deckName, string ownerId, DeckFormat format) : this()
        {
            _deckName = deckName;
            _ownerId = ownerId;
            _format = format;
            SetFormatLimits(format);
        }

        #endregion

        #region Deck Building

        /// <summary>
        /// カードをデッキに追加
        /// </summary>
        /// <param name="cardData">追加するカードデータ</param>
        /// <param name="count">枚数</param>
        /// <returns>追加成功した場合true</returns>
        public bool AddCard(BaseCardData cardData, int count = 1)
        {
            if (cardData == null || count <= 0)
                return false;

            // 制限チェック
            if (!CanAddCard(cardData, count))
                return false;

            // 既存カードの検索
            var existingCard = _deckCards.Find(dc => dc.CardData.CardID == cardData.CardID);
            
            if (existingCard != null)
            {
                existingCard.Count += count;
            }
            else
            {
                _deckCards.Add(new DeckCard(cardData, count));
            }

            _statisticsNeedUpdate = true;
            Debug.Log($"[Deck] Added {count}x {cardData.CardName} to {_deckName}");
            return true;
        }

        /// <summary>
        /// カードをデッキから削除
        /// </summary>
        /// <param name="cardData">削除するカードデータ</param>
        /// <param name="count">枚数</param>
        /// <returns>削除成功した場合true</returns>
        public bool RemoveCard(BaseCardData cardData, int count = 1)
        {
            if (cardData == null || count <= 0)
                return false;

            var existingCard = _deckCards.Find(dc => dc.CardData.CardID == cardData.CardID);
            if (existingCard == null)
                return false;

            if (existingCard.Count <= count)
            {
                _deckCards.Remove(existingCard);
            }
            else
            {
                existingCard.Count -= count;
            }

            _statisticsNeedUpdate = true;
            Debug.Log($"[Deck] Removed {count}x {cardData.CardName} from {_deckName}");
            return true;
        }

        /// <summary>
        /// デッキをクリア
        /// </summary>
        public void Clear()
        {
            _deckCards.Clear();
            _runtimeCards.Clear();
            _statisticsNeedUpdate = true;
            _isShuffled = false;
            Debug.Log($"[Deck] Cleared deck {_deckName}");
        }

        /// <summary>
        /// カードを追加できるかチェック
        /// </summary>
        /// <param name="cardData">カードデータ</param>
        /// <param name="count">追加枚数</param>
        /// <returns>追加可能な場合true</returns>
        public bool CanAddCard(BaseCardData cardData, int count = 1)
        {
            if (cardData == null || count <= 0)
                return false;

            // 総数制限チェック
            int totalCards = GetTotalCardCount();
            if (totalCards + count > _maxDeckSize)
                return false;

            // 同名カード制限チェック
            int currentCount = GetCardCount(cardData);
            if (currentCount + count > cardData.DeckLimit)
                return false;

            // ACE SPEC制限チェック（特殊カードの場合）
            if (cardData.IsLimitedCard)
            {
                int limitedCardCount = GetLimitedCardCount();
                if (limitedCardCount > 0 && currentCount == 0)
                    return false; // 既に他の制限カードがある場合
            }

            return true;
        }

        #endregion

        #region Deck Operations

        /// <summary>
        /// デッキをシャッフル
        /// </summary>
        public void Shuffle()
        {
            if (_runtimeCards.Count <= 1)
                return;

            // Fisher-Yates shuffle
            var random = new System.Random();
            for (int i = _runtimeCards.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = _runtimeCards[i];
                _runtimeCards[i] = _runtimeCards[j];
                _runtimeCards[j] = temp;
            }

            _isShuffled = true;
            OnDeckShuffled?.Invoke(this);
            Debug.Log($"[Deck] Shuffled deck {_deckName}");
        }

        /// <summary>
        /// カードをドロー
        /// </summary>
        /// <param name="count">ドロー枚数</param>
        /// <returns>ドローしたカードリスト</returns>
        public List<Card> DrawCards(int count = 1)
        {
            var drawnCards = new List<Card>();
            
            for (int i = 0; i < count && _runtimeCards.Count > 0; i++)
            {
                var card = _runtimeCards[0];
                _runtimeCards.RemoveAt(0);
                drawnCards.Add(card);
                
                OnCardDrawn?.Invoke(this, card);
            }

            Debug.Log($"[Deck] Drew {drawnCards.Count} cards from {_deckName}");
            return drawnCards;
        }

        /// <summary>
        /// 一番上のカードを見る（ドローしない）
        /// </summary>
        /// <param name="count">見る枚数</param>
        /// <returns>カードリスト</returns>
        public List<Card> PeekCards(int count = 1)
        {
            var peekedCards = new List<Card>();
            
            for (int i = 0; i < count && i < _runtimeCards.Count; i++)
            {
                peekedCards.Add(_runtimeCards[i]);
            }

            return peekedCards;
        }

        /// <summary>
        /// 指定カードを検索してドロー
        /// </summary>
        /// <param name="cardData">検索するカードデータ</param>
        /// <returns>見つかったカード（なければnull）</returns>
        public Card SearchAndDraw(BaseCardData cardData)
        {
            var card = _runtimeCards.Find(c => c.CardData.CardID == cardData.CardID);
            if (card != null)
            {
                _runtimeCards.Remove(card);
                OnCardDrawn?.Invoke(this, card);
                Debug.Log($"[Deck] Searched and drew {card.CardData.CardName}");
            }
            return card;
        }

        /// <summary>
        /// カードをデッキの任意の位置に挿入
        /// </summary>
        /// <param name="card">挿入するカード</param>
        /// <param name="position">位置（0=一番上）</param>
        public void InsertCard(Card card, int position = 0)
        {
            if (card == null)
                return;

            position = Mathf.Clamp(position, 0, _runtimeCards.Count);
            _runtimeCards.Insert(position, card);
            
            OnCardAdded?.Invoke(this, card);
            Debug.Log($"[Deck] Inserted {card.CardData.CardName} at position {position}");
        }

        /// <summary>
        /// カードをデッキの底に追加
        /// </summary>
        /// <param name="card">追加するカード</param>
        public void AddToBottom(Card card)
        {
            if (card == null)
                return;

            _runtimeCards.Add(card);
            OnCardAdded?.Invoke(this, card);
            Debug.Log($"[Deck] Added {card.CardData.CardName} to bottom");
        }

        #endregion

        #region Deck Initialization

        /// <summary>
        /// デッキデータから実行時カードを生成
        /// </summary>
        /// <param name="cardPool">カードプール</param>
        public void InitializeRuntimeCards(CardPool cardPool = null)
        {
            _runtimeCards.Clear();

            foreach (var deckCard in _deckCards)
            {
                for (int i = 0; i < deckCard.Count; i++)
                {
                    Card runtimeCard;
                    
                    if (cardPool != null)
                    {
                        runtimeCard = cardPool.GetCard(deckCard.CardData, _ownerId);
                    }
                    else
                    {
                        // カードプールがない場合は新規作成
                        var cardGO = new GameObject($"Card_{deckCard.CardData.CardName}");
                        runtimeCard = cardGO.AddComponent<Card>();
                        runtimeCard.Initialize(deckCard.CardData, _ownerId, GenerateInstanceId());
                    }

                    runtimeCard.SetZone(CardZone.Deck);
                    _runtimeCards.Add(runtimeCard);
                }
            }

            Debug.Log($"[Deck] Initialized {_runtimeCards.Count} runtime cards for {_deckName}");
        }

        /// <summary>
        /// インスタンスIDを生成
        /// </summary>
        /// <returns>一意のインスタンスID</returns>
        private int GenerateInstanceId()
        {
            return UnityEngine.Random.Range(100000, 999999);
        }

        #endregion

        #region Validation

        /// <summary>
        /// デッキの有効性を検証
        /// </summary>
        /// <returns>検証結果</returns>
        public DeckValidationResult ValidateDeck()
        {
            var result = new DeckValidationResult();
            int totalCards = GetTotalCardCount();

            // サイズチェック
            if (totalCards < _minDeckSize)
            {
                result.Errors.Add($"Deck size ({totalCards}) is below minimum ({_minDeckSize})");
            }
            else if (totalCards > _maxDeckSize)
            {
                result.Errors.Add($"Deck size ({totalCards}) exceeds maximum ({_maxDeckSize})");
            }

            // カード制限チェック
            foreach (var deckCard in _deckCards)
            {
                if (deckCard.Count > deckCard.CardData.DeckLimit)
                {
                    result.Errors.Add($"Too many copies of {deckCard.CardData.CardName} ({deckCard.Count}/{deckCard.CardData.DeckLimit})");
                }
            }

            // 制限カードチェック
            int limitedCardTypes = GetLimitedCardCount();
            if (limitedCardTypes > 1)
            {
                result.Errors.Add($"Multiple limited card types not allowed ({limitedCardTypes})");
            }

            // フォーマット固有のチェック
            ValidateFormat(result);

            result.IsValid = result.Errors.Count == 0;
            OnDeckValidated?.Invoke(this);

            return result;
        }

        /// <summary>
        /// フォーマット固有の検証
        /// </summary>
        /// <param name="result">検証結果</param>
        private void ValidateFormat(DeckValidationResult result)
        {
            switch (_format)
            {
                case DeckFormat.Standard60:
                    ValidateStandardFormat(result);
                    break;
                case DeckFormat.Pocket20:
                    ValidatePocketFormat(result);
                    break;
                case DeckFormat.Unlimited:
                    // 無制限フォーマットは基本チェックのみ
                    break;
            }
        }

        /// <summary>
        /// スタンダードフォーマットの検証
        /// </summary>
        /// <param name="result">検証結果</param>
        private void ValidateStandardFormat(DeckValidationResult result)
        {
            // レギュレーションチェック
            foreach (var deckCard in _deckCards)
            {
                if (!deckCard.CardData.LegalRegulations.Contains(CardRegulation.Standard))
                {
                    result.Errors.Add($"{deckCard.CardData.CardName} is not legal in Standard format");
                }
            }
        }

        /// <summary>
        /// ポケットフォーマットの検証
        /// </summary>
        /// <param name="result">検証結果</param>
        private void ValidatePocketFormat(DeckValidationResult result)
        {
            // ポケット版の制限をチェック
            var trainerCards = _deckCards.Where(dc => dc.CardData.CardType == CardType.Trainer).ToList();
            foreach (var trainerCard in trainerCards)
            {
                if (trainerCard.CardData is TrainerCardData trainerData && 
                    trainerData.TrainerType != TrainerType.Supporter)
                {
                    result.Errors.Add($"Only Supporter cards allowed in Pocket format: {trainerCard.CardData.CardName}");
                }
            }
        }

        #endregion

        #region Statistics and Queries

        /// <summary>
        /// 統計情報を更新
        /// </summary>
        private void UpdateStatistics()
        {
            _statistics = new DeckStatistics();
            
            foreach (var deckCard in _deckCards)
            {
                _statistics.TotalCards += deckCard.Count;

                switch (deckCard.CardData.CardType)
                {
                    case CardType.Pokemon:
                        _statistics.PokemonCount += deckCard.Count;
                        break;
                    case CardType.Trainer:
                        _statistics.TrainerCount += deckCard.Count;
                        break;
                    case CardType.Energy:
                        _statistics.EnergyCount += deckCard.Count;
                        break;
                }

                // レアリティ統計
                if (!_statistics.RarityDistribution.ContainsKey(deckCard.CardData.Rarity))
                {
                    _statistics.RarityDistribution[deckCard.CardData.Rarity] = 0;
                }
                _statistics.RarityDistribution[deckCard.CardData.Rarity] += deckCard.Count;
            }

            _statisticsNeedUpdate = false;
        }

        /// <summary>
        /// 指定カードの枚数を取得
        /// </summary>
        /// <param name="cardData">カードデータ</param>
        /// <returns>枚数</returns>
        public int GetCardCount(BaseCardData cardData)
        {
            var deckCard = _deckCards.Find(dc => dc.CardData.CardID == cardData.CardID);
            return deckCard?.Count ?? 0;
        }

        /// <summary>
        /// 総カード枚数を取得
        /// </summary>
        /// <returns>総枚数</returns>
        public int GetTotalCardCount()
        {
            return _deckCards.Sum(dc => dc.Count);
        }

        /// <summary>
        /// 制限カードの種類数を取得
        /// </summary>
        /// <returns>制限カード種類数</returns>
        private int GetLimitedCardCount()
        {
            return _deckCards.Count(dc => dc.CardData.IsLimitedCard && dc.Count > 0);
        }

        /// <summary>
        /// 名前でカードを検索
        /// </summary>
        /// <param name="cardName">カード名</param>
        /// <returns>該当するカードデータリスト</returns>
        public List<DeckCard> SearchCardsByName(string cardName)
        {
            return _deckCards.Where(dc => dc.CardData.CardName.Contains(cardName)).ToList();
        }

        /// <summary>
        /// タイプでカードを検索
        /// </summary>
        /// <param name="cardType">カードタイプ</param>
        /// <returns>該当するカードデータリスト</returns>
        public List<DeckCard> GetCardsByType(CardType cardType)
        {
            return _deckCards.Where(dc => dc.CardData.CardType == cardType).ToList();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// フォーマットによる制限を設定
        /// </summary>
        /// <param name="format">デッキフォーマット</param>
        private void SetFormatLimits(DeckFormat format)
        {
            switch (format)
            {
                case DeckFormat.Standard60:
                    _minDeckSize = 60;
                    _maxDeckSize = 60;
                    break;
                case DeckFormat.Pocket20:
                    _minDeckSize = 20;
                    _maxDeckSize = 20;
                    break;
                case DeckFormat.Unlimited:
                    _minDeckSize = 40;
                    _maxDeckSize = 100;
                    break;
            }
        }

        /// <summary>
        /// デッキをコピー
        /// </summary>
        /// <returns>コピーされたデッキ</returns>
        public Deck Clone()
        {
            var clonedDeck = new Deck(_deckName + " (Copy)", _ownerId, _format);
            
            foreach (var deckCard in _deckCards)
            {
                clonedDeck.AddCard(deckCard.CardData, deckCard.Count);
            }

            return clonedDeck;
        }

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        /// <returns>デバッグ情報文字列</returns>
        public string GetDebugInfo()
        {
            var stats = Statistics;
            return $"Deck: {_deckName}\n" +
                   $"Owner: {_ownerId}\n" +
                   $"Format: {_format}\n" +
                   $"Total Cards: {stats.TotalCards}\n" +
                   $"Pokemon: {stats.PokemonCount}, Trainer: {stats.TrainerCount}, Energy: {stats.EnergyCount}\n" +
                   $"Valid: {IsValid}\n" +
                   $"Shuffled: {_isShuffled}";
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// デッキ内のカード情報
    /// </summary>
    [System.Serializable]
    public class DeckCard
    {
        [SerializeField] private BaseCardData _cardData;
        [SerializeField] private int _count;

        public BaseCardData CardData => _cardData;
        public int Count { get; set; }

        public DeckCard(BaseCardData cardData, int count)
        {
            _cardData = cardData;
            _count = count;
            Count = count;
        }
    }

    /// <summary>
    /// デッキ統計情報
    /// </summary>
    public class DeckStatistics
    {
        public int TotalCards { get; set; } = 0;
        public int PokemonCount { get; set; } = 0;
        public int TrainerCount { get; set; } = 0;
        public int EnergyCount { get; set; } = 0;
        public Dictionary<CardRarity, int> RarityDistribution { get; set; } = new Dictionary<CardRarity, int>();
    }

    /// <summary>
    /// デッキ検証結果
    /// </summary>
    public class DeckValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// デッキフォーマット
    /// </summary>
    public enum DeckFormat
    {
        Standard60 = 0,     // スタンダード（60枚）
        Pocket20 = 1,       // ポケット版（20枚）
        Unlimited = 2,      // 無制限
        Custom = 99         // カスタム
    }

    #endregion
}