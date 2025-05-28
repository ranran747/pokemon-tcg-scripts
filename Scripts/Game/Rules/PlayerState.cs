using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.Game
{
    /// <summary>
    /// プレイヤーの状態を包括的に管理するクラス
    /// カード、ポケモン、エネルギー、勝利条件などすべての状態を追跡
    /// Claude拡張でのカスタムプレイヤー状態追加にも対応
    /// </summary>
    [System.Serializable]
    public class PlayerState
    {
        #region Fields

        [Header("基本情報")]
        [SerializeField] private string _playerId;
        [SerializeField] private string _playerName;
        [SerializeField] private PlayerType _playerType;
        [SerializeField] private bool _isActive;

        [Header("カード管理")]
        [SerializeField] private List<Card> _deck = new List<Card>();
        [SerializeField] private List<Card> _hand = new List<Card>();
        [SerializeField] private List<Card> _discardPile = new List<Card>();
        [SerializeField] private List<Card> _prizeCards = new List<Card>();

        [Header("ポケモン管理")]
        [SerializeField] private Card _activePokemon;
        [SerializeField] private List<Card> _benchPokemons = new List<Card>();
        [SerializeField] private Dictionary<string, List<Card>> _attachedEnergies = new Dictionary<string, List<Card>>();
        [SerializeField] private Dictionary<string, List<Card>> _attachedTools = new Dictionary<string, List<Card>>();

        [Header("勝利条件管理")]
        [SerializeField] private int _prizeCardsRemaining;
        [SerializeField] private int _pointsScored; // ポケット版用
        [SerializeField] private List<VictoryCondition> _metVictoryConditions = new List<VictoryCondition>();

        [Header("特殊状態")]
        [SerializeField] private Dictionary<string, object> _playerVariables = new Dictionary<string, object>();
        [SerializeField] private List<PlayerEffect> _activeEffects = new List<PlayerEffect>();
        [SerializeField] private PlayerActionHistory _actionHistory = new PlayerActionHistory();

        [Header("ルール固有状態")]
        [SerializeField] private int _energyZoneSize; // ポケット版用
        [SerializeField] private int _energyAttachedThisTurn;
        [SerializeField] private bool _hasAttackedThisTurn;
        [SerializeField] private bool _hasUsedSupporterThisTurn;

        [Header("統計情報")]
        [SerializeField] private PlayerStatistics _statistics = new PlayerStatistics();

        #endregion

        #region Properties

        /// <summary>プレイヤーID</summary>
        public string PlayerId => _playerId;

        /// <summary>プレイヤー名</summary>
        public string PlayerName => _playerName;

        /// <summary>プレイヤータイプ</summary>
        public PlayerType PlayerType => _playerType;

        /// <summary>アクティブプレイヤーか</summary>
        public bool IsActive => _isActive;

        /// <summary>デッキ</summary>
        public List<Card> Deck => new List<Card>(_deck);

        /// <summary>手札</summary>
        public List<Card> Hand => new List<Card>(_hand);

        /// <summary>トラッシュ</summary>
        public List<Card> DiscardPile => new List<Card>(_discardPile);

        /// <summary>サイドカード</summary>
        public List<Card> PrizeCards => new List<Card>(_prizeCards);

        /// <summary>バトルポケモン</summary>
        public Card ActivePokemon => _activePokemon;

        /// <summary>ベンチポケモン</summary>
        public List<Card> BenchPokemons => new List<Card>(_benchPokemons);

        /// <summary>つけられているエネルギー</summary>
        public Dictionary<string, List<Card>> AttachedEnergies => new Dictionary<string, List<Card>>(_attachedEnergies);

        /// <summary>つけられているポケモンのどうぐ</summary>
        public Dictionary<string, List<Card>> AttachedTools => new Dictionary<string, List<Card>>(_attachedTools);

        /// <summary>残りサイドカード数</summary>
        public int PrizeCardsRemaining => _prizeCardsRemaining;

        /// <summary>獲得ポイント（ポケット版）</summary>
        public int PointsScored => _pointsScored;

        /// <summary>満たした勝利条件</summary>
        public List<VictoryCondition> MetVictoryConditions => new List<VictoryCondition>(_metVictoryConditions);

        /// <summary>プレイヤー変数</summary>
        public Dictionary<string, object> PlayerVariables => new Dictionary<string, object>(_playerVariables);

        /// <summary>アクティブな効果</summary>
        public List<PlayerEffect> ActiveEffects => new List<PlayerEffect>(_activeEffects);

        /// <summary>アクション履歴</summary>
        public PlayerActionHistory ActionHistory => _actionHistory;

        /// <summary>エネルギーゾーンサイズ（ポケット版）</summary>
        public int EnergyZoneSize => _energyZoneSize;

        /// <summary>今ターンにつけたエネルギー数</summary>
        public int EnergyAttachedThisTurn => _energyAttachedThisTurn;

        /// <summary>今ターンに攻撃したか</summary>
        public bool HasAttackedThisTurn => _hasAttackedThisTurn;

        /// <summary>今ターンにサポートを使ったか</summary>
        public bool HasUsedSupporterThisTurn => _hasUsedSupporterThisTurn;

        /// <summary>統計情報</summary>
        public PlayerStatistics Statistics => _statistics;

        /// <summary>デッキ残り枚数</summary>
        public int DeckCount => _deck.Count;

        /// <summary>手札枚数</summary>
        public int HandCount => _hand.Count;

        /// <summary>トラッシュ枚数</summary>
        public int DiscardCount => _discardPile.Count;

        /// <summary>場のポケモン総数</summary>
        public int TotalPokemonsInPlay => (_activePokemon != null ? 1 : 0) + _benchPokemons.Count;

        /// <summary>バトル可能なポケモンがいるか</summary>
        public bool HasBattleCapablePokemon => _activePokemon != null || _benchPokemons.Count > 0;

        #endregion

        #region Constructors

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <param name="playerName">プレイヤー名</param>
        /// <param name="playerType">プレイヤータイプ</param>
        public PlayerState(string playerId, string playerName = null, PlayerType playerType = PlayerType.Human)
        {
            _playerId = playerId;
            _playerName = playerName ?? playerId;
            _playerType = playerType;
            _isActive = false;
            _actionHistory = new PlayerActionHistory();
            _statistics = new PlayerStatistics();
        }

        #endregion

        #region Card Management

        /// <summary>
        /// デッキにカードを追加
        /// </summary>
        /// <param name="card">カード</param>
        public void AddToDeck(Card card)
        {
            _deck.Add(card);
            card.SetZone(CardZone.Deck);
            Debug.Log($"[PlayerState] {_playerId}: Added card to deck: {card.CardData.CardName}");
        }

        /// <summary>
        /// デッキをシャッフル
        /// </summary>
        public void ShuffleDeck()
        {
            for (int i = 0; i < _deck.Count; i++)
            {
                var temp = _deck[i];
                int randomIndex = UnityEngine.Random.Range(i, _deck.Count);
                _deck[i] = _deck[randomIndex];
                _deck[randomIndex] = temp;
            }
            Debug.Log($"[PlayerState] {_playerId}: Deck shuffled");
        }

        /// <summary>
        /// カードをドロー
        /// </summary>
        /// <param name="count">ドロー枚数</param>
        /// <returns>ドローしたカード</returns>
        public List<Card> DrawCards(int count)
        {
            var drawnCards = new List<Card>();
            
            for (int i = 0; i < count && _deck.Count > 0; i++)
            {
                var card = _deck[0];
                _deck.RemoveAt(0);
                _hand.Add(card);
                card.SetZone(CardZone.Hand);
                drawnCards.Add(card);
                
                // 統計情報を更新
                _statistics.IncrementCardsDrawn();
            }

            Debug.Log($"[PlayerState] {_playerId}: Drew {drawnCards.Count} cards");
            return drawnCards;
        }

        /// <summary>
        /// 手札からカードを捨てる
        /// </summary>
        /// <param name="card">捨てるカード</param>
        public void DiscardFromHand(Card card)
        {
            if (_hand.Remove(card))
            {
                _discardPile.Add(card);
                card.SetZone(CardZone.Discard);
                Debug.Log($"[PlayerState] {_playerId}: Discarded from hand: {card.CardData.CardName}");
            }
        }

        /// <summary>
        /// サイドカードを取る
        /// </summary>
        /// <param name="count">取る枚数</param>
        /// <returns>取ったカード</returns>
        public List<Card> TakePrizeCards(int count)
        {
            var takenCards = new List<Card>();
            
            for (int i = 0; i < count && _prizeCards.Count > 0; i++)
            {
                var card = _prizeCards[0];
                _prizeCards.RemoveAt(0);
                _hand.Add(card);
                card.SetZone(CardZone.Hand);
                takenCards.Add(card);
                
                _prizeCardsRemaining--;
                _statistics.IncrementPrizeCardsTaken();
            }

            Debug.Log($"[PlayerState] {_playerId}: Took {takenCards.Count} prize cards");
            return takenCards;
        }

        #endregion

        #region Pokemon Management

        /// <summary>
        /// バトルポケモンを設定
        /// </summary>
        /// <param name="pokemon">ポケモンカード</param>
        public void SetActivePokemon(Card pokemon)
        {
            if (_activePokemon != null)
            {
                _activePokemon.SetZone(CardZone.Bench);
                _benchPokemons.Add(_activePokemon);
            }

            _activePokemon = pokemon;
            pokemon?.SetZone(CardZone.Active);
            
            Debug.Log($"[PlayerState] {_playerId}: Set active Pokemon: {pokemon?.CardData.CardName ?? "None"}");
        }

        /// <summary>
        /// ベンチにポケモンを出す
        /// </summary>
        /// <param name="pokemon">ポケモンカード</param>
        /// <param name="maxBenchSize">最大ベンチサイズ</param>
        /// <returns>成功した場合true</returns>
        public bool AddToBench(Card pokemon, int maxBenchSize = 5)
        {
            if (_benchPokemons.Count >= maxBenchSize)
            {
                Debug.LogWarning($"[PlayerState] {_playerId}: Bench is full");
                return false;
            }

            _benchPokemons.Add(pokemon);
            pokemon.SetZone(CardZone.Bench);
            
            Debug.Log($"[PlayerState] {_playerId}: Added to bench: {pokemon.CardData.CardName}");
            return true;
        }

        /// <summary>
        /// ポケモンを進化させる
        /// </summary>
        /// <param name="sourcePokemon">進化元</param>
        /// <param name="evolutionCard">進化先カード</param>
        /// <returns>成功した場合true</returns>
        public bool EvolvePokemon(Card sourcePokemon, Card evolutionCard)
        {
            // 進化元の位置を確認
            bool isActive = _activePokemon == sourcePokemon;
            int benchIndex = _benchPokemons.IndexOf(sourcePokemon);

            if (!isActive && benchIndex == -1)
            {
                Debug.LogWarning($"[PlayerState] {_playerId}: Source Pokemon not found in play");
                return false;
            }

            // エネルギーやどうぐを移す
            TransferAttachmentsToEvolution(sourcePokemon, evolutionCard);

            // 進化元をトラッシュに
            _discardPile.Add(sourcePokemon);
            sourcePokemon.SetZone(CardZone.Discard);

            // 進化先を配置
            if (isActive)
            {
                _activePokemon = evolutionCard;
                evolutionCard.SetZone(CardZone.Active);
            }
            else
            {
                _benchPokemons[benchIndex] = evolutionCard;
                evolutionCard.SetZone(CardZone.Bench);
            }

            _statistics.IncrementPokemonEvolved();
            Debug.Log($"[PlayerState] {_playerId}: Evolved {sourcePokemon.CardData.CardName} to {evolutionCard.CardData.CardName}");
            return true;
        }

        /// <summary>
        /// ポケモンがきぜつ
        /// </summary>
        /// <param name="pokemon">きぜつしたポケモン</param>
        public void KnockOutPokemon(Card pokemon)
        {
            // つけられているカードもすべてトラッシュ
            var pokemonId = pokemon.InstanceId.ToString();
            
            if (_attachedEnergies.TryGetValue(pokemonId, out var energies))
            {
                foreach (var energy in energies)
                {
                    _discardPile.Add(energy);
                    energy.SetZone(CardZone.Discard);
                }
                _attachedEnergies.Remove(pokemonId);
            }

            if (_attachedTools.TryGetValue(pokemonId, out var tools))
            {
                foreach (var tool in tools)
                {
                    _discardPile.Add(tool);
                    tool.SetZone(CardZone.Discard);
                }
                _attachedTools.Remove(pokemonId);
            }

            // ポケモン本体をトラッシュ
            _discardPile.Add(pokemon);
            pokemon.SetZone(CardZone.Discard);

            // 場から除去
            if (_activePokemon == pokemon)
            {
                _activePokemon = null;
            }
            else
            {
                _benchPokemons.Remove(pokemon);
            }

            _statistics.IncrementPokemonKnockedOut();
            Debug.Log($"[PlayerState] {_playerId}: Pokemon knocked out: {pokemon.CardData.CardName}");
        }

        #endregion

        #region Energy Management

        /// <summary>
        /// ポケモンにエネルギーをつける
        /// </summary>
        /// <param name="pokemon">対象ポケモン</param>
        /// <param name="energy">エネルギーカード</param>
        public void AttachEnergy(Card pokemon, Card energy)
        {
            var pokemonId = pokemon.InstanceId.ToString();
            
            if (!_attachedEnergies.ContainsKey(pokemonId))
            {
                _attachedEnergies[pokemonId] = new List<Card>();
            }

            _attachedEnergies[pokemonId].Add(energy);
            energy.SetZone(CardZone.Attached);
            
            _energyAttachedThisTurn++;
            _statistics.IncrementEnergyAttached();
            
            Debug.Log($"[PlayerState] {_playerId}: Attached energy {energy.CardData.CardName} to {pokemon.CardData.CardName}");
        }

        /// <summary>
        /// ポケモンからエネルギーを取り除く
        /// </summary>
        /// <param name="pokemon">対象ポケモン</param>
        /// <param name="energyCount">取り除く枚数</param>
        /// <returns>取り除いたエネルギー</returns>
        public List<Card> RemoveEnergy(Card pokemon, int energyCount)
        {
            var pokemonId = pokemon.InstanceId.ToString();
            var removedEnergies = new List<Card>();

            if (_attachedEnergies.TryGetValue(pokemonId, out var energies))
            {
                for (int i = 0; i < energyCount && energies.Count > 0; i++)
                {
                    var energy = energies[0];
                    energies.RemoveAt(0);
                    _discardPile.Add(energy);
                    energy.SetZone(CardZone.Discard);
                    removedEnergies.Add(energy);
                }

                if (energies.Count == 0)
                {
                    _attachedEnergies.Remove(pokemonId);
                }
            }

            Debug.Log($"[PlayerState] {_playerId}: Removed {removedEnergies.Count} energy from {pokemon.CardData.CardName}");
            return removedEnergies;
        }

        /// <summary>
        /// ポケモンについているエネルギー数を取得
        /// </summary>
        /// <param name="pokemon">対象ポケモン</param>
        /// <param name="energyType">エネルギータイプ（指定なしで全体）</param>
        /// <returns>エネルギー数</returns>
        public int GetEnergyCount(Card pokemon, PokemonType? energyType = null)
        {
            var pokemonId = pokemon.InstanceId.ToString();
            
            if (!_attachedEnergies.TryGetValue(pokemonId, out var energies))
                return 0;

            if (energyType == null)
                return energies.Count;

            return energies.Count(e => 
            {
                var energyData = e.GetEnergyData();
                return energyData?.ProvidedTypes.Contains(energyType.Value) == true;
            });
        }

        #endregion

        #region Turn State Management

        /// <summary>
        /// ターン開始時の状態リセット
        /// </summary>
        public void StartTurn()
        {
            _isActive = true;
            _energyAttachedThisTurn = 0;
            _hasAttackedThisTurn = false;
            _hasUsedSupporterThisTurn = false;
            
            // エネルギーゾーン増加（ポケット版）
            if (_energyZoneSize >= 0) // エネルギーゾーンが有効な場合
            {
                _energyZoneSize++;
            }

            Debug.Log($"[PlayerState] {_playerId}: Turn started");
        }

        /// <summary>
        /// ターン終了時の処理
        /// </summary>
        public void EndTurn()
        {
            _isActive = false;
            
            // 期限切れ効果をクリーンアップ
            CleanupExpiredEffects();

            Debug.Log($"[PlayerState] {_playerId}: Turn ended");
        }

        /// <summary>
        /// 攻撃実行をマーク
        /// </summary>
        public void MarkAttackUsed()
        {
            _hasAttackedThisTurn = true;
            _statistics.IncrementAttacksUsed();
        }

        /// <summary>
        /// サポート使用をマーク
        /// </summary>
        public void MarkSupporterUsed()
        {
            _hasUsedSupporterThisTurn = true;
            _statistics.IncrementSupportersUsed();
        }

        #endregion

        #region Victory Conditions

        /// <summary>
        /// ポイントを追加（ポケット版）
        /// </summary>
        /// <param name="points">追加ポイント</param>
        public void AddPoints(int points)
        {
            _pointsScored += points;
            Debug.Log($"[PlayerState] {_playerId}: Added {points} points. Total: {_pointsScored}");
        }

        /// <summary>
        /// 勝利条件をチェック
        /// </summary>
        /// <param name="rule">適用ルール</param>
        /// <returns>満たした勝利条件</returns>
        public List<VictoryCondition> CheckVictoryConditions(BaseRuleData rule)
        {
            var metConditions = new List<VictoryCondition>();

            // サイドカード勝利
            if (rule.HasVictoryCondition(VictoryCondition.AllPrizes) && _prizeCardsRemaining <= 0)
            {
                metConditions.Add(VictoryCondition.AllPrizes);
            }

            // ポイント勝利（ポケット版）
            if (rule.HasVictoryCondition(VictoryCondition.PointTarget) && _pointsScored >= rule.PointsToWin)
            {
                metConditions.Add(VictoryCondition.PointTarget);
            }

            return metConditions;
        }

        #endregion

        #region Variable Management

        /// <summary>
        /// プレイヤー変数を設定
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        public void SetVariable(string key, object value)
        {
            _playerVariables[key] = value;
        }

        /// <summary>
        /// プレイヤー変数を取得
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="key">キー</param>
        /// <param name="defaultValue">デフォルト値</param>
        /// <returns>値</returns>
        public T GetVariable<T>(string key, T defaultValue = default(T))
        {
            if (_playerVariables.TryGetValue(key, out var value) && value is T)
            {
                return (T)value;
            }
            return defaultValue;
        }

        #endregion

        #region Effect Management

        /// <summary>
        /// 効果を追加
        /// </summary>
        /// <param name="effect">効果</param>
        public void AddEffect(PlayerEffect effect)
        {
            _activeEffects.Add(effect);
            Debug.Log($"[PlayerState] {_playerId}: Added effect: {effect.EffectType}");
        }

        /// <summary>
        /// 期限切れ効果をクリーンアップ
        /// </summary>
        public void CleanupExpiredEffects()
        {
            var expiredEffects = _activeEffects.Where(e => e.IsExpired()).ToList();
            foreach (var effect in expiredEffects)
            {
                _activeEffects.Remove(effect);
                Debug.Log($"[PlayerState] {_playerId}: Expired effect removed: {effect.EffectType}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 進化時の添付物移行
        /// </summary>
        /// <param name="source">進化元</param>
        /// <param name="target">進化先</param>
        private void TransferAttachmentsToEvolution(Card source, Card target)
        {
            var sourceId = source.InstanceId.ToString();
            var targetId = target.InstanceId.ToString();

            // エネルギーを移行
            if (_attachedEnergies.TryGetValue(sourceId, out var energies))
            {
                _attachedEnergies[targetId] = new List<Card>(energies);
                _attachedEnergies.Remove(sourceId);
            }

            // どうぐを移行
            if (_attachedTools.TryGetValue(sourceId, out var tools))
            {
                _attachedTools[targetId] = new List<Card>(tools);
                _attachedTools.Remove(sourceId);
            }
        }

        #endregion

        #region Serialization

        /// <summary>
        /// 状態をシリアライズ
        /// </summary>
        /// <returns>シリアライズデータ</returns>
        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                ["playerId"] = _playerId,
                ["playerName"] = _playerName,
                ["playerType"] = _playerType,
                ["isActive"] = _isActive,
                ["deckCount"] = _deck.Count,
                ["handCount"] = _hand.Count,
                ["discardCount"] = _discardPile.Count,
                ["prizeCardsRemaining"] = _prizeCardsRemaining,
                ["pointsScored"] = _pointsScored,
                ["energyZoneSize"] = _energyZoneSize,
                ["energyAttachedThisTurn"] = _energyAttachedThisTurn,
                ["hasAttackedThisTurn"] = _hasAttackedThisTurn,
                ["hasUsedSupporterThisTurn"] = _hasUsedSupporterThisTurn,
                ["totalPokemonsInPlay"] = TotalPokemonsInPlay,
                ["statistics"] = _statistics.Serialize()
            };
        }

        /// <summary>
        /// 状態をデシリアライズ
        /// </summary>
        /// <param name="data">シリアライズデータ</param>
        public void Deserialize(Dictionary<string, object> data)
        {
            if (data.TryGetValue("playerId", out var playerId))
                _playerId = playerId.ToString();
                
            if (data.TryGetValue("playerName", out var playerName))
                _playerName = playerName.ToString();
                
            if (data.TryGetValue("playerType", out var playerType) && playerType is PlayerType)
                _playerType = (PlayerType)playerType;
                
            if (data.TryGetValue("isActive", out var isActive) && isActive is bool)
                _isActive = (bool)isActive;
                
            if (data.TryGetValue("prizeCardsRemaining", out var prizes) && prizes is int)
                _prizeCardsRemaining = (int)prizes;
                
            if (data.TryGetValue("pointsScored", out var points) && points is int)
                _pointsScored = (int)points;
                
            if (data.TryGetValue("energyZoneSize", out var energyZone) && energyZone is int)
                _energyZoneSize = (int)energyZone;
        }

        #endregion

        #region Debug

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        /// <returns>デバッグ情報</returns>
        public string GetDebugInfo()
        {
            return $"=== Player {_playerId} Debug Info ===\n" +
                   $"Name: {_playerName}\n" +
                   $"Type: {_playerType}\n" +
                   $"Active: {_isActive}\n" +
                   $"Deck: {_deck.Count} cards\n" +
                   $"Hand: {_hand.Count} cards\n" +
                   $"Discard: {_discardPile.Count} cards\n" +
                   $"Prize Cards Remaining: {_prizeCardsRemaining}\n" +
                   $"Points Scored: {_pointsScored}\n" +
                   $"Active Pokemon: {(_activePokemon?.CardData.CardName ?? "None")}\n" +
                   $"Bench: {_benchPokemons.Count} Pokemon\n" +
                   $"Energy Zone: {_energyZoneSize}\n" +
                   $"Energy Attached This Turn: {_energyAttachedThisTurn}\n" +
                   $"Has Attacked: {_hasAttackedThisTurn}\n" +
                   $"Has Used Supporter: {_hasUsedSupporterThisTurn}";
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// プレイヤータイプ
    /// </summary>
    public enum PlayerType
    {
        Human = 0,      // 人間プレイヤー
        AI_Easy = 1,    // AI（簡単）
        AI_Normal = 2,  // AI（普通）
        AI_Hard = 3,    // AI（難しい）
        AI_Expert = 4,  // AI（エキスパート）
        Remote = 5      // リモートプレイヤー
    }

    #endregion

    #region Support Classes

    /// <summary>
    /// プレイヤー効果クラス
    /// </summary>
    [System.Serializable]
    public class PlayerEffect
    {
        [SerializeField] private string _effectId;
        [SerializeField] private string _effectType;
        [SerializeField] private DateTime _createdTime;
        [SerializeField] private int _duration;
        
        public string EffectId => _effectId;
        public string EffectType => _effectType;
        public DateTime CreatedTime => _createdTime;
        public int Duration => _duration;
        
        public PlayerEffect(string effectType, int duration = -1)
        {
            _effectId = Guid.NewGuid().ToString();
            _effectType = effectType;
            _duration = duration;
            _createdTime = DateTime.Now;
        }
        
        public bool IsExpired()
        {
            if (_duration < 0) return false;
            return (DateTime.Now - _createdTime).TotalSeconds > _duration;
        }
    }

    /// <summary>
    /// プレイヤーアクション履歴
    /// </summary>
    [System.Serializable]
    public class PlayerActionHistory
    {
        [SerializeField] private List<string> _actionHistory = new List<string>();
        
        public void AddAction(string action)
        {
            _actionHistory.Add($"{DateTime.Now:HH:mm:ss} - {action}");
        }
        
        public List<string> GetRecentActions(int count = 10)
        {
            return _actionHistory.TakeLast(count).ToList();
        }
    }

    /// <summary>
    /// プレイヤー統計情報
    /// </summary>
    [System.Serializable]
    public class PlayerStatistics
    {
        [SerializeField] private int _cardsDrawn;
        [SerializeField] private int _energyAttached;
        [SerializeField] private int _attacksUsed;
        [SerializeField] private int _supportersUsed;
        [SerializeField] private int _prizeCardsTaken;
        [SerializeField] private int _pokemonEvolved;
        [SerializeField] private int _pokemonKnockedOut;
        
        public int CardsDrawn => _cardsDrawn;
        public int EnergyAttached => _energyAttached;
        public int AttacksUsed => _attacksUsed;
        public int SupportersUsed => _supportersUsed;
        public int PrizeCardsTaken => _prizeCardsTaken;
        public int PokemonEvolved => _pokemonEvolved;
        public int PokemonKnockedOut => _pokemonKnockedOut;
        
        // 増分メソッドを追加
        public void IncrementCardsDrawn() => _cardsDrawn++;
        public void IncrementEnergyAttached() => _energyAttached++;
        public void IncrementAttacksUsed() => _attacksUsed++;
        public void IncrementSupportersUsed() => _supportersUsed++;
        public void IncrementPrizeCardsTaken() => _prizeCardsTaken++;
        public void IncrementPokemonEvolved() => _pokemonEvolved++;
        public void IncrementPokemonKnockedOut() => _pokemonKnockedOut++;
        
        public void Reset()
        {
            _cardsDrawn = 0;
            _energyAttached = 0;
            _attacksUsed = 0;
            _supportersUsed = 0;
            _prizeCardsTaken = 0;
            _pokemonEvolved = 0;
            _pokemonKnockedOut = 0;
        }
        
        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                ["cardsDrawn"] = _cardsDrawn,
                ["energyAttached"] = _energyAttached,
                ["attacksUsed"] = _attacksUsed,
                ["supportersUsed"] = _supportersUsed,
                ["prizeCardsTaken"] = _prizeCardsTaken,
                ["pokemonEvolved"] = _pokemonEvolved,
                ["pokemonKnockedOut"] = _pokemonKnockedOut
            };
        }
    }

    #endregion
}