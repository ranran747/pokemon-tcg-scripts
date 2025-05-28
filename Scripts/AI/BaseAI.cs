using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Game.Rules;

namespace PokemonTCG.Game.AI
{
    /// <summary>
    /// AI基底クラス
    /// 各デッキ専用AIの共通機能を提供
    /// </summary>
    public abstract class BaseAI : MonoBehaviour
    {
        #region Fields

        [Header("AI設定")]
        [SerializeField] protected string _aiName = "BaseAI";
        [SerializeField] protected float _thinkingDelay = 1f; // 思考時間演出
        [SerializeField] protected bool _enableDebugLog = true;
        [SerializeField] protected AIPersonality _personality = AIPersonality.Balanced;

        [Header("実行制御")]
        [SerializeField] protected bool _isActive = false;
        [SerializeField] protected bool _isThinking = false;
        [SerializeField] protected float _actionStartTime = 0f;

        [Header("ゲーム参照")]
        [SerializeField] protected string _playerId = "";
        [SerializeField] protected GameStateManager _gameStateManager;
        [SerializeField] protected TurnManager _turnManager;
        [SerializeField] protected ActionValidationSystem _validationSystem;

        [Header("AI統計")]
        [SerializeField] protected int _turnsPlayed = 0;
        [SerializeField] protected int _actionsExecuted = 0;
        [SerializeField] protected int _successfulActions = 0;
        [SerializeField] protected float _totalThinkingTime = 0f;

        // AI思考データ
        protected Dictionary<string, object> _memory = new Dictionary<string, object>();
        protected List<AIAction> _plannedActions = new List<AIAction>();
        protected Queue<AIAction> _actionQueue = new Queue<AIAction>();

        #endregion

        #region Properties

        /// <summary>AI名</summary>
        public string AIName => _aiName;

        /// <summary>プレイヤーID</summary>
        public string PlayerId => _playerId;

        /// <summary>アクティブ状態</summary>
        public bool IsActive => _isActive;

        /// <summary>思考中フラグ</summary>
        public bool IsThinking => _isThinking;

        /// <summary>思考時間</summary>
        public float ThinkingDelay => _thinkingDelay;

        /// <summary>AI統計</summary>
        public AIStatistics Statistics => new AIStatistics
        {
            AIName = _aiName,
            PlayerId = _playerId,
            TurnsPlayed = _turnsPlayed,
            ActionsExecuted = _actionsExecuted,
            SuccessfulActions = _successfulActions,
            TotalThinkingTime = _totalThinkingTime,
            SuccessRate = _actionsExecuted > 0 ? (float)_successfulActions / _actionsExecuted * 100f : 0f
        };

        #endregion

        #region Initialization

        /// <summary>
        /// AI初期化
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        public virtual void Initialize(string playerId)
        {
            _playerId = playerId;
            
            // システム参照取得
            _gameStateManager = ServiceLocator.Get<GameStateManager>();
            _turnManager = ServiceLocator.Get<TurnManager>();
            _validationSystem = ServiceLocator.Get<ActionValidationSystem>();
            
            // AI固有初期化
            OnAIInitialized();
            
            // イベント登録
            if (_turnManager != null)
            {
                TurnManager.OnTurnPlayerChanged += OnTurnPlayerChanged;
            }
            
            DebugLog($"AI {_aiName} initialized for player {playerId}");
        }

        /// <summary>
        /// AI破棄処理
        /// </summary>
        public virtual void Dispose()
        {
            // イベント解除
            if (_turnManager != null)
            {
                TurnManager.OnTurnPlayerChanged -= OnTurnPlayerChanged;
            }
            
            // データクリア
            _memory.Clear();
            _plannedActions.Clear();
            _actionQueue.Clear();
            
            OnAIDisposed();
            
            DebugLog($"AI {_aiName} disposed");
        }

        /// <summary>
        /// AI固有の初期化処理
        /// </summary>
        protected virtual void OnAIInitialized() { }

        /// <summary>
        /// AI固有の破棄処理
        /// </summary>
        protected virtual void OnAIDisposed() { }

        #endregion

        #region Main AI Loop

        /// <summary>
        /// AIターン実行（メイン関数）
        /// </summary>
        public void ExecuteTurn()
        {
            if (!_isActive || _isThinking) return;
            
            DebugLog($"Starting AI turn for {_aiName}");
            
            _turnsPlayed++;
            _actionStartTime = Time.time;
            _isThinking = true;
            
            // AI思考開始
            StartCoroutine(ThinkAndAct());
        }

        /// <summary>
        /// 思考と行動のコルーチン
        /// </summary>
        private System.Collections.IEnumerator ThinkAndAct()
        {
            // 思考時間演出
            yield return new WaitForSeconds(_thinkingDelay);
            
            AIGameState gameState = null;
            AIStrategy strategy = null;
            
            try
            {
                // 現在の状況分析
                gameState = AnalyzeGameState();
                
                // 戦略決定
                strategy = DecideStrategy(gameState);
                
                // アクション計画
                _plannedActions = PlanActions(strategy, gameState);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{_aiName}] AI planning error: {ex.Message}");
                _plannedActions = new List<AIAction>();
            }
                
