using System.Collections.Generic;
using UnityEngine;

namespace PokemonTCG.Core.Data
{
    /// <summary>
    /// エネルギーカードデータの基底クラス
    /// 基本エネルギー・特殊エネルギーの共通機能を定義
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnergyCard", menuName = "Pokemon TCG/Cards/Energy/Base Energy")]
    public class EnergyCardData : BaseCardData
    {
        #region Energy Basic Info

        [Header("エネルギー基本情報")]
        [SerializeField] private EnergyType _energyType = EnergyType.Basic;
        [SerializeField] private List<PokemonType> _providedTypes = new List<PokemonType>();
        [SerializeField] private int _energyValue = 1;
        [SerializeField] private bool _isSpecialEnergy = false;

        [Header("特殊エネルギー設定")]
        [SerializeField] private bool _hasSpecialEffect = false;
        [SerializeField] private List<EnergyEffect> _effects = new List<EnergyEffect>();
        [SerializeField] private List<EnergyRestriction> _restrictions = new List<EnergyRestriction>();

        [Header("使用制限")]
        [SerializeField] private bool _canBeAttachedToAnyPokemon = true;
        [SerializeField] private List<AttachmentRestriction> _attachmentRestrictions = new List<AttachmentRestriction>();
        [SerializeField] private bool _discardAfterUse = false;

        [Header("エネルギー供給")]
        [TextArea(2, 4)]
        [SerializeField] private string _energyDescription;
        [SerializeField] private List<ConditionalEnergyProvision> _conditionalProvisions = new List<ConditionalEnergyProvision>();

        #endregion

        #region Properties

        /// <summary>エネルギー種類</summary>
        public EnergyType EnergyType => _energyType;

        /// <summary>提供するタイプリスト</summary>
        public List<PokemonType> ProvidedTypes => _providedTypes;

        /// <summary>エネルギー供給量</summary>
        public int EnergyValue => _energyValue;

        /// <summary>特殊エネルギーかどうか</summary>
        public bool IsSpecialEnergy => _isSpecialEnergy;

        /// <summary>特殊効果を持つか</summary>
        public bool HasSpecialEffect => _hasSpecialEffect;

        /// <summary>効果リスト</summary>
        public List<EnergyEffect> Effects => _effects;

        /// <summary>制限リスト</summary>
        public List<EnergyRestriction> Restrictions => _restrictions;

        /// <summary>任意のポケモンにつけられるか</summary>
        public bool CanBeAttachedToAnyPokemon => _canBeAttachedToAnyPokemon;

        /// <summary>つける制限リスト</summary>
        public List<AttachmentRestriction> AttachmentRestrictions => _attachmentRestrictions;

        /// <summary>使用後トラッシュするか</summary>
        public bool DiscardAfterUse => _discardAfterUse;

        /// <summary>エネルギー説明</summary>
        public string EnergyDescription => _energyDescription;

        /// <summary>条件付きエネルギー供給</summary>
        public List<ConditionalEnergyProvision> ConditionalProvisions => _conditionalProvisions;

        /// <summary>カード種類</summary>
        public override CardType CardType => CardType.Energy;

        /// <summary>基本エネルギーかどうか</summary>
        public bool IsBasicEnergy => _energyType == EnergyType.Basic;

        /// <summary>無色エネルギーを提供するか</summary>
        public bool ProvidesColorless => _providedTypes.Contains(PokemonType.Colorless);

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
            if (_energyValue <= 0)
                errors.Add("Energy value must be greater than 0");

            if (_providedTypes.Count == 0)
                errors.Add("Must provide at least one energy type");

            // 基本エネルギーのチェック
            if (_energyType == EnergyType.Basic)
            {
                if (_providedTypes.Count > 1)
                    errors.Add("Basic energy should provide only one type");

                if (_isSpecialEnergy)
                    errors.Add("Basic energy cannot be special energy");

                if (_hasSpecialEffect)
                    errors.Add("Basic energy should not have special effects");

                if (DeckLimit != 4)
                    errors.Add("Basic energy should have deck limit of 4");
            }

            // 特殊エネルギーのチェック
            if (_isSpecialEnergy)
            {
                if (_energyType == EnergyType.Basic)
                    errors.Add("Special energy cannot be basic energy type");

                if (DeckLimit > 4)
                    errors.Add("Special energy deck limit should not exceed 4");
            }

            // エネルギー値のチェック
            if (_energyValue > 3)
                errors.Add("Energy value is unusually high (max recommended: 3)");

            // 効果チェック
            if (_hasSpecialEffect && _effects.Count == 0)
                errors.Add("Special effect flag is set but no effects defined");

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

            // 制限チェック
            foreach (var restriction in _restrictions)
            {
                if (restriction == null)
                {
                    errors.Add("Restriction cannot be null");
                    continue;
                }

                if (string.IsNullOrEmpty(restriction.RestrictionName))
                    errors.Add("Restriction name cannot be empty");
            }

