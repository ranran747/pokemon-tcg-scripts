using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Core.Data;

namespace PokemonTCG.Game.Rules
{
    /// <summary>
    /// 高度アクション検証システム
    /// 複雑なゲームルール検証とアクション妥当性チェックを提供
    /// </summary>
    public class ActionValidationSystem : MonoBehaviourSingleton<ActionValidationSystem>, IManager
    {
        #region Events

        public static event Action<ValidationResultEvent> OnValidationCompleted;
        public static event Action<ValidationErrorEvent> OnValidationError;
        public static event Action<ValidationWarningEvent> OnValidationWarning;

        #endregion

        #region Fields

        [Header("検証設定")]
        [SerializeField] private bool _strictValidation = true;
        [SerializeField] private bool _enableWarnings = true;
        [SerializeField] private bool _logValidationDetails = false;
        [SerializeField] private float _validationTimeout = 5f;

        [Header("検証キャッシュ")]
        [SerializeField] private bool _enableValidationCache = true;
        [SerializeField] private int _maxCacheSize = 1000;
        [SerializeField] private float _cacheExpireTime = 300f; // 5分

        [Header("検証統計")]
        [SerializeField] private int _totalValidations = 0;
        [SerializeField] private int _successfulValidations = 0;
        [SerializeField] private int _failedValidations = 0;
        [SerializeField] private float _averageValidationTime = 0f;

        // 検証ルール管理
        private Dictionary<GameActionType, List<IActionValidator>> _validators = new Dictionary<GameActionType, List<IActionValidator>>();
        private Dictionary<string, List<IContextValidator>> _contextValidators = new Dictionary<string, List<IContextValidator>>();
        private Dictionary<string, ValidationResult> _validationCache = new Dictionary<string, ValidationResult>();
        private Dictionary<string, float> _cacheTimestamps = new Dictionary<string, float>();

        // 検証プロファイル
        private Dictionary<string, ValidationProfile> _validationProfiles = new Dictionary<string, ValidationProfile>();
        private string _currentProfileId = "default";

        // 動的検証ルール
        private List<IDynamicValidator> _dynamicValidators = new List<IDynamicValidator>();
        private Dictionary<string, Func<IGameAction, ValidationResult>> _customValidators = new Dictionary<string, Func<IGameAction, ValidationResult>>();

        #endregion

        #region Properties

        /// <summary>マネージャー名</summary>
        public string ManagerName => "ActionValidationSystem";

        /// <summary>初期化順序</summary>
        public int InitializationOrder => 160; // TurnManagerより後

        /// <summary>厳密検証モード</summary>
        public bool StrictValidation => _strictValidation;

        /// <summary>検証成功率</summary>
        public float ValidationSuccessRate => 
            _totalValidations > 0 ? (float)_successfulValidations / _totalValidations * 100f : 0f;

        /// <summary>現在のプロファイル</summary>
        public ValidationProfile CurrentProfile => 
            _validationProfiles.GetValueOrDefault(_currentProfileId, CreateDefaultProfile());

        /// <summary>登録済み検証ルール数</summary>
        public int RegisteredValidatorCount => 
            _validators.Values.Sum(list => list.Count) + _contextValidators.Values.Sum(list => list.Count);

        #endregion

        #region Initialization

        public void Initialize()
        {
            Debug.Log($"[{ManagerName}] Initializing Action Validation System...");
            
            // デフォルト検証ルール登録
            RegisterDefaultValidators();
            
            // デフォルトプロファイル作成
            CreateDefaultValidationProfiles();
            
            // イベント登録
            EventBus.On<GameStartedEvent>(OnGameStarted);
            EventBus.On<GameEndedEvent>(OnGameEnded);
            EventBus.On<RuleChangedEvent>(OnRuleChanged);
            
            Debug.Log($"[{ManagerName}] Action Validation System initialized");
        }

