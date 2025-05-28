using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.Cards.Effects.Common
{
    #region Heal Effect

    /// <summary>
    /// 回復エフェクト
    /// ポケモンのダメージを回復する
    /// </summary>
    public class HealEffect : BaseCardEffect, ITargetableEffect
    {
        private int _healAmount;
        private bool _healAllDamage;
        private bool _removeStatusEffects;

        public override string EffectName => "Heal";
        public override string Description => _healAllDamage ? "Heal all damage" : $"Heal {_healAmount} damage";
        public override EffectType EffectType => EffectType.Heal;
        public override int Priority => 80;

        // ITargetableEffect implementation
        public TargetType TargetType => TargetType.Pokemon;
        public int RequiredTargets => 1;
        public int MaxTargets => 1;

        public HealEffect(int healAmount, bool removeStatusEffects = false)
        {
            _healAmount = healAmount;
            _healAllDamage = false;
            _removeStatusEffects = removeStatusEffects;
        }

        public HealEffect(bool healAll = true, bool removeStatusEffects = false)
        {
            _healAmount = 0;
            _healAllDamage = healAll;
            _removeStatusEffects = removeStatusEffects;
        }

        public override bool CanExecute(EffectContext context)
        {
            if (!ValidateContext(context))
                return false;

            if (context.PrimaryTarget == null || !IsValidTarget(context, context.PrimaryTarget))
            {
                LogError("Invalid target for heal effect");
                return false;
            }

            return true;
        }

        public override EffectResult Execute(EffectContext context)
        {
            if (!CanExecute(context))
                return EffectResult.Failed("Cannot execute heal effect");

            var targetCard = context.PrimaryTarget as Card;
            var pokemonData = targetCard.GetPokemonData();

            // TODO: 実際のダメージシステムと連携
            int actualHealAmount = _healAllDamage ? pokemonData.HP : _healAmount;

            // 状態異常除去
            if (_removeStatusEffects)
            {
                var statusesToRemove = new List<CardState> 
                { 
                    CardState.Poisoned, CardState.Burned, CardState.Paralyzed, 
                    CardState.Confused, CardState.Asleep 
                };
                
                foreach (var status in statusesToRemove)
                {
                    targetCard.RemoveStatus(status);
                }
            }

            LogEffect($"Healed {actualHealAmount} damage from {pokemonData.CardName}");

            return EffectResult.Success($"Healed {actualHealAmount} damage", actualHealAmount);
        }

        public List<object> FilterValidTargets(EffectContext context, List<object> potentialTargets)
        {
            return potentialTargets.Where(target => IsValidTarget(context, target)).ToList();
        }

        public bool IsValidTarget(EffectContext context, object target)
        {
            if (!(target is Card card) || !card.IsPokemonCard)
                return false;

            // きぜつしたポケモンは回復できない
            if (card.CardState == CardState.Knocked)
                return false;

            // TODO: ダメージを受けているかチェック
            return true;
        }
    }

    #endregion

    #region Draw Effect

    /// <summary>
    /// ドローエフェクト
    /// カードを引く
    /// </summary>
    public class DrawEffect : BaseCardEffect
    {
        private int _drawCount;
        private bool _upToAmount;
        private int _maxHandSize;

        public override string EffectName => "Draw";
        public override string Description => _upToAmount ? 
            $"Draw up to {_drawCount} cards" : 
            $"Draw {_drawCount} card{(_drawCount > 1 ? "s" : "")}";
        public override EffectType EffectType => EffectType.Draw;
        public override int Priority => 60;

        public DrawEffect(int drawCount, bool upToAmount = false, int maxHandSize = 10)
        {
            _drawCount = drawCount;
            _upToAmount = upToAmount;
            _maxHandSize = maxHandSize;
        }

        public override bool CanExecute(EffectContext context)
        {
            if (!ValidateContext(context))
                return false;

            // プレイヤーが存在するかチェック
            if (context.CurrentPlayer == null)
            {
                LogError("No player specified for draw effect");
                return false;
            }

            // TODO: デッキが空でないかチェック
            // if (context.CurrentPlayer.Deck.IsEmpty)
            // {
            //     LogError("Cannot draw from empty deck");
            //     return false;
            // }

            return true;
        }

        public override EffectResult Execute(EffectContext context)
        {
            if (!CanExecute(context))
                return EffectResult.Failed("Cannot execute draw effect");

            // TODO: 実際のドローシステムと連携
            // var drawnCards = context.CurrentPlayer.DrawCards(_drawCount);
            int actualDrawCount = _drawCount;

            // 手札上限チェック
            // if (context.CurrentPlayer.Hand.Count + actualDrawCount > _maxHandSize)
            // {
            //     actualDrawCount = _maxHandSize - context.CurrentPlayer.Hand.Count;
            // }

            LogEffect($"Drew {actualDrawCount} cards");

            var result = EffectResult.Success($"Drew {actualDrawCount} cards", actualDrawCount);
            return result;
        }

        public static DrawEffect Draw(int count) => new DrawEffect(count);
        public static DrawEffect DrawUpTo(int count) => new DrawEffect(count, true);
    }

    #endregion

    #region Search Effect

    /// <summary>
    /// サーチエフェクト
    /// デッキからカードを検索する
    /// </summary>
    public class SearchEffect : BaseCardEffect
    {
        private int _searchCount;
        private SearchCriteria _criteria;
        private bool _revealCards;
        private bool _shuffleAfter;

        public override string EffectName => "Search";
        public override string Description => $"Search for {_searchCount} {_criteria.Description}";
        public override EffectType EffectType => EffectType.Search;
        public override int Priority => 70;

        public SearchEffect(int searchCount, SearchCriteria criteria, bool revealCards = false, bool shuffleAfter = true)
        {
            _searchCount = searchCount;
            _criteria = criteria;
            _revealCards = revealCards;
            _shuffleAfter = shuffleAfter;
        }

        public override bool CanExecute(EffectContext context)
        {
            if (!ValidateContext(context))
                return false;

            if (context.CurrentPlayer == null)
            {
                LogError("No player specified for search effect");
                return false;
            }

            return true;
        }

        public override EffectResult Execute(EffectContext context)
        {
            if (!CanExecute(context))
                return EffectResult.Failed("Cannot execute search effect");

            // TODO: 実際の検索システムと連携
            // var searchResults = SearchDeck(context.CurrentPlayer.Deck, _criteria, _searchCount);
            var searchResults = new List<Card>(); // 仮実装

            // カードを公開
            if (_revealCards && searchResults.Count > 0)
            {
                LogEffect($"Revealed {searchResults.Count} cards from search");
            }

            // デッキをシャッフル
            if (_shuffleAfter)
            {
                // context.CurrentPlayer.Deck.Shuffle();
                LogEffect("Shuffled deck after search");
            }

            LogEffect($"Searched for {searchResults.Count} cards");

            var result = EffectResult.Success($"Found {searchResults.Count} cards", searchResults.Count);
            result.Data["SearchResults"] = searchResults;
            return result;
        }

        // ファクトリーメソッド
        public static SearchEffect SearchPokemon(int count = 1) => 
            new SearchEffect(count, SearchCriteria.Pokemon());
        
        public static SearchEffect SearchEnergy(int count = 1) => 
            new SearchEffect(count, SearchCriteria.Energy());
        
        public static SearchEffect SearchTrainer(int count = 1) => 
            new SearchEffect(count, SearchCriteria.Trainer());
        
        public static SearchEffect SearchBasicPokemon(int count = 1) => 
            new SearchEffect(count, SearchCriteria.BasicPokemon());
    }

    #endregion

    #region Discard Effect

    /// <summary>
    /// 破棄エフェクト
    /// カードを捨てる
    /// </summary>
    public class DiscardEffect : BaseCardEffect, ITargetableEffect
    {
        private int _discardCount;
        private DiscardSource _source;
        private bool _isRandom;
        private SearchCriteria _criteria;

        public override string EffectName => "Discard";
        public override string Description => $"Discard {_discardCount} cards from {_source}";
        public override EffectType EffectType => EffectType.Discard;
        public override int Priority => 50;

        // ITargetableEffect implementation
        public TargetType TargetType => TargetType.Card;
        public int RequiredTargets => _discardCount;
        public int MaxTargets => _discardCount;

        public DiscardEffect(int discardCount, DiscardSource source = DiscardSource.Hand, bool isRandom = false)
        {
            _discardCount = discardCount;
            _source = source;
            _isRandom = isRandom;
        }

        public DiscardEffect(int discardCount, SearchCriteria criteria, DiscardSource source = DiscardSource.Hand)
        {
            _discardCount = discardCount;
            _source = source;
            _criteria = criteria;
            _isRandom = false;
        }

        public override bool CanExecute(EffectContext context)
        {
            if (!ValidateContext(context))
                return false;

            // TODO: 破棄元にカードが存在するかチェック
            return true;
        }

        public override EffectResult Execute(EffectContext context)
        {
            if (!CanExecute(context))
                return EffectResult.Failed("Cannot execute discard effect");

            var discardedCards = new List<Card>();

            // TODO: 実際の破棄システムと連携
            // switch (_source)
            // {
            //     case DiscardSource.Hand:
            //         discardedCards = DiscardFromHand(context, _discardCount);
            //         break;
            //     case DiscardSource.Deck:
            //         discardedCards = DiscardFromDeck(context, _discardCount);
            //         break;
            //     case DiscardSource.Field:
            //         discardedCards = DiscardFromField(context, _discardCount);
            //         break;
            // }

            LogEffect($"Discarded {discardedCards.Count} cards from {_source}");

            var result = EffectResult.Success($"Discarded {discardedCards.Count} cards", discardedCards.Count);
            result.Data["DiscardedCards"] = discardedCards;
            return result;
        }

        public List<object> FilterValidTargets(EffectContext context, List<object> potentialTargets)
        {
            return potentialTargets.Where(target => IsValidTarget(context, target)).ToList();
        }

        public bool IsValidTarget(EffectContext context, object target)
        {
            if (!(target is Card card))
                return false;

            // 破棄元に応じた条件チェック
            switch (_source)
            {
                case DiscardSource.Hand:
                    return card.InHand;
                case DiscardSource.Field:
                    return card.IsInPlay;
                default:
                    return true;
            }
        }
    }

    #endregion

    #region Status Effect

    /// <summary>
    /// 状態異常エフェクト
    /// ポケモンに状態異常を与える
    /// </summary>
    public class StatusEffect : BaseCardEffect, ITargetableEffect
    {
        private CardState _statusType;
        private int _duration;
        private int _power;

        public override string EffectName => $"Inflict {_statusType}";
        public override string Description => $"Inflict {_statusType} status";
        public override EffectType EffectType => EffectType.StatusChange;
        public override int Priority => 90;

        // ITargetableEffect implementation
        public TargetType TargetType => TargetType.Pokemon;
        public int RequiredTargets => 1;
        public int MaxTargets => 1;

        public StatusEffect(CardState statusType, int duration = -1, int power = 0)
        {
            _statusType = statusType;
            _duration = duration;
            _power = power;
        }

        public override bool CanExecute(EffectContext context)
        {
            if (!ValidateContext(context))
                return false;

            if (context.PrimaryTarget == null || !IsValidTarget(context, context.PrimaryTarget))
            {
                LogError("Invalid target for status effect");
                return false;
            }

            return true;
        }

        public override EffectResult Execute(EffectContext context)
        {
            if (!CanExecute(context))
                return EffectResult.Failed("Cannot execute status effect");

            var targetCard = context.PrimaryTarget as Card;
            var status = new CardStatus(_statusType, _duration, _power);
            
            targetCard.AddStatus(status);

            LogEffect($"Applied {_statusType} to {targetCard.CardData.CardName}");

            return EffectResult.Success($"Applied {_statusType} status");
        }

        public List<object> FilterValidTargets(EffectContext context, List<object> potentialTargets)
        {
            return potentialTargets.Where(target => IsValidTarget(context, target)).ToList();
        }

        public bool IsValidTarget(EffectContext context, object target)
        {
            if (!(target is Card card) || !card.IsPokemonCard)
                return false;

            // きぜつしたポケモンには状態異常を与えられない
            if (card.CardState == CardState.Knocked)
                return false;

            // 既に同じ状態異常がある場合は重複しない
            return !card.HasStatus(_statusType);
        }

        // ファクトリーメソッド
        public static StatusEffect Poison() => new StatusEffect(CardState.Poisoned);
        public static StatusEffect Burn() => new StatusEffect(CardState.Burned);
        public static StatusEffect Paralyze() => new StatusEffect(CardState.Paralyzed);
        public static StatusEffect Confuse() => new StatusEffect(CardState.Confused);
        public static StatusEffect Sleep() => new StatusEffect(CardState.Asleep);
    }

    #endregion

    #region Energy Attachment Effect

    /// <summary>
    /// エネルギー添付エフェクト
    /// ポケモンにエネルギーを付ける
    /// </summary>
    public class EnergyAttachEffect : BaseCardEffect, ITargetableEffect
    {
        private int _attachCount;
        private PokemonType _energyType;
        private bool _fromHand;
        private bool _fromDiscard;

        public override string EffectName => "Attach Energy";
        public override string Description => $"Attach {_attachCount} {_energyType} energy";
        public override EffectType EffectType => EffectType.EnergyAttach;
        public override int Priority => 75;

        // ITargetableEffect implementation
        public TargetType TargetType => TargetType.Pokemon;
        public int RequiredTargets => 1;
        public int MaxTargets => 1;

        public EnergyAttachEffect(int attachCount = 1, PokemonType energyType = PokemonType.Colorless, bool fromHand = true, bool fromDiscard = false)
        {
            _attachCount = attachCount;
            _energyType = energyType;
            _fromHand = fromHand;
            _fromDiscard = fromDiscard;
        }

        public override bool CanExecute(EffectContext context)
        {
            if (!ValidateContext(context))
                return false;

            if (context.PrimaryTarget == null || !IsValidTarget(context, context.PrimaryTarget))
            {
                LogError("Invalid target for energy attach effect");
                return false;
            }

            // TODO: エネルギーが利用可能かチェック
            return true;
        }

        public override EffectResult Execute(EffectContext context)
        {
            if (!CanExecute(context))
                return EffectResult.Failed("Cannot execute energy attach effect");

            var targetCard = context.PrimaryTarget as Card;

            // TODO: 実際のエネルギー添付システムと連携
            // var energyCards = FindEnergyCards(context, _energyType, _attachCount);
            // foreach (var energy in energyCards)
            // {
            //     AttachEnergyToTarget(targetCard, energy);
            // }

            LogEffect($"Attached {_attachCount} {_energyType} energy to {targetCard.CardData.CardName}");

            return EffectResult.Success($"Attached {_attachCount} energy", _attachCount);
        }

        public List<object> FilterValidTargets(EffectContext context, List<object> potentialTargets)
        {
            return potentialTargets.Where(target => IsValidTarget(context, target)).ToList();
        }

        public bool IsValidTarget(EffectContext context, object target)
        {
            if (!(target is Card card) || !card.IsPokemonCard)
                return false;

            // きぜつしたポケモンにはエネルギーをつけられない
            if (card.CardState == CardState.Knocked)
                return false;

            // 場にいるポケモンのみ
            return card.IsInPlay;
        }
    }

    #endregion

    #region Supporting Types

    /// <summary>
    /// 検索条件
    /// </summary>
    public class SearchCriteria
    {
        public CardType? CardType { get; set; }
        public PokemonType? PokemonType { get; set; }
        public EvolutionStage? EvolutionStage { get; set; }
        public TrainerType? TrainerType { get; set; }
        public string CardName { get; set; }
        public CardRarity? Rarity { get; set; }
        public string Description { get; set; }

        public static SearchCriteria Pokemon() => new SearchCriteria 
        { 
            CardType = Core.Data.CardType.Pokemon, 
            Description = "Pokemon card" 
        };

        public static SearchCriteria BasicPokemon() => new SearchCriteria 
        { 
            CardType = Core.Data.CardType.Pokemon, 
            EvolutionStage = Core.Data.EvolutionStage.Basic,
            Description = "Basic Pokemon card" 
        };

        public static SearchCriteria Energy() => new SearchCriteria 
        { 
            CardType = Core.Data.CardType.Energy, 
            Description = "Energy card" 
        };

        public static SearchCriteria Trainer() => new SearchCriteria 
        { 
            CardType = Core.Data.CardType.Trainer, 
            Description = "Trainer card" 
        };

        public static SearchCriteria ByName(string name) => new SearchCriteria 
        { 
            CardName = name, 
            Description = $"card named {name}" 
        };
    }

    /// <summary>
    /// 破棄元
    /// </summary>
    public enum DiscardSource
    {
        Hand = 0,       // 手札から
        Deck = 1,       // デッキから
        Field = 2,      // 場から
        Prize = 3,      // サイドから
        Any = 99        // 任意
    }

    #endregion
}