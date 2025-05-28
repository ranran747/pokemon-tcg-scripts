using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Game.Rules;

namespace PokemonTCG.Game.AI
{
    /// <summary>
    /// AI統合管理システム
    /// 複数のAIを管理し、対戦を制御する
    /// </summary>
    public class AIManager : MonoBehaviourSingleton<AIManager>, IManager
    {
        #region Events

        public static event Action<AIBattleStartedEvent> OnAIBattleStarted;
        public static event Action<AIBattleEndedEvent> OnAIBattleEnded;
        public static event Action<AIActionExecutedEvent> OnAIActionExecuted;
        public static event Action<AIErrorEvent> OnAIError;

        #endregion

        #region Fields

        [Header("AI管理設定")]
        [SerializeField] private bool _enableAIBattles = true;
        [SerializeField] private float _globalAISpeed = 1f;
        [SerializeField] private bool _enableAIDebugLog = true;
        [SerializeField] private int _maxConcurrentBattles = 4;

        [Header("登録済みAI")]
        [SerializeField] private List<BaseAI> _registeredAIs = new List<BaseAI>();
        [SerializeField] private Dictionary<string, BaseAI> _aiByPlayerId = new Dictionary<string, BaseAI>();
        [SerializeField] private Dictionary<string, AIProfile> _aiProfiles = new Dictionary<string, AIProfile>();

        [Header("バトル管理")]
        [SerializeField] private List<AIBattleSession> _activeBattles = new List<AIBattleSession>();
        [SerializeField] private Queue<AIBattleRequest> _battleQueue = new Queue<AIBattleRequest>();
        [SerializeField] private bool _processingBattles = false;

        [Header("統計")]
        [SerializeField] private int _totalBattles = 0;
        [SerializeField] private int _completedBattles = 0;
        [SerializeField] private Dictionary<string, AIStatistics> _aiStatistics = new Dictionary<string, AIStatistics>();

        // システム参照
        private GameStateManager _gameStateManager;
        private TurnManager _turnManager;
        private ActionValidationSystem _validationSystem;

        #endregion

        #region Properties

        /// <summary>マネージャー名</summary>
        public string ManagerName => "AIManager";

        /// <summary>初期化順序</summary>
        public int InitializationOrder => 180; // AI systems

        /// <summary>登録済みAI数</summary>
        public int RegisteredAICount => _registeredAIs.Count;

        /// <summary>アクティブバトル数</summary>
        public int ActiveBattleCount => _activeBattles.Count;

        /// <summary>バトル完了率</summary>
        public float BattleCompletionRate => 
            _totalBattles > 0 ? (float)_completedBattles / _totalBattles * 100f : 0f;

        /// <summary>AI一覧</summary>
        public IReadOnlyList<BaseAI> RegisteredAIs => _registeredAIs.AsReadOnly();

        #endregion

        #region Initialization

        public void Initialize()
        {
            Debug.Log($"[{ManagerName}] Initializing AI Manager...");
            
            // システム参照取得
            _gameStateManager = ServiceLocator.Get<GameStateManager>();
            _turnManager = ServiceLocator.Get<TurnManager>();
            _validationSystem = ServiceLocator.Get<ActionValidationSystem>();
            
            // サービス登録
            ServiceLocator.Register<AIManager>(this);
            
            // 標準AIプロファイル作成
            CreateDefaultAIProfiles();
            
            // イベント登録
            EventBus.On<GameStartedEvent>(OnGameStarted);
            EventBus.On<GameEndedEvent>(OnGameEnded);
            
            Debug.Log($"[{ManagerName}] AI Manager initialized");
        }

        public void Dispose()
        {
            Debug.Log($"[{ManagerName}] Disposing AI Manager...");
            
            // バトル停止
            StopAllBattles();
            
            // AI破棄
            foreach (var ai in _registeredAIs)
            {
                ai.Dispose();
            }
            
            // イベント解除
            EventBus.Off<GameStartedEvent>(OnGameStarted);
            EventBus.Off<GameEndedEvent>(OnGameEnded);
            
            // データクリア
            _registeredAIs.Clear();
            _aiByPlayerId.Clear();
            _aiProfiles.Clear();
            _activeBattles.Clear();
            _battleQueue.Clear();
            _aiStatistics.Clear();
            
            Debug.Log($"[{ManagerName}] AI Manager disposed");
        }

