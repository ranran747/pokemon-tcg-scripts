using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Game.Rules;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.Game
{
    /// <summary>
    /// ゲーム状態の包括的管理クラス
    /// プレイヤー、フィールド、カード、ルールの状態をすべて管理
    /// Claude拡張でのカスタムゲーム状態追加にも対応
    /// </summary>
    [System.Serializable]
    public class GameState
    {
        #region Fields

        [Header("基本ゲーム情報")]
        [SerializeField] private string _gameId;
        [SerializeField] private DateTime _gameStartTime;
        [SerializeField] private GamePhase _currentPhase;
        [SerializeField] private int _currentTurn;
        [SerializeField] private GameRule _activeRule;

        [Header("プレイヤー管理")]
        [SerializeField] private List<PlayerState> _players = new List<PlayerState>();
        [SerializeField] private string _currentPlayerId;
        [SerializeField] private string _firstPlayerId;

        [Header("勝敗管理")]
        [SerializeField] private bool _gameEnded;
        [SerializeField] private string _winnerId;
        [SerializeField] private VictoryReason _victoryReason;
        [SerializeField] private DateTime _gameEndTime;

        [Header("フィールド状態")]
        [SerializeField] private FieldState _fieldState;
        [SerializeField] private List<Card> _stadiumCards = new List<Card>();

        [Header("特殊状態")]
        [SerializeField] private Dictionary<string, object> _gameVariables = new Dictionary<string, object>();
        [SerializeField] private List<GameEffect> _activeEffects = new List<GameEffect>();
        [SerializeField] private Queue<GameEvent> _eventQueue = new Queue<GameEvent>();

        #endregion

        #region Properties

        /// <summary>ゲームID</summary>
        public string GameId => _gameId;

        /// <summary>ゲーム開始時刻</summary>
        public DateTime GameStartTime => _gameStartTime;

        /// <summary>現在のフェーズ</summary>
        public GamePhase CurrentPhase => _currentPhase;

        /// <summary>現在のターン数</summary>
        public int CurrentTurn => _currentTurn;

        /// <summary>適用されているルール</summary>
        public GameRule ActiveRule => _activeRule;

        /// <summary>プレイヤーリスト</summary>
        public List<PlayerState> Players => new List<PlayerState>(_players);

        /// <summary>現在のプレイヤーID</summary>
        public string CurrentPlayerId => _currentPlayerId;

        /// <summary>先攻プレイヤーID</summary>
        public string FirstPlayerId => _firstPlayerId;

        /// <summary>ゲーム終了フラグ</summary>
        public bool GameEnded => _gameEnded;

        /// <summary>勝利者ID</summary>
        public string WinnerId => _winnerId;

        /// <summary>勝利理由</summary>
        public VictoryReason VictoryReason => _victoryReason;

        /// <summary>ゲーム終了時刻</summary>
        public DateTime GameEndTime => _gameEndTime;

        /// <summary>フィールド状態</summary>
        public FieldState FieldState => _fieldState;

        /// <summary>場のスタジアムカード</summary>
        public List<Card> StadiumCards => new List<Card>(_stadiumCards);

        /// <summary>アクティブな効果リスト</summary>
        public List<GameEffect> ActiveEffects => new List<GameEffect>(_activeEffects);

        /// <summary>イベントキュー</summary>
        public Queue<GameEvent> EventQueue => new Queue<GameEvent>(_eventQueue);

        /// <summary>ゲーム変数</summary>
        public Dictionary<string, object> GameVariables => new Dictionary<string, object>(_gameVariables);

        /// <summary>ゲーム継続時間</summary>
        public TimeSpan GameDuration => _gameEnded ? _gameEndTime - _gameStartTime : DateTime.Now - _gameStartTime;

        /// <summary>プレイヤー数</summary>
        public int PlayerCount => _players.Count;

        /// <summary>現在のプレイヤー状態</summary>
        public PlayerState CurrentPlayer => GetPlayer(_currentPlayerId);

        #endregion

        #region Constructors

        /// <summary>
        /// デフォルトコンストラクタ
        /// </summary>
        public GameState()
        {
            _gameId = Guid.NewGuid().ToString();
            _gameStartTime = DateTime.Now;
            _fieldState = new FieldState();
        }

        /// <summary>
        /// ルール指定コンストラクタ
        /// </summary>
        /// <param name="gameRule">適用するルール</param>
        /// <param name="playerIds">プレイヤーIDリスト</param>
        public GameState(GameRule gameRule, List<string> playerIds) : this()
        {
            _activeRule = gameRule;
            InitializePlayers(playerIds);
        }

        #endregion

        #region Player Management

        /// <summary>
        /// プレイヤーを初期化
        /// </summary>
        /// <param name="playerIds">プレイヤーIDリスト</param>
        public void InitializePlayers(List<string> playerIds)
        {
            _players.Clear();
            
            foreach (var playerId in playerIds)
            {
                var playerState = new PlayerState(playerId);
                _players.Add(playerState);
            }

            if (_players.Count > 0)
            {
                _firstPlayerId = _players[0].PlayerId;
                _currentPlayerId = _firstPlayerId;
            }

            Debug.Log($"[GameState] Initialized {_players.Count} players");
        }

        /// <summary>
        /// プレイヤー状態を取得
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>プレイヤー状態</returns>
        public PlayerState GetPlayer(string playerId)
        {
            return _players.FirstOrDefault(p => p.PlayerId == playerId);
        }

        /// <summary>
        /// 対戦相手のプレイヤー状態を取得
        /// </summary>
        /// <param name="playerId">基準プレイヤーID</param>
        /// <returns>対戦相手の状態</returns>
        public PlayerState GetOpponent(string playerId)
        {
            return _players.FirstOrDefault(p => p.PlayerId != playerId);
        }

        /// <summary>
        /// 全プレイヤーの特定状態を取得
        /// </summary>
        /// <typeparam name="T">状態の型</typeparam>
        /// <param name="selector">状態セレクタ</param>
        /// <returns>状態の辞書</returns>
        public Dictionary<string, T> GetAllPlayerStates<T>(Func<PlayerState, T> selector)
        {
            return _players.ToDictionary(p => p.PlayerId, selector);
        }

        #endregion

        #region Game Flow Control

        /// <summary>
        /// ゲームを開始
        /// </summary>
        /// <param name="firstPlayerId">先攻プレイヤーID</param>
        public void StartGame(string firstPlayerId = null)
        {
            if (string.IsNullOrEmpty(firstPlayerId))
            {
                firstPlayerId = _players[UnityEngine.Random.Range(0, _players.Count)].PlayerId;
            }

            _firstPlayerId = firstPlayerId;
            _currentPlayerId = firstPlayerId;
            _currentPhase = GamePhase.Setup;
            _currentTurn = 1;
            _gameStartTime = DateTime.Now;

            Debug.Log($"[GameState] Game started. First player: {_firstPlayerId}");
        }

        /// <summary>
        /// ゲームを終了
        /// </summary>
        /// <param name="winnerId">勝利者ID</param>
        /// <param name="reason">勝利理由</param>
        public void EndGame(string winnerId, VictoryReason reason)
        {
            if (_gameEnded) return;

            _gameEnded = true;
            _winnerId = winnerId;
            _victoryReason = reason;
            _gameEndTime = DateTime.Now;
            _currentPhase = GamePhase.GameEnd;

            Debug.Log($"[GameState] Game ended. Winner: {_winnerId}, Reason: {_victoryReason}");
        }

        /// <summary>
        /// フェーズを変更
        /// </summary>
        /// <param name="newPhase">新しいフェーズ</param>
        public void SetPhase(GamePhase newPhase)
        {
            var oldPhase = _currentPhase;
            _currentPhase = newPhase;

            // フェーズ変更イベントをキューに追加
            EnqueueEvent(new PhaseChangedEvent
            {
                OldPhase = oldPhase,
                NewPhase = newPhase,
                PlayerId = _currentPlayerId
            });

            Debug.Log($"[GameState] Phase changed: {oldPhase} -> {newPhase}");
        }

        /// <summary>
        /// ターンを進める
        /// </summary>
        /// <param name="nextPlayerId">次のプレイヤーID</param>
        public void AdvanceTurn(string nextPlayerId = null)
        {
            if (string.IsNullOrEmpty(nextPlayerId))
            {
                // 次のプレイヤーを自動決定
                var currentIndex = _players.FindIndex(p => p.PlayerId == _currentPlayerId);
                var nextIndex = (currentIndex + 1) % _players.Count;
                nextPlayerId = _players[nextIndex].PlayerId;
            }

            var previousPlayerId = _currentPlayerId;
            _currentPlayerId = nextPlayerId;
            _currentTurn++;

            // ターン変更イベントをキューに追加
            EnqueueEvent(new TurnChangedEvent
            {
                PreviousPlayerId = previousPlayerId,
                CurrentPlayerId = _currentPlayerId,
                TurnNumber = _currentTurn
            });

            Debug.Log($"[GameState] Turn advanced to {_currentTurn}. Current player: {_currentPlayerId}");
        }

        #endregion

        #region State Queries

        /// <summary>
        /// 指定プレイヤーがアクティブかチェック
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>アクティブな場合true</returns>
        public bool IsActivePlayer(string playerId)
        {
            return _currentPlayerId == playerId;
        }

        /// <summary>
        /// ゲームが指定フェーズかチェック
        /// </summary>
        /// <param name="phase">フェーズ</param>
        /// <returns>指定フェーズの場合true</returns>
        public bool IsInPhase(GamePhase phase)
        {
            return _currentPhase == phase;
        }

        /// <summary>
        /// 指定フェーズのいずれかかチェック
        /// </summary>
        /// <param name="phases">フェーズリスト</param>
        /// <returns>いずれかのフェーズの場合true</returns>
        public bool IsInAnyPhase(params GamePhase[] phases)
        {
            return phases.Contains(_currentPhase);
        }

        /// <summary>
        /// ゲーム変数の存在チェック
        /// </summary>
        /// <param name="key">キー</param>
        /// <returns>存在する場合true</returns>
        public bool HasGameVariable(string key)
        {
            return _gameVariables.ContainsKey(key);
        }

        /// <summary>
        /// アクティブな効果の存在チェック
        /// </summary>
        /// <param name="effectType">効果タイプ</param>
        /// <returns>存在する場合true</returns>
        public bool HasActiveEffect(string effectType)
        {
            return _activeEffects.Any(e => e.EffectType == effectType);
        }

        #endregion

        #region Variable Management

        /// <summary>
        /// ゲーム変数を設定
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        public void SetGameVariable(string key, object value)
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
        public T GetGameVariable<T>(string key, T defaultValue = default(T))
        {
            if (_gameVariables.TryGetValue(key, out var value) && value is T)
            {
                return (T)value;
            }
            return defaultValue;
        }

        /// <summary>
        /// ゲーム変数を削除
        /// </summary>
        /// <param name="key">キー</param>
        /// <returns>削除された場合true</returns>
        public bool RemoveGameVariable(string key)
        {
            return _gameVariables.Remove(key);
        }

        #endregion

        #region Effect Management

        /// <summary>
        /// 効果を追加
        /// </summary>
        /// <param name="effect">効果</param>
        public void AddEffect(GameEffect effect)
        {
            _activeEffects.Add(effect);
            Debug.Log($"[GameState] Added effect: {effect.EffectType}");
        }

        /// <summary>
        /// 効果を削除
        /// </summary>
        /// <param name="effectId">効果ID</param>
        /// <returns>削除された場合true</returns>
        public bool RemoveEffect(string effectId)
        {
            var effect = _activeEffects.FirstOrDefault(e => e.EffectId == effectId);
            if (effect != null)
            {
                _activeEffects.Remove(effect);
                Debug.Log($"[GameState] Removed effect: {effect.EffectType}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 指定タイプの効果をすべて取得
        /// </summary>
        /// <param name="effectType">効果タイプ</param>
        /// <returns>効果リスト</returns>
        public List<GameEffect> GetEffects(string effectType)
        {
            return _activeEffects.Where(e => e.EffectType == effectType).ToList();
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
                Debug.Log($"[GameState] Expired effect removed: {effect.EffectType}");
            }
        }

        #endregion

        #region Event Management

        /// <summary>
        /// イベントをキューに追加
        /// </summary>
        /// <param name="gameEvent">ゲームイベント</param>
        public void EnqueueEvent(GameEvent gameEvent)
        {
            _eventQueue.Enqueue(gameEvent);
        }

        /// <summary>
        /// イベントキューから次のイベントを取得
        /// </summary>
        /// <returns>次のイベント（なければnull）</returns>
        public GameEvent DequeueEvent()
        {
            return _eventQueue.Count > 0 ? _eventQueue.Dequeue() : null;
        }

        /// <summary>
        /// イベントキューをクリア
        /// </summary>
        public void ClearEventQueue()
        {
            _eventQueue.Clear();
        }

        #endregion

        #region Field Management

        /// <summary>
        /// スタジアムカードを設置
        /// </summary>
        /// <param name="stadiumCard">スタジアムカード</param>
        public void SetStadium(Card stadiumCard)
        {
            // 既存のスタジアムを置き換え
            _stadiumCards.Clear();
            _stadiumCards.Add(stadiumCard);
            
            Debug.Log($"[GameState] Stadium set: {stadiumCard.CardData.CardName}");
        }

        /// <summary>
        /// スタジアムカードを除去
        /// </summary>
        public void RemoveStadium()
        {
            if (_stadiumCards.Count > 0)
            {
                var removedStadium = _stadiumCards[0];
                _stadiumCards.Clear();
                Debug.Log($"[GameState] Stadium removed: {removedStadium.CardData.CardName}");
            }
        }

        /// <summary>
        /// アクティブなスタジアムを取得
        /// </summary>
        /// <returns>アクティブなスタジアム（なければnull）</returns>
        public Card GetActiveStadium()
        {
            return _stadiumCards.FirstOrDefault();
        }

        #endregion

        #region Serialization

        /// <summary>
        /// 状態を辞書形式でシリアライズ
        /// </summary>
        /// <returns>シリアライズされた状態</returns>
        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                ["gameId"] = _gameId,
                ["gameStartTime"] = _gameStartTime,
                ["currentPhase"] = _currentPhase,
                ["currentTurn"] = _currentTurn,
                ["currentPlayerId"] = _currentPlayerId,
                ["firstPlayerId"] = _firstPlayerId,
                ["gameEnded"] = _gameEnded,
                ["winnerId"] = _winnerId,
                ["victoryReason"] = _victoryReason,
                ["gameEndTime"] = _gameEndTime,
                ["players"] = _players.Select(p => p.Serialize()).ToList(),
                ["gameVariables"] = new Dictionary<string, object>(_gameVariables),
                ["activeEffects"] = _activeEffects.Select(e => e.Serialize()).ToList()
            };
        }

        /// <summary>
        /// 辞書から状態をデシリアライズ
        /// </summary>
        /// <param name="data">シリアライズされた状態</param>
        public void Deserialize(Dictionary<string, object> data)
        {
            if (data.TryGetValue("gameId", out var gameId))
                _gameId = gameId.ToString();
            
            if (data.TryGetValue("gameStartTime", out var startTime) && startTime is DateTime)
                _gameStartTime = (DateTime)startTime;
            
            if (data.TryGetValue("currentPhase", out var phase) && phase is GamePhase)
                _currentPhase = (GamePhase)phase;
            
            if (data.TryGetValue("currentTurn", out var turn) && turn is int)
                _currentTurn = (int)turn;
            
            if (data.TryGetValue("currentPlayerId", out var currentId))
                _currentPlayerId = currentId.ToString();
            
            if (data.TryGetValue("firstPlayerId", out var firstId))
                _firstPlayerId = firstId.ToString();
            
            if (data.TryGetValue("gameEnded", out var ended) && ended is bool)
                _gameEnded = (bool)ended;
            
            if (data.TryGetValue("winnerId", out var winner))
                _winnerId = winner.ToString();
            
            if (data.TryGetValue("victoryReason", out var reason) && reason is VictoryReason)
                _victoryReason = (VictoryReason)reason;
            
            if (data.TryGetValue("gameEndTime", out var endTime) && endTime is DateTime)
                _gameEndTime = (DateTime)endTime;
            
            // プレイヤー状態のデシリアライズ
            if (data.TryGetValue("players", out var playersData) && playersData is List<object> playersList)
            {
                _players.Clear();
                foreach (var playerData in playersList)
                {
                    if (playerData is Dictionary<string, object> playerDict)
                    {
                        var playerState = new PlayerState("");
                        playerState.Deserialize(playerDict);
                        _players.Add(playerState);
                    }
                }
            }
            
            // ゲーム変数のデシリアライズ
            if (data.TryGetValue("gameVariables", out var variables) && variables is Dictionary<string, object> variablesDict)
            {
                _gameVariables = new Dictionary<string, object>(variablesDict);
            }
        }

        #endregion

        #region Debug

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        /// <returns>デバッグ情報文字列</returns>
        public string GetDebugInfo()
        {
            return $"=== Game State Debug Info ===\n" +
                   $"Game ID: {_gameId}\n" +
                   $"Phase: {_currentPhase}\n" +
                   $"Turn: {_currentTurn}\n" +
                   $"Current Player: {_currentPlayerId}\n" +
                   $"Game Ended: {_gameEnded}\n" +
                   $"Winner: {_winnerId}\n" +
                   $"Victory Reason: {_victoryReason}\n" +
                   $"Players: {_players.Count}\n" +
                   $"Active Effects: {_activeEffects.Count}\n" +
                   $"Event Queue: {_eventQueue.Count}\n" +
                   $"Game Variables: {_gameVariables.Count}\n" +
                   $"Duration: {GameDuration.TotalMinutes:F2} minutes\n" +
                   $"Stadium: {(GetActiveStadium()?.CardData.CardName ?? "None")}";
        }

        #endregion
    }

    #region State Classes

    /// <summary>
    /// フィールド状態クラス
    /// </summary>
    [System.Serializable]
    public class FieldState
    {
        [SerializeField] private Dictionary<string, object> _fieldVariables = new Dictionary<string, object>();
        [SerializeField] private List<string> _fieldEffects = new List<string>();

        public Dictionary<string, object> FieldVariables => _fieldVariables;
        public List<string> FieldEffects => _fieldEffects;

        public void SetVariable(string key, object value) => _fieldVariables[key] = value;
        public T GetVariable<T>(string key, T defaultValue = default(T))
        {
            return _fieldVariables.TryGetValue(key, out var value) && value is T ? (T)value : defaultValue;
        }
    }

    /// <summary>
    /// ゲーム効果クラス
    /// </summary>
    [System.Serializable]
    public class GameEffect
    {
        [SerializeField] private string _effectId;
        [SerializeField] private string _effectType;
        [SerializeField] private string _sourceId;
        [SerializeField] private DateTime _createdTime;
        [SerializeField] private int _duration; // -1 = 永続
        [SerializeField] private Dictionary<string, object> _parameters = new Dictionary<string, object>();

        public string EffectId => _effectId;
        public string EffectType => _effectType;
        public string SourceId => _sourceId;
        public DateTime CreatedTime => _createdTime;
        public int Duration => _duration;
        public Dictionary<string, object> Parameters => _parameters;

        public GameEffect(string effectType, string sourceId, int duration = -1)
        {
            _effectId = Guid.NewGuid().ToString();
            _effectType = effectType;
            _sourceId = sourceId;
            _duration = duration;
            _createdTime = DateTime.Now;
        }

        public bool IsExpired()
        {
            if (_duration < 0) return false; // 永続効果
            return (DateTime.Now - _createdTime).TotalSeconds > _duration;
        }

        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                ["effectId"] = _effectId,
                ["effectType"] = _effectType,
                ["sourceId"] = _sourceId,
                ["createdTime"] = _createdTime,
                ["duration"] = _duration,
                ["parameters"] = new Dictionary<string, object>(_parameters)
            };
        }
    }

    /// <summary>
    /// ゲームイベントクラス
    /// </summary>
    [System.Serializable]
    public abstract class GameEvent
    {
        [SerializeField] private string _eventId;
        [SerializeField] private DateTime _timestamp;
        [SerializeField] private string _eventType;

        public string EventId => _eventId;
        public DateTime Timestamp => _timestamp;
        public string EventType => _eventType;

        protected GameEvent(string eventType)
        {
            _eventId = Guid.NewGuid().ToString();
            _eventType = eventType;
            _timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// フェーズ変更イベント
    /// </summary>
    public class PhaseChangedEvent : GameEvent
    {
        public GamePhase OldPhase { get; set; }
        public GamePhase NewPhase { get; set; }
        public string PlayerId { get; set; }

        public PhaseChangedEvent() : base("PhaseChanged") { }
    }

    /// <summary>
    /// ターン変更イベント
    /// </summary>
    public class TurnChangedEvent : GameEvent
    {
        public string PreviousPlayerId { get; set; }
        public string CurrentPlayerId { get; set; }
        public int TurnNumber { get; set; }

        public TurnChangedEvent() : base("TurnChanged") { }
    }

    #endregion
}