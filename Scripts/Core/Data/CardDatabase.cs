using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PokemonTCG.Core.Data
{
    /// <summary>
    /// カードデータベース
    /// 全カードデータの一元管理とクエリ機能を提供
    /// Claude拡張での新カード追加と動的データ管理に対応
    /// </summary>
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "Pokemon TCG/Database/Card Database")]
    public class CardDatabase : ScriptableObject
    {
        #region Database Fields

        [Header("データベース情報")]
        [SerializeField] private string _databaseVersion = "1.0";
        [SerializeField] private string _databaseName = "Pokemon TCG Card Database";
        [SerializeField] private int _totalCardCount = 0;

        [Header("カードコレクション")]
        [SerializeField] private List<PokemonCardData> _pokemonCards = new List<PokemonCardData>();
        [SerializeField] private List<TrainerCardData> _trainerCards = new List<TrainerCardData>();
        [SerializeField] private List<EnergyCardData> _energyCards = new List<EnergyCardData>();

        [Header("インデックス管理")]
        [SerializeField] private bool _autoGenerateIndexes = true;
        [SerializeField] private bool _validateOnBuild = true;

        // ランタイムキャッシュ
        private Dictionary<string, BaseCardData> _cardCache;
        private Dictionary<CardType, List<BaseCardData>> _typeCache;
        private Dictionary<string, List<BaseCardData>> _setCache;
        private bool _isCacheBuilt = false;

        #endregion

        #region Properties

        /// <summary>データベースバージョン</summary>
        public string DatabaseVersion => _databaseVersion;

        /// <summary>データベース名</summary>
        public string DatabaseName => _databaseName;

        /// <summary>総カード数</summary>
        public int TotalCardCount => _totalCardCount;

        /// <summary>ポケモンカードリスト</summary>
        public List<PokemonCardData> PokemonCards => _pokemonCards;

        /// <summary>トレーナーカードリスト</summary>
        public List<TrainerCardData> TrainerCards => _trainerCards;

        /// <summary>エネルギーカードリスト</summary>
        public List<EnergyCardData> EnergyCards => _energyCards;

        /// <summary>全カードリスト</summary>
        public List<BaseCardData> AllCards
        {
            get
            {
                var allCards = new List<BaseCardData>();
                allCards.AddRange(_pokemonCards);
                allCards.AddRange(_trainerCards);
                allCards.AddRange(_energyCards);
                return allCards;
            }
        }

        /// <summary>キャッシュが構築済みか</summary>
        public bool IsCacheBuilt => _isCacheBuilt;

        #endregion

        #region Database Management

        /// <summary>
        /// データベースを初期化
        /// </summary>
        public void Initialize()
        {
            BuildCache();
            UpdateCardCount();
            
            if (_validateOnBuild)
            {
                ValidateDatabase();
            }
            
            Debug.Log($"[CardDatabase] Initialized with {_totalCardCount} cards");
        }

        /// <summary>
        /// キャッシュを構築
        /// </summary>
        public void BuildCache()
        {
            _cardCache = new Dictionary<string, BaseCardData>();
            _typeCache = new Dictionary<CardType, List<BaseCardData>>();
            _setCache = new Dictionary<string, List<BaseCardData>>();

            // カードキャッシュ構築
            foreach (var card in AllCards)
            {
                if (card != null && !string.IsNullOrEmpty(card.CardID))
                {
                    _cardCache[card.CardID] = card;
                }
            }

            // タイプ別キャッシュ構築
            foreach (CardType cardType in Enum.GetValues(typeof(CardType)))
            {
                _typeCache[cardType] = AllCards.Where(card => card.CardType == cardType).ToList();
            }

            // セット別キャッシュ構築
            var sets = AllCards.Where(card => !string.IsNullOrEmpty(card.SetCode))
                              .GroupBy(card => card.SetCode);
            
            foreach (var setGroup in sets)
            {
                _setCache[setGroup.Key] = setGroup.ToList();
            }

            _isCacheBuilt = true;
            Debug.Log($"[CardDatabase] Cache built: {_cardCache.Count} cards indexed");
        }

        /// <summary>
        /// カード数を更新
        /// </summary>
        public void UpdateCardCount()
        {
            _totalCardCount = AllCards.Count;
        }

        /// <summary>
        /// データベースの整合性を検証
        /// </summary>
        /// <returns>検証結果</returns>
        public DatabaseValidationResult ValidateDatabase()
        {
            var result = new DatabaseValidationResult();
            var allCards = AllCards;

            // 重複IDチェック
            var cardIds = new HashSet<string>();
            foreach (var card in allCards)
            {
                if (card == null)
                {
                    result.Errors.Add("Null card found in database");
                    continue;
                }

                if (string.IsNullOrEmpty(card.CardID))
                {
                    result.Errors.Add($"Card {card.name} has empty ID");
                    continue;
                }

                if (!cardIds.Add(card.CardID))
                {
                    result.Errors.Add($"Duplicate card ID found: {card.CardID}");
                }

                // 個別カード検証
                if (!card.IsValid())
                {
                    result.Errors.AddRange(card.GetValidationErrors().Select(error => 
                        $"{card.CardName}: {error}"));
                }
            }

            // 統計情報
            result.TotalCards = allCards.Count;
            result.PokemonCount = _pokemonCards.Count;
            result.TrainerCount = _trainerCards.Count;
            result.EnergyCount = _energyCards.Count;
            result.IsValid = result.Errors.Count == 0;

            if (result.IsValid)
            {
                Debug.Log($"[CardDatabase] Validation passed: {result.TotalCards} cards");
            }
            else
            {
                Debug.LogError($"[CardDatabase] Validation failed: {result.Errors.Count} errors");
            }

            return result;
        }

        #endregion

        #region Card Addition/Removal

        /// <summary>
        /// ポケモンカードを追加
        /// </summary>
        /// <param name="pokemonCard">追加するポケモンカード</param>
        /// <returns>追加成功した場合true</returns>
        public bool AddPokemonCard(PokemonCardData pokemonCard)
        {
            if (pokemonCard == null)
                return false;

            if (_pokemonCards.Contains(pokemonCard))
                return false;

            if (GetCardById(pokemonCard.CardID) != null)
            {
                Debug.LogWarning($"[CardDatabase] Card with ID {pokemonCard.CardID} already exists");
                return false;
            }

            _pokemonCards.Add(pokemonCard);
            InvalidateCache();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            
            Debug.Log($"[CardDatabase] Added Pokemon card: {pokemonCard.CardName}");
            return true;
        }

        /// <summary>
        /// トレーナーカードを追加
        /// </summary>
        /// <param name="trainerCard">追加するトレーナーカード</param>
        /// <returns>追加成功した場合true</returns>
        public bool AddTrainerCard(TrainerCardData trainerCard)
        {
            if (trainerCard == null)
                return false;

            if (_trainerCards.Contains(trainerCard))
                return false;

            if (GetCardById(trainerCard.CardID) != null)
            {
                Debug.LogWarning($"[CardDatabase] Card with ID {trainerCard.CardID} already exists");
                return false;
            }

            _trainerCards.Add(trainerCard);
            InvalidateCache();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            
            Debug.Log($"[CardDatabase] Added Trainer card: {trainerCard.CardName}");
            return true;
        }

        /// <summary>
        /// エネルギーカードを追加
        /// </summary>
        /// <param name="energyCard">追加するエネルギーカード</param>
        /// <returns>追加成功した場合true</returns>
        public bool AddEnergyCard(EnergyCardData energyCard)
        {
            if (energyCard == null)
                return false;

            if (_energyCards.Contains(energyCard))
                return false;

            if (GetCardById(energyCard.CardID) != null)
            {
                Debug.LogWarning($"[CardDatabase] Card with ID {energyCard.CardID} already exists");
                return false;
            }

            _energyCards.Add(energyCard);
            InvalidateCache();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            
            Debug.Log($"[CardDatabase] Added Energy card: {energyCard.CardName}");
            return true;
        }

        /// <summary>
        /// カードを削除
        /// </summary>
        /// <param name="cardId">削除するカードのID</param>
        /// <returns>削除成功した場合true</returns>
        public bool RemoveCard(string cardId)
        {
            var card = GetCardById(cardId);
            if (card == null)
                return false;

            bool removed = false;

            if (card is PokemonCardData pokemonCard)
            {
                removed = _pokemonCards.Remove(pokemonCard);
            }
            else if (card is TrainerCardData trainerCard)
            {
                removed = _trainerCards.Remove(trainerCard);
            }
            else if (card is EnergyCardData energyCard)
            {
                removed = _energyCards.Remove(energyCard);
            }

            if (removed)
            {
                InvalidateCache();
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
                
                Debug.Log($"[CardDatabase] Removed card: {card.CardName}");
            }

            return removed;
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// IDでカードを取得
        /// </summary>
        /// <param name="cardId">カードID</param>
        /// <returns>カードデータ（見つからない場合null）</returns>
        public BaseCardData GetCardById(string cardId)
        {
            if (!_isCacheBuilt)
                BuildCache();

            _cardCache.TryGetValue(cardId, out var card);
            return card;
        }

        /// <summary>
        /// 名前でカードを検索
        /// </summary>
        /// <param name="cardName">カード名</param>
        /// <param name="exactMatch">完全一致検索か</param>
        /// <returns>見つかったカードリスト</returns>
        public List<BaseCardData> GetCardsByName(string cardName, bool exactMatch = true)
        {
            var allCards = AllCards;
            
            if (exactMatch)
            {
                return allCards.Where(card => card.CardName == cardName).ToList();
            }
            else
            {
                return allCards.Where(card => card.CardName.Contains(cardName)).ToList();
            }
        }

        /// <summary>
        /// タイプでカードを取得
        /// </summary>
        /// <param name="cardType">カードタイプ</param>
        /// <returns>該当カードリスト</returns>
        public List<BaseCardData> GetCardsByType(CardType cardType)
        {
            if (!_isCacheBuilt)
                BuildCache();

            _typeCache.TryGetValue(cardType, out var cards);
            return cards ?? new List<BaseCardData>();
        }

        /// <summary>
        /// セットでカードを取得
        /// </summary>
        /// <param name="setCode">セットコード</param>
        /// <returns>該当カードリスト</returns>
        public List<BaseCardData> GetCardsBySet(string setCode)
        {
            if (!_isCacheBuilt)
                BuildCache();

            _setCache.TryGetValue(setCode, out var cards);
            return cards ?? new List<BaseCardData>();
        }

        /// <summary>
        /// レアリティでカードを取得
        /// </summary>
        /// <param name="rarity">レアリティ</param>
        /// <returns>該当カードリスト</returns>
        public List<BaseCardData> GetCardsByRarity(CardRarity rarity)
        {
            return AllCards.Where(card => card.Rarity == rarity).ToList();
        }

        /// <summary>
        /// レギュレーションで使用可能なカードを取得
        /// </summary>
        /// <param name="regulation">レギュレーション</param>
        /// <returns>該当カードリスト</returns>
        public List<BaseCardData> GetLegalCards(CardRegulation regulation)
        {
            return AllCards.Where(card => card.LegalRegulations.Contains(regulation)).ToList();
        }

        /// <summary>
        /// ランダムカードを取得
        /// </summary>
        /// <param name="count">取得枚数</param>
        /// <param name="cardType">カードタイプ制限（null=制限なし）</param>
        /// <returns>ランダムカードリスト</returns>
        public List<BaseCardData> GetRandomCards(int count, CardType? cardType = null)
        {
            var targetCards = cardType.HasValue ? GetCardsByType(cardType.Value) : AllCards;
            
            if (targetCards.Count == 0)
                return new List<BaseCardData>();

            var random = new System.Random();
            return targetCards.OrderBy(x => random.Next()).Take(count).ToList();
        }

        /// <summary>
        /// 高度なクエリ検索
        /// </summary>
        /// <param name="query">検索クエリ</param>
        /// <returns>検索結果</returns>
        public List<BaseCardData> SearchCards(CardSearchQuery query)
        {
            var results = AllCards.AsEnumerable();

            // 各フィルターを適用
            if (query.CardTypes != null && query.CardTypes.Count > 0)
            {
                results = results.Where(card => query.CardTypes.Contains(card.CardType));
            }

            if (query.Rarities != null && query.Rarities.Count > 0)
            {
                results = results.Where(card => query.Rarities.Contains(card.Rarity));
            }

            if (!string.IsNullOrEmpty(query.SetCode))
            {
                results = results.Where(card => card.SetCode == query.SetCode);
            }

            if (!string.IsNullOrEmpty(query.NameFilter))
            {
                results = results.Where(card => card.CardName.Contains(query.NameFilter));
            }

            if (query.Regulations != null && query.Regulations.Count > 0)
            {
                results = results.Where(card => query.Regulations.Any(reg => card.LegalRegulations.Contains(reg)));
            }

            return results.ToList();
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// キャッシュを無効化
        /// </summary>
        private void InvalidateCache()
        {
            _isCacheBuilt = false;
            _cardCache?.Clear();
            _typeCache?.Clear();
            _setCache?.Clear();
        }

        #endregion

        #region Unity Lifecycle

        private void OnValidate()
        {
            #if UNITY_EDITOR
            if (_autoGenerateIndexes)
            {
                UpdateCardCount();
            }
            #endif
        }

        private void Awake()
        {
            Initialize();
        }

        #endregion

        #region Static Access

        private static CardDatabase _instance;

        /// <summary>
        /// データベースインスタンス（シングルトンアクセス）
        /// </summary>
        public static CardDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CardDatabase>("CardDatabase");
                    if (_instance == null)
                    {
                        Debug.LogError("[CardDatabase] CardDatabase not found in Resources folder");
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Debug and Statistics

        /// <summary>
        /// データベース統計を取得
        /// </summary>
        /// <returns>統計情報</returns>
        public DatabaseStatistics GetStatistics()
        {
            return new DatabaseStatistics
            {
                TotalCards = _totalCardCount,
                PokemonCards = _pokemonCards.Count,
                TrainerCards = _trainerCards.Count,
                EnergyCards = _energyCards.Count,
                UniqueCardIds = AllCards.Select(card => card.CardID).Distinct().Count(),
                UniqueSets = AllCards.Select(card => card.SetCode).Where(set => !string.IsNullOrEmpty(set)).Distinct().Count()
            };
        }

        /// <summary>
        /// データベース情報をログ出力
        /// </summary>
        [ContextMenu("Log Database Info")]
        public void LogDatabaseInfo()
        {
            var stats = GetStatistics();
            Debug.Log($"=== CardDatabase Info ===");
            Debug.Log($"Version: {_databaseVersion}");
            Debug.Log($"Total Cards: {stats.TotalCards}");
            Debug.Log($"Pokemon: {stats.PokemonCards}, Trainer: {stats.TrainerCards}, Energy: {stats.EnergyCards}");
            Debug.Log($"Unique Sets: {stats.UniqueSets}");
            Debug.Log($"Cache Built: {_isCacheBuilt}");
        }

        #endregion
    }

    #region Query and Result Classes

    /// <summary>
    /// カード検索クエリ
    /// </summary>
    [System.Serializable]
    public class CardSearchQuery
    {
        public List<CardType> CardTypes { get; set; } = new List<CardType>();
        public List<CardRarity> Rarities { get; set; } = new List<CardRarity>();
        public List<CardRegulation> Regulations { get; set; } = new List<CardRegulation>();
        public string SetCode { get; set; } = "";
        public string NameFilter { get; set; } = "";
        public int MaxResults { get; set; } = 100;
    }

    /// <summary>
    /// データベース検証結果
    /// </summary>
    public class DatabaseValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public int TotalCards { get; set; } = 0;
        public int PokemonCount { get; set; } = 0;
        public int TrainerCount { get; set; } = 0;
        public int EnergyCount { get; set; } = 0;
    }

    /// <summary>
    /// データベース統計情報
    /// </summary>
    public class DatabaseStatistics
    {
        public int TotalCards { get; set; } = 0;
        public int PokemonCards { get; set; } = 0;
        public int TrainerCards { get; set; } = 0;
        public int EnergyCards { get; set; } = 0;
        public int UniqueCardIds { get; set; } = 0;
        public int UniqueSets { get; set; } = 0;
    }

    #endregion
}