using System.Collections.Generic;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;
using PokemonTCG.Core.Architecture; // ServiceLocator用
using PokemonTCG.Game; // GameStateManager用

namespace PokemonTCG.Cards.Effects.Common
{
    /// <summary>
    /// ダメージエフェクト
    /// ポケモンにダメージを与える基本エフェクト
    /// 弱点・抵抗力・その他の修正も適用
    /// </summary>
    public class DamageEffect : BaseCardEffect, ITargetableEffect
    {
        #region Fields

        private int _baseDamage;
        private DamageType _damageType;
        private bool _ignoreWeakness;
        private bool _ignoreResistance;
        private bool _pierceProtection;
        private List<DamageModifier> _modifiers;

        #endregion

        #region Properties

        public override string EffectName => "Damage";
        public override string Description => $"Deal {_baseDamage} damage to target Pokemon";
        public override EffectType EffectType => EffectType.Damage;
        public override int Priority => 100; // 高優先度
        public override float EstimatedDuration => 0.5f;

        // ITargetableEffect implementation
        public TargetType TargetType => TargetType.Pokemon;
        public int RequiredTargets => 1;
        public int MaxTargets => 1;

        /// <summary>基本ダメージ量</summary>
        public int BaseDamage => _baseDamage;

        /// <summary>ダメージタイプ</summary>
        public DamageType DamageType => _damageType;

        #endregion

        #region Constructors

        /// <summary>
        /// 基本コンストラクタ
        /// </summary>
        /// <param name="damage">ダメージ量</param>
        public DamageEffect(int damage) : this(damage, DamageType.Attack)
        {
        }

        /// <summary>
        /// 詳細コンストラクタ
        /// </summary>
        /// <param name="damage">ダメージ量</param>
        /// <param name="damageType">ダメージタイプ</param>
        public DamageEffect(int damage, DamageType damageType)
        {
            _baseDamage = damage;
            _damageType = damageType;
            _ignoreWeakness = false;
            _ignoreResistance = false;
            _pierceProtection = false;
            _modifiers = new List<DamageModifier>();
        }

        /// <summary>
        /// 設定付きコンストラクタ
        /// </summary>
        /// <param name="damage">ダメージ量</param>
        /// <param name="damageType">ダメージタイプ</param>
        /// <param name="ignoreWeakness">弱点を無視するか</param>
        /// <param name="ignoreResistance">抵抗力を無視するか</param>
        /// <param name="pierceProtection">保護効果を貫通するか</param>
        public DamageEffect(int damage, DamageType damageType, bool ignoreWeakness, bool ignoreResistance, bool pierceProtection = false)
        {
            _baseDamage = damage;
            _damageType = damageType;
            _ignoreWeakness = ignoreWeakness;
            _ignoreResistance = ignoreResistance;
            _pierceProtection = pierceProtection;
            _modifiers = new List<DamageModifier>();
        }

        #endregion

        #region Effect Implementation

        public override bool CanExecute(EffectContext context)
        {
            if (!ValidateContext(context))
                return false;

            // 対象チェック
            if (context.PrimaryTarget == null)
            {
                LogError("No target specified for damage effect");
                return false;
            }

            // 対象がポケモンかチェック
            if (!IsValidTarget(context, context.PrimaryTarget))
            {
                LogError("Target is not a valid Pokemon");
                return false;
            }

            // ポケモンがきぜつしていないかチェック
            if (context.PrimaryTarget is Card pokemonCard)
            {
                if (pokemonCard.CardState == CardState.Knocked)
                {
                    LogError("Cannot damage knocked out Pokemon");
                    return false;
                }
            }

            return true;
        }

        public override EffectResult Execute(EffectContext context)
        {
            if (!CanExecute(context))
            {
                return EffectResult.Failed("Cannot execute damage effect");
            }

            var targetCard = context.PrimaryTarget as Card;
            var pokemonData = targetCard.GetPokemonData();

            if (pokemonData == null)
            {
                return EffectResult.Failed("Target is not a Pokemon card");
            }

            // ダメージ計算
            int finalDamage = CalculateFinalDamage(context, targetCard, pokemonData);

            // 保護効果チェック
            if (!_pierceProtection && IsProtected(targetCard, context))
            {
                LogEffect($"Damage blocked by protection effect on {pokemonData.CardName}");
                return EffectResult.Failed("Damage blocked by protection");
            }

            // ダメージ適用
            var damageResult = ApplyDamage(targetCard, finalDamage, context);

            // 結果作成
            var result = EffectResult.Success($"Dealt {finalDamage} damage to {pokemonData.CardName}", finalDamage);
            result.Targets.Add(targetCard);
            result.UndoData = new DamageUndoData { Target = targetCard, Damage = finalDamage };

            // ログ出力
            LogEffect($"Dealt {finalDamage} damage to {pokemonData.CardName}");

            // 副次効果をチェック
            CheckSecondaryEffects(targetCard, finalDamage, context, result);

            return result;
        }

