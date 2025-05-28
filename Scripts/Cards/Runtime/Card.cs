using System;
using System.Collections.Generic;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.Cards.Runtime
{
    /// <summary>
    /// 実行時カード表現クラス
    /// ScriptableObjectのカードデータをゲーム内で操作可能なオブジェクトに変換
    /// UI表示、アニメーション、ゲームロジックとの橋渡しを行う
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Card : MonoBehaviour
    {
        #region Fields

        [Header("カードデータ")]
        [SerializeField] private BaseCardData _cardData;
        [SerializeField] private int _instanceId;
        [SerializeField] private string _ownerPlayerId;

        [Header("カード状態")]
        [SerializeField] private CardZone _currentZone = CardZone.Unknown;
        [SerializeField] private CardState _cardState = CardState.Normal;
        [SerializeField] private List<CardStatus> _statusEffects = new List<CardStatus>();

        [Header("UI要素")]
        [SerializeField] private SpriteRenderer _cardRenderer;
        [SerializeField] private Canvas _cardCanvas;
        [SerializeField] private Transform _cardTransform;

        [Header("アニメーション")]
        [SerializeField] private Animator _animator;
        [SerializeField] private bool _enableAnimations = true;
        [SerializeField] private float _animationSpeed = 1.0f;

        [Header("インタラクション")]
        [SerializeField] private bool _isInteractable = true;
        [SerializeField] private bool _isSelected = false;
        [SerializeField] private bool _isHighlighted = false;

        // イベント
        public event Action<Card> OnCardSelected;
        public event Action<Card> OnCardDeselected;
        public event Action<Card> OnCardClicked;
        public event Action<Card> OnCardHovered;
        public event Action<Card, CardZone, CardZone> OnZoneChanged;
        public event Action<Card, CardState, CardState> OnStateChanged;

        // キャッシュ
        private Collider2D _collider;
        private bool _isInitialized = false;

        #endregion

        #region Properties

        /// <summary>カードデータ</summary>
        public BaseCardData CardData => _cardData;

        /// <summary>インスタンスID（ゲーム内一意識別子）</summary>
        public int InstanceId => _instanceId;

        /// <summary>所有者プレイヤーID</summary>
        public string OwnerPlayerId => _ownerPlayerId;

        /// <summary>現在のゾーン</summary>
        public CardZone CurrentZone => _currentZone;

        /// <summary>カード状態</summary>
        public CardState CardState => _cardState;

        /// <summary>状態異常リスト</summary>
        public List<CardStatus> StatusEffects => _statusEffects;

        /// <summary>選択されているか</summary>
        public bool IsSelected => _isSelected;

        /// <summary>ハイライトされているか</summary>
        public bool IsHighlighted => _isHighlighted;

        /// <summary>インタラクション可能か</summary>
        public bool IsInteractable => _isInteractable;

        /// <summary>初期化済みか</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>ポケモンカードか</summary>
        public bool IsPokemonCard => _cardData is PokemonCardData;

        /// <summary>トレーナーカードか</summary>
        public bool IsTrainerCard => _cardData is TrainerCardData;

        /// <summary>エネルギーカードか</summary>
        public bool IsEnergyCard => _cardData is EnergyCardData;

        /// <summary>場にあるか</summary>
        public bool IsInPlay => _currentZone == CardZone.Active || _currentZone == CardZone.Bench;

        /// <summary>手札にあるか</summary>
        public bool InHand => _currentZone == CardZone.Hand;

        #endregion

        #region Initialization

        /// <summary>
        /// カードを初期化
        /// </summary>
        /// <param name="cardData">カードデータ</param>
        /// <param name="ownerPlayerId">所有者プレイヤーID</param>
        /// <param name="instanceId">インスタンスID</param>
        public void Initialize(BaseCardData cardData, string ownerPlayerId, int instanceId)
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"[Card] Card {name} is already initialized");
                return;
            }

            _cardData = cardData;
            _ownerPlayerId = ownerPlayerId;
            _instanceId = instanceId;

            InitializeComponents();
            SetupVisuals();
            SetupInteraction();

            _isInitialized = true;

            Debug.Log($"[Card] Initialized card: {_cardData.CardName} (ID: {_instanceId})");
        }

        /// <summary>
        /// コンポーネントを初期化
        /// </summary>
        private void InitializeComponents()
        {
            // コンポーネント取得・作成
            _collider = GetComponent<Collider2D>();
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<BoxCollider2D>();
            }

            if (_cardRenderer == null)
            {
                _cardRenderer = GetComponent<SpriteRenderer>();
                if (_cardRenderer == null)
                {
                    _cardRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            if (_cardCanvas == null)
            {
                var canvasGO = new GameObject("CardCanvas");
                canvasGO.transform.SetParent(transform);
                canvasGO.transform.localPosition = Vector3.zero;
                
                _cardCanvas = canvasGO.AddComponent<Canvas>();
                _cardCanvas.renderMode = RenderMode.WorldSpace;
                _cardCanvas.sortingLayerName = "Cards";
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            _cardTransform = transform;
        }

        /// <summary>
        /// ビジュアルを設定
        /// </summary>
        private void SetupVisuals()
        {
            if (_cardData == null) return;

            // カードアートを設定
            if (_cardData.CardArt != null)
            {
                _cardRenderer.sprite = _cardData.CardArt;
            }

            // カード名を設定
            name = $"Card_{_cardData.CardName}_{_instanceId}";

            // ソート順を設定
            _cardRenderer.sortingLayerName = "Cards";
            _cardRenderer.sortingOrder = 0;
        }

        /// <summary>
        /// インタラクションを設定
        /// </summary>
        private void SetupInteraction()
        {
            // コライダーサイズをスプライトに合わせる
            if (_cardRenderer.sprite != null && _collider is BoxCollider2D boxCollider)
            {
                boxCollider.size = _cardRenderer.sprite.bounds.size;
            }
        }

        #endregion

        #region Zone Management

        /// <summary>
        /// ゾーンを変更
        /// </summary>
        /// <param name="newZone">新しいゾーン</param>
        public void SetZone(CardZone newZone)
        {
            var oldZone = _currentZone;
            _currentZone = newZone;

            // ゾーン変更時の処理
            OnZoneChangedInternal(oldZone, newZone);

            // イベント発火
            OnZoneChanged?.Invoke(this, oldZone, newZone);

            Debug.Log($"[Card] {_cardData.CardName} moved from {oldZone} to {newZone}");
        }

        /// <summary>
        /// ゾーン変更時の内部処理
        /// </summary>
        /// <param name="oldZone">旧ゾーン</param>
        /// <param name="newZone">新ゾーン</param>
        private void OnZoneChangedInternal(CardZone oldZone, CardZone newZone)
        {
            // インタラクション設定
            switch (newZone)
            {
                case CardZone.Hand:
                    SetInteractable(true);
                    break;
                case CardZone.Active:
                case CardZone.Bench:
                    SetInteractable(true);
                    break;
                case CardZone.Deck:
                case CardZone.Discard:
                case CardZone.Prize:
                    SetInteractable(false);
                    break;
            }

            // ビジュアル更新
            UpdateVisualForZone(newZone);

            // アニメーション再生
            if (_enableAnimations && _animator != null)
            {
                PlayZoneTransitionAnimation(oldZone, newZone);
            }
        }

        /// <summary>
        /// ゾーンに応じたビジュアル更新
        /// </summary>
        /// <param name="zone">ゾーン</param>
        private void UpdateVisualForZone(CardZone zone)
        {
            switch (zone)
            {
                case CardZone.Hand:
                    _cardRenderer.sortingOrder = 10;
                    transform.localScale = Vector3.one;
                    break;
                case CardZone.Active:
                    _cardRenderer.sortingOrder = 20;
                    transform.localScale = Vector3.one * 1.2f;
                    break;
                case CardZone.Bench:
                    _cardRenderer.sortingOrder = 15;
                    transform.localScale = Vector3.one;
                    break;
                case CardZone.Deck:
                    _cardRenderer.sortingOrder = 5;
                    transform.localScale = Vector3.one * 0.8f;
                    break;
                case CardZone.Discard:
                    _cardRenderer.sortingOrder = 8;
                    transform.localScale = Vector3.one * 0.9f;
                    break;
            }
        }

        #endregion

        #region State Management

        /// <summary>
        /// カード状態を設定
        /// </summary>
        /// <param name="newState">新しい状態</param>
        public void SetState(CardState newState)
        {
            var oldState = _cardState;
            _cardState = newState;

            OnStateChangedInternal(oldState, newState);
            OnStateChanged?.Invoke(this, oldState, newState);

            Debug.Log($"[Card] {_cardData.CardName} state changed from {oldState} to {newState}");
        }

        /// <summary>
        /// 状態変更時の内部処理
        /// </summary>
        /// <param name="oldState">旧状態</param>
        /// <param name="newState">新状態</param>
        private void OnStateChangedInternal(CardState oldState, CardState newState)
        {
            // 状態に応じた処理
            switch (newState)
            {
                case CardState.Tapped:
                    if (!IsTapped())
                    {
                        SetRotation(90f);
                    }
                    break;
                case CardState.Normal:
                    if (IsTapped())
                    {
                        SetRotation(0f);
                    }
                    break;
                case CardState.Poisoned:
                case CardState.Burned:
                case CardState.Paralyzed:
                case CardState.Confused:
                case CardState.Asleep:
                    ApplyStatusVisual(newState);
                    break;
            }
        }

        /// <summary>
        /// 状態異常を追加
        /// </summary>
        /// <param name="status">状態異常</param>
        public void AddStatus(CardStatus status)
        {
            if (!_statusEffects.Contains(status))
            {
                _statusEffects.Add(status);
                ApplyStatusVisual(status.StatusType);
                Debug.Log($"[Card] Added status {status.StatusType} to {_cardData.CardName}");
            }
        }

        /// <summary>
        /// 状態異常を削除
        /// </summary>
        /// <param name="statusType">状態異常種類</param>
        public void RemoveStatus(CardState statusType)
        {
            _statusEffects.RemoveAll(status => status.StatusType == statusType);
            Debug.Log($"[Card] Removed status {statusType} from {_cardData.CardName}");
        }

        /// <summary>
        /// 指定状態異常を持っているかチェック
        /// </summary>
        /// <param name="statusType">状態異常種類</param>
        /// <returns>持っている場合true</returns>
        public bool HasStatus(CardState statusType)
        {
            return _statusEffects.Exists(status => status.StatusType == statusType);
        }

        #endregion

        #region Visual and Animation

        /// <summary>
        /// 選択状態を設定
        /// </summary>
        /// <param name="selected">選択状態</param>
        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;

            _isSelected = selected;

            // ビジュアル更新
            UpdateSelectionVisual();

            // イベント発火
            if (_isSelected)
                OnCardSelected?.Invoke(this);
            else
                OnCardDeselected?.Invoke(this);
        }

        /// <summary>
        /// ハイライト状態を設定
        /// </summary>
        /// <param name="highlighted">ハイライト状態</param>
        public void SetHighlighted(bool highlighted)
        {
            _isHighlighted = highlighted;
            UpdateHighlightVisual();
        }

        /// <summary>
        /// インタラクション可能状態を設定
        /// </summary>
        /// <param name="interactable">インタラクション可能状態</param>
        public void SetInteractable(bool interactable)
        {
            _isInteractable = interactable;
            _collider.enabled = interactable;
            
            // ビジュアル更新
            _cardRenderer.color = interactable ? Color.white : Color.gray;
        }

        /// <summary>
        /// 回転を設定
        /// </summary>
        /// <param name="angle">角度</param>
        private void SetRotation(float angle)
        {
            if (_enableAnimations)
            {
                // TODO: アニメーションで回転
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
            else
            {
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        /// <summary>
        /// 選択ビジュアルを更新
        /// </summary>
        private void UpdateSelectionVisual()
        {
            if (_isSelected)
            {
                // 選択時のエフェクト（光る、拡大など）
                transform.localScale = Vector3.one * 1.1f;
                _cardRenderer.color = new Color(1f, 1f, 1f, 1f);
            }
            else
            {
                transform.localScale = Vector3.one;
                _cardRenderer.color = Color.white;
            }
        }

        /// <summary>
        /// ハイライトビジュアルを更新
        /// </summary>
        private void UpdateHighlightVisual()
        {
            if (_isHighlighted)
            {
                _cardRenderer.color = new Color(1f, 1f, 0.8f, 1f); // 薄い黄色
            }
            else if (!_isSelected)
            {
                _cardRenderer.color = Color.white;
            }
        }

        /// <summary>
        /// 状態異常ビジュアルを適用
        /// </summary>
        /// <param name="statusType">状態異常種類</param>
        private void ApplyStatusVisual(CardState statusType)
        {
            switch (statusType)
            {
                case CardState.Poisoned:
                    _cardRenderer.color = new Color(0.8f, 0.4f, 0.8f, 1f); // 紫
                    break;
                case CardState.Burned:
                    _cardRenderer.color = new Color(1f, 0.5f, 0.3f, 1f); // オレンジ
                    break;
                case CardState.Paralyzed:
                    _cardRenderer.color = new Color(0.8f, 0.8f, 0.3f, 1f); // 黄色
                    break;
                case CardState.Confused:
                    _cardRenderer.color = new Color(0.9f, 0.7f, 0.9f, 1f); // ピンク
                    break;
                case CardState.Asleep:
                    _cardRenderer.color = new Color(0.6f, 0.6f, 0.9f, 1f); // 青
                    break;
            }
        }

        /// <summary>
        /// ゾーン遷移アニメーションを再生
        /// </summary>
        /// <param name="fromZone">移動元ゾーン</param>
        /// <param name="toZone">移動先ゾーン</param>
        private void PlayZoneTransitionAnimation(CardZone fromZone, CardZone toZone)
        {
            if (_animator == null) return;

            string animationName = $"{fromZone}To{toZone}";
            if (HasAnimationState(animationName))
            {
                _animator.Play(animationName);
            }
        }

        /// <summary>
        /// アニメーション状態が存在するかチェック
        /// </summary>
        /// <param name="stateName">状態名</param>
        /// <returns>存在する場合true</returns>
        private bool HasAnimationState(string stateName)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return false;

            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == stateName)
                    return true;
            }
            return false;
        }

        #endregion

        #region Input Handling

        private void OnMouseDown()
        {
            if (!_isInteractable) return;

            OnCardClicked?.Invoke(this);
            Debug.Log($"[Card] Clicked: {_cardData.CardName}");
        }

        private void OnMouseEnter()
        {
            if (!_isInteractable) return;

            SetHighlighted(true);
            OnCardHovered?.Invoke(this);
        }

        private void OnMouseExit()
        {
            if (!_isInteractable) return;

            SetHighlighted(false);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// タップ状態かチェック
        /// </summary>
        /// <returns>タップ状態の場合true</returns>
        public bool IsTapped()
        {
            return Mathf.Abs(transform.eulerAngles.z - 90f) < 1f;
        }

        /// <summary>
        /// カードデータをキャストしてPokemonCardDataとして取得
        /// </summary>
        /// <returns>PokemonCardData（該当しない場合null）</returns>
        public PokemonCardData GetPokemonData()
        {
            return _cardData as PokemonCardData;
        }

        /// <summary>
        /// カードデータをキャストしてTrainerCardDataとして取得
        /// </summary>
        /// <returns>TrainerCardData（該当しない場合null）</returns>
        public TrainerCardData GetTrainerData()
        {
            return _cardData as TrainerCardData;
        }

        /// <summary>
        /// カードデータをキャストしてEnergyCardDataとして取得
        /// </summary>
        /// <returns>EnergyCardData（該当しない場合null）</returns>
        public EnergyCardData GetEnergyData()
        {
            return _cardData as EnergyCardData;
        }

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        /// <returns>デバッグ情報文字列</returns>
        public string GetDebugInfo()
        {
            return $"Card: {_cardData?.CardName ?? "None"}\n" +
                   $"Instance ID: {_instanceId}\n" +
                   $"Owner: {_ownerPlayerId}\n" +
                   $"Zone: {_currentZone}\n" +
                   $"State: {_cardState}\n" +
                   $"Status Effects: {_statusEffects.Count}\n" +
                   $"Interactable: {_isInteractable}\n" +
                   $"Selected: {_isSelected}";
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // 基本的な初期化はここで行う
            if (_cardTransform == null)
                _cardTransform = transform;
        }

        private void Start()
        {
            // 外部からの初期化を待つため、ここでは何もしない
        }

        private void OnDestroy()
        {
            // イベントのクリーンアップ
            OnCardSelected = null;
            OnCardDeselected = null;
            OnCardClicked = null;
            OnCardHovered = null;
            OnZoneChanged = null;
            OnStateChanged = null;
        }

        #endregion
    }

    #region Enums and Data Classes

    /// <summary>
    /// カードゾーン（カードの居場所）
    /// </summary>
    public enum CardZone
    {
        Unknown = 0,    // 不明
        Deck = 1,       // 山札
        Hand = 2,       // 手札  
        Active = 3,     // バトル場
        Bench = 4,      // ベンチ
        Discard = 5,    // トラッシュ
        Prize = 6,      // サイド
        Stadium = 7,    // スタジアム
        Attached = 8    // エネルギーなどの付いているカード
    }

    /// <summary>
    /// カード状態
    /// </summary>
    public enum CardState
    {
        Normal = 0,     // 通常
        Tapped = 1,     // タップ（横向き）
        Poisoned = 2,   // どく
        Burned = 3,     // やけど
        Paralyzed = 4,  // まひ
        Confused = 5,   // こんらん
        Asleep = 6,     // ねむり
        Frozen = 7,     // こおり（一部のゲームで使用）
        Knocked = 8     // きぜつ
    }

    /// <summary>
    /// カード状態異常データ
    /// </summary>
    [System.Serializable]
    public class CardStatus
    {
        [SerializeField] private CardState _statusType;
        [SerializeField] private int _duration = -1; // -1 = 永続
        [SerializeField] private int _power = 0;
        [SerializeField] private string _source = "";

        public CardState StatusType => _statusType;
        public int Duration => _duration;
        public int Power => _power;  
        public string Source => _source;

        public CardStatus(CardState statusType, int duration = -1, int power = 0, string source = "")
        {
            _statusType = statusType;
            _duration = duration;
            _power = power;
            _source = source;
        }
    }

    #endregion
}