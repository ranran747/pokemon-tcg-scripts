using System.Collections.Generic;
using UnityEngine;

namespace PokemonTCG.Core.Data
{
    /// <summary>
    /// トレーナーズカードデータの基底クラス
    /// サポート・グッズ・スタジアムの共通機能を定義
    /// </summary>
    [CreateAssetMenu(fileName = "NewTrainerCard", menuName = "Pokemon TCG/Cards/Trainer/Base Trainer")]
    public class TrainerCardData : BaseCardData
    {
        #region Trainer Basic Info

        [Header("トレーナー基本情報")]
        [SerializeField] private TrainerType _trainerType = TrainerType.Item;
        [SerializeField] private TrainerSubType _subType = TrainerSubType.Normal;
        [SerializeField] private bool _isAceSpec = false;
        [SerializeField] private bool _isPrismStar = false;

        [Header("使用制限")]
        [SerializeField] private int _usageLimit = 0; // 0 = 無制限
        [SerializeField] private bool _oncePerTurn = false;
        [SerializeField] private bool _oncePerGame = false;
        [SerializeField] private List<UsageRestriction> _restrictions = new List<UsageRestriction>();

        [Header("効果")]
        [TextArea(3, 6)]
        [SerializeField] private string _effectDescription;
        [SerializeField] private List<TrainerEffect> _effects = new List<TrainerEffect>();
        [SerializeField] private List<string> _effectParameters = new List<string>();

        [Header("発動条件")]
        [SerializeField] private List<ActivationCondition> _activationConditions = new List<ActivationCondition>();
        [SerializeField] private bool _canUseFromDiscard = false;

        #endregion

        #region Properties

        /// <summary>トレーナー種類</summary>
        public TrainerType TrainerType => _trainerType;

        /// <summary>トレーナーサブ種類</summary>
        public TrainerSubType SubType => _subType;

        /// <summary>ACE SPECかどうか</summary>
        public bool IsAceSpec => _isAceSpec;

        /// <summary>プリズムスターかどうか</summary>
        public bool IsPrismStar => _isPrismStar;

        /// <summary>使用制限回数</summary>
        public int UsageLimit => _usageLimit;

        /// <summary>1ターンに1回制限</summary>
        public bool OncePerTurn => _oncePerTurn;

        /// <summary>1ゲームに1回制限</summary>
        public bool OncePerGame => _oncePerGame;

        /// <summary>使用制限リスト</summary>
        public List<UsageRestriction> Restrictions => _restrictions;

        /// <summary>効果説明</summary>
        public string EffectDescription => _effectDescription;

        /// <summary>効果リスト</summary>
        public List<TrainerEffect> Effects => _effects;

        /// <summary>効果パラメータ</summary>
        public List<string> EffectParameters => _effectParameters;

        /// <summary>発動条件リスト</summary>
        public List<ActivationCondition> ActivationConditions => _activationConditions;

        /// <summary>トラッシュから使用可能</summary>
        public bool CanUseFromDiscard => _canUseFromDiscard;

        /// <summary>カード種類</summary>
        public override CardType CardType => CardType.Trainer;

        /// <summary>サポートカードかどうか</summary>
        public bool IsSupporter => _trainerType == TrainerType.Supporter;

        /// <summary>グッズカードかどうか</summary>
        public bool IsItem => _trainerType == TrainerType.Item;

        /// <summary>スタジアムカードかどうか</summary>
        public bool IsStadium => _trainerType == TrainerType.Stadium;

        #endregion

        #region Validation

        public override bool IsValid()
        {
            var errors = GetValidationErrors();
            return errors.Count == 0;
        }

        public override List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            // 基本チェック
            if (string.IsNullOrEmpty(_effectDescription))
                errors.Add("Effect description is required");

            if (_effects.Count == 0)
                errors.Add("At least one effect is required");

            // ACE SPEC制限チェック
            if (_isAceSpec && DeckLimit != 1)
                errors.Add("ACE SPEC cards must have deck limit of 1");

            // プリズムスター制限チェック
            if (_isPrismStar && DeckLimit != 1)
                errors.Add("Prism Star cards must have deck limit of 1");

            // ACE SPECとプリズムスターの重複チェック
            if (_isAceSpec && _isPrismStar)
                errors.Add("Card cannot be both ACE SPEC and Prism Star");

