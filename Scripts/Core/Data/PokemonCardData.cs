using System.Collections.Generic;
using UnityEngine;

namespace PokemonTCG.Core.Data
{
    /// <summary>
    /// ポケモンカードデータの基底クラス
    /// たね・進化・特殊ポケモンの共通機能を定義
    /// </summary>
    [CreateAssetMenu(fileName = "NewPokemonCard", menuName = "Pokemon TCG/Cards/Pokemon/Base Pokemon")]
    public class PokemonCardData : BaseCardData
    {
        #region Pokemon Basic Info

        [Header("ポケモン基本情報")]
        [SerializeField] private int _hp = 60;
        [SerializeField] private PokemonType _pokemonType = PokemonType.Colorless;
        [SerializeField] private PokemonType _weaknessType = PokemonType.None;
        [SerializeField] private PokemonType _resistanceType = PokemonType.None;
        [SerializeField] private int _retreatCost = 1;

        [Header("進化情報")]
        [SerializeField] private EvolutionStage _evolutionStage = EvolutionStage.Basic;
        [SerializeField] private PokemonCardData _evolvesFrom;
        [SerializeField] private List<PokemonCardData> _evolutionTargets = new List<PokemonCardData>();

        [Header("特殊分類")]
        [SerializeField] private SpecialPokemonType _specialType = SpecialPokemonType.Normal;
        [SerializeField] private bool _isRuleBox = false;
        [SerializeField] private int _prizePenalty = 1; // 倒されたときに相手が取るサイド枚数

        #endregion

        #region Abilities and Attacks

        [Header("特性")]
        [SerializeField] private List<PokemonAbility> _abilities = new List<PokemonAbility>();

        [Header("ワザ")]
        [SerializeField] private List<PokemonAttack> _attacks = new List<PokemonAttack>();

        #endregion

        #region Properties

        /// <summary>HP</summary>
        public int HP => _hp;

        /// <summary>ポケモンタイプ</summary>
        public PokemonType PokemonType => _pokemonType;

        /// <summary>弱点タイプ</summary>
        public PokemonType WeaknessType => _weaknessType;

        /// <summary>抵抗力タイプ</summary>
        public PokemonType ResistanceType => _resistanceType;

        /// <summary>にげるコスト</summary>
        public int RetreatCost => _retreatCost;

        /// <summary>進化段階</summary>
        public EvolutionStage EvolutionStage => _evolutionStage;

        /// <summary>進化元ポケモン</summary>
        public PokemonCardData EvolvesFrom => _evolvesFrom;

        /// <summary>進化先ポケモンリスト</summary>
        public List<PokemonCardData> EvolutionTargets => _evolutionTargets;

        /// <summary>特殊ポケモン種類</summary>
        public SpecialPokemonType SpecialType => _specialType;

        /// <summary>ルールボックス持ちか</summary>
        public bool IsRuleBox => _isRuleBox;

        /// <summary>サイドペナルティ</summary>
        public int PrizePenalty => _prizePenalty;

        /// <summary>特性リスト</summary>
        public List<PokemonAbility> Abilities => _abilities;

        /// <summary>ワザリスト</summary>
        public List<PokemonAttack> Attacks => _attacks;

        /// <summary>カード種類</summary>
        public override CardType CardType => CardType.Pokemon;

        /// <summary>たねポケモンかどうか</summary>
        public bool IsBasicPokemon => _evolutionStage == EvolutionStage.Basic;

        /// <summary>進化ポケモンかどうか</summary>
        public bool IsEvolutionPokemon => _evolutionStage != EvolutionStage.Basic;

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
            if (_hp <= 0)
                errors.Add("HP must be greater than 0");

            if (_retreatCost < 0)
                errors.Add("Retreat cost cannot be negative");

            if (_prizePenalty < 1)
                errors.Add("Prize penalty must be at least 1");

            // 進化チェック
            if (_evolutionStage != EvolutionStage.Basic && _evolvesFrom == null)
                errors.Add($"{_evolutionStage} Pokemon must have an evolution source");