            // つける制限チェック
            foreach (var attachRestriction in _attachmentRestrictions)
            {
                if (attachRestriction == null)
                {
                    errors.Add("Attachment restriction cannot be null");
                    continue;
                }

                if (string.IsNullOrEmpty(attachRestriction.RestrictionName))
                    errors.Add("Attachment restriction name cannot be empty");
            }

            return errors;
        }

        public override int GetContentHash()
        {
            int hash = base.GetHashCode();
            hash ^= _energyType.GetHashCode();
            hash ^= _energyValue.GetHashCode();
            hash ^= _isSpecialEnergy.GetHashCode();
            
            foreach (var type in _providedTypes)
            {
                hash ^= type.GetHashCode();
            }
            
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
            ValidateEnergyData();
            #endif
        }

        #if UNITY_EDITOR
        private void ValidateEnergyData()
        {
            // エネルギー値の範囲チェック
            _energyValue = Mathf.Clamp(_energyValue, 1, 5);

            // 基本エネルギーの自動設定
            if (_energyType == EnergyType.Basic)
            {
                _isSpecialEnergy = false;
                _hasSpecialEffect = false;
                _canBeAttachedToAnyPokemon = true;
                _discardAfterUse = false;
                _deckLimit = 4;

                // 基本エネルギーは1種類のタイプのみ提供
                if (_providedTypes.Count > 1)
                {
                    var firstType = _providedTypes[0];
                    _providedTypes.Clear();
                    _providedTypes.Add(firstType);
                }
            }

            // 特殊エネルギーの自動設定
            if (_isSpecialEnergy)
            {
                if (_energyType == EnergyType.Basic)
                    _energyType = EnergyType.Special;

                // 特殊エネルギーは制限あり
                if (_deckLimit > 4)
                    _deckLimit = 4;
            }

            // 効果フラグの整合性チェック
            if (_effects.Count > 0)
                _hasSpecialEffect = true;
            else if (_hasSpecialEffect && _effects.Count == 0)
                _hasSpecialEffect = false;
        }
        #endif

        #endregion

        #region Helper Methods

        /// <summary>
        /// 指定タイプのエネルギーを提供するかチェック
        /// </summary>
        /// <param name="pokemonType">ポケモンタイプ</param>
        /// <returns>提供する場合true</returns>
        public bool ProvidesEnergyType(PokemonType pokemonType)
        {
            return _providedTypes.Contains(pokemonType) || _providedTypes.Contains(PokemonType.Colorless);
        }

