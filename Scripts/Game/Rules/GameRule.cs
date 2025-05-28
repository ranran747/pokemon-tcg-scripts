using System;
using System.Collections.Generic;
using UnityEngine;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Core.Data;

namespace PokemonTCG.Game.Rules
{
    /// <summary>
    /// ゲームルールの基底クラス
    /// BaseRuleDataを基に実際のゲームロジックを管理
    /// Claude拡張での新ルール追加を容易にする抽象基盤
    /// </summary>
    public abstract class GameRule : MonoBehaviour, IManager
    {
        #region Fields

        [Header("ルールデータ")]
        [SerializeField] protected BaseRuleData _ruleData;
        
        [Header("ゲーム状態")]
        [SerializeField] protected GamePhase _currentPhase = GamePhase.Setup;
        [SerializeField] protected int _currentTurn = 0;
        [SerializeField] protected string _currentPlayerId = "";
        
        [Header("勝利条件")]
        [SerializeField] protected bool _gameEnded = false;
        [SerializeField] protected string _winnerId = "";
        [SerializeField] protected VictoryReason _victoryReason = VictoryReason.None;

        // イベント
        public event Action<GamePhase, GamePhase> OnPhaseChanged;
        public event Action<string> OnTurnStarted;
        public event Action<string> OnTurnEnded;
        public event Action<string, VictoryReason> OnGameEnded;
        public event Action<IGameAction> OnActionExecuted;
        public event Action<IGameAction> OnActionBlocked;

        // 内部管理
        protected readonly List<IGameAction> _actionHistory = new List<IGameAction>();
        protected readonly Dictionary<string, object> _gameVariables = new Dictionary<string, object>();

        #endregion

        #region IManager Implementation

        /// <summary>初期化済みフラグ</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>初期化順序</summary>
        public virtual int InitializationOrder => 200; // Game Logic層

        /// <summary>
        /// IManager実装 - 初期化
        /// </summary>
        public virtual void Initialize()
        {
            if (IsInitialized) return;
            OnInitialize();
        }

        /// <summary>
        /// IManager実装 - 破棄
        /// </summary>
        public virtual void Dispose()
        {
            OnDispose();
        }

        /// <summary>
        /// IManager実装 - 更新（必要に応じてオーバーライド）
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// IManager実装 - 固定更新（必要に応じてオーバーライド）
        /// </summary>
        public virtual void FixedUpdate() { }

        #endregion

        #region Properties

        /// <summary>マネージャー名</summary>
        public virtual string ManagerName => GetType().Name;

        /// <summary>ルールデータ</summary>
        public BaseRuleData RuleData => _ruleData;

        /// <summary>現在のゲームフェーズ</summary>
        public GamePhase CurrentPhase => _currentPhase;

        /// <summary>現在のターン数</summary>
        public int CurrentTurn => _currentTurn;

        /// <summary>現在のプレイヤーID</summary>
        public string CurrentPlayerId => _currentPlayerId;

        /// <summary>ゲーム終了フラグ</summary>
        public bool GameEnded => _gameEnded;

        /// <summary>勝利者ID</summary>
        public string WinnerId => _winnerId;

        /// <summary>勝利理由</summary>
        public VictoryReason VictoryReason => _victoryReason;

        /// <summary>アクション履歴</summary>
        public List<IGameAction> ActionHistory => new List<IGameAction>(_actionHistory);

        /// <summary>ルール名（表示用）</summary>
        public virtual string RuleName => _ruleData?.RuleName ?? "Unknown Rule";

        /// <summary>ゲーム変数（拡張用）</summary>
        public Dictionary<string, object> GameVariables => _gameVariables;

        #endregion

        #region Initialization

        /// <summary>
        /// ルールデータを設定して初期化
        /// </summary>
        /// <param name="ruleData">ルールデータ</param>
        public virtual void InitializeWithRuleData(BaseRuleData ruleData)
        {
            _ruleData = ruleData;
            
            if (!IsInitialized)
            {
                Initialize();
            }
            
            SetupGameRules();
            Debug.Log($"[{ManagerName}] Initialized with rule data: {ruleData.RuleName}");
        }

        /// <summary>
        /// 初期化処理の実装
        /// </summary>
        protected virtual void OnInitialize()
        {
            // 基本初期化処理
            ResetGameState();
            
            // 拡張初期化
            OnRuleInitialized();
            
            IsInitialized = true;
            Debug.Log($"[{ManagerName}] Rule engine initialized");
        }

