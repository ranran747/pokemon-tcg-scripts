using System.Collections.Generic;
using UnityEngine;

namespace PokemonTCG.Core.Data
{
    /// <summary>
    /// ゲームルールデータの基底ScriptableObject
    /// 従来版・ポケット版の両方に対応する柔軟なルール定義
    /// Claude拡張での新ルール追加を容易にする
    /// </summary>
    public abstract class BaseRuleData : ScriptableObject
    {
        #region Basic Rule Information

        [Header("基本ルール情報")]
        [SerializeField] private string _ruleID;
        [SerializeField] private string _ruleName;
        [SerializeField] private string _ruleDescription;
        [SerializeField] private GameMode _gameMode;
        [SerializeField] private string _version = "1.0";

        [Header("デッキ構築ルール")]
        [SerializeField] private int _deckSize = 60;
        [SerializeField] private int _minimumDeckSize = 60;
        [SerializeField] private int _maximumDeckSize = 60;
        [SerializeField] private int _maxCopiesPerCard = 4;
        [SerializeField] private bool _allowMultipleAceSpecs = false;

        [Header("ゲーム開始時設定")]
        [SerializeField] private int _initialHandSize = 7;
        [SerializeField] private int _maxMulligan = 3;
        [SerializeField] private bool _firstPlayerDraws = false;

        [Header("場の制限")]
        [SerializeField] private int _maxBenchSize = 5;
        [SerializeField] private int _maxActivePokemons = 1;
        [SerializeField] private bool _hasEnergyZone = false;

        [Header("ターン制限")]
        [SerializeField] private int _maxTurns = 0; // 0 = 無制限
        [SerializeField] private float _turnTimeLimit = 0f; // 0 = 無制限
        [SerializeField] private float _gameTimeLimit = 0f; // 0 = 無制限

        #endregion

        #region Victory Conditions

        [Header("勝利条件")]
        [SerializeField] private List<VictoryCondition> _victoryConditions = new List<VictoryCondition>();
        [SerializeField] private int _prizeCardsToWin = 6;
        [SerializeField] private int _pointsToWin = 0; // ポケット版用
        [SerializeField] private bool _suddenDeathMode = false;

        #endregion

        #region Energy Rules

        [Header("エネルギールール")]
        [SerializeField] private int _energyAttachPerTurn = 1;
        [SerializeField] private bool _energyZoneAutomatic = false;
        [SerializeField] private int _energyZonePerTurn = 1;
        [SerializeField] private bool _canAttachSpecialEnergy = true;

        #endregion

        #region Special Rules

        [Header("特殊ルール")]
        [SerializeField] private List<SpecialRule> _specialRules = new List<SpecialRule>();
        [SerializeField] private float _weaknessMultiplier = 2.0f;
        [SerializeField] private int _resistanceReduction = 30;
        [SerializeField] private bool _allowRetreating = true;
        [SerializeField] private bool _allowEvolution = true;

        #endregion

        #region Properties

        /// <summary>ルールID</summary>
        public string RuleID => _ruleID;

        /// <summary>ルール名</summary>
        public string RuleName => _ruleName;

        /// <summary>ルール説明</summary>
        public string RuleDescription => _ruleDescription;

        /// <summary>ゲームモード</summary>
        public GameMode GameMode => _gameMode;

        /// <summary>ルールバージョン</summary>
        public string Version => _version;

        /// <summary>デッキサイズ</summary>
        public int DeckSize => _deckSize;

        /// <summary>最小デッキサイズ</summary>
        public int MinimumDeckSize => _minimumDeckSize;

        /// <summary>最大デッキサイズ</summary>
        public int MaximumDeckSize => _maximumDeckSize;

        /// <summary>同名カード最大枚数</summary>
        public int MaxCopiesPerCard => _maxCopiesPerCard;