        public override bool Undo(EffectContext context)
        {
            if (context.ExecutionResult?.UndoData is DamageUndoData undoData)
            {
                // TODO: 実際のヒールエフェクトが実装されたら使用
                // 現在は仮実装として直接HPを回復
                LogEffect($"Undoing {undoData.Damage} damage to target");
                return true;
            }

            return false;
        }

        #endregion

        #region ITargetableEffect Implementation

        public List<object> FilterValidTargets(EffectContext context, List<object> potentialTargets)
        {
            var validTargets = new List<object>();

            foreach (var target in potentialTargets)
            {
                if (IsValidTarget(context, target))
                {
                    validTargets.Add(target);
                }
            }

            return validTargets;
        }

        public bool IsValidTarget(EffectContext context, object target)
        {
            // ポケモンカードかチェック
            if (!(target is Card card) || !card.IsPokemonCard)
                return false;

            // きぜつしていないかチェック
            if (card.CardState == CardState.Knocked)
                return false;

            // 場にいるかチェック
            if (!card.IsInPlay)
                return false;

            // ダメージタイプ固有の条件
            switch (_damageType)
            {
                case DamageType.Attack:
                    // 攻撃ダメージは相手のポケモンのみ
                    return context.IsOpponentCard(card);
                
                case DamageType.Ability:
                    // 特性ダメージは条件により自分・相手両方可能
                    return true;
                
                case DamageType.Recoil:
                    // 反動ダメージは自分のポケモンのみ
                    return context.IsOwnCard(card);
                
                case DamageType.Burn:
                case DamageType.Poison:
                    // 状態異常ダメージは既に状態異常のポケモンのみ
                    return card.HasStatus(GetStatusFromDamageType(_damageType));
                
                default:
                    return true;
            }
        }

        #endregion

        #region Damage Calculation

        /// <summary>
        /// 最終ダメージを計算
        /// </summary>
        /// <param name="context">エフェクトコンテキスト</param>
        /// <param name="targetCard">対象カード</param>
        /// <param name="pokemonData">ポケモンデータ</param>
        /// <returns>最終ダメージ量</returns>
        private int CalculateFinalDamage(EffectContext context, Card targetCard, PokemonCardData pokemonData)
        {
            int damage = _baseDamage;

            // コンテキストからパワー値を取得
            if (context.Power > 0)
            {
                damage = context.Power;
            }

            // 弱点計算
            if (!_ignoreWeakness)
            {
                damage = ApplyWeakness(damage, pokemonData, context);
            }

            // 抵抗力計算
            if (!_ignoreResistance)
            {
                damage = ApplyResistance(damage, pokemonData, context);
            }

            // ダメージ修正適用
            damage = ApplyDamageModifiers(damage, targetCard, context);

            // 最小値制限（0以下にはならない）
            damage = Mathf.Max(0, damage);

            return damage;
        }

        /// <summary>
        /// 弱点を適用
        /// </summary>
        /// <param name="damage">基本ダメージ</param>
        /// <param name="pokemonData">対象ポケモンデータ</param>
        /// <param name="context">コンテキスト</param>
        /// <returns>弱点適用後ダメージ</returns>
        private int ApplyWeakness(int damage, PokemonCardData pokemonData, EffectContext context)
        {
            // 攻撃ポケモンのタイプを取得
            var attackerType = GetAttackerType(context);
            
            if (attackerType == pokemonData.WeaknessType)
            {
                // ルールに基づく弱点倍率を適用
                var gameStateManager = ServiceLocator.Get<GameStateManager>();
                var gameRules = gameStateManager?.ActiveRule?.RuleData;
                
                if (gameRules != null)
                {
                    if (gameRules.WeaknessMultiplier > 1.0f)
                    {
                        // 従来版: ×2
                        damage = Mathf.RoundToInt(damage * gameRules.WeaknessMultiplier);
                    }
                    else
                    {
                        // ポケット版: +20
                        damage += 20;
                    }
                }
                else
                {
                    // デフォルト: ×2
                    damage *= 2;
                }

                LogEffect($"Weakness applied: {attackerType} -> {pokemonData.WeaknessType}");
            }

            return damage;
        }