        /// <summary>
        /// 破棄処理の実装
        /// </summary>
        protected virtual void OnDispose()
        {
            // イベントクリア
            OnPhaseChanged = null;
            OnTurnStarted = null;
            OnTurnEnded = null;
            OnGameEnded = null;
            OnActionExecuted = null;
            OnActionBlocked = null;
            
            // データクリア
            _actionHistory.Clear();
            _gameVariables.Clear();
            
            IsInitialized = false;
            Debug.Log($"[{ManagerName}] Rule engine disposed");
        }

        /// <summary>
        /// ゲームルールの具体的なセットアップ（継承クラスで実装）
        /// </summary>
        protected abstract void SetupGameRules();

        /// <summary>
        /// ルール初期化後の処理（継承クラスでオーバーライド可能）
        /// </summary>
        protected virtual void OnRuleInitialized() { }

        #endregion

        #region Game Flow Control

        /// <summary>
        /// ゲームを開始
        /// </summary>
        /// <param name="playerIds">プレイヤーIDリスト</param>
        public virtual void StartGame(List<string> playerIds)
        {
            if (playerIds == null || playerIds.Count < 2)
            {
                Debug.LogError($"[{ManagerName}] Cannot start game with less than 2 players");
                return;
            }

            ResetGameState();
            SetPhase(GamePhase.Setup);
            
            OnGameStarted(playerIds);
            
            Debug.Log($"[{ManagerName}] Game started with {playerIds.Count} players");
        }

        /// <summary>
        /// ゲームを終了
        /// </summary>
        /// <param name="winnerId">勝利者ID</param>
        /// <param name="reason">勝利理由</param>
        public virtual void EndGame(string winnerId, VictoryReason reason)
        {
            if (_gameEnded) return;

            _gameEnded = true;
            _winnerId = winnerId;
            _victoryReason = reason;
            
            SetPhase(GamePhase.GameEnd);
            
            OnGameEnded?.Invoke(winnerId, reason);
            OnGameFinished(winnerId, reason);
            
            Debug.Log($"[{ManagerName}] Game ended. Winner: {winnerId}, Reason: {reason}");
        }

        /// <summary>
        /// フェーズを変更
        /// </summary>
        /// <param name="newPhase">新しいフェーズ</param>
        protected virtual void SetPhase(GamePhase newPhase)
        {
            var oldPhase = _currentPhase;
            _currentPhase = newPhase;
            
            OnPhaseChangedInternal(oldPhase, newPhase);
            OnPhaseChanged?.Invoke(oldPhase, newPhase);
            
            Debug.Log($"[{ManagerName}] Phase changed: {oldPhase} -> {newPhase}");
        }

        /// <summary>
        /// ターンを開始
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        public virtual void StartTurn(string playerId)
        {
            _currentPlayerId = playerId;
            _currentTurn++;
            
            SetPhase(GamePhase.TurnStart);
            
            OnTurnStarted?.Invoke(playerId);
            OnTurnStartedInternal(playerId);
            
            Debug.Log($"[{ManagerName}] Turn {_currentTurn} started for player: {playerId}");
        }

        /// <summary>
        /// ターンを終了
        /// </summary>
        public virtual void EndTurn()
        {
            OnTurnEndedInternal(_currentPlayerId);
            OnTurnEnded?.Invoke(_currentPlayerId);
            
            SetPhase(GamePhase.TurnEnd);
            
            Debug.Log($"[{ManagerName}] Turn ended for player: {_currentPlayerId}");
        }

        #endregion

        #region Action Validation and Execution

        /// <summary>
        /// アクションの実行可能性をチェック
        /// </summary>
        /// <param name="action">アクション</param>
        /// <returns>実行可能な場合のValidationResult</returns>
        public virtual ValidationResult ValidateAction(IGameAction action)
        {
            if (action == null)
            {
                return ValidationResult.CreateError("Action is null");
            }

            if (_gameEnded)
            {
                return ValidationResult.CreateError("Game has already ended");
            }

            // 基本チェック
            var basicValidation = ValidateBasicAction(action);
            if (!basicValidation.IsValid)
            {
                return basicValidation;
            }

            // フェーズ固有チェック
            var phaseValidation = ValidateActionForPhase(action, _currentPhase);
            if (!phaseValidation.IsValid)
            {
                return phaseValidation;
            }

            // ルール固有チェック
            return ValidateRuleSpecificAction(action);
        }