        /// <summary>
        /// 指定ポケモンにつけられるかチェック
        /// </summary>
        /// <param name="pokemonCard">ポケモンカード</param>
        /// <returns>つけられる場合true</returns>
        public bool CanAttachTo(PokemonCardData pokemonCard)
        {
            if (_canBeAttachedToAnyPokemon)
                return true;

            foreach (var restriction in _attachmentRestrictions)
            {
                if (!restriction.CheckRestriction(pokemonCard))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 指定名の効果を取得
        /// </summary>
        /// <param name="effectName">効果名</param>
        /// <returns>効果データ（見つからない場合null）</returns>
        public EnergyEffect GetEffect(string effectName)
        {
            return _effects.Find(effect => effect.EffectName == effectName);
        }

        /// <summary>
        /// 指定種類の効果を取得
        /// </summary>
        /// <param name="effectType">効果種類</param>
        /// <returns>効果データリスト</returns>
        public List<EnergyEffect> GetEffects(EnergyEffectType effectType)
        {
            return _effects.FindAll(effect => effect.EffectType == effectType);
        }

        /// <summary>
        /// 条件付きエネルギー供給量を計算
        /// </summary>
        /// <param name="pokemonCard">つけられているポケモン</param>
        /// <param name="gameState">ゲーム状態</param>
        /// <returns>実際の供給量</returns>
        public int CalculateEnergyProvision(PokemonCardData pokemonCard, object gameState)
        {
            int totalEnergy = _energyValue;

            foreach (var provision in _conditionalProvisions)
            {
                if (provision.CheckCondition(pokemonCard, gameState))
                {
                    totalEnergy += provision.BonusEnergy;
                }
            }

            return totalEnergy;
        }

        /// <summary>
        /// 制限に引っかかるかチェック
        /// </summary>
        /// <param name="restrictionType">制限種類</param>
        /// <returns>制限に引っかかる場合true</returns>
        public bool HasRestriction(EnergyRestrictionType restrictionType)
        {
            return _restrictions.Exists(restriction => restriction.RestrictionType == restrictionType);
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// エネルギー種類
    /// </summary>
    public enum EnergyType
    {
        Basic = 0,      // 基本エネルギー
        Special = 1,    // 特殊エネルギー
        Rainbow = 2,    // レインボーエネルギー
        Multi = 3,      // マルチエネルギー
        Double = 4,     // ダブルエネルギー
        Counter = 5,    // カウンターエネルギー
        Unit = 6,       // ユニットエネルギー
        Custom = 99     // カスタムエネルギー
    }

    /// <summary>
    /// エネルギー効果種類
    /// </summary>
    public enum EnergyEffectType
    {
        None = 0,           // 効果なし
        Damage = 1,         // ダメージ増減
        Protection = 2,     // 保護効果
        Draw = 3,           // ドロー効果
        Heal = 4,           // 回復効果
        StatusEffect = 5,   // 状態異常
        Retreat = 6,        // にげる関連
        Attack = 7,         // ワザ関連
        Ability = 8,        // 特性関連
        Evolution = 9,      // 進化関連
        Custom = 99         // カスタム効果
    }

    /// <summary>
    /// エネルギー制限種類
    /// </summary>
    public enum EnergyRestrictionType
    {
        None = 0,               // 制限なし
        AttachmentRestriction = 1, // つける制限
        UsageRestriction = 2,   // 使用制限
        DiscardRestriction = 3, // トラッシュ制限
        EffectRestriction = 4,  // 効果制限
        TypeRestriction = 5,    // タイプ制限
        Custom = 99             // カスタム制限
    }

    #endregion

    #region Data Classes

    /// <summary>
    /// エネルギー効果データ
    /// </summary>
    [System.Serializable]
    public class EnergyEffect
    {
        [SerializeField] private string _effectName;
        [SerializeField] private EnergyEffectType _effectType;
        [SerializeField] private int _power = 0;
        [TextArea(2, 4)]
        [SerializeField] private string _description;
        [SerializeField] private List<string> _parameters = new List<string>();
        [SerializeField] private bool _isPassive = true;

        public string EffectName => _effectName;
        public EnergyEffectType EffectType => _effectType;
        public int Power => _power;
        public string Description => _description;
        public List<string> Parameters => _parameters;
        public bool IsPassive => _isPassive;
    }

    /// <summary>
    /// エネルギー制限データ
    /// </summary>
    [System.Serializable]
    public class EnergyRestriction
    {
        [SerializeField] private string _restrictionName;
        [SerializeField] private EnergyRestrictionType _restrictionType;
        [SerializeField] private string _description;
        [SerializeField] private List<string> _conditions = new List<string>();

        public string RestrictionName => _restrictionName;
        public EnergyRestrictionType RestrictionType => _restrictionType;
        public string Description => _description;
        public List<string> Conditions => _conditions;
    }

    /// <summary>
    /// つける制限データ
    /// </summary>
    [System.Serializable]
    public class AttachmentRestriction
    {
        [SerializeField] private string _restrictionName;
        [SerializeField] private AttachmentRestrictionType _restrictionType;
        [SerializeField] private string _description;
        [SerializeField] private List<string> _allowedTypes = new List<string>();
        [SerializeField] private List<string> _allowedNames = new List<string>();

        public string RestrictionName => _restrictionName;
        public AttachmentRestrictionType RestrictionType => _restrictionType;
        public string Description => _description;
        public List<string> AllowedTypes => _allowedTypes;
        public List<string> AllowedNames => _allowedNames;

        /// <summary>
        /// 制限チェック
        /// </summary>
        /// <param name="pokemonCard">対象ポケモン</param>
        /// <returns>つけられる場合true</returns>
        public bool CheckRestriction(PokemonCardData pokemonCard)
        {
            switch (_restrictionType)
            {
                case AttachmentRestrictionType.PokemonType:
                    return _allowedTypes.Contains(pokemonCard.PokemonType.ToString());
                
                case AttachmentRestrictionType.PokemonName:
                    return _allowedNames.Contains(pokemonCard.CardName);
                
                case AttachmentRestrictionType.EvolutionStage:
                    return _allowedTypes.Contains(pokemonCard.EvolutionStage.ToString());
                
                default:
                    return true;
            }
        }
    }

    /// <summary>
    /// 条件付きエネルギー供給データ
    /// </summary>
    [System.Serializable]
    public class ConditionalEnergyProvision
    {
        [SerializeField] private string _conditionName;
        [SerializeField] private int _bonusEnergy = 1;
        [SerializeField] private string _conditionDescription;
        [SerializeField] private List<string> _conditionParameters = new List<string>();

        public string ConditionName => _conditionName;
        public int BonusEnergy => _bonusEnergy;
        public string ConditionDescription => _conditionDescription;
        public List<string> ConditionParameters => _conditionParameters;

        /// <summary>
        /// 条件チェック
        /// </summary>
        /// <param name="pokemonCard">つけられているポケモン</param>
        /// <param name="gameState">ゲーム状態</param>
        /// <returns>条件を満たす場合true</returns>
        public bool CheckCondition(PokemonCardData pokemonCard, object gameState)
        {
            // TODO: 実際の条件チェックロジックを実装
            return true;
        }
    }

    /// <summary>
    /// つける制限種類
    /// </summary>
    public enum AttachmentRestrictionType
    {
        None = 0,           // 制限なし
        PokemonType = 1,    // ポケモンタイプ
        PokemonName = 2,    // ポケモン名
        EvolutionStage = 3, // 進化段階
        SpecialType = 4,    // 特殊タイプ
        Custom = 99         // カスタム制限
    }

    #endregion
}