            // アクション実行
            yield return StartCoroutine(ExecutePlannedActions());
            
            // ターン終了
            EndAITurn();
            
            _isThinking = false;
            _totalThinkingTime += Time.time - _actionStartTime;
        }

        /// <summary>
        /// 計画されたアクションを実行
        /// </summary>
        private System.Collections.IEnumerator ExecutePlannedActions()
        {
            foreach (var action in _plannedActions)
            {
                // アクション実行
                var result = ExecuteAIAction(action);
                
                if (result.Success)
                {
                    _successfulActions++;
                    DebugLog($"Action executed: {action.ActionType} - {action.Description}");
                }
                else
                {
                    DebugLog($"Action failed: {action.ActionType} - {result.ErrorMessage}");
                }
                
                _actionsExecuted++;
                
                // アクション間の間隔
                yield return new WaitForSeconds(0.2f);
            }
        }

        /// <summary>
        /// AIターン終了
        /// </summary>
        private void EndAITurn()
        {
            DebugLog($"AI turn ended for {_aiName}");
            
            // ターン終了アクション実行
            var endTurnAction = new AIAction
            {
                ActionType = GameActionType.EndTurn,
                Description = "Turn End",
                Priority = 0,
                Parameters = new Dictionary<string, object>()
            };
            
            ExecuteAIAction(endTurnAction);
        }

        #endregion

        #region Abstract Methods (継承クラスで実装)

        /// <summary>
        /// ゲーム状況分析
        /// </summary>
        /// <returns>ゲーム状況</returns>
        protected abstract AIGameState AnalyzeGameState();

        /// <summary>
        /// 戦略決定
        /// </summary>
        /// <param name="gameState">ゲーム状況</param>
        /// <returns>戦略</returns>
        protected abstract AIStrategy DecideStrategy(AIGameState gameState);

        /// <summary>
        /// アクション計画
        /// </summary>
        /// <param name="strategy">戦略</param>
        /// <param name="gameState">ゲーム状況</param>
        /// <returns>アクションプラン</returns>
        protected abstract List<AIAction> PlanActions(AIStrategy strategy, AIGameState gameState);

        #endregion

        #region Game State Access

        /// <summary>
        /// 自分のプレイヤー状態取得
        /// </summary>
        /// <returns>プレイヤー状態</returns>
        protected PlayerState GetMyPlayerState()
        {
            return _gameStateManager?.GetPlayerState(_playerId);
        }

        /// <summary>
        /// 相手のプレイヤー状態取得
        /// </summary>
        /// <returns>相手のプレイヤー状態</returns>
        protected PlayerState GetOpponentPlayerState()
        {
            return _gameStateManager?.GetOpponentState(_playerId);
        }

        /// <summary>
        /// 現在のゲーム状態取得
        /// </summary>
        /// <returns>ゲーム状態</returns>
        protected GameState GetCurrentGameState()
        {
            return _gameStateManager?.CurrentGameState;
        }

        /// <summary>
        /// 現在のルール取得
        /// </summary>
        /// <returns>ゲームルール</returns>
        protected GameRule GetActiveRule()
        {
            return _gameStateManager?.ActiveRule;
        }

        #endregion

        #region Action Execution

        /// <summary>
        /// AIアクション実行
        /// </summary>
        /// <param name="aiAction">AIアクション</param>
        /// <returns>実行結果</returns>
        protected ActionResult ExecuteAIAction(AIAction aiAction)
        {
            try
            {
                // GameActionに変換
                var gameAction = ConvertToGameAction(aiAction);
                
                // 検証
                if (_validationSystem != null)
                {
                    var validation = _validationSystem.ValidateAction(gameAction);
                    if (!validation.IsValid)
                    {
                        return ActionResult.CreateFailure($"Validation failed: {validation.ErrorMessage}");
                    }
                }
                
                // 実行
                var rule = GetActiveRule();
                if (rule != null)
                {
                    return rule.ExecuteAction(gameAction);
                }
                
                return ActionResult.CreateFailure("No active rule found");
            }
            catch (Exception ex)
            {
                return ActionResult.CreateFailure($"Execution error: {ex.Message}");
            }
        }

        /// <summary>
        /// AIActionをGameActionに変換
        /// </summary>
        /// <param name="aiAction">AIアクション</param>
        /// <returns>ゲームアクション</returns>
        protected virtual IGameAction ConvertToGameAction(AIAction aiAction)
        {
            return new GameAction
            {
                ActionId = Guid.NewGuid().ToString(),
                PlayerId = _playerId,
                ActionType = aiAction.ActionType,
                ExecutedAt = DateTime.Now,
                Parameters = new Dictionary<string, object>(aiAction.Parameters)
            };
        }