            if (_evolutionStage == EvolutionStage.Basic && _evolvesFrom != null)
                errors.Add("Basic Pokemon cannot have an evolution source");

            // 進化チェーンの整合性
            if (_evolvesFrom != null)
            {
                if (_evolvesFrom._evolutionStage >= _evolutionStage)
                    errors.Add("Evolution stage must be higher than evolution source");

                if (!_evolvesFrom._evolutionTargets.Contains(this))
                    errors.Add("Evolution source must reference this card in evolution targets");
            }

            // 特殊ポケモンチェック
            if (_specialType != SpecialPokemonType.Normal)
            {
                if (_specialType == SpecialPokemonType.Ex || 
                    _specialType == SpecialPokemonType.V || 
                    _specialType == SpecialPokemonType.VMax ||
                    _specialType == SpecialPokemonType.VStar)
                {
                    if (_prizePenalty < 2)
                        errors.Add($"{_specialType} Pokemon should have prize penalty of 2 or more");
                }
            }

            // ワザチェック
            foreach (var attack in _attacks)
            {
                if (attack == null)
                {
                    errors.Add("Attack cannot be null");
                    continue;
                }

                if (string.IsNullOrEmpty(attack.AttackName))
                    errors.Add("Attack name cannot be empty");

                if (attack.Damage < 0)
                    errors.Add("Attack damage cannot be negative");

                if (attack.EnergyCost.Count > 5)
                    errors.Add("Attack energy cost is too high (max 5)");
            }

            // 特性チェック
            foreach (var ability in _abilities)
            {
                if (ability == null)
                {
                    errors.Add("Ability cannot be null");
                    continue;
                }

                if (string.IsNullOrEmpty(ability.AbilityName))
                    errors.Add("Ability name cannot be empty");
            }

            return errors;
        }

        public override int GetContentHash()
        {
            int hash = base.GetHashCode();
            hash ^= _hp.GetHashCode();
            hash ^= _pokemonType.GetHashCode();
            hash ^= _evolutionStage.GetHashCode();
            hash ^= _specialType.GetHashCode();
            
            // ワザのハッシュを追加
            foreach (var attack in _attacks)
            {
                if (attack != null)
                    hash ^= attack.GetHashCode();
            }
            
            return hash;
        }

        #endregion

        #region Unity Lifecycle

        protected override void OnValidate()
        {
            base.OnValidate();

            #if UNITY_EDITOR
            ValidatePokemonData();
            #endif
        }

        #if UNITY_EDITOR
        private void ValidatePokemonData()
        {
            // HP範囲チェック
            _hp = Mathf.Clamp(_hp, 10, 340);
            
            // にげるコスト範囲チェック
            _retreatCost = Mathf.Clamp(_retreatCost, 0, 4);
            
            // サイドペナルティ範囲チェック
            _prizePenalty = Mathf.Clamp(_prizePenalty, 1, 3);

            // 特殊タイプによるルールボックス自動設定
            if (_specialType != SpecialPokemonType.Normal)
            {
                _isRuleBox = true;
            }

            // 進化段階による自動調整
            if (_evolutionStage == EvolutionStage.Basic)
            {
                _evolvesFrom = null;
            }
        }
        #endif

        #endregion

        #region Helper Methods