        public void Dispose()
        {
            Debug.Log($"[{ManagerName}] Disposing Action Validation System...");
            
            // イベント解除
            EventBus.Off<GameStartedEvent>(OnGameStarted);
            EventBus.Off<GameEndedEvent>(OnGameEnded);
            EventBus.Off<RuleChangedEvent>(OnRuleChanged);
            
            // データクリア
            ClearValidationData();
            
            Debug.Log($"[{ManagerName}] Action Validation System disposed");
        }

        private void RegisterDefaultValidators()
        {
            // カードプレイ検証
            RegisterValidator(GameActionType.PlayCard, new CardPlayValidator());
            RegisterValidator(GameActionType.PlayCard, new HandSizeValidator());
            RegisterValidator(GameActionType.PlayCard, new ManaCostValidator());
            RegisterValidator(GameActionType.PlayCard, new FieldLimitValidator());

            // 攻撃検証
            RegisterValidator(GameActionType.UseAttack, new AttackValidator());
            RegisterValidator(GameActionType.UseAttack, new EnergyRequirementValidator());
            RegisterValidator(GameActionType.UseAttack, new TargetValidator());
            RegisterValidator(GameActionType.UseAttack, new StatusConditionValidator());

            // エネルギー添付検証
            RegisterValidator(GameActionType.AttachEnergy, new EnergyAttachValidator());
            RegisterValidator(GameActionType.AttachEnergy, new EnergyLimitValidator());

            // 進化検証
            RegisterValidator(GameActionType.Evolution, new EvolutionValidator());
            RegisterValidator(GameActionType.Evolution, new EvolutionTimingValidator());

            // にげる検証
            RegisterValidator(GameActionType.Retreat, new RetreatValidator());
            RegisterValidator(GameActionType.Retreat, new RetreatCostValidator());

            // 特性使用検証
            RegisterValidator(GameActionType.UseAbility, new AbilityValidator());
            RegisterValidator(GameActionType.UseAbility, new AbilityUsageValidator());

            Debug.Log($"[{ManagerName}] Default validators registered");
        }

        private void CreateDefaultValidationProfiles()
        {
            // 標準プロファイル
            _validationProfiles["default"] = new ValidationProfile
            {
                ProfileId = "default",
                Name = "Standard Validation",
                StrictMode = true,
                EnableWarnings = true,
                TimeoutSeconds = 5f,
                CacheEnabled = true
            };

            // 緩和プロファイル
            _validationProfiles["relaxed"] = new ValidationProfile
            {
                ProfileId = "relaxed",
                Name = "Relaxed Validation",
                StrictMode = false,
                EnableWarnings = false,
                TimeoutSeconds = 10f,
                CacheEnabled = true
            };

            // デバッグプロファイル
            _validationProfiles["debug"] = new ValidationProfile
            {
                ProfileId = "debug",
                Name = "Debug Validation",
                StrictMode = true,
                EnableWarnings = true,
                TimeoutSeconds = 30f,
                CacheEnabled = false,
                VerboseLogging = true
            };
        }

        #endregion

        #region Validation Core

