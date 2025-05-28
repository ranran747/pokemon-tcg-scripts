using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.Core.Data
{
    /// <summary>
    /// データ検証システム
    /// ScriptableObjectデータの整合性チェックとバリデーション
    /// Claude拡張での新データ型の検証にも対応
    /// </summary>
    public class DataValidator : Singleton<DataValidator>
    {
        #region Fields

        /// <summary>登録されたバリデーター</summary>
        private readonly Dictionary<Type, List<IDataValidator>> _validators = new Dictionary<Type, List<IDataValidator>>();
        
        /// <summary>検証結果キャッシュ</summary>
        private readonly Dictionary<string, ValidationResult> _validationCache = new Dictionary<string, ValidationResult>();
        
        /// <summary>検証統計</summary>
        private readonly ValidationStatistics _statistics = new ValidationStatistics();

        /// <summary>キャッシュの有効期限（秒）</summary>
        private const float CACHE_EXPIRY_TIME = 300f; // 5分

        #endregion

        #region Properties

        /// <summary>検証統計情報</summary>
        public ValidationStatistics Statistics => _statistics;

        /// <summary>初期化順序</summary>
        public override int InitializationOrder => -500;

        #endregion

        #region Validator Registration

        /// <summary>
        /// データバリデーターを登録
        /// </summary>
        /// <typeparam name="T">検証対象データ型</typeparam>
        /// <param name="validator">バリデーター</param>
        public void RegisterValidator<T>(IDataValidator<T> validator) where T : ScriptableObject
        {
            Type dataType = typeof(T);
            
            if (!_validators.ContainsKey(dataType))
            {
                _validators[dataType] = new List<IDataValidator>();
            }
            
            _validators[dataType].Add(validator);
            
            Debug.Log($"[DataValidator] Registered validator for {dataType.Name}");
        }

        /// <summary>
        /// バリデーターの登録を解除
        /// </summary>
        /// <typeparam name="T">検証対象データ型</typeparam>
        /// <param name="validator">バリデーター</param>
        public void UnregisterValidator<T>(IDataValidator<T> validator) where T : ScriptableObject
        {
            Type dataType = typeof(T);
            
            if (_validators.TryGetValue(dataType, out var validatorList))
            {
                validatorList.Remove(validator);
                
                if (validatorList.Count == 0)
                {
                    _validators.Remove(dataType);
                }
            }
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// 単一オブジェクトの検証
        /// </summary>
        /// <param name="data">検証対象データ</param>
        /// <param name="useCache">キャッシュを使用するか</param>
        /// <returns>検証結果</returns>
        public ValidationResult ValidateObject(ScriptableObject data, bool useCache = true)
        {
            if (data == null)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Data object is null" }
                };
            }

            string cacheKey = GetCacheKey(data);
            
            // キャッシュチェック
            if (useCache && _validationCache.TryGetValue(cacheKey, out var cachedResult))
            {
                if (Time.time - cachedResult.Timestamp < CACHE_EXPIRY_TIME)
                {
                    _statistics.CacheHits++;
                    return cachedResult;
                }
                else
                {
                    _validationCache.Remove(cacheKey);
                }
            }

            // 検証実行
            ValidationResult result = PerformValidation(data);
            
            // キャッシュに保存
            if (useCache)
            {
                _validationCache[cacheKey] = result;
            }

            // 統計更新
            _statistics.TotalValidations++;
            if (result.IsValid)
            {
                _statistics.SuccessfulValidations++;
            }
            else
            {
                _statistics.FailedValidations++;
            }

            return result;
        }

        /// <summary>
        /// 複数オブジェクトの一括検証
        /// </summary>
        /// <param name="dataObjects">検証対象データリスト</param>
        /// <param name="stopOnFirstError">最初のエラーで停止するか</param>
        /// <returns>一括検証結果</returns>
        public BatchValidationResult ValidateBatch(IEnumerable<ScriptableObject> dataObjects, bool stopOnFirstError = false)
        {
            var batchResult = new BatchValidationResult();
            var validationTasks = new List<ValidationResult>();

            foreach (var data in dataObjects)
            {
                try
                {
                    ValidationResult result = ValidateObject(data);
                    validationTasks.Add(result);
                    
                    batchResult.Results[data.name] = result;
                    
                    if (!result.IsValid)
                    {
                        batchResult.TotalErrors += result.Errors.Count;
                        batchResult.TotalWarnings += result.Warnings.Count;
                        
                        if (stopOnFirstError)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    var errorResult = new ValidationResult
                    {
                        IsValid = false,
                        Errors = new List<string> { $"Validation exception: {ex.Message}" }
                    };
                    
                    batchResult.Results[data.name] = errorResult;
                    batchResult.TotalErrors++;
                }
            }

            batchResult.IsAllValid = batchResult.Results.Values.All(r => r.IsValid);
            batchResult.ProcessedCount = batchResult.Results.Count;
            
            return batchResult;
        }

        /// <summary>
        /// 型別の検証
        /// </summary>
        /// <typeparam name="T">検証対象データ型</typeparam>
        /// <param name="data">検証対象データ</param>
        /// <returns>検証結果</returns>
        public ValidationResult ValidateTyped<T>(T data) where T : ScriptableObject
        {
            Type dataType = typeof(T);
            
            if (!_validators.TryGetValue(dataType, out var validatorList))
            {
                // デフォルト検証
                return ValidateDefault(data);
            }

            var result = new ValidationResult();
            
            foreach (var validator in validatorList)
            {
                if (validator is IDataValidator<T> typedValidator)
                {
                    try
                    {
                        ValidationResult validatorResult = typedValidator.Validate(data);
                        
                        result.Errors.AddRange(validatorResult.Errors);
                        result.Warnings.AddRange(validatorResult.Warnings);
                        result.Info.AddRange(validatorResult.Info);
                        
                        if (!validatorResult.IsValid)
                        {
                            result.IsValid = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Validator exception: {ex.Message}");
                    }
                }
            }

            return result;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 実際の検証処理
        /// </summary>
        /// <param name="data">検証対象データ</param>
        /// <returns>検証結果</returns>
        private ValidationResult PerformValidation(ScriptableObject data)
        {
            Type dataType = data.GetType();
            
            // カスタムバリデーターによる検証
            if (_validators.TryGetValue(dataType, out var validatorList))
            {
                var result = new ValidationResult();
                
                foreach (var validator in validatorList)
                {
                    try
                    {
                        ValidationResult validatorResult = validator.ValidateObject(data);
                        
                        result.Errors.AddRange(validatorResult.Errors);
                        result.Warnings.AddRange(validatorResult.Warnings);
                        result.Info.AddRange(validatorResult.Info);
                        
                        if (!validatorResult.IsValid)
                        {
                            result.IsValid = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Validator exception in {validator.GetType().Name}: {ex.Message}");
                    }
                }
                
                return result;
            }

            // デフォルト検証
            return ValidateDefault(data);
        }

        /// <summary>
        /// デフォルト検証
        /// </summary>
        /// <param name="data">検証対象データ</param>
        /// <returns>検証結果</returns>
        private ValidationResult ValidateDefault(ScriptableObject data)
        {
            var result = new ValidationResult();
            
            // 基本チェック
            if (data == null)
            {
                result.IsValid = false;
                result.Errors.Add("Data is null");
                return result;
            }

            if (string.IsNullOrEmpty(data.name))
            {
                result.Warnings.Add("Object name is empty");
            }

            // BaseCardData固有の検証
            if (data is BaseCardData cardData)
            {
                if (!cardData.IsValid())
                {
                    result.IsValid = false;
                    result.Errors.AddRange(cardData.GetValidationErrors());
                }
            }

            // BaseRuleData固有の検証
            if (data is BaseRuleData ruleData)
            {
                if (!ruleData.IsValid())
                {
                    result.IsValid = false;
                    result.Errors.AddRange(ruleData.GetValidationErrors());
                }
            }

            return result;
        }

        /// <summary>
        /// キャッシュキーを生成
        /// </summary>
        /// <param name="data">データオブジェクト</param>
        /// <returns>キャッシュキー</returns>
        private string GetCacheKey(ScriptableObject data)
        {
            // オブジェクトの内容を基にしたハッシュキー
            int hash = data.GetHashCode();
            
            if (data is BaseCardData cardData)
            {
                hash ^= cardData.GetContentHash();
            }
            
            return $"{data.GetType().Name}_{data.name}_{hash}";
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// キャッシュをクリア
        /// </summary>
        public void ClearCache()
        {
            _validationCache.Clear();
            Debug.Log("[DataValidator] Cache cleared");
        }

        /// <summary>
        /// 期限切れキャッシュを削除
        /// </summary>
        public void CleanupExpiredCache()
        {
            var expiredKeys = _validationCache
                .Where(kvp => Time.time - kvp.Value.Timestamp > CACHE_EXPIRY_TIME)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _validationCache.Remove(key);
            }

            if (expiredKeys.Count > 0)
            {
                Debug.Log($"[DataValidator] Cleaned up {expiredKeys.Count} expired cache entries");
            }
        }

        #endregion

        #region Unity Lifecycle

        protected override void OnInitialize()
        {
            Debug.Log("[DataValidator] DataValidator initialized");
            
            // デフォルトバリデーターの登録
            RegisterDefaultValidators();
        }

        protected override void OnDispose()
        {
            _validators.Clear();
            _validationCache.Clear();
            Debug.Log("[DataValidator] DataValidator disposed");
        }

        #endregion

        #region Default Validators

        /// <summary>
        /// デフォルトバリデーターを登録
        /// </summary>
        private void RegisterDefaultValidators()
        {
            // カードデータ用バリデーター
            RegisterValidator<BaseCardData>(new CardDataValidator());
            
            // ルールデータ用バリデーター
            RegisterValidator<BaseRuleData>(new RuleDataValidator());
        }

        #endregion
    }

    #region Validation Interfaces

    /// <summary>
    /// データバリデーターの基底インターフェース
    /// </summary>
    public interface IDataValidator
    {
        ValidationResult ValidateObject(ScriptableObject data);
    }

    /// <summary>
    /// 型付きデータバリデーターインターフェース
    /// </summary>
    /// <typeparam name="T">検証対象データ型</typeparam>
    public interface IDataValidator<T> : IDataValidator where T : ScriptableObject
    {
        ValidationResult Validate(T data);
    }

    #endregion

    #region Validation Results

    /// <summary>
    /// 検証結果
    /// </summary>
    [System.Serializable]
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Info { get; set; } = new List<string>();
        public float Timestamp { get; set; } = Time.time;

        public bool HasErrors => Errors.Count > 0;
        public bool HasWarnings => Warnings.Count > 0;
        public int TotalIssues => Errors.Count + Warnings.Count;
    }

    /// <summary>
    /// 一括検証結果
    /// </summary>
    public class BatchValidationResult
    {
        public Dictionary<string, ValidationResult> Results { get; set; } = new Dictionary<string, ValidationResult>();
        public bool IsAllValid { get; set; } = true;
        public int ProcessedCount { get; set; } = 0;
        public int TotalErrors { get; set; } = 0;
        public int TotalWarnings { get; set; } = 0;
    }

    /// <summary>
    /// 検証統計情報
    /// </summary>
    [System.Serializable]
    public class ValidationStatistics
    {
        public int TotalValidations { get; set; } = 0;
        public int SuccessfulValidations { get; set; } = 0;
        public int FailedValidations { get; set; } = 0;
        public int CacheHits { get; set; } = 0;

        public float SuccessRate => TotalValidations > 0 ? (float)SuccessfulValidations / TotalValidations : 0f;
        public float CacheHitRate => TotalValidations > 0 ? (float)CacheHits / TotalValidations : 0f;
    }

    #endregion

    #region Default Validators

    /// <summary>
    /// カードデータ用デフォルトバリデーター
    /// </summary>
    internal class CardDataValidator : IDataValidator<BaseCardData>
    {
        public ValidationResult Validate(BaseCardData data)
        {
            var result = new ValidationResult();
            
            if (string.IsNullOrEmpty(data.CardID))
            {
                result.IsValid = false;
                result.Errors.Add("Card ID is required");
            }
            
            if (string.IsNullOrEmpty(data.CardName))
            {
                result.Warnings.Add("Card name is empty");
            }
            
            if (data.DeckLimit < 1 || data.DeckLimit > 4)
            {
                result.IsValid = false;
                result.Errors.Add("Deck limit must be between 1 and 4");
            }
            
            return result;
        }

        public ValidationResult ValidateObject(ScriptableObject data)
        {
            return data is BaseCardData cardData ? Validate(cardData) : new ValidationResult { IsValid = false };
        }
    }

    /// <summary>
    /// ルールデータ用デフォルトバリデーター
    /// </summary>
    internal class RuleDataValidator : IDataValidator<BaseRuleData>
    {
        public ValidationResult Validate(BaseRuleData data)
        {
            var result = new ValidationResult();
            
            if (string.IsNullOrEmpty(data.RuleID))
            {
                result.IsValid = false;
                result.Errors.Add("Rule ID is required");
            }
            
            if (data.DeckSize < data.MinimumDeckSize || data.DeckSize > data.MaximumDeckSize)
            {
                result.IsValid = false;
                result.Errors.Add($"Deck size must be between {data.MinimumDeckSize} and {data.MaximumDeckSize}");
            }
            
            if (data.MaxCopiesPerCard < 1)
            {
                result.IsValid = false;
                result.Errors.Add("Max copies per card must be at least 1");
            }
            
            return result;
        }

        public ValidationResult ValidateObject(ScriptableObject data)
        {
            return data is BaseRuleData ruleData ? Validate(ruleData) : new ValidationResult { IsValid = false };
        }
    }

    #endregion
}