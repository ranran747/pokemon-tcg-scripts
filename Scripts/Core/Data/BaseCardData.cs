using System.Collections.Generic;
using UnityEngine;

namespace PokemonTCG.Core.Data
{
    /// <summary>
    /// カードデータの基底ScriptableObject
    /// 全てのカード種類で共通の基本情報を定義
    /// Claude拡張での新カード種類追加を容易にする
    /// </summary>
    public abstract class BaseCardData : ScriptableObject
    {
        #region Basic Information

        [Header("基本情報")]
        [SerializeField] private string _cardID;
        [SerializeField] private string _cardName;
        [SerializeField] private string _cardNameEn;
        [SerializeField] private Sprite _cardArt;
        [SerializeField] private Sprite _cardThumbnail;

        [Header("セット情報")]
        [SerializeField] private string _setCode;
        [SerializeField] private string _setName;
        [SerializeField] private int _cardNumber;
        [SerializeField] private CardRarity _rarity;

        [Header("説明文")]
        [TextArea(3, 5)]
        [SerializeField] private string _flavorText;
        [TextArea(2, 4)]
        [SerializeField] private string _rulesText;

        [Header("ゲームルール")]
        [SerializeField] private List<CardRegulation> _legalRegulations = new List<CardRegulation>();
        [SerializeField] protected bool _isLimitedCard;  // protectedに変更
        [SerializeField] protected int _deckLimit = 4;   // protectedに変更

        #endregion

        #region Properties

        /// <summary>カードの一意識別子</summary>
        public string CardID => _cardID;

        /// <summary>カード名（日本語）</summary>
        public string CardName => _cardName;

        /// <summary>カード名（英語）</summary>
        public string CardNameEn => _cardNameEn;

        /// <summary>カードアート</summary>
        public Sprite CardArt => _cardArt;

        /// <summary>カードサムネイル</summary>
        public Sprite CardThumbnail => _cardThumbnail;

        /// <summary>セットコード</summary>
        public string SetCode => _setCode;

        /// <summary>セット名</summary>
        public string SetName => _setName;

        /// <summary>カード番号</summary>
        public int CardNumber => _cardNumber;

        /// <summary>レアリティ</summary>
        public CardRarity Rarity => _rarity;

        /// <summary>フレーバーテキスト</summary>
        public string FlavorText => _flavorText;

        /// <summary>ルールテキスト</summary>
        public string RulesText => _rulesText;

        /// <summary>使用可能レギュレーション</summary>
        public List<CardRegulation> LegalRegulations => _legalRegulations;

        /// <summary>制限カードかどうか</summary>
        public bool IsLimitedCard => _isLimitedCard;

        /// <summary>デッキ投入制限枚数</summary>
        public int DeckLimit => _deckLimit;

        /// <summary>カード種類（継承クラスで実装）</summary>
        public abstract CardType CardType { get; }

        /// <summary>表示名（UI用）</summary>
        public virtual string DisplayName => string.IsNullOrEmpty(_cardName) ? name : _cardName;

        #endregion

        #region Abstract Methods

        /// <summary>
        /// カードの有効性チェック
        /// 継承クラスで具体的な検証ロジックを実装
        /// </summary>
        /// <returns>有効な場合true</returns>
        public abstract bool IsValid();

        /// <summary>
        /// カードデータの検証エラーメッセージを取得
        /// </summary>
        /// <returns>エラーメッセージのリスト</returns>
        public abstract List<string> GetValidationErrors();

        /// <summary>
        /// カードのハッシュ値を取得（重複チェック用）
        /// </summary>
        /// <returns>ハッシュ値</returns>
        public abstract int GetContentHash();

        #endregion

        #region Virtual Methods

        /// <summary>
        /// カードの基本情報を初期化
        /// 継承クラスでオーバーライド可能
        /// </summary>
        public virtual void InitializeCard()
        {
            if (string.IsNullOrEmpty(_cardID))
            {
                _cardID = System.Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// カードの複製を作成
        /// </summary>
        /// <returns>複製されたカードデータ</returns>
        public virtual BaseCardData Clone()
        {
            return Instantiate(this);
        }

        /// <summary>
        /// デバッグ用文字列表現
        /// </summary>
        /// <returns>デバッグ文字列</returns>
        public override string ToString()
        {
            return $"[{CardType}] {DisplayName} ({CardID})";
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void OnValidate()
        {
            // エディター上でのリアルタイム検証
            #if UNITY_EDITOR
            ValidateInEditor();
            #endif
        }

        protected virtual void Awake()
        {
            InitializeCard();
        }

        #endregion

        #region Editor Validation

        #if UNITY_EDITOR
        /// <summary>
        /// エディター用検証
        /// </summary>
        private void ValidateInEditor()
        {
            // カードIDの自動生成
            if (string.IsNullOrEmpty(_cardID))
            {
                _cardID = System.Guid.NewGuid().ToString();
                UnityEditor.EditorUtility.SetDirty(this);
            }

            // カード名の自動設定
            if (string.IsNullOrEmpty(_cardName))
            {
                _cardName = name;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            // デッキ制限のバリデーション
            if (_deckLimit < 1)
            {
                _deckLimit = 1;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            else if (_deckLimit > 4 && !_isLimitedCard)
            {
                _deckLimit = 4;
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
        #endif

        #endregion

        #region Protected Helper Methods

        /// <summary>
        /// デッキ制限を設定（継承クラス用）
        /// </summary>
        /// <param name="limit">制限枚数</param>
        protected void SetDeckLimit(int limit)
        {
            _deckLimit = Mathf.Clamp(limit, 1, 4);
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        /// <summary>
        /// 制限カードフラグを設定（継承クラス用）
        /// </summary>
        /// <param name="isLimited">制限カードか</param>
        protected void SetLimitedCard(bool isLimited)
        {
            _isLimitedCard = isLimited;
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// カード種類の列挙
    /// </summary>
    public enum CardType
    {
        Pokemon,    // ポケモンカード
        Trainer,    // トレーナーズカード
        Energy      // エネルギーカード
    }

    /// <summary>
    /// カードレアリティ
    /// </summary>
    public enum CardRarity
    {
        Common,         // ●（コモン）
        Uncommon,       // ◆（アンコモン）
        Rare,           // ★（レア）
        RareHolo,       // ★（キラレア）
        SuperRare,      // ☆（スーパーレア）
        UltraRare,      // ☆☆（ウルトラレア）
        SecretRare,     // UR（シークレットレア）
        SpecialArt,     // SAR（スペシャルアート）
        PromoCard       // PROMO（プロモカード）
    }

    /// <summary>
    /// レギュレーション（使用可能な大会形式）
    /// </summary>
    public enum CardRegulation
    {
        Standard,       // スタンダード
        Expanded,       // エクストリーム（従来版）
        Unlimited,      // 無制限
        PocketStandard, // ポケット版スタンダード
        Legacy,         // レガシー
        Custom          // カスタム
    }

    #endregion
}