            // サポート固有チェック
            if (_trainerType == TrainerType.Supporter)
            {
                if (!_oncePerTurn)
                    errors.Add("Supporter cards should be limited to once per turn");
            }

            // 使用制限の整合性チェック
            if (_oncePerGame && _oncePerTurn)
                errors.Add("Cannot have both once per turn and once per game restrictions");

            if (_usageLimit > 0 && (_oncePerTurn || _oncePerGame))
                errors.Add("Usage limit conflicts with once per turn/game restrictions");

            // 効果チェック
            foreach (var effect in _effects)
            {
                if (effect == null)
                {
                    errors.Add("Effect cannot be null");
                    continue;
                }

                if (string.IsNullOrEmpty(effect.EffectName))
                    errors.Add("Effect name cannot be empty");
            }

            // 発動条件チェック
            foreach (var condition in _activationConditions)
            {
                if (condition == null)
                {
                    errors.Add("Activation condition cannot be null");
                    continue;
                }

                if (string.IsNullOrEmpty(condition.ConditionName))
                    errors.Add("Activation condition name cannot be empty");
            }

            return errors;
        }

        public override int GetContentHash()
        {
            int hash = base.GetHashCode();
            hash ^= _trainerType.GetHashCode();
            hash ^= _subType.GetHashCode();
            hash ^= _isAceSpec.GetHashCode();
            hash ^= _effectDescription?.GetHashCode() ?? 0;
            
            // 効果のハッシュを追加
            foreach (var effect in _effects)
            {
                if (effect != null)
                    hash ^= effect.GetHashCode();
            }
            
            return hash;
        }

        #endregion

        #region Unity Lifecycle

        protected override void OnValidate()
        {
            base.OnValidate();

            #if UNITY_EDITOR
            ValidateTrainerData();
            #endif
        }

        #if UNITY_EDITOR
        private void ValidateTrainerData()
        {
            // ACE SPECの制限設定
            if (_isAceSpec)
            {
                _deckLimit = 1;
                _isLimitedCard = true;
            }

            // プリズムスターの制限設定
            if (_isPrismStar)
            {
                _deckLimit = 1;
                _isLimitedCard = true;
            }

            // サポートカードの制限設定
            if (_trainerType == TrainerType.Supporter)
            {
                _oncePerTurn = true;
            }

            // 使用制限の自動調整
            if (_usageLimit < 0)
                _usageLimit = 0;
        }
        #endif

        #endregion

        #region Helper Methods

