using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Core.Data;
using PokemonTCG.Game.Rules;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.Game
{
    /// <summary>
    /// ゲーム状態管理マネージャー
    /// GameStateとPlayerStateを統合管理し、ゲーム全体の状態制御を行う
    /// Claude拡張での新機能追加やカスタムゲームモードにも対応
    /// </summary>
    public class GameStateManager : MonoBehaviourSingleton<GameStateManager>
    {
        #region Fields

        [Header("ゲーム状態")]
        [SerializeField] private GameState _currentGameState;
        [SerializeField] private GameRule _activeRule;
        [SerializeField] private bool _gameInProgress;

        [Header("状態変更管理")]
        [SerializeField] private List<GameStateSnapshot> _stateHistory = new List<GameStateSnapshot>();
        [SerializeField] private int _maxHistorySize = 100;
        [SerializeField] private bool _enableStateHistory = true;

        [Header("イベント管理")]
        [SerializeField] private Queue<StateChangeEvent> _pendingEvents = new Queue<StateChangeEvent>();
        [SerializeField] private bool _processingEvents = false;

        [Header("自動保存")]
        [SerializeField] private bool _enableAutoSave = true;
        [SerializeField] private float _autoSaveInterval = 60f; // 60秒間隔
        [SerializeField] private float _lastAutoSaveTime;

        // イベント
        public event Action<GameState> OnGameStateChanged;
        public event Action<PlayerState> OnPlayerStateChanged;
        public event Action<GamePhase, GamePhase> OnPhaseChanged;
        public event Action<string, string> OnTurnChanged;
        public event Action<string, VictoryReason> OnGameEnded;

        #endregion

        #region Properties

        /// <summary>現在のゲーム状態</summary>
        public GameState CurrentGameState => _currentGameState;

        /// <summary>アクティブなルール</summary>
        public GameRule ActiveRule => _activeRule;

        /// <summary>ゲーム進行中フラグ</summary>
        public bool GameInProgress => _gameInProgress;

        /// <summary>状態履歴</summary>
        public List<GameStateSnapshot> StateHistory => new List<GameStateSnapshot>(_stateHistory);

        /// <summary>初期化順序</summary>
        public override int InitializationOrder => 150;

        /// <summary>現在のプレイヤー</summary>
        public PlayerState CurrentPlayer => _currentGameState?.CurrentPlayer;

        /// <summary>全プレイヤー</summary>
        public List<PlayerState> AllPlayers => _currentGameState?.Players ?? new List<PlayerState>();

        /// <summary>マネージャー名</summary>
        protected string ManagerName => "GameStateManager";

        #endregion

        #region Initialization

        protected override void OnInitialize()
        {
            // サービス登録
            ServiceLocator.Register<GameStateManager>(this);
            
            // イベント登録
            EventBus.On<GameStartRequestEvent>(OnGameStartRequested);
            EventBus.On<GameEndRequestEvent>(OnGameEndRequested);
            EventBus.On<PlayerActionEvent>(OnPlayerActionExecuted);
            EventBus.On<StateChangeRequestEvent>(OnStateChangeRequested);
            
            Debug.Log($"[{ManagerName}] GameStateManager initialized");
        }

        protected override void OnDispose()
        {
            // イベント解除
            EventBus.Off<GameStartRequestEvent>(OnGameStartRequested);
            EventBus.Off<GameEndRequestEvent>(OnGameEndRequested);
            EventBus.Off<PlayerActionEvent>(OnPlayerActionExecuted);
            EventBus.Off<StateChangeRequestEvent>(OnStateChangeRequested);
            
            // イベントクリア
            OnGameStateChanged = null;
            OnPlayerStateChanged = null;
            OnPhaseChanged = null;
            OnTurnChanged = null;
            OnGameEnded = null;
            
            // データクリア
            _stateHistory.Clear();
            _pendingEvents.Clear();
            
            Debug.Log($"[{ManagerName}] GameStateManager disposed");
        }

        #endregion

        #region Game Management

        /// <summary>
        /// 新しいゲームを開始
        /// </summary>
        /// <param name="rule">適用するルール</param>
        /// <param name="playerIds">プレイヤーIDリスト</param>
        /// <returns>成功した場合true</returns>
        public bool StartNewGame(GameRule rule, List<string> playerIds)
        {
            if (_gameInProgress)
            {
                Debug.LogWarning($"[{ManagerName}] Cannot start new game while another game is in progress");
                return false;
            }

            if (rule == null || playerIds == null || playerIds.Count < 2)
            {
                Debug.LogError($"[{ManagerName}] Invalid parameters for starting new game");
                return false;
            }

            try
            {
                // 新しいゲーム状態を作成
                _currentGameState = new GameState(rule, playerIds);
                _activeRule = rule;
                _gameInProgress = true;
                
                // 履歴をクリア
                if (_enableStateHistory)
                {
                    _stateHistory.Clear();
                    CreateStateSnapshot("Game Started");
                }
                
                // ルールを初期化
                rule.InitializeWithRuleData(rule.RuleData);
                rule.StartGame(playerIds);
                
                // 自動保存タイマーリセット
                _lastAutoSaveTime = Time.time;
                
                // イベント通知
                OnGameStateChanged?.Invoke(_currentGameState);
                
                EventBus.Emit(new GameStartedEvent
                {
                    PlayerIds = playerIds,
                    RuleType = rule.RuleName
                });
                
                Debug.Log($"[{ManagerName}] New game started with rule: {rule.RuleName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Failed to start new game: {ex.Message}");
                _gameInProgress = false;
                return false;
            }
        }

        /// <summary>
        /// ゲームを終了
        /// </summary>
        /// <param name="winnerId">勝利者ID</param>
        /// <param name="reason">勝利理由</param>
        public void EndGame(string winnerId, VictoryReason reason)
        {
            if (!_gameInProgress)
            {
                Debug.LogWarning($"[{ManagerName}] No game in progress to end");
                return;
            }

            try
            {
                // ゲーム状態を終了
                _currentGameState.EndGame(winnerId, reason);
                _gameInProgress = false;
                
                // 最終スナップショット作成
                if (_enableStateHistory)
                {
                    CreateStateSnapshot($"Game Ended - Winner: {winnerId}");
                }
                
                // 最終自動保存
                if (_enableAutoSave)
                {
                    AutoSaveGameState();
                }
                
                // イベント通知
                OnGameEnded?.Invoke(winnerId, reason);
                
                EventBus.Emit(new GameEndedEvent
                {
                    WinnerId = winnerId,
                    Reason = reason
                });
                
                Debug.Log($"[{ManagerName}] Game ended. Winner: {winnerId}, Reason: {reason}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Error ending game: {ex.Message}");
            }
        }

        /// <summary>
        /// ゲームを一時停止
        /// </summary>
        public void PauseGame()
        {
            if (!_gameInProgress)
            {
                Debug.LogWarning($"[{ManagerName}] No game in progress to pause");
                return;
            }

            _currentGameState.SetGameVariable("IsPaused", true);
            CreateStateSnapshot("Game Paused");
            
            EventBus.Emit(new GamePausedEvent
            {
                PausedAt = DateTime.Now
            });
            
            Debug.Log($"[{ManagerName}] Game paused");
        }

        /// <summary>
        /// ゲームを再開
        /// </summary>
        public void ResumeGame()
        {
            if (!_gameInProgress)
            {
                Debug.LogWarning($"[{ManagerName}] No game in progress to resume");
                return;
            }

            _currentGameState.SetGameVariable("IsPaused", false);
            CreateStateSnapshot("Game Resumed");
            
            EventBus.Emit(new GameResumedEvent
            {
                ResumedAt = DateTime.Now
            });
            
            Debug.Log($"[{ManagerName}] Game resumed");
        }

        #endregion

        #region State Management

        /// <summary>
        /// フェーズを変更
        /// </summary>
        /// <param name="newPhase">新しいフェーズ</param>
        /// <param name="playerId">プレイヤーID</param>
        public void ChangePhase(GamePhase newPhase, string playerId = null)
        {
            if (!_gameInProgress)
            {
                Debug.LogWarning($"[{ManagerName}] Cannot change phase when game is not in progress");
                return;
            }

            var oldPhase = _currentGameState.CurrentPhase;
            _currentGameState.SetPhase(newPhase);
            
            // 状態履歴記録
            if (_enableStateHistory)
            {
                CreateStateSnapshot($"Phase Changed: {oldPhase} -> {newPhase}");
            }
            
            // イベント通知
            OnPhaseChanged?.Invoke(oldPhase, newPhase);
            
            Debug.Log($"[{ManagerName}] Phase changed: {oldPhase} -> {newPhase}");
        }

        /// <summary>
        /// ターンを進める
        /// </summary>
        /// <param name="nextPlayerId">次のプレイヤーID</param>
        public void AdvanceTurn(string nextPlayerId = null)
        {
            if (!_gameInProgress)
            {
                Debug.LogWarning($"[{ManagerName}] Cannot advance turn when game is not in progress");
                return;
            }

            var previousPlayerId = _currentGameState.CurrentPlayerId;
            
            // 現在のプレイヤーのターン終了処理
            var currentPlayer = _currentGameState.GetPlayer(previousPlayerId);
            currentPlayer?.EndTurn();
            
            // ターンを進める
            _currentGameState.AdvanceTurn(nextPlayerId);
            
            // 新しいプレイヤーのターン開始処理
            var newCurrentPlayer = _currentGameState.CurrentPlayer;
            newCurrentPlayer?.StartTurn();
            
            // 状態履歴記録
            if (_enableStateHistory)
            {
                CreateStateSnapshot($"Turn Advanced: {previousPlayerId} -> {_currentGameState.CurrentPlayerId}");
            }
            
            // イベント通知
            OnTurnChanged?.Invoke(previousPlayerId, _currentGameState.CurrentPlayerId);
            OnPlayerStateChanged?.Invoke(newCurrentPlayer);
            
            Debug.Log($"[{ManagerName}] Turn advanced to {_currentGameState.CurrentTurn}. Current player: {_currentGameState.CurrentPlayerId}");
        }

        /// <summary>
        /// プレイヤー状態を更新
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <param name="updateAction">更新アクション</param>
        public void UpdatePlayerState(string playerId, Action<PlayerState> updateAction)
        {
            if (!_gameInProgress)
            {
                Debug.LogWarning($"[{ManagerName}] Cannot update player state when game is not in progress");
                return;
            }

            var player = _currentGameState.GetPlayer(playerId);
            if (player == null)
            {
                Debug.LogWarning($"[{ManagerName}] Player not found: {playerId}");
                return;
            }

            try
            {
                updateAction(player);
                
                // イベント通知
                OnPlayerStateChanged?.Invoke(player);
                
                Debug.Log($"[{ManagerName}] Player state updated: {playerId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Failed to update player state: {ex.Message}");
            }
        }

        #endregion

        #region State History

        /// <summary>
        /// 状態スナップショットを作成
        /// </summary>
        /// <param name="description">説明</param>
        private void CreateStateSnapshot(string description)
        {
            if (!_enableStateHistory || _currentGameState == null)
                return;

            try
            {
                var snapshot = new GameStateSnapshot
                {
                    Timestamp = DateTime.Now,
                    Description = description,
                    GameState = CloneGameState(_currentGameState),
                    Turn = _currentGameState.CurrentTurn,
                    Phase = _currentGameState.CurrentPhase,
                    CurrentPlayerId = _currentGameState.CurrentPlayerId
                };

                _stateHistory.Add(snapshot);
                
                // 履歴サイズ制限
                while (_stateHistory.Count > _maxHistorySize)
                {
                    _stateHistory.RemoveAt(0);
                }
                
                Debug.Log($"[{ManagerName}] State snapshot created: {description}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Failed to create state snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定時点の状態に復元
        /// </summary>
        /// <param name="snapshotIndex">スナップショットインデックス</param>
        /// <returns>成功した場合true</returns>
        public bool RestoreFromSnapshot(int snapshotIndex)
        {
            if (!_enableStateHistory || snapshotIndex < 0 || snapshotIndex >= _stateHistory.Count)
            {
                Debug.LogWarning($"[{ManagerName}] Invalid snapshot index: {snapshotIndex}");
                return false;
            }

            try
            {
                var snapshot = _stateHistory[snapshotIndex];
                _currentGameState = CloneGameState(snapshot.GameState);
                
                // イベント通知
                OnGameStateChanged?.Invoke(_currentGameState);
                
                CreateStateSnapshot($"Restored from snapshot: {snapshot.Description}");
                
                Debug.Log($"[{ManagerName}] State restored from snapshot: {snapshot.Description}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Failed to restore from snapshot: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// プレイヤー状態を取得
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>プレイヤー状態</returns>
        public PlayerState GetPlayerState(string playerId)
        {
            return _currentGameState?.GetPlayer(playerId);
        }

        /// <summary>
        /// 現在のプレイヤー状態を取得
        /// </summary>
        /// <returns>現在のプレイヤー状態</returns>
        public PlayerState GetCurrentPlayerState()
        {
            return _currentGameState?.CurrentPlayer;
        }

        /// <summary>
        /// 対戦相手の状態を取得
        /// </summary>
        /// <param name="playerId">基準プレイヤーID</param>
        /// <returns>対戦相手の状態</returns>
        public PlayerState GetOpponentState(string playerId)
        {
            return _currentGameState?.GetOpponent(playerId);
        }

        /// <summary>
        /// ゲーム状態をクエリ
        /// </summary>
        /// <param name="query">クエリ関数</param>
        /// <returns>クエリ結果</returns>
        public T QueryGameState<T>(Func<GameState, T> query)
        {
            return _currentGameState != null ? query(_currentGameState) : default(T);
        }

        /// <summary>
        /// プレイヤー状態をクエリ
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <param name="query">クエリ関数</param>
        /// <returns>クエリ結果</returns>
        public T QueryPlayerState<T>(string playerId, Func<PlayerState, T> query)
        {
            var player = GetPlayerState(playerId);
            return player != null ? query(player) : default(T);
        }

        #endregion

        #region Event Handlers

        private void OnGameStartRequested(GameStartRequestEvent evt)
        {
            StartNewGame(evt.Rule, evt.PlayerIds);
        }

        private void OnGameEndRequested(GameEndRequestEvent evt)
        {
            EndGame(evt.WinnerId, evt.Reason);
        }

        private void OnPlayerActionExecuted(PlayerActionEvent evt)
        {
            if (_enableStateHistory)
            {
                CreateStateSnapshot($"Player Action: {evt.ActionType} by {evt.PlayerId}");
            }
        }

        private void OnStateChangeRequested(StateChangeRequestEvent evt)
        {
            // 状態変更要求の処理
            EnqueueStateChangeEvent(evt);
        }

        #endregion

        #region Event Processing

        /// <summary>
        /// 状態変更イベントをキューに追加
        /// </summary>
        /// <param name="stateChangeEvent">状態変更イベント</param>
        private void EnqueueStateChangeEvent(StateChangeRequestEvent stateChangeEvent)
        {
            var evt = new StateChangeEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.Now,
                ChangeType = stateChangeEvent.ChangeType,
                PlayerId = stateChangeEvent.PlayerId,
                Parameters = stateChangeEvent.Parameters
            };

            _pendingEvents.Enqueue(evt);
        }

        /// <summary>
        /// 保留中のイベントを処理
        /// </summary>
        private void ProcessPendingEvents()
        {
            if (_processingEvents || _pendingEvents.Count == 0)
                return;

            _processingEvents = true;

            try
            {
                while (_pendingEvents.Count > 0)
                {
                    var evt = _pendingEvents.Dequeue();
                    ProcessStateChangeEvent(evt);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Error processing events: {ex.Message}");
            }
            finally
            {
                _processingEvents = false;
            }
        }

        /// <summary>
        /// 状態変更イベントを処理
        /// </summary>
        /// <param name="evt">状態変更イベント</param>
        private void ProcessStateChangeEvent(StateChangeEvent evt)
        {
            switch (evt.ChangeType)
            {
                case StateChangeType.PhaseChange:
                    if (evt.Parameters.TryGetValue("NewPhase", out var phaseObj) && phaseObj is GamePhase newPhase)
                    {
                        ChangePhase(newPhase, evt.PlayerId);
                    }
                    break;

                case StateChangeType.TurnAdvance:
                    AdvanceTurn(evt.Parameters.GetValueOrDefault("NextPlayerId", "").ToString());
                    break;

                case StateChangeType.PlayerUpdate:
                    // プレイヤー状態の更新処理
                    break;

                default:
                    Debug.LogWarning($"[{ManagerName}] Unknown state change type: {evt.ChangeType}");
                    break;
            }
        }

        #endregion

        #region Auto Save

        /// <summary>
        /// 自動保存の処理
        /// </summary>
        private void ProcessAutoSave()
        {
            if (!_enableAutoSave || !_gameInProgress || _currentGameState == null)
                return;

            if (Time.time - _lastAutoSaveTime >= _autoSaveInterval)
            {
                AutoSaveGameState();
                _lastAutoSaveTime = Time.time;
            }
        }

        /// <summary>
        /// ゲーム状態を自動保存
        /// </summary>
        private void AutoSaveGameState()
        {
            try
            {
                var saveData = _currentGameState.Serialize();
                // TODO: 実際の保存処理を実装
                Debug.Log($"[{ManagerName}] Game state auto-saved");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Auto-save failed: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// ゲーム状態を複製
        /// </summary>
        /// <param name="gameState">複製元</param>
        /// <returns>複製されたゲーム状態</returns>
        private GameState CloneGameState(GameState gameState)
        {
            // 簡易的な複製実装
            // 実際のプロジェクトでは、より効率的なシリアライゼーション手法を使用
            var serialized = gameState.Serialize();
            var cloned = new GameState();
            cloned.Deserialize(serialized);
            return cloned;
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void Update()
        {
            if (_gameInProgress)
            {
                ProcessPendingEvents();
                ProcessAutoSave();
            }
        }

        #endregion

        #region Debug

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        /// <returns>デバッグ情報</returns>
        public string GetDebugInfo()
        {
            return $"=== GameStateManager Debug Info ===\n" +
                   $"Game In Progress: {_gameInProgress}\n" +
                   $"Active Rule: {(_activeRule?.RuleName ?? "None")}\n" +
                   $"Current Game State: {(_currentGameState?.GameId ?? "None")}\n" +
                   $"State History Count: {_stateHistory.Count}\n" +
                   $"Pending Events: {_pendingEvents.Count}\n" +
                   $"Auto Save Enabled: {_enableAutoSave}\n" +
                   $"Last Auto Save: {_lastAutoSaveTime:F2}s ago\n" +
                   (_currentGameState != null ? _currentGameState.GetDebugInfo() : "No active game state");
        }

        [ContextMenu("Show Debug Info")]
        private void ShowDebugInfo()
        {
            Debug.Log(GetDebugInfo());
        }

        #endregion
    }

    #region Support Classes

    /// <summary>
    /// ゲーム状態スナップショット
    /// </summary>
    [System.Serializable]
    public class GameStateSnapshot
    {
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public GameState GameState { get; set; }
        public int Turn { get; set; }
        public GamePhase Phase { get; set; }
        public string CurrentPlayerId { get; set; }
    }

    /// <summary>
    /// 状態変更イベント
    /// </summary>
    [System.Serializable]
    public class StateChangeEvent
    {
        public string EventId { get; set; }
        public DateTime Timestamp { get; set; }
        public StateChangeType ChangeType { get; set; }
        public string PlayerId { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 状態変更タイプ
    /// </summary>
    public enum StateChangeType
    {
        PhaseChange,
        TurnAdvance,
        PlayerUpdate,
        GameEnd,
        Custom
    }

    #endregion

    #region Event Classes

    public class GameStartRequestEvent
    {
        public GameRule Rule { get; set; }
        public List<string> PlayerIds { get; set; }
    }

    public class GameEndRequestEvent
    {
        public string WinnerId { get; set; }
        public VictoryReason Reason { get; set; }
    }

    public class PlayerActionEvent
    {
        public string PlayerId { get; set; }
        public PokemonTCG.Core.Data.GameActionType ActionType { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class StateChangeRequestEvent
    {
        public StateChangeType ChangeType { get; set; }
        public string PlayerId { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class GameStartedEvent
    {
        public List<string> PlayerIds { get; set; }
        public string RuleType { get; set; }
    }

    public class GameEndedEvent
    {
        public string WinnerId { get; set; }
        public VictoryReason Reason { get; set; }
    }

    public class GamePausedEvent
    {
        public DateTime PausedAt { get; set; }
    }

    public class GameResumedEvent
    {
        public DateTime ResumedAt { get; set; }
    }

    #endregion
}