        /// <summary>
        /// アクションを実行
        /// </summary>
        /// <param name="action">アクション</param>
        /// <returns>実行結果</returns>
        public virtual ActionResult ExecuteAction(IGameAction action)
        {
            // バリデーション
            var validation = ValidateAction(action);
            if (!validation.IsValid)
            {
                OnActionBlocked?.Invoke(action);
                return ActionResult.CreateFailure(validation.ErrorMessage);
            }

            try
            {
                // アクション実行
                var result = ExecuteActionInternal(action);
                
                if (result.Success)
                {
                    // 履歴に追加
                    _actionHistory.Add(action);
                    
                    // 事後処理
                    OnActionExecutedInternal(action);
                    OnActionExecuted?.Invoke(action);
                    
                    // 勝利条件チェック
                    CheckVictoryConditions();
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Action execution failed: {ex.Message}");
                return ActionResult.CreateFailure($"Execution error: {ex.Message}");
            }
        }

        #endregion

        #region Abstract Methods (継承クラスで実装)

        /// <summary>
        /// ゲーム開始時の処理
        /// </summary>
        /// <param name="playerIds">プレイヤーIDリスト</param>
        protected abstract void OnGameStarted(List<string> playerIds);

        /// <summary>
        /// ゲーム終了時の処理
        /// </summary>
        /// <param name="winnerId">勝利者ID</param>
        /// <param name="reason">勝利理由</param>
        protected abstract void OnGameFinished(string winnerId, VictoryReason reason);

        /// <summary>
        /// ルール固有のアクション検証
        /// </summary>
        /// <param name="action">アクション</param>
        /// <returns>検証結果</returns>
        protected abstract ValidationResult ValidateRuleSpecificAction(IGameAction action);

        /// <summary>
        /// アクションの実際の実行処理
        /// </summary>
        /// <param name="action">アクション</param>
        /// <returns>実行結果</returns>
        protected abstract ActionResult ExecuteActionInternal(IGameAction action);

        /// <summary>
        /// 勝利条件のチェック
        /// </summary>
        protected abstract void CheckVictoryConditions();

        #endregion

        #region Virtual Methods (継承クラスでオーバーライド可能)

        /// <summary>
        /// 基本アクション検証
        /// </summary>
        /// <param name="action">アクション</param>
        /// <returns>検証結果</returns>
        protected virtual ValidationResult ValidateBasicAction(IGameAction action)
        {
            // プレイヤーチェック
            if (action.PlayerId != _currentPlayerId)
            {
                return ValidationResult.CreateError("Not current player's turn");
            }

            return ValidationResult.CreateSuccess();
        }

        /// <summary>
        /// フェーズ固有のアクション検証
        /// </summary>
        /// <param name="action">アクション</param>
        /// <param name="phase">フェーズ</param>
        /// <returns>検証結果</returns>
        protected virtual ValidationResult ValidateActionForPhase(IGameAction action, GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Setup:
                    return ValidateSetupPhaseAction(action);
                case GamePhase.TurnStart:
                case GamePhase.MainPhase:
                    return ValidateMainPhaseAction(action);
                case GamePhase.TurnEnd:
                    return ValidateEndPhaseAction(action);
                default:
                    return ValidationResult.CreateError($"Actions not allowed in {phase} phase");
            }
        }

        /// <summary>
        /// フェーズ変更時の内部処理
        /// </summary>
        /// <param name="oldPhase">旧フェーズ</param>
        /// <param name="newPhase">新フェーズ</param>
        protected virtual void OnPhaseChangedInternal(GamePhase oldPhase, GamePhase newPhase) { }

        /// <summary>
        /// ターン開始時の内部処理
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        protected virtual void OnTurnStartedInternal(string playerId) { }

        /// <summary>
        /// ターン終了時の内部処理
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        protected virtual void OnTurnEndedInternal(string playerId) { }

        /// <summary>
        /// アクション実行後の内部処理
        /// </summary>
        /// <param name="action">実行されたアクション</param>
        protected virtual void OnActionExecutedInternal(IGameAction action) { }

        /// <summary>
        /// セットアップフェーズのアクション検証
        /// </summary>
        /// <param name="action">アクション</param>
        /// <returns>検証結果</returns>
        protected virtual ValidationResult ValidateSetupPhaseAction(IGameAction action)
        {
            return ValidationResult.CreateSuccess();
        }

        /// <summary>
        /// メインフェーズのアクション検証
        /// </summary>
        /// <param name="action">アクション</param>
        /// <returns>検証結果</returns>
        protected virtual ValidationResult ValidateMainPhaseAction(IGameAction action)
        {
            return ValidationResult.CreateSuccess();
        }