        /// <summary>
        /// 指定条件で使用可能かチェック
        /// </summary>
        /// <param name="gameState">ゲーム状態</param>
        /// <returns>使用可能な場合true</returns>
        public bool CanUse(object gameState)
        {
            // 発動条件をすべてチェック
            foreach (var condition in _activationConditions)
            {
                if (!condition.IsMet(gameState))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 指定名の効果を取得
        /// </summary>
        /// <param name="effectName">効果名</param>
        /// <returns>効果データ（見つからない場合null）</returns>
        public TrainerEffect GetEffect(string effectName)
        {
            return _effects.Find(effect => effect.EffectName == effectName);
        }

        /// <summary>
        /// 指定種類の効果を取得
        /// </summary>
        /// <param name="effectType">効果種類</param>
        /// <returns>効果データリスト</returns>
        public List<TrainerEffect> GetEffects(TrainerEffectType effectType)
        {
            return _effects.FindAll(effect => effect.EffectType == effectType);
        }

        /// <summary>
        /// 制限に引っかかるかチェック
        /// </summary>
        /// <param name="restrictionType">制限種類</param>
        /// <returns>制限に引っかかる場合true</returns>
        public bool HasRestriction(UsageRestrictionType restrictionType)
        {
            return _restrictions.Exists(restriction => restriction.RestrictionType == restrictionType);
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// トレーナー種類
    /// </summary>
    public enum TrainerType
    {
        Supporter = 0,  // サポート
        Item = 1,       // グッズ
        Stadium = 2,    // スタジアム
        Tool = 3        // ポケモンのどうぐ
    }

    /// <summary>
    /// トレーナーサブ種類
    /// </summary>
    public enum TrainerSubType
    {
        Normal = 0,         // 通常
        Technical = 1,      // テクニカル
        Rocket = 2,         // ロケット団
        Plasma = 3,         // プラズマ団
        Flare = 4,          // フレア団
        Aqua = 5,           // アクア団
        Magma = 6,          // マグマ団
        Galaxy = 7,         // ギンガ団
        TeamUp = 8,         // チームアップ
        Special = 9         // 特殊
    }

    /// <summary>
    /// 使用制限種類
    /// </summary>
    public enum UsageRestrictionType
    {
        None = 0,               // 制限なし
        OncePerTurn = 1,        // 1ターンに1回
        OncePerGame = 2,        // 1ゲームに1回
        MaxUsage = 3,           // 最大使用回数
        SpecificPhase = 4,      // 特定フェーズのみ
        PlayerRestriction = 5,  // プレイヤー制限
        GameStateRestriction = 6 // ゲーム状況制限
    }

    /// <summary>
    /// トレーナー効果種類
    /// </summary>
    public enum TrainerEffectType
    {
        Draw = 0,           // ドロー
        Search = 1,         // サーチ
        Heal = 2,           // 回復
        Damage = 3,         // ダメージ
        StatusEffect = 4,   // 状態異常
        EnergyManipulation = 5, // エネルギー操作
        CardManipulation = 6,   // カード操作
        FieldEffect = 7,    // 場効果
        PlayerEffect = 8,   // プレイヤー効果
        Custom = 99         // カスタム効果
    }

    #endregion

    #region Data Classes

    /// <summary>
    /// トレーナー効果データ
    /// </summary>
    [System.Serializable]
    public class TrainerEffect
    {
        [SerializeField] private string _effectName;
        [SerializeField] private TrainerEffectType _effectType;
        [SerializeField] private int _power = 0;
        [TextArea(2, 4)]
        [SerializeField] private string _description;
        [SerializeField] private List<string> _parameters = new List<string>();
        [SerializeField] private bool _isOptional = false;

        public string EffectName => _effectName;
        public TrainerEffectType EffectType => _effectType;
        public int Power => _power;
        public string Description => _description;
        public List<string> Parameters => _parameters;
        public bool IsOptional => _isOptional;
    }

    /// <summary>
    /// 使用制限データ
    /// </summary>
    [System.Serializable]
    public class UsageRestriction
    {
        [SerializeField] private UsageRestrictionType _restrictionType;
        [SerializeField] private int _maxCount = 1;
        [SerializeField] private string _restrictionDescription;
        [SerializeField] private List<string> _conditions = new List<string>();

        public UsageRestrictionType RestrictionType => _restrictionType;
        public int MaxCount => _maxCount;
        public string RestrictionDescription => _restrictionDescription;
        public List<string> Conditions => _conditions;
    }

    /// <summary>
    /// 発動条件データ
    /// </summary>
    [System.Serializable]
    public class ActivationCondition
    {
        [SerializeField] private string _conditionName;
        [SerializeField] private ConditionType _conditionType;
        [SerializeField] private string _conditionDescription;
        [SerializeField] private List<string> _parameters = new List<string>();

        public string ConditionName => _conditionName;
        public ConditionType ConditionType => _conditionType;
        public string ConditionDescription => _conditionDescription;
        public List<string> Parameters => _parameters;

        /// <summary>
        /// 条件が満たされているかチェック
        /// </summary>
        /// <param name="gameState">ゲーム状態</param>
        /// <returns>満たされている場合true</returns>
        public bool IsMet(object gameState)
        {
            // TODO: 実際のゲーム状態をチェックするロジックを実装
            // 現在はダミー実装
            return true;
        }
    }

    /// <summary>
    /// 条件種類
    /// </summary>
    public enum ConditionType
    {
        Always = 0,         // 常時
        TurnPhase = 1,      // ターンフェーズ
        PlayerState = 2,    // プレイヤー状況
        FieldState = 3,     // 場の状況
        HandCount = 4,      // 手札枚数
        DeckCount = 5,      // 山札枚数
        DiscardCount = 6,   // トラッシュ枚数
        PrizeCount = 7,     // サイド枚数
        Custom = 99         // カスタム条件
    }

    #endregion
}