        /// <summary>
        /// アクション検証（メイン関数）
        /// </summary>
        /// <param name="action">検証するアクション</param>
        /// <returns>検証結果</returns>
        public ValidationResult ValidateAction(IGameAction action)
        {
            var startTime = Time.realtimeSinceStartup;
            _totalValidations++;
            
            try
            {
                Debug.Log($"[{ManagerName}] Validating action {action.ActionType} for player {action.PlayerId}");
                
                // キャッシュチェック
                if (_enableValidationCache)
                {
                    var cachedResult = CheckValidationCache(action);
                    if (cachedResult != null)
                    {
                        _successfulValidations++;
                        return cachedResult;
                    }
                }
                
                // 基本検証
                var basicValidation = ValidateBasicRequirements(action);
                if (!basicValidation.IsValid)
                {
                    return CacheAndReturn(action, basicValidation, startTime);
                }
                
                // 型別検証
                var typeValidation = ValidateByActionType(action);
                if (!typeValidation.IsValid)
                {
                    return CacheAndReturn(action, typeValidation, startTime);
                }
                
                // コンテキスト検証
                var contextValidation = ValidateContext(action);
                if (!contextValidation.IsValid)
                {
                    return CacheAndReturn(action, contextValidation, startTime);
                }
                
                // 動的検証
                var dynamicValidation = ValidateDynamic(action);
                if (!dynamicValidation.IsValid)
                {
                    return CacheAndReturn(action, dynamicValidation, startTime);
                }
                
                // カスタム検証
                var customValidation = ValidateCustom(action);
                if (!customValidation.IsValid)
                {
                    return CacheAndReturn(action, customValidation, startTime);
                }
                
                // 最終検証結果
                var finalResult = ValidationResult.CreateSuccess();
                var warnings = CollectWarnings(action);
                foreach (var warning in warnings)
                {
                    finalResult.AddWarning(warning);
                }
                
                _successfulValidations++;
                
                return CacheAndReturn(action, finalResult, startTime);
            }
            catch (Exception ex)
            {
                _failedValidations++;
                var errorResult = ValidationResult.CreateError($"Validation exception: {ex.Message}");
                
                EventBus.Emit(new ValidationErrorEvent
                {
                    Action = action,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
                
                return CacheAndReturn(action, errorResult, startTime);
            }
        }

        /// <summary>
        /// 基本要件検証
        /// </summary>
        private ValidationResult ValidateBasicRequirements(IGameAction action)
        {
            // nullチェック
            if (action == null)
                return ValidationResult.CreateError("Action cannot be null");
            
            // プレイヤーID検証
            if (string.IsNullOrEmpty(action.PlayerId))
                return ValidationResult.CreateError("Player ID is required");
            
            // アクション種別検証
            if (!Enum.IsDefined(typeof(GameActionType), action.ActionType))
                return ValidationResult.CreateError($"Invalid action type: {action.ActionType}");
            
            // 実行時刻検証
            if (action.ExecutedAt == default(DateTime))
                return ValidationResult.CreateError("Invalid execution time");
            
            // プレイヤー存在確認
            var gameStateManager = ServiceLocator.Get<GameStateManager>();
            if (gameStateManager?.CurrentGameState != null)
            {
                var playerState = gameStateManager.GetPlayerState(action.PlayerId);
                if (playerState == null)
                    return ValidationResult.CreateError($"Player {action.PlayerId} not found in game");
            }
            
            return ValidationResult.CreateSuccess();
        }

        /// <summary>
        /// アクション種別別検証
        /// </summary>
        private ValidationResult ValidateByActionType(IGameAction action)
        {
            if (!_validators.ContainsKey(action.ActionType))
            {
                if (_strictValidation)
                    return ValidationResult.CreateError($"No validators registered for action type {action.ActionType}");
                else
                    return ValidationResult.CreateSuccess();
            }
            
            var validators = _validators[action.ActionType];
            foreach (var validator in validators)
            {
                var result = validator.Validate(action);
                if (!result.IsValid)
                {
                    if (_logValidationDetails)
                        Debug.LogWarning($"[{ManagerName}] Validator {validator.GetType().Name} failed: {result.ErrorMessage}");
                    
                    return result;
                }
            }
            
            return ValidationResult.CreateSuccess();
        }

        /// <summary>
        /// コンテキスト検証
        /// </summary>
        private ValidationResult ValidateContext(IGameAction action)
        {
            var gameStateManager = ServiceLocator.Get<GameStateManager>();
            if (gameStateManager?.CurrentGameState == null)
                return ValidationResult.CreateError("Game state not available for validation");
            
            var gameState = gameStateManager.CurrentGameState;
            
            // ゲーム状態別検証
            var stateKey = gameState.CurrentPhase.ToString();
            if (_contextValidators.ContainsKey(stateKey))
            {
                foreach (var validator in _contextValidators[stateKey])
                {
                    var result = validator.ValidateContext(action, gameState);
                    if (!result.IsValid)
                        return result;
                }
            }
            
            // ターン順序検証
            var turnManager = ServiceLocator.Get<TurnManager>();
            if (turnManager != null && turnManager.CurrentPlayerId != action.PlayerId)
            {
                return ValidationResult.CreateError($"It's not player {action.PlayerId}'s turn");
            }
            
            return ValidationResult.CreateSuccess();
        }

        /// <summary>
        /// 動的検証
        /// </summary>
        private ValidationResult ValidateDynamic(IGameAction action)
        {
            foreach (var validator in _dynamicValidators)
            {
                if (validator.ShouldValidate(action))
                {
                    var result = validator.ValidateDynamic(action);
                    if (!result.IsValid)
                        return result;
                }
            }
            
            return ValidationResult.CreateSuccess();
        }

        /// <summary>
        /// カスタム検証
        /// </summary>
        private ValidationResult ValidateCustom(IGameAction action)
        {
            var customKey = $"{action.ActionType}_{action.PlayerId}";
            if (_customValidators.ContainsKey(customKey))
            {
                return _customValidators[customKey](action);
            }
            
            // アクション種別のみのカスタム検証
            var typeKey = action.ActionType.ToString();
            if (_customValidators.ContainsKey(typeKey))
            {
                return _customValidators[typeKey](action);
            }
            
            return ValidationResult.CreateSuccess();
        }

        /// <summary>
        /// 警告収集
        /// </summary>
        private List<string> CollectWarnings(IGameAction action)
        {
            var warnings = new List<string>();
            
            if (!_enableWarnings) return warnings;
            
            // パフォーマンス警告
            var gameStateManager = ServiceLocator.Get<GameStateManager>();
            if (gameStateManager?.CurrentGameState != null)
            {
                var playerState = gameStateManager.GetPlayerState(action.PlayerId);
                if (playerState != null)
                {
                    // 手札枚数警告
                    if (playerState.Hand != null && playerState.Hand.Count > 8)
                        warnings.Add("Large hand size may slow down gameplay");
                    
                    // フィールド枚数警告
                    if (playerState.BenchPokemons != null && playerState.BenchPokemons.Count >= 4)
                        warnings.Add("Bench is nearly full");
                }
            }
            
            return warnings;
        }

        #endregion

        #region Cache Management

        private ValidationResult CheckValidationCache(IGameAction action)
        {
            var cacheKey = GenerateCacheKey(action);
            
            if (_validationCache.ContainsKey(cacheKey))
            {
                var timestamp = _cacheTimestamps[cacheKey];
                if (Time.time - timestamp < _cacheExpireTime)
                {
                    if (_logValidationDetails)
                        Debug.Log($"[{ManagerName}] Cache hit for action {action.ActionType}");
                    
                    return _validationCache[cacheKey];
                }
                else
                {
                    // 期限切れキャッシュを削除
                    _validationCache.Remove(cacheKey);
                    _cacheTimestamps.Remove(cacheKey);
                }
            }
            
            return null;
        }

        private ValidationResult CacheAndReturn(IGameAction action, ValidationResult result, float startTime)
        {
            // 検証時間記録
            var validationTime = Time.realtimeSinceStartup - startTime;
            _averageValidationTime = (_averageValidationTime * (_totalValidations - 1) + validationTime) / _totalValidations;
            
            // キャッシュに保存
            if (_enableValidationCache && result.IsValid)
            {
                var cacheKey = GenerateCacheKey(action);
                
                // キャッシュサイズ制限
                if (_validationCache.Count >= _maxCacheSize)
                {
                    ClearOldestCacheEntries();
                }
                
                _validationCache[cacheKey] = result;
                _cacheTimestamps[cacheKey] = Time.time;
            }
            
            // イベント発行
            EventBus.Emit(new ValidationResultEvent
            {
                Action = action,
                Result = result,
                ValidationTime = validationTime
            });
            
            if (!result.IsValid)
            {
                _failedValidations++;
                EventBus.Emit(new ValidationErrorEvent
                {
                    Action = action,
                    Error = result.ErrorMessage
                });
            }
            else if (result.Warnings.Count > 0)
            {
                EventBus.Emit(new ValidationWarningEvent
                {
                    Action = action,
                    Warnings = result.Warnings
                });
            }
            
            return result;
        }

        private string GenerateCacheKey(IGameAction action)
        {
            return $"{action.ActionType}_{action.PlayerId}_{action.GetHashCode()}";
        }

        private void ClearOldestCacheEntries()
        {
            var oldestEntries = _cacheTimestamps
                .OrderBy(kvp => kvp.Value)
                .Take(_maxCacheSize / 4)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in oldestEntries)
            {
                _validationCache.Remove(key);
                _cacheTimestamps.Remove(key);
            }
        }

        #endregion

        #region Registration

        /// <summary>
        /// 検証ルール登録
        /// </summary>
        public void RegisterValidator(GameActionType actionType, IActionValidator validator)
        {
            if (!_validators.ContainsKey(actionType))
                _validators[actionType] = new List<IActionValidator>();
            
            _validators[actionType].Add(validator);
            Debug.Log($"[{ManagerName}] Registered validator {validator.GetType().Name} for {actionType}");
        }

        /// <summary>
        /// コンテキスト検証ルール登録
        /// </summary>
        public void RegisterContextValidator(string contextKey, IContextValidator validator)
        {
            if (!_contextValidators.ContainsKey(contextKey))
                _contextValidators[contextKey] = new List<IContextValidator>();
            
            _contextValidators[contextKey].Add(validator);
            Debug.Log($"[{ManagerName}] Registered context validator {validator.GetType().Name} for {contextKey}");
        }

        /// <summary>
        /// 動的検証ルール登録
        /// </summary>
        public void RegisterDynamicValidator(IDynamicValidator validator)
        {
            _dynamicValidators.Add(validator);
            Debug.Log($"[{ManagerName}] Registered dynamic validator {validator.GetType().Name}");
        }

        /// <summary>
        /// カスタム検証ルール登録
        /// </summary>
        public void RegisterCustomValidator(string key, Func<IGameAction, ValidationResult> validator)
        {
            _customValidators[key] = validator;
            Debug.Log($"[{ManagerName}] Registered custom validator for {key}");
        }

        /// <summary>
        /// 検証ルール削除
        /// </summary>
        public void UnregisterValidator(GameActionType actionType, IActionValidator validator)
        {
            if (_validators.ContainsKey(actionType))
            {
                _validators[actionType].Remove(validator);
                if (_validators[actionType].Count == 0)
                    _validators.Remove(actionType);
            }
        }

        #endregion

        #region Profile Management

        /// <summary>
        /// 検証プロファイル設定
        /// </summary>
        public void SetValidationProfile(string profileId)
        {
            if (_validationProfiles.ContainsKey(profileId))
            {
                _currentProfileId = profileId;
                var profile = _validationProfiles[profileId];
                
                _strictValidation = profile.StrictMode;
                _enableWarnings = profile.EnableWarnings;
                _validationTimeout = profile.TimeoutSeconds;
                _enableValidationCache = profile.CacheEnabled;
                _logValidationDetails = profile.VerboseLogging;
                
                Debug.Log($"[{ManagerName}] Switched to validation profile: {profile.Name}");
            }
            else
            {
                Debug.LogWarning($"[{ManagerName}] Validation profile {profileId} not found");
            }
        }

        /// <summary>
        /// カスタムプロファイル作成
        /// </summary>
        public void CreateValidationProfile(ValidationProfile profile)
        {
            _validationProfiles[profile.ProfileId] = profile;
            Debug.Log($"[{ManagerName}] Created validation profile: {profile.Name}");
        }

        private ValidationProfile CreateDefaultProfile()
        {
            return new ValidationProfile
            {
                ProfileId = "default",
                Name = "Default",
                StrictMode = true,
                EnableWarnings = true,
                TimeoutSeconds = 5f,
                CacheEnabled = true
            };
        }

        #endregion

        #region Event Handlers

        private void OnGameStarted(GameStartedEvent evt)
        {
            Debug.Log($"[{ManagerName}] Game started, resetting validation statistics");
            ResetStatistics();
        }

        private void OnGameEnded(GameEndedEvent evt)
        {
            Debug.Log($"[{ManagerName}] Game ended, clearing validation cache");
            ClearValidationCache();
        }

        private void OnRuleChanged(RuleChangedEvent evt)
        {
            Debug.Log($"[{ManagerName}] Rule changed, clearing validation cache");
            ClearValidationCache();
        }

        #endregion

        #region Utility

        private void ResetStatistics()
        {
            _totalValidations = 0;
            _successfulValidations = 0;
            _failedValidations = 0;
            _averageValidationTime = 0f;
        }

        private void ClearValidationCache()
        {
            _validationCache.Clear();
            _cacheTimestamps.Clear();
        }

        private void ClearValidationData()
        {
            _validators.Clear();
            _contextValidators.Clear();
            _dynamicValidators.Clear();
            _customValidators.Clear();
            _validationProfiles.Clear();
            ClearValidationCache();
            ResetStatistics();
        }

        #endregion

        #region Debug

        /// <summary>
        /// デバッグ情報取得
        /// </summary>
        public string GetDebugInfo()
        {
            return $"=== Action Validation System Debug Info ===\n" +
                   $"Current Profile: {_currentProfileId} ({CurrentProfile.Name})\n" +
                   $"Strict Validation: {_strictValidation}\n" +
                   $"Total Validations: {_totalValidations}\n" +
                   $"Success Rate: {ValidationSuccessRate:F1}%\n" +
                   $"Average Time: {_averageValidationTime * 1000:F2}ms\n" +
                   $"Registered Validators: {RegisteredValidatorCount}\n" +
                   $"Cache Size: {_validationCache.Count}/{_maxCacheSize}\n" +
                   $"Dynamic Validators: {_dynamicValidators.Count}\n" +
                   $"Custom Validators: {_customValidators.Count}";
        }

        #endregion
    }