        /// <summary>複数のACE SPEC許可</summary>
        public bool AllowMultipleAceSpecs => _allowMultipleAceSpecs;

        /// <summary>初期手札枚数</summary>
        public int InitialHandSize => _initialHandSize;

        /// <summary>最大マリガン回数</summary>
        public int MaxMulligan => _maxMulligan;

        /// <summary>先攻プレイヤーがドローするか</summary>
        public bool FirstPlayerDraws => _firstPlayerDraws;

        /// <summary>最大ベンチサイズ</summary>
        public int MaxBenchSize => _maxBenchSize;

        /// <summary>最大バトルポケモン数</summary>
        public int MaxActivePokemons => _maxActivePokemons;

        /// <summary>エネルギーゾーンの有無</summary>
        public bool HasEnergyZone => _hasEnergyZone;

        /// <summary>最大ターン数</summary>
        public int MaxTurns => _maxTurns;

        /// <summary>ターン制限時間</summary>
        public float TurnTimeLimit => _turnTimeLimit;

        /// <summary>ゲーム制限時間</summary>
        public float GameTimeLimit => _gameTimeLimit;

        /// <summary>勝利条件リスト</summary>
        public List<VictoryCondition> VictoryConditions => _victoryConditions;

        /// <summary>勝利に必要なサイドカード枚数</summary>
        public int PrizeCardsToWin => _prizeCardsToWin;

        /// <summary>勝利に必要なポイント数</summary>
        public int PointsToWin => _pointsToWin;

        /// <summary>サドンデスモード</summary>
        public bool SuddenDeathMode => _suddenDeathMode;

        /// <summary>1ターンの手張りエネルギー数</summary>
        public int EnergyAttachPerTurn => _energyAttachPerTurn;

        /// <summary>エネルギーゾーン自動供給</summary>
        public bool EnergyZoneAutomatic => _energyZoneAutomatic;

        /// <summary>1ターンのエネルギーゾーン増加数</summary>
        public int EnergyZonePerTurn => _energyZonePerTurn;

        /// <summary>特殊エネルギー使用可能</summary>
        public bool CanAttachSpecialEnergy => _canAttachSpecialEnergy;

        /// <summary>特殊ルールリスト</summary>
        public List<SpecialRule> SpecialRules => _specialRules;

        /// <summary>弱点倍率</summary>
        public float WeaknessMultiplier => _weaknessMultiplier;

        /// <summary>抵抗力軽減値</summary>
        public int ResistanceReduction => _resistanceReduction;

        /// <summary>にげる可能</summary>
        public bool AllowRetreating => _allowRetreating;

        /// <summary>進化可能</summary>
        public bool AllowEvolution => _allowEvolution;

        #endregion

        #region Abstract Methods

        /// <summary>
        /// ルールの有効性チェック
        /// </summary>
        /// <returns>有効な場合true</returns>
        public abstract bool IsValid();

        /// <summary>
        /// ルールの検証エラーメッセージを取得
        /// </summary>
        /// <returns>エラーメッセージのリスト</returns>
        public abstract List<string> GetValidationErrors();

        /// <summary>
        /// アクションの有効性チェック
        /// </summary>
        /// <param name="actionType">アクション種類</param>
        /// <param name="context">ゲームコンテキスト</param>
        /// <returns>有効な場合true</returns>
        public abstract bool IsActionValid(GameActionType actionType, object context);

        #endregion

        #region Virtual Methods