        /// <summary>
        /// 終了フェーズのアクション検証
        /// </summary>
        /// <param name="action">アクション</param>
        /// <returns>検証結果</returns>
        protected virtual ValidationResult ValidateEndPhaseAction(IGameAction action)
        {
            return ValidationResult.CreateSuccess();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// ゲーム状態をリセット
        /// </summary>
        protected virtual void ResetGameState()
        {
            _currentPhase = GamePhase.Setup;
            _currentTurn = 0;
            _currentPlayerId = "";
            _gameEnded = false;
            _winnerId = "";
            _victoryReason = VictoryReason.None;
            _actionHistory.Clear();
            _gameVariables.Clear();
        }

        /// <summary>
        /// ゲーム変数を設定
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        protected void SetGameVariable(string key, object value)
        {
            _gameVariables[key] = value;
        }

        /// <summary>
        /// ゲーム変数を取得
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="key">キー</param>
        /// <param name="defaultValue">デフォルト値</param>
        /// <returns>値</returns>
        protected T GetGameVariable<T>(string key, T defaultValue = default(T))
        {
            if (_gameVariables.TryGetValue(key, out var value) && value is T)
            {
                return (T)value;
            }
            return defaultValue;
        }

        /// <summary>
        /// 指定タイプのアクション履歴を取得
        /// </summary>
        /// <typeparam name="T">アクション型</typeparam>
        /// <returns>アクションリスト</returns>
        protected List<T> GetActionsOfType<T>() where T : IGameAction
        {
            var result = new List<T>();
            foreach (var action in _actionHistory)
            {
                if (action is T typedAction)
                {
                    result.Add(typedAction);
                }
            }
            return result;
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            // Unity Awake処理
        }

        protected virtual void Start()
        {
            // 自動初期化（必要に応じて）
            if (!IsInitialized)
            {
                Initialize();
            }
        }

        protected virtual void OnDestroy()
        {
            // Unity破棄時の処理
            Dispose();
        }

        #endregion

        #region Debug

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        /// <returns>デバッグ情報</returns>
        public virtual string GetDebugInfo()
        {
            return $"Rule: {RuleName}\n" +
                   $"Phase: {_currentPhase}\n" +
                   $"Turn: {_currentTurn}\n" +
                   $"Current Player: {_currentPlayerId}\n" +
                   $"Game Ended: {_gameEnded}\n" +
                   $"Winner: {_winnerId}\n" +
                   $"Victory Reason: {_victoryReason}\n" +
                   $"Actions Executed: {_actionHistory.Count}";
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// ゲームフェーズ
    /// </summary>
    public enum GamePhase
    {
        Setup = 0,          // セットアップ
        GameStart = 1,      // ゲーム開始
        TurnStart = 2,      // ターン開始
        DrawPhase = 3,      // ドローフェーズ
        MainPhase = 4,      // メインフェーズ
        AttackPhase = 5,    // アタックフェーズ
        TurnEnd = 6,        // ターン終了
        GameEnd = 7         // ゲーム終了
    }

    /// <summary>
    /// 勝利理由
    /// </summary>
    public enum VictoryReason
    {
        None = 0,           // なし
        AllPrizes = 1,      // サイドカード取得
        NoBench = 2,        // ベンチなし
        DeckOut = 3,        // デッキ切れ
        PointTarget = 4,    // ポイント到達
        TimeLimit = 5,      // 時間切れ
        Surrender = 6,      // 降参
        Error = 7           // エラー
    }

    #endregion

    #region Interfaces

    /// <summary>
    /// ゲームアクションの基本インターフェース
    /// </summary>
    public interface IGameAction
    {
        /// <summary>アクションID</summary>
        string ActionId { get; }

        /// <summary>実行者プレイヤーID</summary>
        string PlayerId { get; }

        /// <summary>アクション種類</summary>
        GameActionType ActionType { get; }

        /// <summary>実行時刻</summary>
        DateTime ExecutedAt { get; }

        /// <summary>アクション固有データ</summary>
        Dictionary<string, object> Parameters { get; }
    }

    #endregion

    #region Result Classes

    /// <summary>
    /// バリデーション結果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorMessage { get; private set; }
        public List<string> Warnings { get; private set; }

        private ValidationResult(bool isValid, string errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
            Warnings = new List<string>();
        }

        public static ValidationResult CreateSuccess()
        {
            return new ValidationResult(true);
        }

        public static ValidationResult CreateError(string errorMessage)
        {
            return new ValidationResult(false, errorMessage);
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }
    }

    /// <summary>
    /// アクション実行結果
    /// </summary>
    public class ActionResult
    {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public Dictionary<string, object> ResultData { get; private set; }

        private ActionResult(bool success, string errorMessage = null)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ResultData = new Dictionary<string, object>();
        }

        public static ActionResult CreateSuccess()
        {
            return new ActionResult(true);
        }

        public static ActionResult CreateFailure(string errorMessage)
        {
            return new ActionResult(false, errorMessage);
        }

        public void SetData(string key, object value)
        {
            ResultData[key] = value;
        }

        public T GetData<T>(string key, T defaultValue = default(T))
        {
            if (ResultData.TryGetValue(key, out var value) && value is T)
            {
                return (T)value;
            }
            return defaultValue;
        }
    }

    #endregion
}