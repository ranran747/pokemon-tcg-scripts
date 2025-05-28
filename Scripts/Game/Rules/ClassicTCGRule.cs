using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.Game.Rules
{
    /// <summary>
    /// 従来版ポケモンカードゲームルール実装
    /// 60枚デッキ、6枚サイドカード、標準的なTCGルール
    /// </summary>
    [CreateAssetMenu(fileName = "ClassicTCGRule", menuName = "Pokemon TCG/Rules/Classic TCG Rule")]
    public class ClassicTCGRule : GameRule
    {
        #region Constants

        private const int DECK_SIZE = 60;
        private const int INITIAL_HAND_SIZE = 7;
        private const int PRIZE_CARDS = 6;
        private const int MAX_BENCH_SIZE = 5;
        private const int ENERGY_PER_TURN = 1;
        private const float WEAKNESS_MULTIPLIER = 2.0f;
        private const int RESISTANCE_REDUCTION = 30;

        #endregion

        #region Fields

        [Header("従来版ルール設定")]
        [SerializeField] private bool _allowFirstTurnAttack = false;
        [SerializeField] private bool _enablePoisonBetweenTurns = true;
        [SerializeField] private bool _enableBurnFlipBetweenTurns = true;
        [SerializeField] private int _maxHandSize = 10;

        [Header("ゲーム状態管理")]
        [SerializeField] private Dictionary<string, int> _playerPrizeCards = new Dictionary<string, int>();
        [SerializeField] private Dictionary<string, int> _playerDeckSizes = new Dictionary<string, int>();
        [SerializeField] private Dictionary<string, bool> _playerHasAttacked = new Dictionary<string, bool>();
        [SerializeField] private Dictionary<string, int> _playerEnergyAttached = new Dictionary<string, int>();

        // 特殊状態管理
        [SerializeField] private Dictionary<string, List<string>> _poisonedPokemons = new Dictionary<string, List<string>>();
        [SerializeField] private Dictionary<string, List<string>> _burnedPokemons = new Dictionary<string, List<string>>();

        #endregion

        #region Properties

        /// <summary>初期化順序</summary>
        public override int InitializationOrder => 201; // ClassicTCG専用

        /// <summary>ルール名</summary>
        public override string RuleName => "Classic Pokemon TCG";

        /// <summary>先攻1ターン目攻撃許可</summary>
        public bool AllowFirstTurnAttack => _allowFirstTurnAttack;

        /// <summary>最大手札サイズ</summary>
        public int MaxHandSize => _maxHandSize;

        #endregion

        #region Initialization

        protected override void SetupGameRules()
        {
            // 従来版ルール設定
            SetGameVariable("DeckSize", DECK_SIZE);
            SetGameVariable("InitialHandSize", INITIAL_HAND_SIZE);
            SetGameVariable("PrizeCards", PRIZE_CARDS);
            SetGameVariable("MaxBenchSize", MAX_BENCH_SIZE);
            SetGameVariable("EnergyPerTurn", ENERGY_PER_TURN);
            SetGameVariable("WeaknessMultiplier", WEAKNESS_MULTIPLIER);
            SetGameVariable("ResistanceReduction", RESISTANCE_REDUCTION);
            
            Debug.Log($"[{ManagerName}] Classic TCG rules configured");
        }

        protected override void OnRuleInitialized()
        {
            // イベント登録
            EventBus.On<TurnStartEvent>(OnTurnStarted);
            EventBus.On<TurnEndEvent>(OnTurnEnded);
            EventBus.On<PokemonDamagedEvent>(OnPokemonDamaged);
            EventBus.On<PokemonKnockedOutEvent>(OnPokemonKnockedOut);
            
            Debug.Log($"[{ManagerName}] Classic TCG event handlers registered");
        }

        protected override void OnDispose()
        {
            // イベント解除
            EventBus.Off<TurnStartEvent>(OnTurnStarted);
            EventBus.Off<TurnEndEvent>(OnTurnEnded);
            EventBus.Off<PokemonDamagedEvent>(OnPokemonDamaged);
            EventBus.Off<PokemonKnockedOutEvent>(OnPokemonKnockedOut);
            
            // データクリア
            _playerPrizeCards.Clear();
            _playerDeckSizes.Clear();
            _playerHasAttacked.Clear();
            _playerEnergyAttached.Clear();
            _poisonedPokemons.Clear();
            _burnedPokemons.Clear();
            
            base.OnDispose();
        }

        #endregion

        #region Game Flow Implementation

        protected override void OnGameStarted(List<string> playerIds)
        {
            Debug.Log($"[{ManagerName}] Starting Classic TCG game with {playerIds.Count} players");
            
            // プレイヤー状態初期化
            foreach (var playerId in playerIds)
            {
                _playerPrizeCards[playerId] = PRIZE_CARDS;
                _playerDeckSizes[playerId] = DECK_SIZE - INITIAL_HAND_SIZE - PRIZE_CARDS; // 手札7枚+サイド6枚を引いた後
                _playerHasAttacked[playerId] = false;
                _playerEnergyAttached[playerId] = 0;
                _poisonedPokemons[playerId] = new List<string>();
                _burnedPokemons[playerId] = new List<string>();
            }
            
            // セットアップフェーズ開始
            SetPhase(GamePhase.Setup);
            
            // 最初のプレイヤーを決定（ランダムまたはルールに基づく）
            var firstPlayerId = DetermineFirstPlayer(playerIds);
            StartTurn(firstPlayerId);
            
            EventBus.Emit(new GameStartedEvent 
            { 
                PlayerIds = playerIds, 
                FirstPlayerId = firstPlayerId,
                RuleType = "ClassicTCG"
            });
        }

        protected override void OnGameFinished(string winnerId, VictoryReason reason)
        {
            Debug.Log($"[{ManagerName}] Classic TCG game finished. Winner: {winnerId}, Reason: {reason}");
            
            // 統計情報の記録
            var gameStats = new GameStatistics
            {
                WinnerId = winnerId,
                VictoryReason = reason,
                TotalTurns = CurrentTurn,
                PlayerStats = new Dictionary<string, PlayerStatistics>()
            };
            
            foreach (var kvp in _playerPrizeCards)
            {
                gameStats.PlayerStats[kvp.Key] = new PlayerStatistics
                {
                    PlayerId = kvp.Key,
                    PrizesRemaining = kvp.Value,
                    CardsInDeck = _playerDeckSizes.GetValueOrDefault(kvp.Key, 0)
                };
            }
            
            EventBus.Emit(new GameEndedEvent 
            { 
                WinnerId = winnerId, 
                Reason = reason,
                Statistics = gameStats
            });
        }

        #endregion

        #region Action Validation

        protected override ValidationResult ValidateRuleSpecificAction(IGameAction action)
        {
            switch (action.ActionType)
            {
                case GameActionType.PlayCard:
                    return ValidatePlayCardAction(action);
                case GameActionType.AttachEnergy:
                    return ValidateAttachEnergyAction(action);
                case GameActionType.UseAttack:
                    return ValidateAttackAction(action);
                case GameActionType.Retreat:
                    return ValidateRetreatAction(action);
                case GameActionType.Evolution:
                    return ValidateEvolutionAction(action);
                case GameActionType.UseAbility:
                    return ValidateAbilityAction(action);
                case GameActionType.EndTurn:
                    return ValidateEndTurnAction(action);
                default:
                    return ValidationResult.CreateError($"Unknown action type: {action.ActionType}");
            }
        }

        private ValidationResult ValidatePlayCardAction(IGameAction action)
        {
            // カードプレイの基本チェック
            if (!action.Parameters.TryGetValue("CardId", out var cardIdObj) || !(cardIdObj is string cardId))
            {
                return ValidationResult.CreateError("Card ID is required for play card action");
            }

            // 手札にカードがあるかチェック
            // TODO: 実際のカード管理システムと連携して検証

            return ValidationResult.CreateSuccess();
        }

        private ValidationResult ValidateAttachEnergyAction(IGameAction action)
        {
            var playerId = action.PlayerId;
            
            // 1ターンに1枚制限チェック
            if (_playerEnergyAttached.GetValueOrDefault(playerId, 0) >= ENERGY_PER_TURN)
            {
                return ValidationResult.CreateError("Already attached energy this turn");
            }

            // エネルギーカードの存在チェック
            if (!action.Parameters.TryGetValue("EnergyCardId", out var energyCardId))
            {
                return ValidationResult.CreateError("Energy card ID is required");
            }

            // 対象ポケモンの存在チェック
            if (!action.Parameters.TryGetValue("TargetPokemonId", out var targetPokemonId))
            {
                return ValidationResult.CreateError("Target Pokemon ID is required");
            }

            return ValidationResult.CreateSuccess();
        }

        private ValidationResult ValidateAttackAction(IGameAction action)
        {
            var playerId = action.PlayerId;
            
            // 既に攻撃済みかチェック
            if (_playerHasAttacked.GetValueOrDefault(playerId, false))
            {
                return ValidationResult.CreateError("Already attacked this turn");
            }

            // 先攻1ターン目攻撃制限
            if (CurrentTurn == 1 && !_allowFirstTurnAttack)
            {
                return ValidationResult.CreateError("First turn attack is not allowed");
            }

            // 攻撃するポケモンと技の指定チェック
            if (!action.Parameters.TryGetValue("AttackingPokemonId", out var attackingPokemonId))
            {
                return ValidationResult.CreateError("Attacking Pokemon ID is required");
            }

            if (!action.Parameters.TryGetValue("AttackName", out var attackName))
            {
                return ValidationResult.CreateError("Attack name is required");
            }

            // エネルギー要件チェック
            // TODO: 実際のポケモンとエネルギー状態をチェック

            return ValidationResult.CreateSuccess();
        }

        private ValidationResult ValidateRetreatAction(IGameAction action)
        {
            // にげるコストの支払い可能性チェック
            if (!action.Parameters.TryGetValue("RetreatCost", out var retreatCostObj) || !(retreatCostObj is int retreatCost))
            {
                return ValidationResult.CreateError("Retreat cost is required");
            }

            // まひ状態チェック
            if (action.Parameters.TryGetValue("IsParalyzed", out var isParalyzedObj) && isParalyzedObj is bool isParalyzed && isParalyzed)
            {
                return ValidationResult.CreateError("Cannot retreat while paralyzed");
            }

            return ValidationResult.CreateSuccess();
        }

        private ValidationResult ValidateEvolutionAction(IGameAction action)
        {
            // 進化制限チェック（出したターンは進化不可、など）
            if (!action.Parameters.TryGetValue("EvolutionSourceId", out var sourceId))
            {
                return ValidationResult.CreateError("Evolution source Pokemon ID is required");
            }

            if (!action.Parameters.TryGetValue("EvolutionTargetCardId", out var targetCardId))
            {
                return ValidationResult.CreateError("Evolution target card ID is required");
            }

            // 先攻1ターン目制限
            if (CurrentTurn == 1)
            {
                return ValidationResult.CreateError("Cannot evolve on first turn");
            }

            return ValidationResult.CreateSuccess();
        }

        private ValidationResult ValidateAbilityAction(IGameAction action)
        {
            // 特性使用の制限チェック
            if (!action.Parameters.TryGetValue("PokemonId", out var pokemonId))
            {
                return ValidationResult.CreateError("Pokemon ID is required for ability use");
            }

            if (!action.Parameters.TryGetValue("AbilityName", out var abilityName))
            {
                return ValidationResult.CreateError("Ability name is required");
            }

            return ValidationResult.CreateSuccess();
        }

        private ValidationResult ValidateEndTurnAction(IGameAction action)
        {
            // ターン終了の前提条件チェック
            // 必須アクション（ドローなど）が完了しているかチェック

            return ValidationResult.CreateSuccess();
        }

        #endregion

        #region Action Execution

        protected override ActionResult ExecuteActionInternal(IGameAction action)
        {
            switch (action.ActionType)
            {
                case GameActionType.PlayCard:
                    return ExecutePlayCardAction(action);
                case GameActionType.AttachEnergy:
                    return ExecuteAttachEnergyAction(action);
                case GameActionType.UseAttack:
                    return ExecuteAttackAction(action);
                case GameActionType.Retreat:
                    return ExecuteRetreatAction(action);
                case GameActionType.Evolution:
                    return ExecuteEvolutionAction(action);
                case GameActionType.UseAbility:
                    return ExecuteAbilityAction(action);
                case GameActionType.EndTurn:
                    return ExecuteEndTurnAction(action);
                default:
                    return ActionResult.CreateFailure($"Cannot execute unknown action type: {action.ActionType}");
            }
        }

        private ActionResult ExecutePlayCardAction(IGameAction action)
        {
            // カードプレイの実行
            Debug.Log($"[{ManagerName}] Executing play card action for player {action.PlayerId}");
            
            // TODO: 実際のカード移動処理
            
            return ActionResult.CreateSuccess();
        }

        private ActionResult ExecuteAttachEnergyAction(IGameAction action)
        {
            // エネルギー添付の実行
            var playerId = action.PlayerId;
            _playerEnergyAttached[playerId] = _playerEnergyAttached.GetValueOrDefault(playerId, 0) + 1;
            
            Debug.Log($"[{ManagerName}] Energy attached for player {playerId}");
            
            // エネルギー添付イベント発行
            EventBus.Emit(new EnergyAttachedEvent
            {
                PlayerId = playerId,
                EnergyCardId = action.Parameters["EnergyCardId"].ToString(),
                TargetPokemonId = action.Parameters["TargetPokemonId"].ToString()
            });
            
            return ActionResult.CreateSuccess();
        }

        private ActionResult ExecuteAttackAction(IGameAction action)
        {
            // 攻撃の実行
            var playerId = action.PlayerId;
            _playerHasAttacked[playerId] = true;
            
            Debug.Log($"[{ManagerName}] Attack executed by player {playerId}");
            
            // 攻撃イベント発行
            EventBus.Emit(new AttackExecutedEvent
            {
                AttackingPlayerId = playerId,
                AttackingPokemonId = action.Parameters["AttackingPokemonId"].ToString(),
                AttackName = action.Parameters["AttackName"].ToString(),
                TargetPokemonId = action.Parameters.GetValueOrDefault("TargetPokemonId", "").ToString()
            });
            
            return ActionResult.CreateSuccess();
        }

        private ActionResult ExecuteRetreatAction(IGameAction action)
        {
            Debug.Log($"[{ManagerName}] Retreat executed for player {action.PlayerId}");
            
            // にげる処理の実行
            EventBus.Emit(new PokemonRetreatEvent
            {
                PlayerId = action.PlayerId,
                RetreatPokemonId = action.Parameters["RetreatPokemonId"].ToString(),
                NewActivePokemonId = action.Parameters["NewActivePokemonId"].ToString()
            });
            
            return ActionResult.CreateSuccess();
        }

        private ActionResult ExecuteEvolutionAction(IGameAction action)
        {
            Debug.Log($"[{ManagerName}] Evolution executed for player {action.PlayerId}");
            
            // 進化処理の実行
            EventBus.Emit(new PokemonEvolvedEvent
            {
                PlayerId = action.PlayerId,
                SourcePokemonId = action.Parameters["EvolutionSourceId"].ToString(),
                EvolutionCardId = action.Parameters["EvolutionTargetCardId"].ToString()
            });
            
            return ActionResult.CreateSuccess();
        }

        private ActionResult ExecuteAbilityAction(IGameAction action)
        {
            Debug.Log($"[{ManagerName}] Ability used by player {action.PlayerId}");
            
            // 特性使用の実行
            EventBus.Emit(new AbilityUsedEvent
            {
                PlayerId = action.PlayerId,
                PokemonId = action.Parameters["PokemonId"].ToString(),
                AbilityName = action.Parameters["AbilityName"].ToString()
            });
            
            return ActionResult.CreateSuccess();
        }

        private ActionResult ExecuteEndTurnAction(IGameAction action)
        {
            Debug.Log($"[{ManagerName}] Turn ended by player {action.PlayerId}");
            
            // ターン終了処理
            EndTurn();
            
            return ActionResult.CreateSuccess();
        }

        #endregion

        #region Victory Conditions

        protected override void CheckVictoryConditions()
        {
            foreach (var kvp in _playerPrizeCards)
            {
                var playerId = kvp.Key;
                var prizesRemaining = kvp.Value;
                
                // サイドカード勝利
                if (prizesRemaining <= 0)
                {
                    EndGame(playerId, VictoryReason.AllPrizes);
                    return;
                }
                
                // デッキ切れチェック
                if (_playerDeckSizes.GetValueOrDefault(playerId, 0) <= 0)
                {
                    // 相手プレイヤーの勝利
                    var opponentId = GetOpponentId(playerId);
                    if (!string.IsNullOrEmpty(opponentId))
                    {
                        EndGame(opponentId, VictoryReason.DeckOut);
                        return;
                    }
                }
            }
            
            // ベンチなし勝利は別途チェック
            // TODO: 実際のポケモン状態管理システムと連携
        }

        #endregion

        #region Event Handlers

        private void OnTurnStarted(TurnStartEvent evt)
        {
            var playerId = evt.PlayerId;
            
            // ターン開始時のリセット
            _playerHasAttacked[playerId] = false;
            _playerEnergyAttached[playerId] = 0;
            
            // ポケモンチェック実行
            ProcessPokemonChecks(playerId);
            
            Debug.Log($"[{ManagerName}] Turn started for player {playerId}");
        }

        private void OnTurnEnded(TurnEndEvent evt)
        {
            Debug.Log($"[{ManagerName}] Turn ended for player {evt.PlayerId}");
            
            // 次のプレイヤーのターン開始
            var nextPlayerId = GetNextPlayerId(evt.PlayerId);
            if (!string.IsNullOrEmpty(nextPlayerId))
            {
                StartTurn(nextPlayerId);
            }
        }

        private void OnPokemonDamaged(PokemonDamagedEvent evt)
        {
            Debug.Log($"[{ManagerName}] Pokemon {evt.PokemonId} damaged for {evt.Damage} points");
            
            // ダメージ計算（弱点・抵抗力）
            var finalDamage = CalculateActualDamage(evt.Damage, evt.AttackingType, evt.DefendingType);
            
            // ダメージ適用後のきぜつチェック
            if (evt.RemainingHP <= 0)
            {
                EventBus.Emit(new PokemonKnockedOutEvent
                {
                    PlayerId = evt.PlayerId,
                    PokemonId = evt.PokemonId,
                    AttackingPlayerId = evt.AttackingPlayerId
                });
            }
        }

        private void OnPokemonKnockedOut(PokemonKnockedOutEvent evt)
        {
            Debug.Log($"[{ManagerName}] Pokemon {evt.PokemonId} knocked out");
            
            // サイドカード処理
            if (!string.IsNullOrEmpty(evt.AttackingPlayerId))
            {
                var currentPrizes = _playerPrizeCards.GetValueOrDefault(evt.AttackingPlayerId, 0);
                _playerPrizeCards[evt.AttackingPlayerId] = Math.Max(0, currentPrizes - 1);
            }
            
            // 勝利条件チェック
            CheckVictoryConditions();
        }

        #endregion

        #region Helper Methods

        private string DetermineFirstPlayer(List<string> playerIds)
        {
            // ランダムで先攻を決定
            return playerIds[UnityEngine.Random.Range(0, playerIds.Count)];
        }

        private string GetOpponentId(string playerId)
        {
            // 2人対戦前提での相手ID取得
            foreach (var kvp in _playerPrizeCards)
            {
                if (kvp.Key != playerId)
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        private string GetNextPlayerId(string currentPlayerId)
        {
            // 次のプレイヤーID取得（2人対戦前提）
            return GetOpponentId(currentPlayerId);
        }

        private void ProcessPokemonChecks(string playerId)
        {
            if (!_enablePoisonBetweenTurns && !_enableBurnFlipBetweenTurns)
                return;

            // どく状態チェック
            if (_enablePoisonBetweenTurns && _poisonedPokemons.ContainsKey(playerId))
            {
                foreach (var pokemonId in _poisonedPokemons[playerId])
                {
                    EventBus.Emit(new PoisonDamageEvent
                    {
                        PlayerId = playerId,
                        PokemonId = pokemonId,
                        Damage = 10
                    });
                }
            }

            // やけど状態チェック
            if (_enableBurnFlipBetweenTurns && _burnedPokemons.ContainsKey(playerId))
            {
                foreach (var pokemonId in _burnedPokemons[playerId])
                {
                    // コインフリップ
                    bool coinResult = UnityEngine.Random.Range(0, 2) == 0; // 50%
                    if (!coinResult) // 裏の場合ダメージ
                    {
                        EventBus.Emit(new BurnDamageEvent
                        {
                            PlayerId = playerId,
                            PokemonId = pokemonId,
                            Damage = 20
                        });
                    }
                }
            }
        }

        private int CalculateActualDamage(int baseDamage, PokemonType attackingType, PokemonType defendingType)
        {
            // 基本ダメージから開始
            float finalDamage = baseDamage;

            // 弱点計算
            if (IsWeakness(attackingType, defendingType))
            {
                finalDamage *= WEAKNESS_MULTIPLIER;
            }

            // 抵抗力計算
            if (IsResistance(attackingType, defendingType))
            {
                finalDamage = Math.Max(0, finalDamage - RESISTANCE_REDUCTION);
            }

            return Mathf.RoundToInt(finalDamage);
        }

        private bool IsWeakness(PokemonType attackingType, PokemonType defendingType)
        {
            // 簡易的な弱点関係（実際はもっと複雑）
            return (attackingType == PokemonType.Fire && defendingType == PokemonType.Grass) ||
                   (attackingType == PokemonType.Water && defendingType == PokemonType.Fire) ||
                   (attackingType == PokemonType.Grass && defendingType == PokemonType.Water);
        }

        private bool IsResistance(PokemonType attackingType, PokemonType defendingType)
        {
            // 簡易的な抵抗力関係
            return (attackingType == PokemonType.Fighting && defendingType == PokemonType.Psychic);
        }

        #endregion

        #region Debug

        public override string GetDebugInfo()
        {
            var baseInfo = base.GetDebugInfo();
            var classicInfo = $"\n--- Classic TCG Specific ---\n" +
                             $"Allow First Turn Attack: {_allowFirstTurnAttack}\n" +
                             $"Max Hand Size: {_maxHandSize}\n" +
                             $"Player Prize Cards: {string.Join(", ", _playerPrizeCards.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}\n" +
                             $"Player Deck Sizes: {string.Join(", ", _playerDeckSizes.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}";
            
            return baseInfo + classicInfo;
        }

        #endregion
    }

    #region Event Classes

    public class GameStartedEvent
    {
        public List<string> PlayerIds { get; set; }
        public string FirstPlayerId { get; set; }
        public string RuleType { get; set; }
    }

    public class GameEndedEvent
    {
        public string WinnerId { get; set; }
        public VictoryReason Reason { get; set; }
        public GameStatistics Statistics { get; set; }
    }

    public class TurnStartEvent
    {
        public string PlayerId { get; set; }
        public int TurnNumber { get; set; }
    }

    public class TurnEndEvent  
    {
        public string PlayerId { get; set; }
        public int TurnNumber { get; set; }
    }

    public class EnergyAttachedEvent
    {
        public string PlayerId { get; set; }
        public string EnergyCardId { get; set; }
        public string TargetPokemonId { get; set; }
    }

    public class AttackExecutedEvent
    {
        public string AttackingPlayerId { get; set; }
        public string AttackingPokemonId { get; set; }
        public string AttackName { get; set; }
        public string TargetPokemonId { get; set; }
    }

    public class PokemonRetreatEvent
    {
        public string PlayerId { get; set; }
        public string RetreatPokemonId { get; set; }
        public string NewActivePokemonId { get; set; }
    }

    public class PokemonEvolvedEvent
    {
        public string PlayerId { get; set; }
        public string SourcePokemonId { get; set; }
        public string EvolutionCardId { get; set; }
    }

    public class AbilityUsedEvent
    {
        public string PlayerId { get; set; }
        public string PokemonId { get; set; }
        public string AbilityName { get; set; }
    }

    public class PokemonDamagedEvent
    {
        public string PlayerId { get; set; }
        public string PokemonId { get; set; }
        public string AttackingPlayerId { get; set; }
        public int Damage { get; set; }
        public int RemainingHP { get; set; }
        public PokemonType AttackingType { get; set; }
        public PokemonType DefendingType { get; set; }
    }

    public class PokemonKnockedOutEvent
    {
        public string PlayerId { get; set; }
        public string PokemonId { get; set; }
        public string AttackingPlayerId { get; set; }
    }

    public class PoisonDamageEvent
    {
        public string PlayerId { get; set; }
        public string PokemonId { get; set; }
        public int Damage { get; set; }
    }

    public class BurnDamageEvent
    {
        public string PlayerId { get; set; }
        public string PokemonId { get; set; }
        public int Damage { get; set; }
    }

    #endregion

    #region Statistics Classes

    public class GameStatistics
    {
        public string WinnerId { get; set; }
        public VictoryReason VictoryReason { get; set; }
        public int TotalTurns { get; set; }
        public Dictionary<string, PlayerStatistics> PlayerStats { get; set; }
    }

    public class PlayerStatistics
    {
        public string PlayerId { get; set; }
        public int PrizesRemaining { get; set; }
        public int CardsInDeck { get; set; }
        public int AttacksExecuted { get; set; }
        public int PokemonKnockedOut { get; set; }
    }

    #endregion
}