        /// <summary>
        /// 抵抗力を適用
        /// </summary>
        /// <param name="damage">基本ダメージ</param>
        /// <param name="pokemonData">対象ポケモンデータ</param>
        /// <param name="context">コンテキスト</param>
        /// <returns>抵抗力適用後ダメージ</returns>
        private int ApplyResistance(int damage, PokemonCardData pokemonData, EffectContext context)
        {
            var attackerType = GetAttackerType(context);
            
            if (attackerType == pokemonData.ResistanceType)
            {
                // ルールに基づく抵抗力軽減を適用
                var gameStateManager = ServiceLocator.Get<GameStateManager>();
                var gameRules = gameStateManager?.ActiveRule?.RuleData;
                int reduction = gameRules?.ResistanceReduction ?? 30;
                
                damage = Mathf.Max(0, damage - reduction);
                LogEffect($"Resistance applied: -{reduction} damage");
            }

            return damage;
        }


        /// <summary>
        /// ダメージ修正を適用
        /// </summary>
        /// <param name="damage">基本ダメージ</param>
        /// <param name="targetCard">対象カード</param>
        /// <param name="context">コンテキスト</param>
        /// <returns>修正後ダメージ</returns>
        private int ApplyDamageModifiers(int damage, Card targetCard, EffectContext context)
        {
            foreach (var modifier in _modifiers)
            {
                damage = modifier.ApplyModifier(damage, targetCard, context);
            }

            return damage;
        }

        /// <summary>
        /// 攻撃者のタイプを取得
        /// </summary>
        /// <param name="context">コンテキスト</param>
        /// <returns>攻撃者のポケモンタイプ</returns>
        private PokemonType GetAttackerType(EffectContext context)
        {
            if (context.SourceCard.IsPokemonCard)
            {
                var pokemonData = context.SourceCard.GetPokemonData();
                return pokemonData?.PokemonType ?? PokemonType.Colorless;
            }

            return PokemonType.Colorless;
        }

        #endregion

        #region Damage Application

        /// <summary>
        /// ダメージを適用
        /// </summary>
        /// <param name="targetCard">対象カード</param>
        /// <param name="damage">ダメージ量</param>
        /// <param name="context">コンテキスト</param>
        /// <returns>ダメージ適用結果</returns>
        private DamageResult ApplyDamage(Card targetCard, int damage, EffectContext context)
        {
            var result = new DamageResult
            {
                TargetCard = targetCard,
                DamageDealt = damage,
                DamageType = _damageType
            };

            // TODO: 実際のダメージシステムと連携
            // 現在は仮実装
            
            // ダメージカウンターの概念がある場合はここで管理
            // var currentDamage = GetCurrentDamage(targetCard);
            // var newDamage = currentDamage + damage;
            // SetCurrentDamage(targetCard, newDamage);

            // きぜつ判定
            var pokemonData = targetCard.GetPokemonData();
            // if (newDamage >= pokemonData.HP)
            // {
            //     result.IsKnockedOut = true;
            //     targetCard.SetState(CardState.Knocked);
            // }

            return result;
        }

        /// <summary>
        /// 保護効果をチェック
        /// </summary>
        /// <param name="targetCard">対象カード</param>
        /// <param name="context">コンテキスト</param>
        /// <returns>保護されている場合true</returns>
        private bool IsProtected(Card targetCard, EffectContext context)
        {
            // TODO: 保護効果システムと連携
            // 現在は仮実装
            return false;
        }

        #endregion

        #region Secondary Effects

        /// <summary>
        /// 副次効果をチェック
        /// </summary>
        /// <param name="targetCard">対象カード</param>
        /// <param name="damage">与えたダメージ</param>
        /// <param name="context">コンテキスト</param>
        /// <param name="result">実行結果</param>
        private void CheckSecondaryEffects(Card targetCard, int damage, EffectContext context, EffectResult result)
        {
            // きぜつ判定
            var pokemonData = targetCard.GetPokemonData();
            if (pokemonData != null)
            {
                // TODO: 実際のHP/ダメージシステムと連携
                // if (IsKnockedOut(targetCard))
                // {
                //     var knockoutEffect = new KnockoutEffect();
                //     var knockoutResult = knockoutEffect.Execute(context);
                //     result.SecondaryEffects.Add(knockoutResult);
                // }
            }

            // ダメージタイプによる副次効果
            switch (_damageType)
            {
                case DamageType.Burn:
                    // やけど状態を適用
                    targetCard.AddStatus(new CardStatus(CardState.Burned));
                    break;
                
                case DamageType.Poison:
                    // どく状態を適用
                    targetCard.AddStatus(new CardStatus(CardState.Poisoned));
                    break;
            }
        }

