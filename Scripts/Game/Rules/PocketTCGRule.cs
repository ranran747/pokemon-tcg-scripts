using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.Game.Rules
{
    /// <summary>
    /// ポケモンTCGポケット版ルール実装
    /// 20枚デッキ、3ポイント先取制、エネルギーゾーンシステム
    /// </summary>
    [CreateAssetMenu(fileName = "PocketTCGRule", menuName = "Pokemon TCG/Rules/Pocket TCG Rule")]
    public class PocketTCGRule : GameRule
    {
        #region Constants

        private const int DECK_SIZE = 20;
        private const int INITIAL_HAND_SIZE = 3;
        private const int POINTS_TO_WIN = 3;
        private const int MAX_BENCH_SIZE = 3;
        private const int ENERGY_ZONE_PER_TURN = 1;
        private const float WEAKNESS_DAMAGE_BONUS = 20f;
        private const int RESISTANCE_REDUCTION = 0; // ポケット版では抵抗力なし
        private const float GAME_TIME_LIMIT = 1200f; // 20分

        #endregion

        #region Fields

        [Header("ポケット版ルール設定")]
        [SerializeField] private bool _enableTimeLimit = true;
        [SerializeField] private bool _energyZoneAutomatic = true;
        [SerializeField] private bool _skipFirstTurnEnergy = true; // 先攻1ターン目はエネルギーゾーン増加なし
        [SerializeField] private int _maxPointsPerKnockout = 2; // ポケモンex撃破時

        [Header("ゲーム状態管理")]
        [SerializeField] private Dictionary<string, int> _playerPoints = new Dictionary<string, int>();
        [SerializeField] private Dictionary<string, int> _playerEnergyZones = new Dictionary<string, int>();
        [SerializeField] private Dictionary<string, float> _playerTimeRemaining = new Dictionary<string, float>();
        [SerializeField] private Dictionary<string, int> _playerDeckSizes = new Dictionary<string, int>();

        [Header("時間管理")]
        [SerializeField] private float _gameStartTime;
        [SerializeField] private float _turnStartTime;
        [SerializeField] private bool _timeWarningIssued = false;

        #endregion

        #region Properties

        /// <summary>初期化順序</summary>
        public override int InitializationOrder => 202; // PocketTCG専用

        /// <summary>ルール名</summary>
        public override string RuleName => "Pokemon TCG Pocket";

        /// <summary>時間制限有効</summary>
        public bool EnableTimeLimit => _enableTimeLimit;

        /// <summary>エネルギーゾーン自動増加</summary>
        public bool EnergyZoneAutomatic => _energyZoneAutomatic;

        /// <summary>残り時間取得</summary>
        public float RemainingGameTime => _enableTimeLimit ? GAME_TIME_LIMIT - (Time.time - _gameStartTime) : -1f;

        #endregion

        #region Initialization

        protected override void SetupGameRules()
        {
            // ポケット版ルール設定
            SetGameVariable("DeckSize", DECK_SIZE);
            SetGameVariable("InitialHandSize", INITIAL_HAND_SIZE);
            SetGameVariable("PointsToWin", POINTS_TO_WIN);
            SetGameVariable("MaxBenchSize", MAX_BENCH_SIZE);
            SetGameVariable("EnergyZonePerTurn", ENERGY_ZONE_PER_TURN);
            SetGameVariable("WeaknessDamageBonus", WEAKNESS_DAMAGE_BONUS);
            SetGameVariable("ResistanceReduction", RESISTANCE_REDUCTION);
            SetGameVariable("GameTimeLimit", GAME_TIME_LIMIT);
            
            Debug.Log($"[{ManagerName}] Pocket TCG rules configured");
        }

        protected override void OnRuleInitialized()
        {
            // イベント登録
            EventBus.On<TurnStartEvent>(OnTurnStarted);
            EventBus.On<TurnEndEvent>(OnTurnEnded);
            EventBus.On<PokemonDamagedEvent>(OnPokemonDamaged);
            EventBus.On<PokemonKnockedOutEvent>(OnPokemonKnockedOut);
            EventBus.On<EnergyZoneIncreasedEvent>(OnEnergyZoneIncreased);
            
            Debug.Log($"[{ManagerName}] Pocket TCG event handlers registered");
        }

        protected override void OnDispose()
        {
            // イベント解除
            EventBus.Off<TurnStartEvent>(OnTurnStarted);
            EventBus.Off<TurnEndEvent>(OnTurnEnded);
            EventBus.Off<PokemonDamagedEvent>(OnPokemonDamaged);
            EventBus.Off<PokemonKnockedOutEvent>(OnPokemonKnockedOut);
            EventBus.Off<EnergyZoneIncreasedEvent>(OnEnergyZoneIncreased);
            
            // データクリア
            _playerPoints.Clear();
            _playerEnergyZones.Clear();
            _playerTimeRemaining.Clear();
            _playerDeckSizes.Clear();
            
            base.OnDispose();
        }

        #endregion

        #region Game Flow Implementation

        protected override void OnGameStarted(List<string> playerIds)
        {
            Debug.Log($"[{ManagerName}] Starting Pocket TCG game with {playerIds.Count} players");
            
            _gameStartTime = Time.time;
            
            // プレイヤー状態初期化
            foreach (var playerId in playerIds)
            {
                _playerPoints[playerId] = 0;
                _playerEnergyZones[playerId] = 0; // エネルギーゾーンは0から開始
                _playerTimeRemaining[playerId] = GAME_TIME_LIMIT / 2f; // 各プレイヤー10分
                _playerDeckSizes[playerId] = DECK_SIZE - INITIAL_HAND_SIZE; // 手札3枚を引いた後
            }
            
            // セットアップフェーズ開始
            SetPhase(GamePhase.Setup);
            
            // 最初のプレイヤーを決定
            var firstPlayerId = DetermineFirstPlayer(playerIds);
            StartTurn(firstPlayerId);
            
            EventBus.Emit(new GameStartedEvent 
            { 
                PlayerIds = playerIds, 
                FirstPlayerId = firstPlayerId,
                RuleType = "PocketTCG"
                // TimeLimitプロパティは削除
            });
        }

        protected override void OnGameFinished(string winnerId, VictoryReason reason)
        {
            Debug.Log($"[{ManagerName}] Pocket TCG game finished. Winner: {winnerId}, Reason: {reason}");
            
            // 統計情報の記録
            var gameStats = new PocketGameStatistics
            {
                WinnerId = winnerId,
                VictoryReason = reason,
                TotalTurns = CurrentTurn,
                GameDuration = Time.time - _gameStartTime,
                PlayerStats = new Dictionary<string, PocketPlayerStatistics>()
            };
            
            foreach (var kvp in _playerPoints)
            {
                gameStats.PlayerStats[kvp.Key] = new PocketPlayerStatistics
                {
                    PlayerId = kvp.Key,
                    PointsScored = kvp.Value,
                    EnergyZoneSize = _playerEnergyZones.GetValueOrDefault(kvp.Key, 0),
                    CardsInDeck = _playerDeckSizes.GetValueOrDefault(kvp.Key, 0),
                    TimeUsed = (GAME_TIME_LIMIT / 2f) - _playerTimeRemaining.GetValueOrDefault(kvp.Key, 0)
                };
            }
            
            EventBus.Emit(new PocketGameEndedEvent 
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
            // ポケット版特有の制限チェック
            if (!action.Parameters.TryGetValue("CardId", out var cardIdObj) || !(cardIdObj is string))
            {
                return ValidationResult.CreateError("Card ID is required for play card action");
            }

            // 簡略化されたルールのため、多くの制限が緩和されている
            return ValidationResult.CreateSuccess();
        }

        private ValidationResult ValidateAttackAction(IGameAction action)
        {
            var playerId = action.PlayerId;
            
            // エネルギーゾーンのエネルギーチェック
            int requiredEnergy = 0; // デフォルト値で初期化
            
            if (!action.Parameters.TryGetValue("RequiredEnergy", out var requiredEnergyObj) || 
                !(requiredEnergyObj is int))
            {
                return ValidationResult.CreateError("Required energy amount is needed");
            }
            
            requiredEnergy = (int)requiredEnergyObj; // 値を代入

            var availableEnergy = _playerEnergyZones.GetValueOrDefault(playerId, 0);
            if (availableEnergy < requiredEnergy)
            {
                return ValidationResult.CreateError($"Not enough energy. Required: {requiredEnergy}, Available: {availableEnergy}");
            }

            return ValidationResult.CreateSuccess();
        }

        private ValidationResult ValidateRetreatAction(IGameAction action)
        {
            // ポケット版では簡略化されたにげるルール
            if (!action.Parameters.TryGetValue("RetreatCost", out var retreatCostObj) || !(retreatCostObj is int retreatCost))
            {
                return ValidationResult.CreateError("Retreat cost is required");
            }

            var playerId = action.PlayerId;
            var availableEnergy = _playerEnergyZones.GetValueOrDefault(playerId, 0);
            
            if (availableEnergy < retreatCost)
            {
                return ValidationResult.CreateError($"Not enough energy to retreat. Required: {retreatCost}, Available: {availableEnergy}");
            }

            return ValidationResult.CreateSuccess();
        }

        private ValidationResult ValidateEvolutionAction(IGameAction action)
        {
            // ポケット版でも進化は可能
            if (!action.Parameters.TryGetValue("EvolutionSourceId", out var sourceId))
            {
                return ValidationResult.CreateError("Evolution source Pokemon ID is required");
            }

            if (!action.Parameters.TryGetValue("EvolutionTargetCardId", out var targetCardId))
            {
                return ValidationResult.CreateError("Evolution target card ID is required");
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
            // ターン終了時の時間チェック
            if (_enableTimeLimit)
            {
                var playerId = action.PlayerId;
                var timeUsed = Time.time - _turnStartTime;
                var remainingTime = _playerTimeRemaining.GetValueOrDefault(playerId, 0);
                
                if (timeUsed > remainingTime)
                {
                    // 時間切れの場合は強制的にターン終了
                    return ValidationResult.CreateSuccess(); // エラーではなく警告として処理
                }
            }

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
            Debug.Log($"[{ManagerName}] Executing play card action for player {action.PlayerId}");
            
            // ポケット版特有の処理
            // より簡略化されたカードプレイ処理
            
            return ActionResult.CreateSuccess();
        }

        private ActionResult ExecuteAttackAction(IGameAction action)
        {
            var playerId = action.PlayerId;
            Debug.Log($"[{ManagerName}] Attack executed by player {playerId}");
            
            int requiredEnergy = 0; // メソッドの最初で変数を宣言
            
            // エネルギーゾーンからエネルギーを消費（ポケット版特有）
            if (action.Parameters.TryGetValue("RequiredEnergy", out var requiredEnergyObj) && 
                requiredEnergyObj is int energyAmount)
            {
                requiredEnergy = energyAmount; // 値を代入
                _playerEnergyZones[playerId] = Math.Max(0, _playerEnergyZones[playerId] - requiredEnergy);
            }
            
            // 攻撃イベント発行
            EventBus.Emit(new PocketAttackExecutedEvent
            {
                AttackingPlayerId = playerId,
                AttackingPokemonId = action.Parameters["AttackingPokemonId"].ToString(),
                AttackName = action.Parameters["AttackName"].ToString(),
                TargetPokemonId = action.Parameters.GetValueOrDefault("TargetPokemonId", "").ToString(),
                EnergyUsed = requiredEnergy // これで使用可能
            });
            
            return ActionResult.CreateSuccess();
        }

        private ActionResult ExecuteRetreatAction(IGameAction action)
        {
            var playerId = action.PlayerId;
            Debug.Log($"[{ManagerName}] Retreat executed for player {playerId}");
            
            // エネルギーゾーンからにげるコストを支払い
            if (action.Parameters.TryGetValue("RetreatCost", out var retreatCostObj) && 
                retreatCostObj is int retreatCost)
            {
                _playerEnergyZones[playerId] = Math.Max(0, _playerEnergyZones[playerId] - retreatCost);
            }
            
            EventBus.Emit(new PokemonRetreatEvent
            {
                PlayerId = playerId,
                RetreatPokemonId = action.Parameters["RetreatPokemonId"].ToString(),
                NewActivePokemonId = action.Parameters["NewActivePokemonId"].ToString()
            });
            
            return ActionResult.CreateSuccess();
        }

        private ActionResult ExecuteEvolutionAction(IGameAction action)
        {
            Debug.Log($"[{ManagerName}] Evolution executed for player {action.PlayerId}");
            
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
            var playerId = action.PlayerId;
            Debug.Log($"[{ManagerName}] Turn ended by player {playerId}");
            
            // 時間管理
            if (_enableTimeLimit)
            {
                var turnDuration = Time.time - _turnStartTime;
                _playerTimeRemaining[playerId] = Math.Max(0, _playerTimeRemaining[playerId] - turnDuration);
            }
            
            EndTurn();
            return ActionResult.CreateSuccess();
        }

        #endregion

        #region Victory Conditions

        protected override void CheckVictoryConditions()
        {
            foreach (var kvp in _playerPoints)
            {
                var playerId = kvp.Key;
                var points = kvp.Value;
                
                // ポイント勝利
                if (points >= POINTS_TO_WIN)
                {
                    EndGame(playerId, VictoryReason.PointTarget);
                    return;
                }
                
                // デッキ切れチェック
                if (_playerDeckSizes.GetValueOrDefault(playerId, 0) <= 0)
                {
                    var opponentId = GetOpponentId(playerId);
                    if (!string.IsNullOrEmpty(opponentId))
                    {
                        EndGame(opponentId, VictoryReason.DeckOut);
                        return;
                    }
                }
            }
            
            // 時間切れチェック
            if (_enableTimeLimit)
            {
                var gameTime = Time.time - _gameStartTime;
                if (gameTime >= GAME_TIME_LIMIT)
                {
                    // ポイントが多い方の勝利
                    var winner = _playerPoints.OrderByDescending(kvp => kvp.Value).First();
                    EndGame(winner.Key, VictoryReason.TimeLimit);
                    return;
                }
                
                // 個別時間切れチェック
                foreach (var kvp in _playerTimeRemaining)
                {
                    if (kvp.Value <= 0)
                    {
                        var opponentId = GetOpponentId(kvp.Key);
                        if (!string.IsNullOrEmpty(opponentId))
                        {
                            EndGame(opponentId, VictoryReason.TimeLimit);
                            return;
                        }
                    }
                }
            }
        }

        #endregion

        #region Event Handlers

        private void OnTurnStarted(TurnStartEvent evt)
        {
            var playerId = evt.PlayerId;
            _turnStartTime = Time.time;
            
            // エネルギーゾーン自動増加
            if (_energyZoneAutomatic)
            {
                // 先攻1ターン目はスキップ
                if (!(CurrentTurn == 1 && _skipFirstTurnEnergy))
                {
                    _playerEnergyZones[playerId] = _playerEnergyZones.GetValueOrDefault(playerId, 0) + ENERGY_ZONE_PER_TURN;
                    
                    EventBus.Emit(new EnergyZoneIncreasedEvent
                    {
                        PlayerId = playerId,
                        NewEnergyZoneSize = _playerEnergyZones[playerId]
                    });
                }
            }
            
            Debug.Log($"[{ManagerName}] Turn started for player {playerId}, Energy Zone: {_playerEnergyZones[playerId]}");
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
            
            // ポケット版では弱点は+20ダメージ
            var finalDamage = CalculateActualDamage(evt.Damage, evt.AttackingType, evt.DefendingType);
            
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
            
            // ポイント加算処理（ポケット版特有）
            if (!string.IsNullOrEmpty(evt.AttackingPlayerId))
            {
                var pointsToAdd = CalculatePointsForKnockout(evt.PokemonId);
                var currentPoints = _playerPoints.GetValueOrDefault(evt.AttackingPlayerId, 0);
                _playerPoints[evt.AttackingPlayerId] = currentPoints + pointsToAdd;
                
                EventBus.Emit(new PointsScoredEvent
                {
                    PlayerId = evt.AttackingPlayerId,
                    PointsAdded = pointsToAdd,
                    TotalPoints = _playerPoints[evt.AttackingPlayerId],
                    KnockedOutPokemonId = evt.PokemonId
                });
            }
            
            // 勝利条件チェック
            CheckVictoryConditions();
        }

        private void OnEnergyZoneIncreased(EnergyZoneIncreasedEvent evt)
        {
            Debug.Log($"[{ManagerName}] Player {evt.PlayerId} energy zone increased to {evt.NewEnergyZoneSize}");
        }

        #endregion

        #region Helper Methods

        private string DetermineFirstPlayer(List<string> playerIds)
        {
            return playerIds[UnityEngine.Random.Range(0, playerIds.Count)];
        }

        private string GetOpponentId(string playerId)
        {
            foreach (var kvp in _playerPoints)
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
            return GetOpponentId(currentPlayerId);
        }

        private int CalculateActualDamage(int baseDamage, PokemonType attackingType, PokemonType defendingType)
        {
            float finalDamage = baseDamage;

            // ポケット版では弱点は+20ダメージ
            if (IsWeakness(attackingType, defendingType))
            {
                finalDamage += WEAKNESS_DAMAGE_BONUS;
            }

            // ポケット版では抵抗力なし
            
            return Mathf.RoundToInt(finalDamage);
        }

        private bool IsWeakness(PokemonType attackingType, PokemonType defendingType)
        {
            // 簡易的な弱点関係
            return (attackingType == PokemonType.Fire && defendingType == PokemonType.Grass) ||
                   (attackingType == PokemonType.Water && defendingType == PokemonType.Fire) ||
                   (attackingType == PokemonType.Grass && defendingType == PokemonType.Water);
        }

        private int CalculatePointsForKnockout(string pokemonId)
        {
            // TODO: 実際のポケモンデータからポイントを計算
            // ポケモンexなら2ポイント、通常ポケモンなら1ポイント
            // 現在は仮実装
            return 1; // デフォルトで1ポイント
        }

        #endregion

        #region Time Management

        /// <summary>
        /// 残り時間の警告チェック
        /// </summary>
        public void CheckTimeWarnings()
        {
            if (!_enableTimeLimit || _timeWarningIssued) return;

            var remainingTime = RemainingGameTime;
            if (remainingTime > 0 && remainingTime <= 300f) // 5分切り
            {
                _timeWarningIssued = true;
                
                EventBus.Emit(new TimeWarningEvent
                {
                    RemainingTime = remainingTime,
                    WarningType = TimeWarningType.FiveMinutes
                });
            }
        }

        /// <summary>
        /// プレイヤーの残り時間を取得
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>残り時間（秒）</returns>
        public float GetPlayerRemainingTime(string playerId)
        {
            return _playerTimeRemaining.GetValueOrDefault(playerId, 0f);
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void Update()
        {
            // 時間管理の更新
            if (_enableTimeLimit && !GameEnded)
            {
                CheckTimeWarnings();
                CheckVictoryConditions(); // 時間切れチェック含む
            }
        }

        #endregion

        #region Debug

        public override string GetDebugInfo()
        {
            var baseInfo = base.GetDebugInfo();
            var pocketInfo = $"\n--- Pocket TCG Specific ---\n" +
                            $"Time Limit Enabled: {_enableTimeLimit}\n" +
                            $"Remaining Game Time: {RemainingGameTime:F1}s\n" +
                            $"Energy Zone Automatic: {_energyZoneAutomatic}\n" +
                            $"Player Points: {string.Join(", ", _playerPoints.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}\n" +
                            $"Player Energy Zones: {string.Join(", ", _playerEnergyZones.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}\n" +
                            $"Player Time Remaining: {string.Join(", ", _playerTimeRemaining.Select(kvp => $"{kvp.Key}:{kvp.Value:F1}"))}";
            
            return baseInfo + pocketInfo;
        }

        #endregion
    }

    #region Pocket-Specific Event Classes

    public class PocketAttackExecutedEvent
    {
        public string AttackingPlayerId { get; set; }
        public string AttackingPokemonId { get; set; }
        public string AttackName { get; set; }
        public string TargetPokemonId { get; set; }
        public int EnergyUsed { get; set; }
    }

    public class EnergyZoneIncreasedEvent
    {
        public string PlayerId { get; set; }
        public int NewEnergyZoneSize { get; set; }
    }

    public class PointsScoredEvent
    {
        public string PlayerId { get; set; }
        public int PointsAdded { get; set; }
        public int TotalPoints { get; set; }
        public string KnockedOutPokemonId { get; set; }
    }

    public class PocketGameEndedEvent
    {
        public string WinnerId { get; set; }
        public VictoryReason Reason { get; set; }
        public PocketGameStatistics Statistics { get; set; }
    }

    public class TimeWarningEvent
    {
        public float RemainingTime { get; set; }
        public TimeWarningType WarningType { get; set; }
    }

    public enum TimeWarningType
    {
        TenMinutes,
        FiveMinutes,
        OneMinute,
        ThirtySeconds
    }

    #endregion

    #region Pocket-Specific Statistics Classes

    public class PocketGameStatistics
    {
        public string WinnerId { get; set; }
        public VictoryReason VictoryReason { get; set; }
        public int TotalTurns { get; set; }
        public float GameDuration { get; set; }
        public Dictionary<string, PocketPlayerStatistics> PlayerStats { get; set; }
    }

    public class PocketPlayerStatistics
    {
        public string PlayerId { get; set; }
        public int PointsScored { get; set; }
        public int EnergyZoneSize { get; set; }
        public int CardsInDeck { get; set; }
        public float TimeUsed { get; set; }
        public int AttacksExecuted { get; set; }
        public int PokemonKnockedOut { get; set; }
    }

    #endregion
}