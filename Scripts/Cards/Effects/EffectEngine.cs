using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.Cards.Effects
{
    /// <summary>
    /// エフェクト実行エンジン
    /// カード効果の登録、管理、実行を統括
    /// 効果チェーン、置換効果、トリガー効果の処理を行う
    /// </summary>
    public class EffectEngine : MonoBehaviourSingleton<EffectEngine>
    {
        #region Fields

        [Header("エンジン設定")]
        [SerializeField] private int _maxEffectChainLength = 50;
        [SerializeField] private float _effectExecutionTimeout = 10f;
        [SerializeField] private bool _enableEffectAnimation = true;
        [SerializeField] private bool _allowEffectCancellation = true;

        [Header("デバッグ")]
        [SerializeField] private bool _logEffectExecution = true;
        [SerializeField] private bool _validateEffectChain = true;
        [SerializeField] private bool _enableStatistics = true;

        // 効果管理
        private Dictionary<string, ICardEffect> _registeredEffects = new Dictionary<string, ICardEffect>();
        private Queue<EffectContext> _effectQueue = new Queue<EffectContext>();
        private Stack<EffectContext> _effectStack = new Stack<EffectContext>();
        private List<IContinuousEffect> _continuousEffects = new List<IContinuousEffect>();
        private List<ITriggerEffect> _triggerEffects = new List<ITriggerEffect>();
        private List<IReplacementEffect> _replacementEffects = new List<IReplacementEffect>();

        // 実行制御
        private bool _isExecutingEffects = false;
        private EffectContext _currentExecutingContext = null;
        private Coroutine _effectExecutionCoroutine = null;
        private Dictionary<string, DateTime> _effectCooldowns = new Dictionary<string, DateTime>();

        // 統計情報
        private EffectEngineStatistics _statistics = new EffectEngineStatistics();

        // イベント
        public event Action<ICardEffect, EffectContext> OnEffectQueued;
        public event Action<ICardEffect, EffectContext> OnEffectExecuted;
        public event Action<ICardEffect, EffectContext, EffectResult> OnEffectCompleted;
        public event Action<ICardEffect, EffectContext> OnEffectCancelled;
        public event Action<EffectEngine> OnEffectChainCompleted;

        #endregion

        #region Properties

        /// <summary>初期化順序</summary>
        public override int InitializationOrder => -700;

        /// <summary>登録されたエフェクト数</summary>
        public int RegisteredEffectCount => _registeredEffects.Count;

        /// <summary>キューに入っているエフェクト数</summary>
        public int QueuedEffectCount => _effectQueue.Count;

        /// <summary>継続効果の数</summary>
        public int ContinuousEffectCount => _continuousEffects.Count;

        /// <summary>エフェクトを実行中か</summary>
        public bool IsExecutingEffects => _isExecutingEffects;

        /// <summary>現在実行中のコンテキスト</summary>
        public EffectContext CurrentExecutingContext => _currentExecutingContext;

        /// <summary>統計情報</summary>
        public EffectEngineStatistics Statistics => _statistics;

        /// <summary>最大効果チェーン長</summary>
        public int MaxEffectChainLength => _maxEffectChainLength;

        #endregion

        #region Unity Lifecycle

        protected override void OnInitialize()
        {
            InitializeEngine();
            RegisterDefaultEffects();
            
            Debug.Log("[EffectEngine] Effect Engine initialized");
        }

        protected override void OnDispose()
        {
            StopAllEffects();
            ClearAllEffects();
            
            Debug.Log("[EffectEngine] Effect Engine disposed");
        }

        private void Update()
        {
            UpdateContinuousEffects();
            UpdateEffectCooldowns();
            
            if (_enableStatistics)
            {
                _statistics.UpdatePerformanceMetrics();
            }
        }

        #endregion

        #region Engine Initialization

        /// <summary>
        /// エンジンを初期化
        /// </summary>
        private void InitializeEngine()
        {
            _registeredEffects = new Dictionary<string, ICardEffect>();
            _effectQueue = new Queue<EffectContext>();
            _effectStack = new Stack<EffectContext>();
            _continuousEffects = new List<IContinuousEffect>();
            _triggerEffects = new List<ITriggerEffect>();
            _replacementEffects = new List<IReplacementEffect>();
            _effectCooldowns = new Dictionary<string, DateTime>();
            _statistics = new EffectEngineStatistics();
        }

        /// <summary>
        /// デフォルトエフェクトを登録
        /// </summary>
        private void RegisterDefaultEffects()
        {
            // 基本エフェクトの登録は後で実装
            // RegisterEffect(new DamageEffect());
            // RegisterEffect(new HealEffect());
            // RegisterEffect(new DrawEffect());
            // RegisterEffect(new SearchEffect());
        }

        #endregion

        #region Effect Registration

        /// <summary>
        /// エフェクトを登録
        /// </summary>
        /// <param name="effect">登録するエフェクト</param>
        /// <returns>登録成功した場合true</returns>
        public bool RegisterEffect(ICardEffect effect)
        {
            if (effect == null)
            {
                LogError("Cannot register null effect");
                return false;
            }

            string effectKey = GetEffectKey(effect);
            
            if (_registeredEffects.ContainsKey(effectKey))
            {
                LogWarning($"Effect {effectKey} is already registered. Overwriting...");
            }

            _registeredEffects[effectKey] = effect;

            // 特殊効果タイプを専用リストに追加
            RegisterSpecialEffectType(effect);

            if (_logEffectExecution)
            {
                Debug.Log($"[EffectEngine] Registered effect: {effectKey}");
            }

            return true;
        }

        /// <summary>
        /// エフェクトの登録を解除
        /// </summary>
        /// <param name="effect">解除するエフェクト</param>
        /// <returns>解除成功した場合true</returns>
        public bool UnregisterEffect(ICardEffect effect)
        {
            if (effect == null)
                return false;

            string effectKey = GetEffectKey(effect);
            bool removed = _registeredEffects.Remove(effectKey);

            if (removed)
            {
                // 特殊効果タイプからも削除
                UnregisterSpecialEffectType(effect);
                
                if (_logEffectExecution)
                {
                    Debug.Log($"[EffectEngine] Unregistered effect: {effectKey}");
                }
            }

            return removed;
        }

        /// <summary>
        /// 特殊効果タイプを専用リストに登録
        /// </summary>
        /// <param name="effect">エフェクト</param>
        private void RegisterSpecialEffectType(ICardEffect effect)
        {
            if (effect is IContinuousEffect continuousEffect)
            {
                if (!_continuousEffects.Contains(continuousEffect))
                {
                    _continuousEffects.Add(continuousEffect);
                }
            }

            if (effect is ITriggerEffect triggerEffect)
            {
                if (!_triggerEffects.Contains(triggerEffect))
                {
                    _triggerEffects.Add(triggerEffect);
                }
            }

            if (effect is IReplacementEffect replacementEffect)
            {
                if (!_replacementEffects.Contains(replacementEffect))
                {
                    _replacementEffects.Add(replacementEffect);
                }
            }
        }

        /// <summary>
        /// 特殊効果タイプの登録を解除
        /// </summary>
        /// <param name="effect">エフェクト</param>
        private void UnregisterSpecialEffectType(ICardEffect effect)
        {
            if (effect is IContinuousEffect continuousEffect)
            {
                _continuousEffects.Remove(continuousEffect);
            }

            if (effect is ITriggerEffect triggerEffect)
            {
                _triggerEffects.Remove(triggerEffect);
            }

            if (effect is IReplacementEffect replacementEffect)
            {
                _replacementEffects.Remove(replacementEffect);
            }
        }

        /// <summary>
        /// エフェクトキーを生成
        /// </summary>
        /// <param name="effect">エフェクト</param>
        /// <returns>エフェクトキー</returns>
        private string GetEffectKey(ICardEffect effect)
        {
            return $"{effect.GetType().Name}_{effect.EffectName}";
        }

        #endregion

        #region Effect Execution

        /// <summary>
        /// エフェクトを実行
        /// </summary>
        /// <param name="effect">実行するエフェクト</param>
        /// <param name="context">実行コンテキスト</param>
        /// <returns>実行結果</returns>
        public EffectResult ExecuteEffect(ICardEffect effect, EffectContext context)
        {
            if (effect == null || context == null)
            {
                return EffectResult.Failed("Effect or context is null");
            }

            // クールダウンチェック
            if (IsEffectOnCooldown(effect))
            {
                return EffectResult.Failed("Effect is on cooldown");
            }

            // 置換効果をチェック
            var replacedEffect = CheckReplacementEffects(effect, context);
            if (replacedEffect != null)
            {
                effect = replacedEffect;
                context.SetReplaced(true);
            }

            // エフェクトをキューに追加
            QueueEffect(effect, context);

            // キューが空でない場合は処理を開始
            if (!_isExecutingEffects)
            {
                StartEffectExecution();
            }

            return context.ExecutionResult ?? EffectResult.Success("Effect queued for execution");
        }

        /// <summary>
        /// エフェクトをキューに追加
        /// </summary>
        /// <param name="effect">エフェクト</param>
        /// <param name="context">コンテキスト</param>
        private void QueueEffect(ICardEffect effect, EffectContext context)
        {
            if (_effectQueue.Count >= _maxEffectChainLength)
            {
                LogError($"Effect queue overflow! Maximum chain length ({_maxEffectChainLength}) exceeded");
                context.SetCancelled(true);
                return;
            }

            _effectQueue.Enqueue(context);
            OnEffectQueued?.Invoke(effect, context);

            if (_logEffectExecution)
            {
                Debug.Log($"[EffectEngine] Queued effect: {effect.EffectName} (Queue size: {_effectQueue.Count})");
            }
        }

        /// <summary>
        /// エフェクト実行を開始
        /// </summary>
        private void StartEffectExecution()
        {
            if (_isExecutingEffects)
                return;

            _effectExecutionCoroutine = StartCoroutine(ProcessEffectQueue());
        }

        /// <summary>
        /// エフェクトキューを処理
        /// </summary>
        /// <returns>コルーチン</returns>
        private IEnumerator ProcessEffectQueue()
        {
            _isExecutingEffects = true;
            _statistics.StartExecution();

            while (_effectQueue.Count > 0)
            {
                var context = _effectQueue.Dequeue();
                if (context.IsCancelled)
                    continue;

                yield return StartCoroutine(ExecuteEffectInternal(context));

                // フレーム間で一時停止（パフォーマンス考慮）
                yield return null;
            }

            _isExecutingEffects = false;
            _currentExecutingContext = null;
            _statistics.EndExecution();

            OnEffectChainCompleted?.Invoke(this);

            if (_logEffectExecution)
            {
                Debug.Log("[EffectEngine] Effect chain completed");
            }
        }

        /// <summary>
        /// エフェクトの内部実行処理
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <returns>コルーチン</returns>
        private IEnumerator ExecuteEffectInternal(EffectContext context)
        {
            _currentExecutingContext = context;
            var effect = GetEffectFromContext(context);
            
            if (effect == null)
            {
                context.ExecutionResult = EffectResult.Failed("Effect not found");
                yield break;
            }

            // 実行前チェック
            if (!effect.CanExecute(context))
            {
                context.ExecutionResult = EffectResult.Failed("Effect cannot be executed");
                yield break;
            }

            // トリガー効果をチェック
            CheckTriggerEffects(TriggerCondition.OnPlay, context);

            var executionStartTime = Time.time;
            EffectResult result = null;
            bool hasError = false;
            string errorMessage = "";

            // エフェクト実行（try-catchブロック）
            try
            {
                // エフェクトを実行
                context.SetExecuted(true);
                OnEffectExecuted?.Invoke(effect, context);

                result = effect.Execute(context);
                context.ExecutionResult = result;

                // 統計更新
                _statistics.RecordExecution(effect, Time.time - executionStartTime, result.IsSuccess);

                // クールダウン設定
                SetEffectCooldown(effect);

                OnEffectCompleted?.Invoke(effect, context, result);

                if (_logEffectExecution)
                {
                    Debug.Log($"[EffectEngine] Executed effect: {effect.EffectName} -> {result}");
                }
            }
            catch (Exception ex)
            {
                result = EffectResult.Failed($"Effect execution error: {ex.Message}");
                context.ExecutionResult = result;
                hasError = true;
                errorMessage = $"Error executing effect {effect.EffectName}: {ex.Message}";
            }

            // エラーログはyieldの外で実行
            if (hasError)
            {
                LogError(errorMessage);
            }

            // アニメーション待機（yieldはtry-catchの外）
            if (_enableEffectAnimation && result != null && result.IsSuccess)
            {
                yield return new WaitForSeconds(effect.EstimatedDuration);
            }

            // 実行タイムアウトチェック
            if (Time.time - executionStartTime > _effectExecutionTimeout)
            {
                LogWarning($"Effect {effect.EffectName} execution timeout ({_effectExecutionTimeout}s)");
            }
        }

        /// <summary>
        /// コンテキストからエフェクトを取得
        /// </summary>
        /// <param name="context">コンテキスト</param>
        /// <returns>エフェクト</returns>
        private ICardEffect GetEffectFromContext(EffectContext context)
        {
            // TODO: コンテキストに格納されたエフェクト情報から適切なエフェクトを取得
            // 現在は仮実装
            return _registeredEffects.Values.FirstOrDefault();
        }

        #endregion

        #region Effect Cancellation

        /// <summary>
        /// エフェクト実行をキャンセル
        /// </summary>
        /// <param name="context">キャンセルするコンテキスト</param>
        /// <returns>キャンセル成功した場合true</returns>
        public bool CancelEffect(EffectContext context)
        {
            if (!_allowEffectCancellation || context == null)
                return false;

            context.SetCancelled(true);
            
            var effect = GetEffectFromContext(context);
            if (effect != null)
            {
                OnEffectCancelled?.Invoke(effect, context);
                
                if (_logEffectExecution)
                {
                    Debug.Log($"[EffectEngine] Cancelled effect: {effect.EffectName}");
                }
            }

            return true;
        }

        /// <summary>
        /// 全てのエフェクトを停止
        /// </summary>
        public void StopAllEffects()
        {
            // キューをクリア
            while (_effectQueue.Count > 0)
            {
                var context = _effectQueue.Dequeue();
                CancelEffect(context);
            }

            // 実行中のコルーチンを停止
            if (_effectExecutionCoroutine != null)
            {
                StopCoroutine(_effectExecutionCoroutine);
                _effectExecutionCoroutine = null;
            }

            _isExecutingEffects = false;
            _currentExecutingContext = null;

            Debug.Log("[EffectEngine] Stopped all effects");
        }

        #endregion

        #region Continuous Effects

        /// <summary>
        /// 継続効果を更新
        /// </summary>
        private void UpdateContinuousEffects()
        {
            var expiredEffects = new List<IContinuousEffect>();

            foreach (var effect in _continuousEffects)
            {
                if (effect.IsExpired)
                {
                    expiredEffects.Add(effect);
                }
                else
                {
                    try
                    {
                        // TODO: 適切なコンテキストを作成
                        // effect.UpdateContinuousEffect(context);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error updating continuous effect {effect.EffectName}: {ex.Message}");
                    }
                }
            }

            // 期限切れの効果を削除
            foreach (var expiredEffect in expiredEffects)
            {
                _continuousEffects.Remove(expiredEffect);
                if (_logEffectExecution)
                {
                    Debug.Log($"[EffectEngine] Expired continuous effect: {expiredEffect.EffectName}");
                }
            }
        }

        #endregion

        #region Trigger Effects

        /// <summary>
        /// トリガー効果をチェック
        /// </summary>
        /// <param name="triggerCondition">トリガー条件</param>
        /// <param name="context">コンテキスト</param>
        public void CheckTriggerEffects(TriggerCondition triggerCondition, EffectContext context)
        {
            foreach (var triggerEffect in _triggerEffects)
            {
                if (triggerEffect.TriggerCondition == triggerCondition)
                {
                    try
                    {
                        if (triggerEffect.CheckTrigger(context, triggerCondition))
                        {
                            var triggerResult = triggerEffect.ExecuteTrigger(context, triggerCondition);
                            
                            if (_logEffectExecution)
                            {
                                Debug.Log($"[EffectEngine] Triggered effect: {triggerEffect.EffectName} -> {triggerResult}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error checking trigger effect {triggerEffect.EffectName}: {ex.Message}");
                    }
                }
            }
        }

        #endregion

        #region Replacement Effects

        /// <summary>
        /// 置換効果をチェック
        /// </summary>
        /// <param name="originalEffect">元のエフェクト</param>
        /// <param name="context">コンテキスト</param>
        /// <returns>置換されたエフェクト（なければnull）</returns>
        private ICardEffect CheckReplacementEffects(ICardEffect originalEffect, EffectContext context)
        {
            foreach (var replacementEffect in _replacementEffects)
            {
                if (replacementEffect.ReplacedEffectType == originalEffect.EffectType)
                {
                    try
                    {
                        if (replacementEffect.CanExecute(context))
                        {
                            var replacementContext = context.Clone();
                            var result = replacementEffect.ReplaceEffect(context, replacementContext);
                            
                            if (result.IsSuccess)
                            {
                                if (_logEffectExecution)
                                {
                                    Debug.Log($"[EffectEngine] Replaced effect: {originalEffect.EffectName} -> {replacementEffect.EffectName}");
                                }
                                return replacementEffect;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error processing replacement effect {replacementEffect.EffectName}: {ex.Message}");
                    }
                }
            }

            return null;
        }

        #endregion

        #region Cooldown Management

        /// <summary>
        /// エフェクトがクールダウン中かチェック
        /// </summary>
        /// <param name="effect">エフェクト</param>
        /// <returns>クールダウン中の場合true</returns>
        private bool IsEffectOnCooldown(ICardEffect effect)
        {
            string effectKey = GetEffectKey(effect);
            
            if (_effectCooldowns.TryGetValue(effectKey, out var cooldownEnd))
            {
                return DateTime.Now < cooldownEnd;
            }

            return false;
        }

        /// <summary>
        /// エフェクトのクールダウンを設定
        /// </summary>
        /// <param name="effect">エフェクト</param>
        /// <param name="cooldownSeconds">クールダウン時間（秒）</param>
        private void SetEffectCooldown(ICardEffect effect, float cooldownSeconds = 0.1f)
        {
            string effectKey = GetEffectKey(effect);
            _effectCooldowns[effectKey] = DateTime.Now.AddSeconds(cooldownSeconds);
        }

        /// <summary>
        /// エフェクトクールダウンを更新
        /// </summary>
        private void UpdateEffectCooldowns()
        {
            var now = DateTime.Now;
            var expiredKeys = _effectCooldowns
                .Where(kvp => kvp.Value <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _effectCooldowns.Remove(key);
            }
        }

        #endregion

        #region Cleanup and Management

        /// <summary>
        /// 全ての効果をクリア
        /// </summary>
        public void ClearAllEffects()
        {
            StopAllEffects();
            
            _registeredEffects.Clear();
            _continuousEffects.Clear();
            _triggerEffects.Clear();
            _replacementEffects.Clear();
            _effectCooldowns.Clear();
            
            Debug.Log("[EffectEngine] Cleared all effects");
        }

        /// <summary>
        /// 期限切れの継続効果をクリア
        /// </summary>
        public void ClearExpiredContinuousEffects()
        {
            var expiredEffects = _continuousEffects.Where(effect => effect.IsExpired).ToList();
            
            foreach (var effect in expiredEffects)
            {
                _continuousEffects.Remove(effect);
            }
            
            if (expiredEffects.Count > 0 && _logEffectExecution)
            {
                Debug.Log($"[EffectEngine] Cleared {expiredEffects.Count} expired continuous effects");
            }
        }

        #endregion

        #region Static Access Methods

        /// <summary>
        /// 静的メソッド: エフェクト実行
        /// </summary>
        public static EffectResult Execute(ICardEffect effect, EffectContext context)
        {
            return Instance?.ExecuteEffect(effect, context) ?? EffectResult.Failed("EffectEngine not available");
        }

        /// <summary>
        /// 静的メソッド: エフェクト登録
        /// </summary>
        public static bool Register(ICardEffect effect)
        {
            return Instance?.RegisterEffect(effect) ?? false;
        }

        /// <summary>
        /// 静的メソッド: トリガー効果チェック
        /// </summary>
        public static void CheckTriggers(TriggerCondition condition, EffectContext context)
        {
            Instance?.CheckTriggerEffects(condition, context);
        }

        #endregion

        #region Logging

        /// <summary>
        /// ログメッセージを出力
        /// </summary>
        /// <param name="message">メッセージ</param>
        private void LogError(string message)
        {
            Debug.LogError($"[EffectEngine] {message}");
        }

        /// <summary>
        /// 警告ログを出力
        /// </summary>
        /// <param name="message">メッセージ</param>
        private void LogWarning(string message)
        {
            Debug.LogWarning($"[EffectEngine] {message}");
        }

        #endregion

        #region Debug and Statistics

        /// <summary>
        /// 統計情報をログ出力
        /// </summary>
        [ContextMenu("Log Statistics")]
        public void LogStatistics()
        {
            Debug.Log("=== EffectEngine Statistics ===");
            Debug.Log($"Registered Effects: {RegisteredEffectCount}");
            Debug.Log($"Queued Effects: {QueuedEffectCount}");
            Debug.Log($"Continuous Effects: {ContinuousEffectCount}");
            Debug.Log($"Total Executions: {_statistics.TotalExecutions}");
            Debug.Log($"Successful Executions: {_statistics.SuccessfulExecutions}");
            Debug.Log($"Failed Executions: {_statistics.FailedExecutions}");
            Debug.Log($"Average Execution Time: {_statistics.AverageExecutionTime:F3}s");
        }

        #endregion
    }

    #region Statistics Class

    /// <summary>
    /// エフェクトエンジン統計情報
    /// </summary>
    [System.Serializable]
    public class EffectEngineStatistics
    {
        public int TotalExecutions = 0;
        public int SuccessfulExecutions = 0;
        public int FailedExecutions = 0;
        public float TotalExecutionTime = 0f;
        public float AverageExecutionTime => TotalExecutions > 0 ? TotalExecutionTime / TotalExecutions : 0f;
        public int CurrentQueueSize = 0;
        public int MaxQueueSize = 0;
        public DateTime LastExecutionTime = DateTime.MinValue;
        public bool IsCurrentlyExecuting = false;
        
        private float _executionStartTime = 0f;

        /// <summary>
        /// 実行記録
        /// </summary>
        /// <param name="effect">実行されたエフェクト</param>
        /// <param name="executionTime">実行時間</param>
        /// <param name="success">成功したか</param>
        public void RecordExecution(ICardEffect effect, float executionTime, bool success)
        {
            TotalExecutions++;
            TotalExecutionTime += executionTime;
            LastExecutionTime = DateTime.Now;
            
            if (success)
                SuccessfulExecutions++;
            else
                FailedExecutions++;
        }

        /// <summary>
        /// 実行開始
        /// </summary>
        public void StartExecution()
        {
            IsCurrentlyExecuting = true;
            _executionStartTime = Time.time;
        }

        /// <summary>
        /// 実行終了
        /// </summary>
        public void EndExecution()
        {
            IsCurrentlyExecuting = false;
        }

        /// <summary>
        /// パフォーマンスメトリクスを更新
        /// </summary>
        public void UpdatePerformanceMetrics()
        {
            // パフォーマンス関連の更新処理
        }
    }

    #endregion
}