        /// <summary>
        /// ルールの初期化
        /// </summary>
        public virtual void InitializeRule()
        {
            if (string.IsNullOrEmpty(_ruleID))
            {
                _ruleID = System.Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// ルールの複製を作成
        /// </summary>
        /// <returns>複製されたルールデータ</returns>
        public virtual BaseRuleData Clone()
        {
            return Instantiate(this);
        }

        /// <summary>
        /// 特定の勝利条件が有効かチェック
        /// </summary>
        /// <param name="condition">勝利条件</param>
        /// <returns>有効な場合true</returns>
        public virtual bool HasVictoryCondition(VictoryCondition condition)
        {
            return _victoryConditions.Contains(condition);
        }

        /// <summary>
        /// 特殊ルールが有効かチェック
        /// </summary>
        /// <param name="rule">特殊ルール</param>
        /// <returns>有効な場合true</returns>
        public virtual bool HasSpecialRule(SpecialRule rule)
        {
            return _specialRules.Contains(rule);
        }

        /// <summary>
        /// デバッグ用文字列表現
        /// </summary>
        /// <returns>デバッグ文字列</returns>
        public override string ToString()
        {
            return $"[{_gameMode}] {_ruleName} v{_version}";
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void OnValidate()
        {
            #if UNITY_EDITOR
            ValidateInEditor();
            #endif
        }

        protected virtual void Awake()
        {
            InitializeRule();
        }

        #endregion

        #region Editor Validation

        #if UNITY_EDITOR
        private void ValidateInEditor()
        {
            // ルールIDの自動生成
            if (string.IsNullOrEmpty(_ruleID))
            {
                _ruleID = System.Guid.NewGuid().ToString();
                UnityEditor.EditorUtility.SetDirty(this);
            }

            // デッキサイズの妥当性チェック
            if (_deckSize < _minimumDeckSize)
            {
                _deckSize = _minimumDeckSize;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            if (_deckSize > _maximumDeckSize)
            {
                _deckSize = _maximumDeckSize;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            // 数値の範囲チェック
            _maxCopiesPerCard = Mathf.Clamp(_maxCopiesPerCard, 1, 4);
            _initialHandSize = Mathf.Clamp(_initialHandSize, 1, 10);
            _maxBenchSize = Mathf.Clamp(_maxBenchSize, 1, 8);
        }
        #endif

        #endregion
    }

    #region Enums

    /// <summary>
    /// ゲームモード
    /// </summary>
    public enum GameMode
    {
        ClassicTCG,     // 従来版ポケモンカードゲーム
        PocketTCG,      // ポケモンTCGポケット
        CustomRules,    // カスタムルール
        Tournament,     // 大会ルール
        Casual          // カジュアル
    }

    /// <summary>
    /// 勝利条件
    /// </summary>
    public enum VictoryCondition
    {
        AllPrizes,      // サイドカードをすべて取る
        NoBench,        // 相手のベンチをなくす
        DeckOut,        // 相手のデッキを切らす
        PointTarget,    // 目標ポイント到達（ポケット版）
        TimeLimit,      // 時間切れ判定
        Knockout,       // ノックアウト
        Surrender       // 降参
    }

    /// <summary>
    /// 特殊ルール
    /// </summary>
    public enum SpecialRule
    {
        NoFirstTurnAttack,      // 先攻1ターン目攻撃禁止
        NoFirstTurnEvolution,   // 先攻1ターン目進化禁止
        MultiPrizeCards,        // 複数枚サイドカード
        EnergyZoneSystem,       // エネルギーゾーンシステム
        TimeLimitBattle,        // 時間制限バトル
        SuddenDeath,            // サドンデス
        CustomWinCondition,     // カスタム勝利条件
        RestrictedCards,        // 使用制限カード
        BoostedDamage,          // ダメージブースト
        SpecialPrizes          // 特殊サイドカード
    }

    /// <summary>
    /// ゲームアクション種類
    /// </summary>
    public enum GameActionType
    {
        PlayCard,               // カードをプレイ
        AttachEnergy,           // エネルギーをつける
        UseAttack,              // ワザを使う
        Retreat,                // にげる
        UseAbility,             // 特性を使う
        Evolution,              // 進化
        DrawCard,               // カードを引く
        UseTrainer,             // トレーナーズを使う
        EndTurn,                // ターン終了
        Surrender               // 降参
    }

    #endregion
}