        #endregion

        #region Memory System

        /// <summary>
        /// 記憶に保存
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        protected void Remember(string key, object value)
        {
            _memory[key] = value;
        }

        /// <summary>
        /// 記憶から取得
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="key">キー</param>
        /// <param name="defaultValue">デフォルト値</param>
        /// <returns>記憶された値</returns>
        protected T Recall<T>(string key, T defaultValue = default(T))
        {
            if (_memory.TryGetValue(key, out var value) && value is T)
            {
                return (T)value;
            }
            return defaultValue;
        }

        /// <summary>
        /// 記憶を忘れる
        /// </summary>
        /// <param name="key">キー</param>
        protected void Forget(string key)
        {
            _memory.Remove(key);
        }

        /// <summary>
        /// すべての記憶をクリア
        /// </summary>
        protected void ForgetAll()
        {
            _memory.Clear();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// ターンプレイヤー変更イベント
        /// </summary>
        /// <param name="evt">イベント</param>
        private void OnTurnPlayerChanged(TurnPlayerChangedEvent evt)
        {
            _isActive = (evt.NewPlayerId == _playerId);
            
            if (_isActive)
            {
                // 自分のターン開始
                ExecuteTurn();
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// デバッグログ出力
        /// </summary>
        /// <param name="message">メッセージ</param>
        protected void DebugLog(string message)
        {
            if (_enableDebugLog)
            {
                Debug.Log($"[{_aiName}] {message}");
            }
        }

        /// <summary>
        /// ランダム選択
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="items">アイテムリスト</param>
        /// <returns>選択されたアイテム</returns>
        protected T RandomChoice<T>(List<T> items)
        {
            if (items == null || items.Count == 0) return default(T);
            return items[UnityEngine.Random.Range(0, items.Count)];
        }

        /// <summary>
        /// 重み付きランダム選択
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="items">アイテムと重みのペア</param>
        /// <returns>選択されたアイテム</returns>
        protected T WeightedRandomChoice<T>(List<(T item, float weight)> items)
        {
            if (items == null || items.Count == 0) return default(T);
            
            var totalWeight = items.Sum(x => x.weight);
            var randomValue = UnityEngine.Random.Range(0f, totalWeight);
            
            float currentWeight = 0f;
            foreach (var (item, weight) in items)
            {
                currentWeight += weight;
                if (randomValue <= currentWeight)
                {
                    return item;
                }
            }
            
            return items.Last().item;
        }

        #endregion

        #region Debug

        /// <summary>
        /// デバッグ情報取得
        /// </summary>
        /// <returns>デバッグ情報</returns>
        public virtual string GetDebugInfo()
        {
            var stats = Statistics;
            return $"=== AI Debug Info ===\n" +
                   $"Name: {_aiName}\n" +
                   $"Player ID: {_playerId}\n" +
                   $"Active: {_isActive}\n" +
                   $"Thinking: {_isThinking}\n" +
                   $"Turns Played: {stats.TurnsPlayed}\n" +
                   $"Actions Executed: {stats.ActionsExecuted}\n" +
                   $"Success Rate: {stats.SuccessRate:F1}%\n" +
                   $"Total Thinking Time: {stats.TotalThinkingTime:F2}s\n" +
                   $"Memory Items: {_memory.Count}\n" +
                   $"Planned Actions: {_plannedActions.Count}";
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// AI性格
    /// </summary>
    public enum AIPersonality
    {
        Aggressive,     // 攻撃的
        Defensive,      // 守備的
        Balanced,       // バランス型
        Strategic,      // 戦略的
        Random          // ランダム
    }

    #endregion

    #region Data Classes

    /// <summary>
    /// AIアクション
    /// </summary>
    [Serializable]
    public class AIAction
    {
        public GameActionType ActionType;
        public string Description;
        public int Priority;
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
        public float Confidence; // 実行の確信度
    }

    /// <summary>
    /// AI戦略
    /// </summary>
    [Serializable]
    public class AIStrategy
    {
        public string StrategyName;
        public AIPersonality Personality;
        public Dictionary<string, float> Priorities = new Dictionary<string, float>();
        public List<string> FocusTargets = new List<string>();
    }

    /// <summary>
    /// AIゲーム状況
    /// </summary>
    [Serializable]
    public class AIGameState
    {
        public int TurnNumber;
        public GamePhase CurrentPhase;
        public PlayerState MyState;
        public PlayerState OpponentState;
        public GameRule ActiveRule;
        public Dictionary<string, object> CustomData = new Dictionary<string, object>();
    }



    /// <summary>
    /// シンプルなGameAction実装
    /// </summary>
    public class GameAction : IGameAction
    {
        public string ActionId { get; set; }
        public string PlayerId { get; set; }
        public GameActionType ActionType { get; set; }
        public DateTime ExecutedAt { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    #endregion
}