    #region Interfaces

    /// <summary>
    /// アクション検証インターフェース
    /// </summary>
    public interface IActionValidator
    {
        ValidationResult Validate(IGameAction action);
    }

    /// <summary>
    /// コンテキスト検証インターフェース
    /// </summary>
    public interface IContextValidator
    {
        ValidationResult ValidateContext(IGameAction action, GameState gameState);
    }

    /// <summary>
    /// 動的検証インターフェース
    /// </summary>
    public interface IDynamicValidator
    {
        bool ShouldValidate(IGameAction action);
        ValidationResult ValidateDynamic(IGameAction action);
    }

    #endregion

    #region Validator Implementations

    /// <summary>
    /// カードプレイ検証
    /// </summary>
    public class CardPlayValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            // カードプレイの基本チェック
            if (!action.Parameters.ContainsKey("CardId"))
                return ValidationResult.CreateError("Card ID is required");
            
            // TODO: 実際のカード存在チェック
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// 手札サイズ検証
    /// </summary>
    public class HandSizeValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            var gameStateManager = ServiceLocator.Get<GameStateManager>();
            if (gameStateManager?.CurrentGameState != null)
            {
                var playerState = gameStateManager.GetPlayerState(action.PlayerId);
                if (playerState?.Hand != null && playerState.Hand.Count == 0)
                    return ValidationResult.CreateError("No cards in hand");
            }
            
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// マナコスト検証
    /// </summary>
    public class ManaCostValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            // TODO: エネルギーコスト検証実装
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// フィールド制限検証
    /// </summary>
    public class FieldLimitValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            var gameStateManager = ServiceLocator.Get<GameStateManager>();
            if (gameStateManager?.CurrentGameState != null)
            {
                var playerState = gameStateManager.GetPlayerState(action.PlayerId);
                if (playerState?.BenchPokemons != null && playerState.BenchPokemons.Count >= 5) // 従来版最大5体
                    return ValidationResult.CreateError("Bench is full");
            }
            
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// 攻撃検証
    /// </summary>
    public class AttackValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            if (!action.Parameters.ContainsKey("AttackingPokemonId"))
                return ValidationResult.CreateError("Attacking Pokemon ID is required");
            
