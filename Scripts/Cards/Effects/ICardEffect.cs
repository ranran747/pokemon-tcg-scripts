using System;
using System.Collections.Generic;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.Cards.Effects
{
    /// <summary>
    /// カード効果の基底インターフェース
    /// 全てのカード効果で共通の機能を定義
    /// Claude拡張での新効果追加を容易にする統一API
    /// </summary>
    public interface ICardEffect
    {
        /// <summary>効果名</summary>
        string EffectName { get; }

        /// <summary>効果の説明</summary>
        string Description { get; }

        /// <summary>効果の種類</summary>
        EffectType EffectType { get; }

        /// <summary>効果の優先度（高い値ほど先に実行）</summary>
        int Priority { get; }

        /// <summary>効果が有効かどうか</summary>
        bool IsActive { get; }

        /// <summary>
        /// 効果を実行できるかチェック
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <returns>実行可能な場合true</returns>
        bool CanExecute(EffectContext context);

        /// <summary>
        /// 効果を実行
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <returns>実行結果</returns>
        EffectResult Execute(EffectContext context);

        /// <summary>
        /// 効果を元に戻す（可能な場合）
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <returns>取り消し成功した場合true</returns>
        bool Undo(EffectContext context);

        /// <summary>
        /// 効果の実行にかかる予想時間（秒）
        /// </summary>
        float EstimatedDuration { get; }
    }

    /// <summary>
    /// 対象指定が可能な効果のインターフェース
    /// </summary>
    public interface ITargetableEffect : ICardEffect
    {
        /// <summary>対象の種類</summary>
        TargetType TargetType { get; }

        /// <summary>必要な対象数</summary>
        int RequiredTargets { get; }

        /// <summary>最大対象数</summary>
        int MaxTargets { get; }

        /// <summary>
        /// 有効な対象をフィルタリング
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <param name="potentialTargets">候補対象リスト</param>
        /// <returns>有効な対象リスト</returns>
        List<object> FilterValidTargets(EffectContext context, List<object> potentialTargets);

        /// <summary>
        /// 対象が有効かチェック
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <param name="target">チェック対象</param>
        /// <returns>有効な場合true</returns>
        bool IsValidTarget(EffectContext context, object target);
    }

    /// <summary>
    /// 継続効果のインターフェース
    /// </summary>
    public interface IContinuousEffect : ICardEffect
    {
        /// <summary>効果の持続時間（-1で永続）</summary>
        int Duration { get; }

        /// <summary>残り時間</summary>
        int RemainingDuration { get; }

        /// <summary>継続効果が終了したか</summary>
        bool IsExpired { get; }

        /// <summary>
        /// 継続効果を更新（ターン終了時など）
        /// </summary>
        /// <param name="context">更新コンテキスト</param>
        void UpdateContinuousEffect(EffectContext context);

        /// <summary>
        /// 継続効果を停止
        /// </summary>
        /// <param name="context">停止コンテキスト</param>
        void StopContinuousEffect(EffectContext context);
    }

    /// <summary>
    /// 条件付き効果のインターフェース
    /// </summary>
    public interface IConditionalEffect : ICardEffect
    {
        /// <summary>
        /// 条件をチェック
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <returns>条件を満たす場合true</returns>
        bool CheckCondition(EffectContext context);

        /// <summary>条件の説明</summary>
        string ConditionDescription { get; }
    }

    /// <summary>
    /// 置換効果のインターフェース
    /// </summary>
    public interface IReplacementEffect : ICardEffect
    {
        /// <summary>置換する効果の種類</summary>
        EffectType ReplacedEffectType { get; }

        /// <summary>
        /// 効果を置換
        /// </summary>
        /// <param name="originalContext">元の効果コンテキスト</param>
        /// <param name="replacementContext">置換効果コンテキスト</param>
        /// <returns>置換結果</returns>
        EffectResult ReplaceEffect(EffectContext originalContext, EffectContext replacementContext);
    }

    /// <summary>
    /// トリガー効果のインターフェース
    /// </summary>
    public interface ITriggerEffect : ICardEffect
    {
        /// <summary>トリガー条件</summary>
        TriggerCondition TriggerCondition { get; }

        /// <summary>
        /// トリガー条件をチェック
        /// </summary>
        /// <param name="context">チェックコンテキスト</param>
        /// <param name="triggerEvent">トリガーイベント</param>
        /// <returns>トリガーする場合true</returns>
        bool CheckTrigger(EffectContext context, object triggerEvent);

        /// <summary>
        /// トリガー効果を実行
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <param name="triggerEvent">トリガーイベント</param>
        /// <returns>実行結果</returns>
        EffectResult ExecuteTrigger(EffectContext context, object triggerEvent);
    }

    /// <summary>
    /// コスト付き効果のインターフェース
    /// </summary>
    public interface ICostEffect : ICardEffect
    {
        /// <summary>
        /// コストを支払えるかチェック
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <returns>コストを支払える場合true</returns>
        bool CanPayCost(EffectContext context);

        /// <summary>
        /// コストを支払う
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <returns>コスト支払い成功した場合true</returns>
        bool PayCost(EffectContext context);

        /// <summary>コストの説明</summary>
        string CostDescription { get; }
    }

    #region Enums

    /// <summary>
    /// 効果の種類
    /// </summary>
    public enum EffectType
    {
        // 基本効果
        Damage = 0,         // ダメージ
        Heal = 1,           // 回復
        Draw = 2,           // ドロー
        Discard = 3,        // 破棄
        Search = 4,         // サーチ

        // 状態変更
        StatusChange = 10,  // 状態異常
        Buff = 11,          // 強化
        Debuff = 12,        // 弱体化
        Protection = 13,    // 保護

        // カード操作
        CardManipulation = 20, // カード操作
        DeckShuffle = 21,   // デッキシャッフル
        HandSize = 22,      // 手札サイズ変更
        EnergyAttach = 23,  // エネルギー添付

        // 場の操作
        FieldEffect = 30,   // 場の効果
        Position = 31,      // 位置変更
        Evolution = 32,     // 進化
        Switch = 33,        // 入れ替え

        // 特殊効果
        Copy = 40,          // コピー
        Transform = 41,     // 変身
        Replacement = 42,   // 置換
        Counter = 43,       // カウンター

        // メタ効果
        EffectNegate = 50,  // 効果無効
        EffectDouble = 51,  // 効果倍化
        EffectRedirect = 52, // 効果転送

        // ゲーム終了
        Win = 60,           // 勝利
        Draw_Game = 61,     // 引き分け

        // カスタム
        Custom = 99         // カスタム効果
    }

    /// <summary>
    /// 対象の種類
    /// </summary>
    public enum TargetType
    {
        None = 0,           // 対象なし
        Self = 1,           // 自分
        Opponent = 2,       // 相手
        Pokemon = 3,        // ポケモン
        Card = 4,           // カード
        Player = 5,         // プレイヤー
        Zone = 6,           // ゾーン
        Effect = 7,         // 効果
        Any = 99            // 任意
    }

    /// <summary>
    /// トリガー条件
    /// </summary>
    public enum TriggerCondition
    {
        OnPlay = 0,         // プレイ時
        OnDamage = 1,       // ダメージ時
        OnKnockOut = 2,     // きぜつ時
        OnEvolution = 3,    // 進化時
        OnAttach = 4,       // 添付時
        OnDetach = 5,       // 取り外し時
        OnTurnStart = 6,    // ターン開始時
        OnTurnEnd = 7,      // ターン終了時
        OnDraw = 8,         // ドロー時
        OnDiscard = 9,      // 破棄時
        OnSearch = 10,      // サーチ時
        OnShuffle = 11,     // シャッフル時
        Custom = 99         // カスタム条件
    }

    /// <summary>
    /// 効果の実行結果
    /// </summary>
    public enum EffectResultType
    {
        Success = 0,        // 成功
        Failed = 1,         // 失敗
        Cancelled = 2,      // キャンセル
        Replaced = 3,       // 置換された
        Blocked = 4,        // 阻止された
        Partial = 5         // 部分的成功
    }

    #endregion

    #region Result Classes

    /// <summary>
    /// 効果の実行結果
    /// </summary>
    public class EffectResult
    {
        /// <summary>実行結果の種類</summary>
        public EffectResultType ResultType { get; set; } = EffectResultType.Success;

        /// <summary>成功したかどうか</summary>
        public bool IsSuccess => ResultType == EffectResultType.Success || ResultType == EffectResultType.Partial;

        /// <summary>結果メッセージ</summary>
        public string Message { get; set; } = "";

        /// <summary>実行された値（ダメージ量など）</summary>
        public int Value { get; set; } = 0;

        /// <summary>追加データ</summary>
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

        /// <summary>実行時間</summary>
        public float ExecutionTime { get; set; } = 0f;

        /// <summary>効果の対象</summary>
        public List<object> Targets { get; set; } = new List<object>();

        /// <summary>副次効果</summary>
        public List<EffectResult> SecondaryEffects { get; set; } = new List<EffectResult>();

        /// <summary>元に戻すための情報</summary>
        public object UndoData { get; set; } = null;

        /// <summary>
        /// 成功結果を作成
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="value">値</param>
        /// <returns>成功結果</returns>
        public static EffectResult Success(string message = "", int value = 0)
        {
            return new EffectResult
            {
                ResultType = EffectResultType.Success,
                Message = message,
                Value = value
            };
        }

        /// <summary>
        /// 失敗結果を作成
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        /// <returns>失敗結果</returns>
        public static EffectResult Failed(string message)
        {
            return new EffectResult
            {
                ResultType = EffectResultType.Failed,
                Message = message
            };
        }

        /// <summary>
        /// キャンセル結果を作成
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <returns>キャンセル結果</returns>
        public static EffectResult Cancelled(string message = "Effect was cancelled")
        {
            return new EffectResult
            {
                ResultType = EffectResultType.Cancelled,
                Message = message
            };
        }

        /// <summary>
        /// 部分的成功結果を作成
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="value">実行された値</param>
        /// <returns>部分的成功結果</returns>
        public static EffectResult Partial(string message, int value)
        {
            return new EffectResult
            {
                ResultType = EffectResultType.Partial,
                Message = message,
                Value = value
            };
        }

        /// <summary>
        /// デバッグ用文字列表現
        /// </summary>
        public override string ToString()
        {
            return $"[{ResultType}] {Message} (Value: {Value})";
        }
    }

    #endregion

    #region Base Classes

    /// <summary>
    /// カード効果の基底抽象クラス
    /// 共通機能の実装を提供
    /// </summary>
    public abstract class BaseCardEffect : ICardEffect
    {
        #region Properties

        public abstract string EffectName { get; }
        public abstract string Description { get; }
        public abstract EffectType EffectType { get; }
        public virtual int Priority => 0;
        public virtual bool IsActive => true;
        public virtual float EstimatedDuration => 0.1f;

        #endregion

        #region Abstract Methods

        public abstract bool CanExecute(EffectContext context);
        public abstract EffectResult Execute(EffectContext context);

        #endregion

        #region Virtual Methods

        public virtual bool Undo(EffectContext context)
        {
            // デフォルトでは元に戻せない
            return false;
        }

        #endregion

        #region Protected Helper Methods

        /// <summary>
        /// ログメッセージを出力
        /// </summary>
        /// <param name="message">メッセージ</param>
        protected virtual void LogEffect(string message)
        {
            Debug.Log($"[{EffectName}] {message}");
        }

        /// <summary>
        /// エラーログを出力
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        protected virtual void LogError(string message)
        {
            Debug.LogError($"[{EffectName}] ERROR: {message}");
        }

        /// <summary>
        /// コンテキストの有効性をチェック
        /// </summary>
        /// <param name="context">チェック対象</param>
        /// <returns>有効な場合true</returns>
        protected virtual bool ValidateContext(EffectContext context)
        {
            if (context == null)
            {
                LogError("Context is null");
                return false;
            }

            if (context.SourceCard == null)
            {
                LogError("Source card is null");
                return false;
            }

            return true;
        }

        #endregion

        #region Utility Methods

        public override string ToString()
        {
            return $"{EffectName} ({EffectType})";
        }

        #endregion
    }

    #endregion
}