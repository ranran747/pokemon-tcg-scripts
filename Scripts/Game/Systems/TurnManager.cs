using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.Game.Rules
{
    /// <summary>
    /// 汎用ターン管理システム
    /// ゲームルールに依存しない汎用的なターン制御を提供
    /// </summary>
    public class TurnManager : MonoBehaviourSingleton<TurnManager>, IManager
    {
        #region Events

        public static event Action<TurnPhaseChangedEvent> OnTurnPhaseChanged;
        public static event Action<TurnPlayerChangedEvent> OnTurnPlayerChanged;
        public static event Action<TurnTimerUpdateEvent> OnTurnTimerUpdate;
        public static event Action<TurnActionExecutedEvent> OnTurnActionExecuted;

        #endregion

        #region Fields

        [Header("ターン管理設定")]
        [SerializeField] private bool _enableTurnTimer = false;
        [SerializeField] private float _defaultTurnTimeLimit = 300f; // 5分
        [SerializeField] private bool _enablePhaseSystem = true;
        [SerializeField] private bool _autoAdvancePhases = false;

        [Header("現在のターン状態")]
        [SerializeField] private int _currentTurnNumber = 0;
        [SerializeField] private string _currentPlayerId = "";
        [SerializeField] private TurnPhase _currentPhase = TurnPhase.None;
        [SerializeField] private float _turnStartTime = 0f;
        [SerializeField] private float _phaseStartTime = 0f;

        [Header("プレイヤー管理")]
        [SerializeField] private List<string> _playerOrder = new List<string>();
        [SerializeField] private int _currentPlayerIndex = 0;
        [SerializeField] private Dictionary<string, float> _playerTimeRemaining = new Dictionary<string, float>();
        [SerializeField] private Dictionary<string, TurnPlayerStatistics> _playerTurnStats = new Dictionary<string, TurnPlayerStatistics>();

        [Header("フェーズ管理")]
        [SerializeField] private List<TurnPhase> _phaseOrder = new List<TurnPhase>();
        [SerializeField] private int _currentPhaseIndex = 0;
        [SerializeField] private Dictionary<TurnPhase, float> _phaseTimeouts = new Dictionary<TurnPhase, float>();
        [SerializeField] private Dictionary<TurnPhase, bool> _phaseMandatory = new Dictionary<TurnPhase, bool>();

        [Header("ターンアクション履歴")]
        [SerializeField] private List<TurnAction> _currentTurnActions = new List<TurnAction>();
        [SerializeField] private Dictionary<int, List<TurnAction>> _turnHistory = new Dictionary<int, List<TurnAction>>();

        #endregion

        #region Properties

        /// <summary>マネージャー名</summary>
        public string ManagerName => "TurnManager";

        /// <summary>初期化順序</summary>
        public int InitializationOrder => 150; // GameStateManagerより後

        /// <summary>現在のターン番号</summary>
        public int CurrentTurnNumber => _currentTurnNumber;

        /// <summary>現在のプレイヤーID</summary>
        public string CurrentPlayerId => _currentPlayerId;

        /// <summary>現在のフェーズ</summary>
        public TurnPhase CurrentPhase => _currentPhase;

        /// <summary>ターン残り時間</summary>
        public float TurnTimeRemaining => 
            _enableTurnTimer ? Mathf.Max(0f, _defaultTurnTimeLimit - (Time.time - _turnStartTime)) : -1f;

        /// <summary>フェーズ残り時間</summary>
        public float PhaseTimeRemaining => 
            _phaseTimeouts.ContainsKey(_currentPhase) 
                ? Mathf.Max(0f, _phaseTimeouts[_currentPhase] - (Time.time - _phaseStartTime))
                : -1f;

        /// <summary>ターンタイマー有効</summary>
        public bool TurnTimerEnabled => _enableTurnTimer;

        /// <summary>フェーズシステム有効</summary>
        public bool PhaseSystemEnabled => _enablePhaseSystem;

        /// <summary>プレイヤー順序</summary>
        public IReadOnlyList<string> PlayerOrder => _playerOrder.AsReadOnly();

        /// <summary>現在ターンのアクション数</summary>
        public int CurrentTurnActionCount => _currentTurnActions.Count;

        #endregion

        #region Initialization

        public void Initialize()
        {
            Debug.Log($"[{ManagerName}] Initializing Turn Manager...");
            
            // デフォルトフェーズ順序設定
            SetupDefaultPhaseOrder();
            
            // イベント登録
            EventBus.On<GameStartedEvent>(OnGameStarted);
            EventBus.On<GameEndedEvent>(OnGameEnded);
            EventBus.On<PlayerActionExecutedEvent>(OnPlayerActionExecuted);
            
            Debug.Log($"[{ManagerName}] Turn Manager initialized");
        }

        public void Dispose()
        {
            Debug.Log($"[{ManagerName}] Disposing Turn Manager...");
            
            // イベント解除
            EventBus.Off<GameStartedEvent>(OnGameStarted);
            EventBus.Off<GameEndedEvent>(OnGameEnded);
            EventBus.Off<PlayerActionExecutedEvent>(OnPlayerActionExecuted);
            
            // データクリア
            ClearTurnData();
            
            Debug.Log($"[{ManagerName}] Turn Manager disposed");
        }

        private void SetupDefaultPhaseOrder()
        {
            _phaseOrder.Clear();
            _phaseOrder.AddRange(new[]
            {
                TurnPhase.Draw,
                TurnPhase.Main,
                TurnPhase.Battle,
                TurnPhase.End
            });

            // デフォルトフェーズ設定
            _phaseTimeouts.Clear();
            _phaseMandatory.Clear();
            
            foreach (var phase in _phaseOrder)
            {
                _phaseTimeouts[phase] = 60f; // デフォルト1分
                _phaseMandatory[phase] = false; // デフォルトは任意
            }
            
            // 必須フェーズ設定
            _phaseMandatory[TurnPhase.Draw] = true;
            _phaseMandatory[TurnPhase.End] = true;
        }

        #endregion

        #region Turn Management

        /// <summary>
        /// ゲーム開始時のターン初期化
        /// </summary>
        /// <param name="playerIds">プレイヤーIDリスト</param>
        /// <param name="firstPlayerId">先攻プレイヤーID</param>
        public void InitializeTurns(List<string> playerIds, string firstPlayerId)
        {
            Debug.Log($"[{ManagerName}] Initializing turns for {playerIds.Count} players");
            
            // プレイヤー順序設定
            _playerOrder.Clear();
            _playerOrder.AddRange(playerIds);
            
            // 先攻プレイヤーを最初に設定
            if (!string.IsNullOrEmpty(firstPlayerId) && _playerOrder.Contains(firstPlayerId))
            {
                _playerOrder.Remove(firstPlayerId);
                _playerOrder.Insert(0, firstPlayerId);
            }
            
            _currentPlayerIndex = 0;
            _currentTurnNumber = 1;
            _currentPlayerId = _playerOrder[0];
            
            // プレイヤー統計初期化
            foreach (var playerId in playerIds)
            {
                _playerTimeRemaining[playerId] = _defaultTurnTimeLimit;
                _playerTurnStats[playerId] = new TurnPlayerStatistics
                {
                    PlayerId = playerId,
                    TurnsPlayed = 0,
                    TotalTimeUsed = 0f,
                    ActionsExecuted = 0,
                    PhasesCompleted = 0
                };
            }
            
            Debug.Log($"[{ManagerName}] Turn order: {string.Join(" -> ", _playerOrder)}");
        }

        /// <summary>
        /// ターン開始
        /// </summary>
        public void StartTurn()
        {
            Debug.Log($"[{ManagerName}] Starting turn {_currentTurnNumber} for player {_currentPlayerId}");
            
            _turnStartTime = Time.time;
            _currentTurnActions.Clear();
            
            // 統計更新
            if (_playerTurnStats.ContainsKey(_currentPlayerId))
            {
                _playerTurnStats[_currentPlayerId].TurnsPlayed++;
            }
            
            // フェーズシステム開始
            if (_enablePhaseSystem)
            {
                StartPhase(TurnPhase.Draw);
            }
            
            // ターン開始イベント発行
            EventBus.Emit(new TurnStartedEvent
            {
                TurnNumber = _currentTurnNumber,
                PlayerId = _currentPlayerId,
                PlayerIndex = _currentPlayerIndex
            });
            
            OnTurnPlayerChanged?.Invoke(new TurnPlayerChangedEvent
            {
                PreviousPlayerId = GetPreviousPlayerId(),
                NewPlayerId = _currentPlayerId,
                TurnNumber = _currentTurnNumber
            });
        }

        /// <summary>
        /// ターン終了
        /// </summary>
        public void EndTurn()
        {
            Debug.Log($"[{ManagerName}] Ending turn {_currentTurnNumber} for player {_currentPlayerId}");
            
            // ターン時間記録
            var turnDuration = Time.time - _turnStartTime;
            if (_playerTurnStats.ContainsKey(_currentPlayerId))
            {
                _playerTurnStats[_currentPlayerId].TotalTimeUsed += turnDuration;
            }
            
            // ターン履歴保存
            _turnHistory[_currentTurnNumber] = new List<TurnAction>(_currentTurnActions);
            
            // ターン終了イベント発行
            EventBus.Emit(new TurnEndedEvent
            {
                TurnNumber = _currentTurnNumber,
                PlayerId = _currentPlayerId,
                TurnDuration = turnDuration,
                ActionsExecuted = _currentTurnActions.Count
            });
            
            // 次のプレイヤーに移行
            AdvanceToNextPlayer();
        }

        /// <summary>
        /// 次のプレイヤーに進む
        /// </summary>
        private void AdvanceToNextPlayer()
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _playerOrder.Count;
            
            // 全プレイヤーが1回ずつプレイしたらターン番号増加
            if (_currentPlayerIndex == 0)
            {
                _currentTurnNumber++;
            }
            
            _currentPlayerId = _playerOrder[_currentPlayerIndex];
            
            Debug.Log($"[{ManagerName}] Advanced to player {_currentPlayerId} (Turn {_currentTurnNumber})");
        }

        /// <summary>
        /// 強制ターン終了（時間切れなど）
        /// </summary>
        /// <param name="reason">終了理由</param>
        public void ForceTurnEnd(TurnEndReason reason)
        {
            Debug.LogWarning($"[{ManagerName}] Force ending turn for {_currentPlayerId}. Reason: {reason}");
            
            EventBus.Emit(new TurnForcedEndEvent
            {
                TurnNumber = _currentTurnNumber,
                PlayerId = _currentPlayerId,
                Reason = reason
            });
            
            EndTurn();
        }

        #endregion

        #region Phase Management

        /// <summary>
        /// フェーズ開始
        /// </summary>
        /// <param name="phase">開始するフェーズ</param>
        public void StartPhase(TurnPhase phase)
        {
            if (!_enablePhaseSystem) return;
            
            Debug.Log($"[{ManagerName}] Starting phase {phase} for player {_currentPlayerId}");
            
            _currentPhase = phase;
            _phaseStartTime = Time.time;
            _currentPhaseIndex = _phaseOrder.IndexOf(phase);
            
            // フェーズ統計更新
            if (_playerTurnStats.ContainsKey(_currentPlayerId))
            {
                _playerTurnStats[_currentPlayerId].PhasesCompleted++;
            }
            
            EventBus.Emit(new PhaseStartedEvent
            {
                Phase = phase,
                PlayerId = _currentPlayerId,
                TurnNumber = _currentTurnNumber
            });
            
            OnTurnPhaseChanged?.Invoke(new TurnPhaseChangedEvent
            {
                PreviousPhase = GetPreviousPhase(),
                NewPhase = phase,
                PlayerId = _currentPlayerId
            });
        }

        /// <summary>
        /// フェーズ終了
        /// </summary>
        public void EndPhase()
        {
            if (!_enablePhaseSystem || _currentPhase == TurnPhase.None) return;
            
            var phaseDuration = Time.time - _phaseStartTime;
            
            Debug.Log($"[{ManagerName}] Ending phase {_currentPhase} (Duration: {phaseDuration:F2}s)");
            
            EventBus.Emit(new PhaseEndedEvent
            {
                Phase = _currentPhase,
                PlayerId = _currentPlayerId,
                TurnNumber = _currentTurnNumber,
                PhaseDuration = phaseDuration
            });
            
            // 自動フェーズ進行
            if (_autoAdvancePhases)
            {
                AdvanceToNextPhase();
            }
        }

        /// <summary>
        /// 次のフェーズに進む
        /// </summary>
        public void AdvanceToNextPhase()
        {
            if (!_enablePhaseSystem) return;
            
            _currentPhaseIndex = (_currentPhaseIndex + 1) % _phaseOrder.Count;
            var nextPhase = _phaseOrder[_currentPhaseIndex];
            
            // 最後のフェーズに達したらターン終了
            if (nextPhase == TurnPhase.End)
            {
                StartPhase(nextPhase);
                EndTurn();
            }
            else
            {
                StartPhase(nextPhase);
            }
        }

        /// <summary>
        /// フェーズスキップ
        /// </summary>
        /// <param name="phase">スキップするフェーズ</param>
        public void SkipPhase(TurnPhase phase)
        {
            if (_phaseMandatory.GetValueOrDefault(phase, false))
            {
                Debug.LogWarning($"[{ManagerName}] Cannot skip mandatory phase {phase}");
                return;
            }
            
            Debug.Log($"[{ManagerName}] Skipping phase {phase}");
            
            EventBus.Emit(new PhaseSkippedEvent
            {
                Phase = phase,
                PlayerId = _currentPlayerId,
                TurnNumber = _currentTurnNumber
            });
            
            AdvanceToNextPhase();
        }

        #endregion

        #region Action Management

        /// <summary>
        /// ターンアクション記録
        /// </summary>
        /// <param name="actionType">アクション種別</param>
        /// <param name="actionData">アクションデータ</param>
        public void RecordTurnAction(GameActionType actionType, Dictionary<string, object> actionData)
        {
            var action = new TurnAction
            {
                ActionType = actionType,
                PlayerId = _currentPlayerId,
                TurnNumber = _currentTurnNumber,
                Phase = _currentPhase,
                Timestamp = Time.time,
                ActionData = actionData ?? new Dictionary<string, object>()
            };
            
            _currentTurnActions.Add(action);
            
            // 統計更新
            if (_playerTurnStats.ContainsKey(_currentPlayerId))
            {
                _playerTurnStats[_currentPlayerId].ActionsExecuted++;
            }
            
            Debug.Log($"[{ManagerName}] Recorded action {actionType} for player {_currentPlayerId}");
            
            OnTurnActionExecuted?.Invoke(new TurnActionExecutedEvent
            {
                Action = action,
                TotalActionsThisTurn = _currentTurnActions.Count
            });
        }

        /// <summary>
        /// 現在ターンのアクション取得
        /// </summary>
        /// <returns>現在ターンのアクションリスト</returns>
        public IReadOnlyList<TurnAction> GetCurrentTurnActions()
        {
            return _currentTurnActions.AsReadOnly();
        }

        /// <summary>
        /// 指定ターンのアクション履歴取得
        /// </summary>
        /// <param name="turnNumber">ターン番号</param>
        /// <returns>アクション履歴</returns>
        public IReadOnlyList<TurnAction> GetTurnHistory(int turnNumber)
        {
            return _turnHistory.ContainsKey(turnNumber) 
                ? _turnHistory[turnNumber].AsReadOnly() 
                : new List<TurnAction>().AsReadOnly();
        }

        #endregion

        #region Timer Management

        /// <summary>
        /// ターンタイマー設定
        /// </summary>
        /// <param name="timeLimit">制限時間（秒）</param>
        public void SetTurnTimeLimit(float timeLimit)
        {
            _defaultTurnTimeLimit = timeLimit;
            Debug.Log($"[{ManagerName}] Turn time limit set to {timeLimit} seconds");
        }

        /// <summary>
        /// フェーズタイマー設定
        /// </summary>
        /// <param name="phase">フェーズ</param>
        /// <param name="timeLimit">制限時間（秒）</param>
        public void SetPhaseTimeLimit(TurnPhase phase, float timeLimit)
        {
            _phaseTimeouts[phase] = timeLimit;
            Debug.Log($"[{ManagerName}] Phase {phase} time limit set to {timeLimit} seconds");
        }

        #endregion

        #region Event Handlers

        private void OnGameStarted(GameStartedEvent evt)
        {
            Debug.Log($"[{ManagerName}] Game started, initializing turns");
            InitializeTurns(evt.PlayerIds, evt.FirstPlayerId);
            StartTurn();
        }

        private void OnGameEnded(GameEndedEvent evt)
        {
            Debug.Log($"[{ManagerName}] Game ended, clearing turn data");
            ClearTurnData();
        }

        private void OnPlayerActionExecuted(PlayerActionExecutedEvent evt)
        {
            RecordTurnAction(evt.ActionType, evt.ActionData);
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (_enableTurnTimer && !string.IsNullOrEmpty(_currentPlayerId))
            {
                UpdateTurnTimer();
            }
            
            if (_enablePhaseSystem && _currentPhase != TurnPhase.None)
            {
                UpdatePhaseTimer();
            }
        }

        private void UpdateTurnTimer()
        {
            var timeRemaining = TurnTimeRemaining;
            
            OnTurnTimerUpdate?.Invoke(new TurnTimerUpdateEvent
            {
                PlayerId = _currentPlayerId,
                TimeRemaining = timeRemaining,
                TotalTime = _defaultTurnTimeLimit
            });
            
            // 時間切れチェック
            if (timeRemaining <= 0f)
            {
                ForceTurnEnd(TurnEndReason.TimeLimit);
            }
        }

        private void UpdatePhaseTimer()
        {
            if (_phaseTimeouts.ContainsKey(_currentPhase))
            {
                var timeRemaining = PhaseTimeRemaining;
                
                // フェーズ時間切れチェック
                if (timeRemaining <= 0f)
                {
                    Debug.LogWarning($"[{ManagerName}] Phase {_currentPhase} timed out");
                    AdvanceToNextPhase();
                }
            }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// フェーズ順序設定
        /// </summary>
        /// <param name="phases">フェーズ順序</param>
        public void SetPhaseOrder(List<TurnPhase> phases)
        {
            _phaseOrder.Clear();
            _phaseOrder.AddRange(phases);
            _currentPhaseIndex = 0;
            
            Debug.Log($"[{ManagerName}] Phase order set: {string.Join(" -> ", phases)}");
        }

        /// <summary>
        /// フェーズ必須設定
        /// </summary>
        /// <param name="phase">フェーズ</param>
        /// <param name="mandatory">必須フラグ</param>
        public void SetPhaseMandatory(TurnPhase phase, bool mandatory)
        {
            _phaseMandatory[phase] = mandatory;
            Debug.Log($"[{ManagerName}] Phase {phase} mandatory: {mandatory}");
        }

        /// <summary>
        /// ターンタイマー有効化/無効化
        /// </summary>
        /// <param name="enabled">有効フラグ</param>
        public void SetTurnTimerEnabled(bool enabled)
        {
            _enableTurnTimer = enabled;
            Debug.Log($"[{ManagerName}] Turn timer enabled: {enabled}");
        }

        /// <summary>
        /// フェーズシステム有効化/無効化
        /// </summary>
        /// <param name="enabled">有効フラグ</param>
        public void SetPhaseSystemEnabled(bool enabled)
        {
            _enablePhaseSystem = enabled;
            Debug.Log($"[{ManagerName}] Phase system enabled: {enabled}");
        }

        #endregion

        #region Helper Methods

        private string GetPreviousPlayerId()
        {
            var prevIndex = (_currentPlayerIndex - 1 + _playerOrder.Count) % _playerOrder.Count;
            return _playerOrder.Count > prevIndex ? _playerOrder[prevIndex] : "";
        }

        private TurnPhase GetPreviousPhase()
        {
            var prevIndex = (_currentPhaseIndex - 1 + _phaseOrder.Count) % _phaseOrder.Count;
            return _phaseOrder.Count > prevIndex ? _phaseOrder[prevIndex] : TurnPhase.None;
        }

        private void ClearTurnData()
        {
            _currentTurnNumber = 0;
            _currentPlayerId = "";
            _currentPhase = TurnPhase.None;
            _currentPlayerIndex = 0;
            _currentPhaseIndex = 0;
            
            _playerOrder.Clear();
            _playerTimeRemaining.Clear();
            _playerTurnStats.Clear();
            _currentTurnActions.Clear();
            _turnHistory.Clear();
        }

        #endregion

        #region Statistics

        /// <summary>
        /// プレイヤー統計取得
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>プレイヤー統計</returns>
        public TurnPlayerStatistics GetPlayerStatistics(string playerId)
        {
            return _playerTurnStats.GetValueOrDefault(playerId, new TurnPlayerStatistics { PlayerId = playerId });
        }

        /// <summary>
        /// 全プレイヤー統計取得
        /// </summary>
        /// <returns>全プレイヤー統計</returns>
        public Dictionary<string, TurnPlayerStatistics> GetAllPlayerStatistics()
        {
            return new Dictionary<string, TurnPlayerStatistics>(_playerTurnStats);
        }

        #endregion

        #region Debug

        /// <summary>
        /// デバッグ情報取得
        /// </summary>
        /// <returns>デバッグ情報</returns>
        public string GetDebugInfo()
        {
            return $"=== Turn Manager Debug Info ===\n" +
                   $"Current Turn: {_currentTurnNumber}\n" +
                   $"Current Player: {_currentPlayerId} (Index: {_currentPlayerIndex})\n" +
                   $"Current Phase: {_currentPhase}\n" +
                   $"Turn Time Remaining: {TurnTimeRemaining:F1}s\n" +
                   $"Phase Time Remaining: {PhaseTimeRemaining:F1}s\n" +
                   $"Actions This Turn: {_currentTurnActions.Count}\n" +
                   $"Player Order: {string.Join(", ", _playerOrder)}\n" +
                   $"Phase Order: {string.Join(", ", _phaseOrder)}\n" +
                   $"Timer Enabled: {_enableTurnTimer}\n" +
                   $"Phase System Enabled: {_enablePhaseSystem}";
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// ターンフェーズ
    /// </summary>
    public enum TurnPhase
    {
        None,
        Draw,      // ドローフェーズ
        Main,      // メインフェーズ
        Battle,    // バトルフェーズ
        End        // 終了フェーズ
    }

    /// <summary>
    /// ターン終了理由
    /// </summary>
    public enum TurnEndReason
    {
        Normal,     // 通常終了
        TimeLimit,  // 時間切れ
        Forfeit,    // 投了
        System      // システム強制終了
    }

    #endregion

    #region Data Classes

    /// <summary>
    /// ターンアクション
    /// </summary>
    [Serializable]
    public class TurnAction
    {
        public GameActionType ActionType;
        public string PlayerId;
        public int TurnNumber;
        public TurnPhase Phase;
        public float Timestamp;
        public Dictionary<string, object> ActionData;
    }

    /// <summary>
    /// ターンプレイヤー統計
    /// </summary>
    [Serializable]
    public class TurnPlayerStatistics
    {
        public string PlayerId;
        public int TurnsPlayed;
        public float TotalTimeUsed;
        public int ActionsExecuted;
        public int PhasesCompleted;
        public float AverageTimePerTurn => TurnsPlayed > 0 ? TotalTimeUsed / TurnsPlayed : 0f;
        public float AverageActionsPerTurn => TurnsPlayed > 0 ? (float)ActionsExecuted / TurnsPlayed : 0f;
    }

    #endregion

    #region Event Classes

    public class TurnStartedEvent
    {
        public int TurnNumber { get; set; }
        public string PlayerId { get; set; }
        public int PlayerIndex { get; set; }
    }

    public class TurnEndedEvent
    {
        public int TurnNumber { get; set; }
        public string PlayerId { get; set; }
        public float TurnDuration { get; set; }
        public int ActionsExecuted { get; set; }
    }

    public class TurnForcedEndEvent
    {
        public int TurnNumber { get; set; }
        public string PlayerId { get; set; }
        public TurnEndReason Reason { get; set; }
    }

    public class PhaseStartedEvent
    {
        public TurnPhase Phase { get; set; }
        public string PlayerId { get; set; }
        public int TurnNumber { get; set; }
    }

    public class PhaseEndedEvent
    {
        public TurnPhase Phase { get; set; }
        public string PlayerId { get; set; }
        public int TurnNumber { get; set; }
        public float PhaseDuration { get; set; }
    }

    public class PhaseSkippedEvent
    {
        public TurnPhase Phase { get; set; }
        public string PlayerId { get; set; }
        public int TurnNumber { get; set; }
    }

    public class TurnPhaseChangedEvent
    {
        public TurnPhase PreviousPhase { get; set; }
        public TurnPhase NewPhase { get; set; }
        public string PlayerId { get; set; }
    }

    public class TurnPlayerChangedEvent
    {
        public string PreviousPlayerId { get; set; }
        public string NewPlayerId { get; set; }
        public int TurnNumber { get; set; }
    }

    public class TurnTimerUpdateEvent
    {
        public string PlayerId { get; set; }
        public float TimeRemaining { get; set; }
        public float TotalTime { get; set; }
    }

    public class TurnActionExecutedEvent
    {
        public TurnAction Action { get; set; }
        public int TotalActionsThisTurn { get; set; }
    }

    public class PlayerActionExecutedEvent
    {
        public string PlayerId { get; set; }
        public GameActionType ActionType { get; set; }
        public Dictionary<string, object> ActionData { get; set; }
    }

    #endregion
}