            if (!action.Parameters.ContainsKey("AttackName"))
                return ValidationResult.CreateError("Attack name is required");
            
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// エネルギー要件検証
    /// </summary>
    public class EnergyRequirementValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            // TODO: エネルギー要件チェック実装
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// ターゲット検証
    /// </summary>
    public class TargetValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            if (action.Parameters.ContainsKey("TargetRequired") && 
                action.Parameters["TargetRequired"] is bool targetRequired && 
                targetRequired &&
                !action.Parameters.ContainsKey("TargetPokemonId"))
            {
                return ValidationResult.CreateError("Attack requires a target");
            }
            
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// 状態異常検証
    /// </summary>
    public class StatusConditionValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            // TODO: まひ、ねむりなどの状態異常チェック
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// エネルギー添付検証
    /// </summary>
    public class EnergyAttachValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            if (!action.Parameters.ContainsKey("EnergyCardId"))
                return ValidationResult.CreateError("Energy card ID is required");
            
            if (!action.Parameters.ContainsKey("TargetPokemonId"))
                return ValidationResult.CreateError("Target Pokemon ID is required");
            
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// エネルギー制限検証
    /// </summary>
    public class EnergyLimitValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            // TODO: 1ターン1枚制限チェック
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// 進化検証
    /// </summary>
    public class EvolutionValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            if (!action.Parameters.ContainsKey("EvolutionSourceId"))
                return ValidationResult.CreateError("Evolution source Pokemon ID is required");
            