        /// <summary>
        /// ダメージタイプから状態異常を取得
        /// </summary>
        /// <param name="damageType">ダメージタイプ</param>
        /// <returns>対応する状態異常</returns>
        private CardState GetStatusFromDamageType(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Burn:
                    return CardState.Burned;
                case DamageType.Poison:
                    return CardState.Poisoned;
                default:
                    return CardState.Normal;
            }
        }

        #endregion

        #region Modifier Management

        /// <summary>
        /// ダメージ修正を追加
        /// </summary>
        /// <param name="modifier">修正</param>
        public void AddModifier(DamageModifier modifier)
        {
            if (modifier != null && !_modifiers.Contains(modifier))
            {
                _modifiers.Add(modifier);
            }
        }

        /// <summary>
        /// ダメージ修正を削除
        /// </summary>
        /// <param name="modifier">修正</param>
        public void RemoveModifier(DamageModifier modifier)
        {
            _modifiers.Remove(modifier);
        }

        /// <summary>
        /// 全ての修正をクリア
        /// </summary>
        public void ClearModifiers()
        {
            _modifiers.Clear();
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// 攻撃ダメージエフェクトを作成
        /// </summary>
        /// <param name="damage">ダメージ量</param>
        /// <returns>攻撃ダメージエフェクト</returns>
        public static DamageEffect CreateAttackDamage(int damage)
        {
            return new DamageEffect(damage, DamageType.Attack);
        }

        /// <summary>
        /// 反動ダメージエフェクトを作成
        /// </summary>
        /// <param name="damage">ダメージ量</param>
        /// <returns>反動ダメージエフェクト</returns>
        public static DamageEffect CreateRecoilDamage(int damage)
        {
            return new DamageEffect(damage, DamageType.Recoil, true, true);
        }

        /// <summary>
        /// 貫通ダメージエフェクトを作成
        /// </summary>
        /// <param name="damage">ダメージ量</param>
        /// <returns>貫通ダメージエフェクト</returns>
        public static DamageEffect CreatePiercingDamage(int damage)
        {
            return new DamageEffect(damage, DamageType.Attack, true, true, true);
        }

        /// <summary>
        /// やけどダメージエフェクトを作成
        /// </summary>
        /// <param name="damage">ダメージ量</param>
        /// <returns>やけどダメージエフェクト</returns>
        public static DamageEffect CreateBurnDamage(int damage = 20)
        {
            return new DamageEffect(damage, DamageType.Burn, true, true);
        }

        /// <summary>
        /// どくダメージエフェクトを作成
        /// </summary>
        /// <param name="damage">ダメージ量</param>
        /// <returns>どくダメージエフェクト</returns>
        public static DamageEffect CreatePoisonDamage(int damage = 10)
        {
            return new DamageEffect(damage, DamageType.Poison, true, true);
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// ダメージタイプ
    /// </summary>
    public enum DamageType
    {
        Attack = 0,     // 攻撃ダメージ
        Ability = 1,    // 特性ダメージ
        Recoil = 2,     // 反動ダメージ
        Burn = 3,       // やけどダメージ
        Poison = 4,     // どくダメージ
        Counter = 5,    // カウンターダメージ
        Direct = 6,     // 直接ダメージ
        Special = 99    // 特殊ダメージ
    }

    /// <summary>
    /// ダメージ修正
    /// </summary>
    public abstract class DamageModifier
    {
        public abstract int ApplyModifier(int damage, Card target, EffectContext context);
    }

    /// <summary>
    /// ダメージ結果
    /// </summary>
    public class DamageResult
    {
        public Card TargetCard { get; set; }
        public int DamageDealt { get; set; }
        public DamageType DamageType { get; set; }
        public bool IsKnockedOut { get; set; } = false;
        public List<CardStatus> StatusEffects { get; set; } = new List<CardStatus>();
    }

    /// <summary>
    /// ダメージ取り消し用データ
    /// </summary>
    public class DamageUndoData
    {
        public Card Target { get; set; }
        public int Damage { get; set; }
    }

    #endregion
}