        private void CreateDefaultAIProfiles()
        {
            // 他のAIプロファイルもここに追加可能
            _aiProfiles["BasicAI"] = new AIProfile
            {
                AIName = "BasicAI",
                AIClass = typeof(BaseAI), // 実際は別のクラス
                Description = "Basic AI for testing",
                Personality = AIPersonality.Balanced,
                ThinkingDelay = 1f,
                Difficulty = AIDifficulty.Normal
            };
        }

        #endregion

        #region AI Registration

        /// <summary>
        /// AIを登録
        /// </summary>
        /// <param name="ai">AIインスタンス</param>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>登録成功フラグ</returns>
        public bool RegisterAI(BaseAI ai, string playerId)
        {
            if (ai == null || string.IsNullOrEmpty(playerId))
            {
                Debug.LogError($"[{ManagerName}] Invalid AI or player ID");
                return false;
            }
            
            if (_aiByPlayerId.ContainsKey(playerId))
            {
                Debug.LogWarning($"[{ManagerName}] AI already registered for player {playerId}");
                return false;
            }
            
            try
            {
                ai.Initialize(playerId);
                _registeredAIs.Add(ai);
                _aiByPlayerId[playerId] = ai;
                
                // 統計初期化
                _aiStatistics[playerId] = new AIStatistics
                {
                    AIName = ai.AIName,
                    PlayerId = playerId
                };
                
                Debug.Log($"[{ManagerName}] AI {ai.AIName} registered for player {playerId}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Failed to register AI: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// AIプロファイルからAIを作成・登録
        /// </summary>
        /// <param name="profileName">プロファイル名</param>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>作成されたAI</returns>
        public BaseAI CreateAndRegisterAI(string profileName, string playerId)
        {
            if (!_aiProfiles.ContainsKey(profileName))
            {
                Debug.LogError($"[{ManagerName}] AI profile {profileName} not found");
                return null;
            }
            
            var profile = _aiProfiles[profileName];
            
            try
            {
                // AIインスタンス作成
                var aiGameObject = new GameObject($"AI_{profile.AIName}_{playerId}");
                var ai = aiGameObject.AddComponent(profile.AIClass) as BaseAI;
                
                if (ai == null)
                {
                    Debug.LogError($"[{ManagerName}] Failed to create AI component");
                    DestroyImmediate(aiGameObject);
                    return null;
                }
                
                // 設定適用
                ai.name = profile.AIName;
                
                // 登録
                if (RegisterAI(ai, playerId))
                {
                    return ai;
                }
                else
                {
                    DestroyImmediate(aiGameObject);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Failed to create AI: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// AI登録解除
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>解除成功フラグ</returns>
        public bool UnregisterAI(string playerId)
        {
            if (!_aiByPlayerId.ContainsKey(playerId))
            {
                Debug.LogWarning($"[{ManagerName}] No AI registered for player {playerId}");
                return false;
            }
            
            var ai = _aiByPlayerId[playerId];
            
            try
            {
                ai.Dispose();
                _registeredAIs.Remove(ai);
                _aiByPlayerId.Remove(playerId);
                _aiStatistics.Remove(playerId);
                
                if (ai.gameObject != null)
                {
                    DestroyImmediate(ai.gameObject);
                }
                
                Debug.Log($"[{ManagerName}] AI unregistered for player {playerId}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Failed to unregister AI: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Battle Management

        /// <summary>
        /// AIバトルを開始
        /// </summary>
        /// <param name="player1Id">プレイヤー1 ID</param>
        /// <param name="player2Id">プレイヤー2 ID</param>
        /// <param name="ruleType">ルール種別</param>
        /// <returns>バトルセッション</returns>
        public AIBattleSession StartAIBattle(string player1Id, string player2Id, string ruleType = "PocketTCG")
        {
            if (!_enableAIBattles)
            {
                Debug.LogWarning($"[{ManagerName}] AI battles are disabled");
                return null;
            }
            
            if (_activeBattles.Count >= _maxConcurrentBattles)
            {
                // キューに追加
                _battleQueue.Enqueue(new AIBattleRequest
                {
                    Player1Id = player1Id,
                    Player2Id = player2Id,
                    RuleType = ruleType,
                    RequestTime = DateTime.Now
                });
                
                Debug.Log($"[{ManagerName}] Battle queued: {player1Id} vs {player2Id}");
                return null;
            }
            
            if (!_aiByPlayerId.ContainsKey(player1Id) || !_aiByPlayerId.ContainsKey(player2Id))
            {
                Debug.LogError($"[{ManagerName}] One or both AIs not registered");
                return null;
            }
            
            var ai1 = _aiByPlayerId[player1Id];
            var ai2 = _aiByPlayerId[player2Id];
            
            var battleSession = new AIBattleSession
            {
                BattleId = Guid.NewGuid().ToString(),
                Player1Id = player1Id,
                Player2Id = player2Id,
                AI1 = ai1,
                AI2 = ai2,
                RuleType = ruleType,
                StartTime = DateTime.Now,
                Status = BattleStatus.Starting
            };
            
            _activeBattles.Add(battleSession);
            _totalBattles++;
            
            // ゲーム開始
            StartGameForBattle(battleSession);
            
            OnAIBattleStarted?.Invoke(new AIBattleStartedEvent
            {
                BattleSession = battleSession
            });
            
            Debug.Log($"[{ManagerName}] AI Battle started: {ai1.AIName} vs {ai2.AIName}");
            
            return battleSession;
        }

        /// <summary>
        /// バトル用ゲーム開始
        /// </summary>
        private void StartGameForBattle(AIBattleSession battleSession)
        {
            try
            {
                battleSession.Status = BattleStatus.InProgress;
                
                // ルール取得・設定
                GameRule rule = null;
                switch (battleSession.RuleType)
                {
                    case "PocketTCG":
                        rule = FindObjectOfType<PocketTCGRule>();
                        break;
                    case "ClassicTCG":
                        rule = FindObjectOfType<ClassicTCGRule>();
                        break;
                    default:
                        rule = FindObjectOfType<PocketTCGRule>();
                        break;
                }
                
                if (rule == null)
                {
                    Debug.LogError($"[{ManagerName}] Rule not found: {battleSession.RuleType}");
                    EndBattle(battleSession, null, "Rule not found");
                    return;
                }
                
                // ゲーム開始
                var playerIds = new List<string> { battleSession.Player1Id, battleSession.Player2Id };
                
                if (_gameStateManager.StartNewGame(rule, playerIds))
                {
                    Debug.Log($"[{ManagerName}] Game started for battle {battleSession.BattleId}");
                }
                else
                {
                    Debug.LogError($"[{ManagerName}] Failed to start game for battle");
                    EndBattle(battleSession, null, "Failed to start game");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{ManagerName}] Error starting battle game: {ex.Message}");
                EndBattle(battleSession, null, ex.Message);
            }
        }

        /// <summary>
        /// バトル終了
        /// </summary>
        /// <param name="battleSession">バトルセッション</param>
        /// <param name="winnerId">勝利者ID</param>
        /// <param name="reason">終了理由</param>
        private void EndBattle(AIBattleSession battleSession, string winnerId, string reason)
        {
            battleSession.Status = BattleStatus.Completed;
            battleSession.EndTime = DateTime.Now;
            battleSession.WinnerId = winnerId;
            battleSession.EndReason = reason;
            
            _activeBattles.Remove(battleSession);
            _completedBattles++;
            
            // 統計更新
            UpdateBattleStatistics(battleSession);
            
            OnAIBattleEnded?.Invoke(new AIBattleEndedEvent
            {
                BattleSession = battleSession
            });
            
            Debug.Log($"[{ManagerName}] Battle ended: {battleSession.BattleId}, Winner: {winnerId ?? "Draw"}, Reason: {reason}");
            
            // キューから次のバトルを開始
            ProcessBattleQueue();
        }

        /// <summary>
        /// バトルキュー処理
        /// </summary>
        private void ProcessBattleQueue()
        {
            if (_battleQueue.Count > 0 && _activeBattles.Count < _maxConcurrentBattles)
            {
                var request = _battleQueue.Dequeue();
                StartAIBattle(request.Player1Id, request.Player2Id, request.RuleType);
            }
        }

        /// <summary>
        /// 全バトル停止
        /// </summary>
        public void StopAllBattles()
        {
            foreach (var battle in _activeBattles.ToList())
            {
                EndBattle(battle, null, "Manager shutdown");
            }
            
            _battleQueue.Clear();
        }

        /// <summary>
        /// バトル統計更新
        /// </summary>
        private void UpdateBattleStatistics(AIBattleSession battleSession)
        {
            var duration = (battleSession.EndTime - battleSession.StartTime).TotalSeconds;
            
            // プレイヤー1統計
            if (_aiStatistics.ContainsKey(battleSession.Player1Id))
            {
                var stats = _aiStatistics[battleSession.Player1Id];
                stats.BattlesPlayed++;
                stats.TotalBattleTime += (float)duration;
                
                if (battleSession.WinnerId == battleSession.Player1Id)
                {
                    stats.BattlesWon++;
                }
                
                stats.WinRate = stats.BattlesPlayed > 0 ? (float)stats.BattlesWon / stats.BattlesPlayed * 100f : 0f;
            }
            
            // プレイヤー2統計
            if (_aiStatistics.ContainsKey(battleSession.Player2Id))
            {
                var stats = _aiStatistics[battleSession.Player2Id];
                stats.BattlesPlayed++;
                stats.TotalBattleTime += (float)duration;
                
                if (battleSession.WinnerId == battleSession.Player2Id)
                {
                    stats.BattlesWon++;
                }
                
                stats.WinRate = stats.BattlesPlayed > 0 ? (float)stats.BattlesWon / stats.BattlesPlayed * 100f : 0f;
            }
        }

        #endregion

        #region AI Control

        /// <summary>
        /// 指定AIを取得
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>AI</returns>
        public BaseAI GetAI(string playerId)
        {
            return _aiByPlayerId.GetValueOrDefault(playerId);
        }

        /// <summary>
        /// 全AI一時停止
        /// </summary>
        public void PauseAllAIs()
        {
            foreach (var ai in _registeredAIs)
            {
                // AI一時停止処理
                ai.gameObject.SetActive(false);
            }
            
            Debug.Log($"[{ManagerName}] All AIs paused");
        }

        /// <summary>
        /// 全AI再開
        /// </summary>
        public void ResumeAllAIs()
        {
            foreach (var ai in _registeredAIs)
            {
                // AI再開処理
                ai.gameObject.SetActive(true);
            }
            
            Debug.Log($"[{ManagerName}] All AIs resumed");
        }

        /// <summary>
        /// AI速度設定
        /// </summary>
        /// <param name="speed">速度倍率</param>
        public void SetGlobalAISpeed(float speed)
        {
            _globalAISpeed = Mathf.Clamp(speed, 0.1f, 10f);
            
            // 全AIに適用
            foreach (var ai in _registeredAIs)
            {
                // AI速度設定処理
            }
            
            Debug.Log($"[{ManagerName}] Global AI speed set to {_globalAISpeed}");
        }

        #endregion

        #region Event Handlers

        private void OnGameStarted(GameStartedEvent evt)
        {
            Debug.Log($"[{ManagerName}] Game started event received with {evt.PlayerIds.Count} players");
        }

        private void OnGameEnded(GameEndedEvent evt)
        {
            Debug.Log($"[{ManagerName}] Game ended event received. Winner: {evt.WinnerId}");
            
            // アクティブバトルを探して終了処理
            var activeBattle = _activeBattles.FirstOrDefault(b => 
                b.Player1Id == evt.WinnerId || b.Player2Id == evt.WinnerId ||
                (b.Status == BattleStatus.InProgress));
            
            if (activeBattle != null)
            {
                EndBattle(activeBattle, evt.WinnerId, evt.Reason.ToString());
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// AI統計取得
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>統計</returns>
        public AIStatistics GetAIStatistics(string playerId)
        {
            return _aiStatistics.GetValueOrDefault(playerId, new AIStatistics { PlayerId = playerId });
        }

        /// <summary>
        /// 全AI統計取得
        /// </summary>
        /// <returns>統計辞書</returns>
        public Dictionary<string, AIStatistics> GetAllAIStatistics()
        {
            return new Dictionary<string, AIStatistics>(_aiStatistics);
        }

        /// <summary>
        /// 統計リセット
        /// </summary>
        public void ResetStatistics()
        {
            foreach (var stats in _aiStatistics.Values)
            {
                stats.BattlesPlayed = 0;
                stats.BattlesWon = 0;
                stats.WinRate = 0f;
                stats.TotalBattleTime = 0f;
            }
            
            _totalBattles = 0;
            _completedBattles = 0;
            
            Debug.Log($"[{ManagerName}] Statistics reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// デバッグ情報取得
        /// </summary>
        /// <returns>デバッグ情報</returns>
        public string GetDebugInfo()
        {
            return $"=== AI Manager Debug Info ===\n" +
                   $"Registered AIs: {RegisteredAICount}\n" +
                   $"Active Battles: {ActiveBattleCount}\n" +
                   $"Total Battles: {_totalBattles}\n" +
                   $"Completed Battles: {_completedBattles}\n" +
                   $"Battle Completion Rate: {BattleCompletionRate:F1}%\n" +
                   $"Battle Queue Size: {_battleQueue.Count}\n" +
                   $"AI Battles Enabled: {_enableAIBattles}\n" +
                   $"Global AI Speed: {_globalAISpeed}\n" +
                   $"Max Concurrent Battles: {_maxConcurrentBattles}";
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// AIプロファイル
    /// </summary>
    [Serializable]
    public class AIProfile
    {
        public string AIName;
        public Type AIClass;
        public string Description;
        public AIPersonality Personality;
        public float ThinkingDelay;
        public AIDifficulty Difficulty;
    }

    /// <summary>
    /// AIバトルセッション
    /// </summary>
    [Serializable]
    public class AIBattleSession
    {
        public string BattleId;
        public string Player1Id;
        public string Player2Id;
        public BaseAI AI1;
        public BaseAI AI2;
        public string RuleType;
        public DateTime StartTime;
        public DateTime EndTime;
        public BattleStatus Status;
        public string WinnerId;
        public string EndReason;
    }

    /// <summary>
    /// AIバトルリクエスト
    /// </summary>
    [Serializable]
    public class AIBattleRequest
    {
        public string Player1Id;
        public string Player2Id;
        public string RuleType;
        public DateTime RequestTime;
    }

    /// <summary>
    /// AI統計（拡張版）
    /// </summary>
    [Serializable]
    public class AIStatistics
    {
        public string AIName;
        public string PlayerId;
        public int TurnsPlayed;
        public int ActionsExecuted;
        public int SuccessfulActions;
        public float TotalThinkingTime;
        public float SuccessRate;
        public int BattlesPlayed;
        public int BattlesWon;
        public float WinRate;
        public float TotalBattleTime;
        public float AverageBattleTime => BattlesPlayed > 0 ? TotalBattleTime / BattlesPlayed : 0f;
    }

    #endregion

    #region Enums

    /// <summary>
    /// AI難易度
    /// </summary>
    public enum AIDifficulty
    {
        Beginner,
        Normal,
        Hard,
        Expert,
        Master
    }

    /// <summary>
    /// バトル状態
    /// </summary>
    public enum BattleStatus
    {
        Queued,
        Starting,
        InProgress,
        Completed,
        Cancelled,
        Error
    }

    #endregion

    #region Event Classes

    public class AIBattleStartedEvent
    {
        public AIBattleSession BattleSession { get; set; }
    }

    public class AIBattleEndedEvent
    {
        public AIBattleSession BattleSession { get; set; }
    }

    public class AIActionExecutedEvent
    {
        public string PlayerId { get; set; }
        public string AIName { get; set; }
        public GameActionType ActionType { get; set; }
        public bool Success { get; set; }
        public string Description { get; set; }
    }

    public class AIErrorEvent
    {
        public string PlayerId { get; set; }
        public string AIName { get; set; }
        public string ErrorMessage { get; set; }
        public string StackTrace { get; set; }
    }

    #endregion
}