            if (!action.Parameters.ContainsKey("EvolutionTargetCardId"))
                return ValidationResult.CreateError("Evolution target card ID is required");
            
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// 進化タイミング検証
    /// </summary>
    public class EvolutionTimingValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            var turnManager = ServiceLocator.Get<TurnManager>();
            if (turnManager?.CurrentTurnNumber == 1)
                return ValidationResult.CreateError("Cannot evolve on first turn");
            
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// にげる検証
    /// </summary>
    public class RetreatValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            if (!action.Parameters.ContainsKey("RetreatPokemonId"))
                return ValidationResult.CreateError("Retreat Pokemon ID is required");
            
            if (!action.Parameters.ContainsKey("NewActivePokemonId"))
                return ValidationResult.CreateError("New active Pokemon ID is required");
            
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// にげるコスト検証
    /// </summary>
    public class RetreatCostValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            // TODO: にげるコスト支払い可能性チェック
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// 特性検証
    /// </summary>
    public class AbilityValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            if (!action.Parameters.ContainsKey("PokemonId"))
                return ValidationResult.CreateError("Pokemon ID is required");
            
            if (!action.Parameters.ContainsKey("AbilityName"))
                return ValidationResult.CreateError("Ability name is required");
            
            return ValidationResult.CreateSuccess();
        }
    }

    /// <summary>
    /// 特性使用回数検証
    /// </summary>
    public class AbilityUsageValidator : IActionValidator
    {
        public ValidationResult Validate(IGameAction action)
        {
            // TODO: 特性使用回数制限チェック
            return ValidationResult.CreateSuccess();
        }
    }

    #endregion

    #region Data Classes

    /// <summary>
    /// 検証プロファイル
    /// </summary>
    [Serializable]
    public class ValidationProfile
    {
        public string ProfileId;
        public string Name;
        public bool StrictMode = true;
        public bool EnableWarnings = true;
        public float TimeoutSeconds = 5f;
        public bool CacheEnabled = true;
        public bool VerboseLogging = false;
    }

    #endregion

    #region Event Classes

    public class ValidationResultEvent
    {
        public IGameAction Action { get; set; }
        public ValidationResult Result { get; set; }
        public float ValidationTime { get; set; }
    }

    public class ValidationErrorEvent
    {
        public IGameAction Action { get; set; }
        public string Error { get; set; }
        public string StackTrace { get; set; }
    }

    public class ValidationWarningEvent
    {
        public IGameAction Action { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class RuleChangedEvent
    {
        public string OldRuleId { get; set; }
        public string NewRuleId { get; set; }
    }

    #endregion
}