using System;
using System.Collections.Generic;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.Cards.Effects
{
    /// <summary>
    /// エフェクト実行コンテキスト
    /// カード効果の実行に必要な情報を集約
    /// ゲーム状態、プレイヤー情報、対象などを管理
    /// </summary>
    public class EffectContext
    {
        #region Core Context

        /// <summary>効果の発生源カード</summary>
        public Card SourceCard { get; private set; }

        /// <summary>効果を実行するプレイヤー</summary>
        public Player CurrentPlayer { get; private set; }

        /// <summary>相手プレイヤー</summary>
        public Player OpponentPlayer { get; private set; }

        /// <summary>現在のゲーム状態</summary>
        public GameState GameState { get; private set; }

        /// <summary>効果の実行時刻</summary>
        public float ExecutionTime { get; private set; }

        /// <summary>効果の実行ID（一意識別子）</summary>
        public string ExecutionId { get; private set; }

        #endregion

        #region Target Information

        /// <summary>効果の主要対象</summary>
        public object PrimaryTarget { get; set; }

        /// <summary>効果の対象リスト</summary>
        public List<object> Targets { get; set; } = new List<object>();

        /// <summary>効果の範囲</summary>
        public EffectScope Scope { get; set; } = EffectScope.Single;

        /// <summary>対象選択が必要か</summary>
        public bool RequiresTargetSelection { get; set; } = false;

        #endregion

        #region Effect Parameters

        /// <summary>効果のパラメーター</summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>効果の強度・威力</summary>
        public int Power { get; set; } = 0;

        /// <summary>効果の持続時間</summary>
        public int Duration { get; set; } = 0;

        /// <summary>効果の実行回数</summary>
        public int ExecutionCount { get; set; } = 1;

        /// <summary>乱数シード（効果の一貫性のため）</summary>
        public int RandomSeed { get; set; }

        #endregion

        #region Game Context

        /// <summary>現在のターン数</summary>
        public int CurrentTurn { get; set; }

        /// <summary>現在のフェーズ</summary>
        public GamePhase CurrentPhase { get; set; }

        /// <summary>アクティブプレイヤーのフィールド</summary>
        public Field ActiveField { get; set; }

        /// <summary>相手プレイヤーのフィールド</summary>
        public Field OpponentField { get; set; }

        /// <summary>現在のスタジアムカード</summary>
        public Card CurrentStadium { get; set; }

        #endregion

        #region Execution State

        /// <summary>効果が実行済みか</summary>
        public bool IsExecuted { get; private set; } = false;

        /// <summary>効果がキャンセルされたか</summary>
        public bool IsCancelled { get; private set; } = false;

        /// <summary>効果が置換されたか</summary>
        public bool IsReplaced { get; private set; } = false;

        /// <summary>効果の実行結果</summary>
        public EffectResult ExecutionResult { get; set; }

        /// <summary>効果の実行履歴</summary>
        public List<EffectExecutionStep> ExecutionHistory { get; private set; } = new List<EffectExecutionStep>();

        #endregion

        #region Chain and Stack

        /// <summary>効果チェーンの位置</summary>
        public int ChainPosition { get; set; } = 0;

        /// <summary>チェーンされた効果のリスト</summary>
        public List<ICardEffect> EffectChain { get; set; } = new List<ICardEffect>();

        /// <summary>親効果（この効果をトリガーした効果）</summary>
        public ICardEffect ParentEffect { get; set; }

        /// <summary>子効果のリスト（この効果によってトリガーされた効果）</summary>
        public List<ICardEffect> ChildEffects { get; set; } = new List<ICardEffect>();

        #endregion

        #region Conditional Flags

        /// <summary>効果の条件をチェックするフラグ</summary>
        public Dictionary<string, bool> ConditionFlags { get; set; } = new Dictionary<string, bool>();

        /// <summary>効果の実行制限フラグ</summary>
        public Dictionary<string, int> RestrictionCounters { get; set; } = new Dictionary<string, int>();

        /// <summary>特殊状態フラグ</summary>
        public HashSet<string> SpecialFlags { get; set; } = new HashSet<string>();

        #endregion

        #region Constructors

        /// <summary>
        /// 基本コンストラクタ
        /// </summary>
        /// <param name="sourceCard">発生源カード</param>
        /// <param name="currentPlayer">実行プレイヤー</param>
        /// <param name="gameState">ゲーム状態</param>
        public EffectContext(Card sourceCard, Player currentPlayer, GameState gameState)
        {
            SourceCard = sourceCard ?? throw new ArgumentNullException(nameof(sourceCard));
            CurrentPlayer = currentPlayer ?? throw new ArgumentNullException(nameof(currentPlayer));
            GameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            
            // 相手プレイヤーを設定
            OpponentPlayer = gameState.GetOpponentPlayer(currentPlayer);
            
            // 実行情報を設定
            ExecutionTime = Time.time;
            ExecutionId = Guid.NewGuid().ToString();
            RandomSeed = UnityEngine.Random.Range(0, int.MaxValue);
            
            // ゲーム情報を設定
            CurrentTurn = gameState.CurrentTurn;
            CurrentPhase = gameState.CurrentPhase;
            
            // フィールド情報を設定
            ActiveField = gameState.GetPlayerField(currentPlayer);
            OpponentField = gameState.GetPlayerField(OpponentPlayer);
            CurrentStadium = gameState.CurrentStadium;
        }

        /// <summary>
        /// パラメーター付きコンストラクタ
        /// </summary>
        public EffectContext(Card sourceCard, Player currentPlayer, GameState gameState, Dictionary<string, object> parameters)
            : this(sourceCard, currentPlayer, gameState)
        {
            Parameters = parameters ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// コピーコンストラクタ
        /// </summary>
        /// <param name="original">コピー元</param>
        public EffectContext(EffectContext original)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));

            // コアコンテキストをコピー
            SourceCard = original.SourceCard;
            CurrentPlayer = original.CurrentPlayer;
            OpponentPlayer = original.OpponentPlayer;
            GameState = original.GameState;
            ExecutionTime = original.ExecutionTime;
            ExecutionId = original.ExecutionId;

            // 対象情報をコピー
            PrimaryTarget = original.PrimaryTarget;
            Targets = new List<object>(original.Targets);
            Scope = original.Scope;
            RequiresTargetSelection = original.RequiresTargetSelection;

            // パラメーターをコピー
            Parameters = new Dictionary<string, object>(original.Parameters);
            Power = original.Power;
            Duration = original.Duration;
            ExecutionCount = original.ExecutionCount;
            RandomSeed = original.RandomSeed;

            // ゲームコンテキストをコピー
            CurrentTurn = original.CurrentTurn;
            CurrentPhase = original.CurrentPhase;
            ActiveField = original.ActiveField;
            OpponentField = original.OpponentField;
            CurrentStadium = original.CurrentStadium;

            // 実行状態をコピー
            IsExecuted = original.IsExecuted;
            IsCancelled = original.IsCancelled;
            IsReplaced = original.IsReplaced;
            ExecutionResult = original.ExecutionResult;
            ExecutionHistory = new List<EffectExecutionStep>(original.ExecutionHistory);

            // チェーン情報をコピー
            ChainPosition = original.ChainPosition;
            EffectChain = new List<ICardEffect>(original.EffectChain);
            ParentEffect = original.ParentEffect;
            ChildEffects = new List<ICardEffect>(original.ChildEffects);

            // 条件フラグをコピー
            ConditionFlags = new Dictionary<string, bool>(original.ConditionFlags);
            RestrictionCounters = new Dictionary<string, int>(original.RestrictionCounters);
            SpecialFlags = new HashSet<string>(original.SpecialFlags);
        }

        #endregion

        #region Parameter Management

        /// <summary>
        /// パラメーターを設定
        /// </summary>
        /// <typeparam name="T">値の型</typeparam>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        public void SetParameter<T>(string key, T value)
        {
            Parameters[key] = value;
        }

        /// <summary>
        /// パラメーターを取得
        /// </summary>
        /// <typeparam name="T">値の型</typeparam>
        /// <param name="key">キー</param>
        /// <param name="defaultValue">デフォルト値</param>
        /// <returns>取得された値</returns>
        public T GetParameter<T>(string key, T defaultValue = default(T))
        {
            if (Parameters.TryGetValue(key, out var value) && value is T)
            {
                return (T)value;
            }
            return defaultValue;
        }

        /// <summary>
        /// パラメーターが存在するかチェック
        /// </summary>
        /// <param name="key">キー</param>
        /// <returns>存在する場合true</returns>
        public bool HasParameter(string key)
        {
            return Parameters.ContainsKey(key);
        }

        #endregion

        #region Target Management

        /// <summary>
        /// 主要対象を設定
        /// </summary>
        /// <param name="target">対象</param>
        public void SetPrimaryTarget(object target)
        {
            PrimaryTarget = target;
            if (target != null && !Targets.Contains(target))
            {
                Targets.Add(target);
            }
        }

        /// <summary>
        /// 対象を追加
        /// </summary>
        /// <param name="target">追加する対象</param>
        public void AddTarget(object target)
        {
            if (target != null && !Targets.Contains(target))
            {
                Targets.Add(target);
                
                // 最初の対象を主要対象として設定
                if (PrimaryTarget == null)
                {
                    PrimaryTarget = target;
                }
            }
        }

        /// <summary>
        /// 複数の対象を追加
        /// </summary>
        /// <param name="targets">追加する対象リスト</param>
        public void AddTargets(IEnumerable<object> targets)
        {
            foreach (var target in targets)
            {
                AddTarget(target);
            }
        }

        /// <summary>
        /// 対象をクリア
        /// </summary>
        public void ClearTargets()
        {
            Targets.Clear();
            PrimaryTarget = null;
        }

        /// <summary>
        /// 指定型の対象を取得
        /// </summary>
        /// <typeparam name="T">対象の型</typeparam>
        /// <returns>該当する対象リスト</returns>
        public List<T> GetTargetsOfType<T>() where T : class
        {
            var result = new List<T>();
            foreach (var target in Targets)
            {
                if (target is T typedTarget)
                {
                    result.Add(typedTarget);
                }
            }
            return result;
        }

        #endregion

        #region Condition and Flag Management

        /// <summary>
        /// 条件フラグを設定
        /// </summary>
        /// <param name="key">フラグ名</param>
        /// <param name="value">値</param>
        public void SetConditionFlag(string key, bool value)
        {
            ConditionFlags[key] = value;
        }

        /// <summary>
        /// 条件フラグを取得
        /// </summary>
        /// <param name="key">フラグ名</param>
        /// <param name="defaultValue">デフォルト値</param>
        /// <returns>フラグの値</returns>
        public bool GetConditionFlag(string key, bool defaultValue = false)
        {
            return ConditionFlags.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 制限カウンターを増加
        /// </summary>
        /// <param name="key">カウンター名</param>
        /// <param name="increment">増加値</param>
        public void IncrementRestrictionCounter(string key, int increment = 1)
        {
            if (RestrictionCounters.ContainsKey(key))
            {
                RestrictionCounters[key] += increment;
            }
            else
            {
                RestrictionCounters[key] = increment;
            }
        }

        /// <summary>
        /// 制限カウンターを取得
        /// </summary>
        /// <param name="key">カウンター名</param>
        /// <returns>カウンター値</returns>
        public int GetRestrictionCounter(string key)
        {
            return RestrictionCounters.TryGetValue(key, out var value) ? value : 0;
        }

        /// <summary>
        /// 特殊フラグを追加
        /// </summary>
        /// <param name="flag">フラグ名</param>
        public void AddSpecialFlag(string flag)
        {
            SpecialFlags.Add(flag);
        }

        /// <summary>
        /// 特殊フラグが存在するかチェック
        /// </summary>
        /// <param name="flag">フラグ名</param>
        /// <returns>存在する場合true</returns>
        public bool HasSpecialFlag(string flag)
        {
            return SpecialFlags.Contains(flag);
        }

        #endregion

        #region Execution State Management

        /// <summary>
        /// 実行状態を設定
        /// </summary>
        internal void SetExecuted(bool executed = true)
        {
            IsExecuted = executed;
        }

        /// <summary>
        /// キャンセル状態を設定
        /// </summary>
        internal void SetCancelled(bool cancelled = true)
        {
            IsCancelled = cancelled;
        }

        /// <summary>
        /// 置換状態を設定
        /// </summary>
        internal void SetReplaced(bool replaced = true)
        {
            IsReplaced = replaced;
        }

        /// <summary>
        /// 実行ステップを追加
        /// </summary>
        /// <param name="step">実行ステップ</param>
        internal void AddExecutionStep(EffectExecutionStep step)
        {
            ExecutionHistory.Add(step);
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// 対象がポケモンかチェック
        /// </summary>
        /// <param name="target">チェック対象</param>
        /// <returns>ポケモンの場合true</returns>
        public bool IsTargetPokemon(object target)
        {
            return target is Card card && card.IsPokemonCard;
        }

        /// <summary>
        /// 対象がエネルギーかチェック
        /// </summary>
        /// <param name="target">チェック対象</param>
        /// <returns>エネルギーの場合true</returns>
        public bool IsTargetEnergy(object target)
        {
            return target is Card card && card.IsEnergyCard;
        }

        /// <summary>
        /// 対象がトレーナーかチェック
        /// </summary>
        /// <param name="target">チェック対象</param>
        /// <returns>トレーナーの場合true</returns>
        public bool IsTargetTrainer(object target)
        {
            return target is Card card && card.IsTrainerCard;
        }

        /// <summary>
        /// 相手のカードかチェック
        /// </summary>
        /// <param name="target">チェック対象</param>
        /// <returns>相手のカードの場合true</returns>
        public bool IsOpponentCard(object target)
        {
            return target is Card card && card.OwnerPlayerId == OpponentPlayer?.PlayerId;
        }

        /// <summary>
        /// 自分のカードかチェック
        /// </summary>
        /// <param name="target">チェック対象</param>
        /// <returns>自分のカードの場合true</returns>
        public bool IsOwnCard(object target)
        {
            return target is Card card && card.OwnerPlayerId == CurrentPlayer?.PlayerId;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// コンテキストを複製
        /// </summary>
        /// <returns>複製されたコンテキスト</returns>
        public EffectContext Clone()
        {
            return new EffectContext(this);
        }

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        /// <returns>デバッグ情報文字列</returns>
        public string GetDebugInfo()
        {
            return $"EffectContext:\n" +
                   $"  Source: {SourceCard?.CardData?.CardName ?? "None"}\n" +
                   $"  Player: {CurrentPlayer?.PlayerId ?? "None"}\n" +
                   $"  Targets: {Targets.Count}\n" +
                   $"  Power: {Power}\n" +
                   $"  Phase: {CurrentPhase}\n" +
                   $"  Turn: {CurrentTurn}\n" +
                   $"  Executed: {IsExecuted}\n" +
                   $"  Cancelled: {IsCancelled}";
        }

        /// <summary>
        /// 文字列表現
        /// </summary>
        public override string ToString()
        {
            return $"EffectContext[{SourceCard?.CardData?.CardName ?? "Unknown"}] -> {Targets.Count} targets";
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// 効果実行ステップ
    /// </summary>
    public class EffectExecutionStep
    {
        public string StepName { get; set; }
        public float Timestamp { get; set; }
        public string Description { get; set; }
        public EffectResult Result { get; set; }
        public Dictionary<string, object> StepData { get; set; } = new Dictionary<string, object>();

        public EffectExecutionStep(string stepName, string description = "")
        {
            StepName = stepName;
            Description = description;
            Timestamp = Time.time;
        }
    }

    /// <summary>
    /// 効果の範囲
    /// </summary>
    public enum EffectScope
    {
        Single = 0,     // 単体
        Multiple = 1,   // 複数
        All = 2,        // 全て
        Area = 3,       // 範囲
        Global = 4      // グローバル
    }

    /// <summary>
    /// ゲームフェーズ
    /// </summary>
    public enum GamePhase
    {
        Setup = 0,      // セットアップ
        Draw = 1,       // ドロー
        Main = 2,       // メイン
        Attack = 3,     // アタック
        End = 4         // エンド
    }

    #endregion

    #region Placeholder Classes
    // 他のクラスが未実装の場合の仮実装
    // 実際の実装時には適切なクラスに置き換える

    public class Player
    {
        public string PlayerId { get; set; }
        // 他のプレイヤー情報
    }

    public class GameState
    {
        public int CurrentTurn { get; set; }
        public GamePhase CurrentPhase { get; set; }
        public Card CurrentStadium { get; set; }

        public Player GetOpponentPlayer(Player currentPlayer) { return null; }
        public Field GetPlayerField(Player player) { return null; }
    }

    #endregion
}