        /// <summary>
        /// 指定タイプのワザを持っているかチェック
        /// </summary>
        /// <param name="energyType">エネルギータイプ</param>
        /// <returns>持っている場合true</returns>
        public bool HasAttackWithEnergyType(PokemonType energyType)
        {
            foreach (var attack in _attacks)
            {
                if (attack.EnergyCost.Contains(energyType))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 指定名の特性を取得
        /// </summary>
        /// <param name="abilityName">特性名</param>
        /// <returns>特性データ（見つからない場合null）</returns>
        public PokemonAbility GetAbility(string abilityName)
        {
            return _abilities.Find(ability => ability.AbilityName == abilityName);
        }

        /// <summary>
        /// 指定名のワザを取得
        /// </summary>
        /// <param name="attackName">ワザ名</param>
        /// <returns>ワザデータ（見つからない場合null）</returns>
        public PokemonAttack GetAttack(string attackName)
        {
            return _attacks.Find(attack => attack.AttackName == attackName);
        }

        /// <summary>
        /// 進化可能かチェック
        /// </summary>
        /// <param name="targetCard">進化先カード</param>
        /// <returns>進化可能な場合true</returns>
        public bool CanEvolveTo(PokemonCardData targetCard)
        {
            return _evolutionTargets.Contains(targetCard);
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// ポケモンタイプ
    /// </summary>
    public enum PokemonType
    {
        None = 0,       // なし
        Grass = 1,      // 草
        Fire = 2,       // 炎
        Water = 3,      // 水
        Lightning = 4,  // 雷
        Psychic = 5,    // 超
        Fighting = 6,   // 闘
        Darkness = 7,   // 悪
        Metal = 8,      // 鋼
        Fairy = 9,      // フェアリー
        Dragon = 10,    // ドラゴン
        Colorless = 11  // 無色
    }

    /// <summary>
    /// 進化段階
    /// </summary>
    public enum EvolutionStage
    {
        Basic = 0,      // たねポケモン
        Stage1 = 1,     // 1進化ポケモン
        Stage2 = 2,     // 2進化ポケモン
        Mega = 3,       // メガ進化
        Break = 4,      // BREAK進化
        LevelX = 5      // LV.X
    }

    /// <summary>
    /// 特殊ポケモン種類
    /// </summary>
    public enum SpecialPokemonType
    {
        Normal = 0,     // 通常ポケモン
        Ex = 1,         // ポケモンex
        V = 2,          // ポケモンV
        VMax = 3,       // ポケモンVMAX
        VStar = 4,      // ポケモンVSTAR
        Tag = 5,        // TAG TEAM
        Prime = 6,      // ポケモンPRIME
        Legend = 7,     // ポケモンLEGEND
        GX = 8,         // ポケモンGX
        EX = 9,         // ポケモンEX
        Star = 10       // ポケモン☆
    }

    #endregion

    #region Data Classes

    /// <summary>
    /// ポケモンの特性データ
    /// </summary>
    [System.Serializable]
    public class PokemonAbility
    {
        [SerializeField] private string _abilityName;
        [SerializeField] private AbilityType _abilityType;
        [TextArea(2, 4)]
        [SerializeField] private string _description;
        [SerializeField] private bool _canUseMultipleTimes = false;
        [SerializeField] private List<string> _effectParameters = new List<string>();

        public string AbilityName => _abilityName;
        public AbilityType AbilityType => _abilityType;
        public string Description => _description;
        public bool CanUseMultipleTimes => _canUseMultipleTimes;
        public List<string> EffectParameters => _effectParameters;
    }

    /// <summary>
    /// ポケモンのワザデータ
    /// </summary>
    [System.Serializable]
    public class PokemonAttack
    {
        [SerializeField] private string _attackName;
        [SerializeField] private List<PokemonType> _energyCost = new List<PokemonType>();
        [SerializeField] private int _damage = 0;
        [TextArea(2, 4)]
        [SerializeField] private string _description;
        [SerializeField] private List<string> _effectParameters = new List<string>();

        public string AttackName => _attackName;
        public List<PokemonType> EnergyCost => _energyCost;
        public int Damage => _damage;
        public string Description => _description;
        public List<string> EffectParameters => _effectParameters;

        /// <summary>
        /// エネルギーコストの合計
        /// </summary>
        public int TotalEnergyCost => _energyCost.Count;

        /// <summary>
        /// 指定タイプのエネルギー必要数
        /// </summary>
        public int GetEnergyCount(PokemonType energyType)
        {
            int count = 0;
            foreach (var energy in _energyCost)
            {
                if (energy == energyType)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// 特性の種類
    /// </summary>
    public enum AbilityType
    {
        Ability = 0,        // 特性
        PokePower = 1,      // ポケパワー
        PokeBody = 2,       // ポケボディー
        AncientTrait = 3    // 古代能力
    }

    #endregion
}