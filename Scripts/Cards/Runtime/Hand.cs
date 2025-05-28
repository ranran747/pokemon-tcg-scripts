using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.Cards.Runtime
{
    /// <summary>
    /// 手札管理システム
    /// プレイヤーの手札の管理、表示、操作機能を提供
    /// UI表示とゲームロジックの橋渡しを担当
    /// </summary>
    public class Hand : MonoBehaviour
    {
        #region Fields

        [Header("手札設定")]
        [SerializeField] private string _playerId;
        [SerializeField] private int _maxHandSize = 10;
        [SerializeField] private bool _isVisible = true;
        [SerializeField] private bool _allowMultipleSelection = false;

        [Header("カード配置")]
        [SerializeField] private Transform _handContainer;
        [SerializeField] private Vector3 _cardSpacing = new Vector3(1.2f, 0, 0);
        [SerializeField] private float _cardRotationSpread = 5f;
        [SerializeField] private AnimationCurve _handCurve = AnimationCurve.Linear(0, 0, 1, 0);

        [Header("アニメーション")]
        [SerializeField] private bool _enableAnimations = true;
        [SerializeField] private float _animationDuration = 0.3f;
        [SerializeField] private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("インタラクション")]
        [SerializeField] private bool _allowCardSelection = true;
        [SerializeField] private bool _allowCardHover = true;
        [SerializeField] private float _hoverHeight = 0.5f;

        // 手札データ
        private List<Card> _cards = new List<Card>();
        private List<Card> _selectedCards = new List<Card>();
        private Card _hoveredCard = null;

        // イベント
        public event Action<Hand, Card> OnCardAdded;
        public event Action<Hand, Card> OnCardRemoved;
        public event Action<Hand, Card> OnCardSelected;
        public event Action<Hand, Card> OnCardDeselected;
        public event Action<Hand, Card> OnCardPlayed;
        public event Action<Hand> OnHandSizeChanged;
        public event Action<Hand> OnHandCleared;

        // アニメーション制御
        private List<Coroutine> _activeAnimations = new List<Coroutine>();

        #endregion

        #region Properties

        /// <summary>プレイヤーID</summary>
        public string PlayerId => _playerId;

        /// <summary>手札のカードリスト</summary>
        public List<Card> Cards => _cards;

        /// <summary>選択されたカードリスト</summary>
        public List<Card> SelectedCards => _selectedCards;

        /// <summary>手札サイズ</summary>
        public int Count => _cards.Count;

        /// <summary>最大手札サイズ</summary>
        public int MaxHandSize => _maxHandSize;

        /// <summary>手札が満杯か</summary>
        public bool IsFull => _cards.Count >= _maxHandSize;

        /// <summary>手札が空か</summary>
        public bool IsEmpty => _cards.Count == 0;

        /// <summary>選択されたカード数</summary>
        public int SelectedCount => _selectedCards.Count;

        /// <summary>選択されたカードがあるか</summary>
        public bool HasSelection => _selectedCards.Count > 0;

        /// <summary>手札が見えるか</summary>
        public bool IsVisible => _isVisible;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeHand();
        }

        private void Start()
        {
            SetupHandContainer();
        }

        private void OnDestroy()
        {
            ClearEventListeners();
            StopAllCoroutines();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 手札を初期化
        /// </summary>
        private void InitializeHand()
        {
            if (string.IsNullOrEmpty(_playerId))
            {
                _playerId = $"Player_{GetInstanceID()}";
            }

            _cards = new List<Card>();
            _selectedCards = new List<Card>();
        }

        /// <summary>
        /// 手札コンテナを設定
        /// </summary>
        private void SetupHandContainer()
        {
            if (_handContainer == null)
            {
                var containerGO = new GameObject("HandContainer");
                containerGO.transform.SetParent(transform);
                containerGO.transform.localPosition = Vector3.zero;
                _handContainer = containerGO.transform;
            }
        }

        /// <summary>
        /// プレイヤーIDを設定
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        public void SetPlayerId(string playerId)
        {
            _playerId = playerId;
            Debug.Log($"[Hand] Set player ID: {playerId}");
        }

        #endregion

        #region Card Management

        /// <summary>
        /// カードを手札に追加
        /// </summary>
        /// <param name="card">追加するカード</param>
        /// <returns>追加成功した場合true</returns>
        public bool AddCard(Card card)
        {
            if (card == null)
                return false;

            if (IsFull)
            {
                Debug.LogWarning($"[Hand] Cannot add card: hand is full ({_maxHandSize})");
                return false;
            }

            if (_cards.Contains(card))
            {
                Debug.LogWarning($"[Hand] Card {card.CardData.CardName} is already in hand");
                return false;
            }

            // カードを手札に追加
            _cards.Add(card);
            card.SetZone(CardZone.Hand);
            card.transform.SetParent(_handContainer);

            // イベントリスナー登録
            RegisterCardEvents(card);

            // 手札の配置を更新
            ArrangeCards();

            // イベント発火
            OnCardAdded?.Invoke(this, card);
            OnHandSizeChanged?.Invoke(this);

            Debug.Log($"[Hand] Added {card.CardData.CardName} to hand (Size: {Count})");
            return true;
        }

        /// <summary>
        /// 複数のカードを手札に追加
        /// </summary>
        /// <param name="cards">追加するカードリスト</param>
        /// <returns>追加されたカード数</returns>
        public int AddCards(List<Card> cards)
        {
            int addedCount = 0;
            
            foreach (var card in cards)
            {
                if (AddCard(card))
                {
                    addedCount++;
                }
                else
                {
                    break; // 手札が満杯になったら終了
                }
            }

            return addedCount;
        }

        /// <summary>
        /// カードを手札から削除
        /// </summary>
        /// <param name="card">削除するカード</param>
        /// <returns>削除成功した場合true</returns>
        public bool RemoveCard(Card card)
        {
            if (card == null || !_cards.Contains(card))
                return false;

            // 選択状態から削除
            if (_selectedCards.Contains(card))
            {
                _selectedCards.Remove(card);
                card.SetSelected(false);
            }

            // ホバー状態をクリア
            if (_hoveredCard == card)
            {
                _hoveredCard = null;
            }

            // イベントリスナー解除
            UnregisterCardEvents(card);

            // 手札から削除
            _cards.Remove(card);

            // 手札の配置を更新
            ArrangeCards();

            // イベント発火
            OnCardRemoved?.Invoke(this, card);
            OnHandSizeChanged?.Invoke(this);

            Debug.Log($"[Hand] Removed {card.CardData.CardName} from hand (Size: {Count})");
            return true;
        }

        /// <summary>
        /// 指定したカードをプレイ（手札から削除してプレイゾーンへ）
        /// </summary>
        /// <param name="card">プレイするカード</param>
        /// <returns>プレイ成功した場合true</returns>
        public bool PlayCard(Card card)
        {
            if (card == null || !_cards.Contains(card))
                return false;

            // 手札から削除
            RemoveCard(card);

            // イベント発火
            OnCardPlayed?.Invoke(this, card);

            Debug.Log($"[Hand] Played {card.CardData.CardName}");
            return true;
        }

        /// <summary>
        /// 手札をクリア
        /// </summary>
        public void Clear()
        {
            // 全カードのイベントリスナーを解除
            foreach (var card in _cards)
            {
                UnregisterCardEvents(card);
            }

            _cards.Clear();
            _selectedCards.Clear();
            _hoveredCard = null;

            OnHandCleared?.Invoke(this);
            OnHandSizeChanged?.Invoke(this);

            Debug.Log("[Hand] Cleared hand");
        }

        #endregion

        #region Card Selection

        /// <summary>
        /// カードを選択
        /// </summary>
        /// <param name="card">選択するカード</param>
        public void SelectCard(Card card)
        {
            if (card == null || !_cards.Contains(card) || !_allowCardSelection)
                return;

            if (_selectedCards.Contains(card))
                return; // 既に選択済み

            // 複数選択が無効で他に選択されたカードがある場合は先にクリア
            if (!_allowMultipleSelection && _selectedCards.Count > 0)
            {
                ClearSelection();
            }

            _selectedCards.Add(card);
            card.SetSelected(true);

            OnCardSelected?.Invoke(this, card);
            Debug.Log($"[Hand] Selected {card.CardData.CardName}");
        }

        /// <summary>
        /// カードの選択を解除
        /// </summary>
        /// <param name="card">選択解除するカード</param>
        public void DeselectCard(Card card)
        {
            if (card == null || !_selectedCards.Contains(card))
                return;

            _selectedCards.Remove(card);
            card.SetSelected(false);

            OnCardDeselected?.Invoke(this, card);
            Debug.Log($"[Hand] Deselected {card.CardData.CardName}");
        }

        /// <summary>
        /// カードの選択状態をトグル
        /// </summary>
        /// <param name="card">対象カード</param>
        public void ToggleCardSelection(Card card)
        {
            if (_selectedCards.Contains(card))
            {
                DeselectCard(card);
            }
            else
            {
                SelectCard(card);
            }
        }

        /// <summary>
        /// 全選択をクリア
        /// </summary>
        public void ClearSelection()
        {
            var selectedCards = new List<Card>(_selectedCards);
            foreach (var card in selectedCards)
            {
                DeselectCard(card);
            }
        }

        /// <summary>
        /// 全カードを選択
        /// </summary>
        public void SelectAll()
        {
            if (!_allowMultipleSelection)
                return;

            foreach (var card in _cards)
            {
                if (!_selectedCards.Contains(card))
                {
                    SelectCard(card);
                }
            }
        }

        #endregion

        #region Card Arrangement

        /// <summary>
        /// 手札のカードを配置
        /// </summary>
        public void ArrangeCards()
        {
            if (_cards.Count == 0)
                return;

            if (_enableAnimations)
            {
                StartCoroutine(ArrangeCardsAnimated());
            }
            else
            {
                ArrangeCardsImmediate();
            }
        }

        /// <summary>
        /// 即座にカードを配置
        /// </summary>
        private void ArrangeCardsImmediate()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                var position = CalculateCardPosition(i);
                var rotation = CalculateCardRotation(i);

                card.transform.localPosition = position;
                card.transform.localRotation = rotation;
                
                // ソート順を設定
                if (card.GetComponent<SpriteRenderer>() != null)
                {
                    card.GetComponent<SpriteRenderer>().sortingOrder = 10 + i;
                }
            }
        }

        /// <summary>
        /// アニメーション付きでカードを配置
        /// </summary>
        private System.Collections.IEnumerator ArrangeCardsAnimated()
        {
            // 既存のアニメーションを停止
            StopActiveAnimations();

            var animationCoroutines = new List<Coroutine>();

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                var targetPosition = CalculateCardPosition(i);
                var targetRotation = CalculateCardRotation(i);

                var animCoroutine = StartCoroutine(AnimateCardToPosition(card, targetPosition, targetRotation, i * 0.05f));
                animationCoroutines.Add(animCoroutine);
                _activeAnimations.Add(animCoroutine);
            }

            // 全アニメーション完了を待つ
            foreach (var coroutine in animationCoroutines)
            {
                yield return coroutine;
            }

            _activeAnimations.Clear();
        }

        /// <summary>
        /// カードを指定位置にアニメーション
        /// </summary>
        private System.Collections.IEnumerator AnimateCardToPosition(Card card, Vector3 targetPosition, Quaternion targetRotation, float delay)
        {
            if (delay > 0)
                yield return new WaitForSeconds(delay);

            var startPosition = card.transform.localPosition;
            var startRotation = card.transform.localRotation;
            var elapsedTime = 0f;

            while (elapsedTime < _animationDuration)
            {
                elapsedTime += Time.deltaTime;
                var t = elapsedTime / _animationDuration;
                var easedT = _animationCurve.Evaluate(t);

                card.transform.localPosition = Vector3.Lerp(startPosition, targetPosition, easedT);
                card.transform.localRotation = Quaternion.Lerp(startRotation, targetRotation, easedT);

                yield return null;
            }

            card.transform.localPosition = targetPosition;
            card.transform.localRotation = targetRotation;
        }

        /// <summary>
        /// カードの位置を計算
        /// </summary>
        /// <param name="index">カードインデックス</param>
        /// <returns>計算された位置</returns>
        private Vector3 CalculateCardPosition(int index)
        {
            if (_cards.Count <= 1)
                return Vector3.zero;

            // 手札の中央を基準とした相対位置
            float normalizedIndex = (float)index / (_cards.Count - 1); // 0.0 to 1.0
            float centeredIndex = normalizedIndex - 0.5f; // -0.5 to 0.5

            var position = new Vector3(
                centeredIndex * _cardSpacing.x * (_cards.Count - 1),
                _handCurve.Evaluate(normalizedIndex) * _cardSpacing.y,
                _cardSpacing.z * index
            );

            return position;
        }

        /// <summary>
        /// カードの回転を計算
        /// </summary>
        /// <param name="index">カードインデックス</param>
        /// <returns>計算された回転</returns>
        private Quaternion CalculateCardRotation(int index)
        {
            if (_cards.Count <= 1 || _cardRotationSpread == 0)
                return Quaternion.identity;

            float normalizedIndex = (float)index / (_cards.Count - 1);
            float centeredIndex = normalizedIndex - 0.5f;
            float rotationZ = centeredIndex * _cardRotationSpread;

            return Quaternion.Euler(0, 0, rotationZ);
        }

        /// <summary>
        /// アクティブなアニメーションを停止
        /// </summary>
        private void StopActiveAnimations()
        {
            foreach (var animation in _activeAnimations)
            {
                if (animation != null)
                {
                    StopCoroutine(animation);
                }
            }
            _activeAnimations.Clear();
        }

        #endregion

        #region Event Handling

        /// <summary>
        /// カードのイベントリスナーを登録
        /// </summary>
        /// <param name="card">対象カード</param>
        private void RegisterCardEvents(Card card)
        {
            card.OnCardClicked += OnCardClickedHandler;
            card.OnCardHovered += OnCardHoveredHandler;
        }

        /// <summary>
        /// カードのイベントリスナーを解除
        /// </summary>
        /// <param name="card">対象カード</param>
        private void UnregisterCardEvents(Card card)
        {
            card.OnCardClicked -= OnCardClickedHandler;
            card.OnCardHovered -= OnCardHoveredHandler;
        }

        /// <summary>
        /// カードクリック時の処理
        /// </summary>
        /// <param name="card">クリックされたカード</param>
        private void OnCardClickedHandler(Card card)
        {
            if (_allowCardSelection)
            {
                ToggleCardSelection(card);
            }
        }

        /// <summary>
        /// カードホバー時の処理
        /// </summary>
        /// <param name="card">ホバーされたカード</param>
        private void OnCardHoveredHandler(Card card)
        {
            if (!_allowCardHover)
                return;

            // 前のホバーをクリア
            if (_hoveredCard != null && _hoveredCard != card)
            {
                ResetCardHover(_hoveredCard);
            }

            _hoveredCard = card;
            ApplyCardHover(card);
        }

        /// <summary>
        /// カードホバー効果を適用
        /// </summary>
        /// <param name="card">対象カード</param>
        private void ApplyCardHover(Card card)
        {
            if (_enableAnimations)
            {
                var targetPos = card.transform.localPosition + Vector3.up * _hoverHeight;
                StartCoroutine(AnimateCardToPosition(card, targetPos, card.transform.localRotation, 0f));
            }
            else
            {
                card.transform.localPosition += Vector3.up * _hoverHeight;
            }

            // ソート順を最前面に
            if (card.GetComponent<SpriteRenderer>() != null)
            {
                card.GetComponent<SpriteRenderer>().sortingOrder = 100;
            }
        }

        /// <summary>
        /// カードホバー効果をリセット
        /// </summary>
        /// <param name="card">対象カード</param>
        private void ResetCardHover(Card card)
        {
            ArrangeCards(); // 全体を再配置してホバー効果をリセット
        }

        /// <summary>
        /// 全イベントリスナーをクリア
        /// </summary>
        private void ClearEventListeners()
        {
            foreach (var card in _cards)
            {
                UnregisterCardEvents(card);
            }

            OnCardAdded = null;
            OnCardRemoved = null;
            OnCardSelected = null;
            OnCardDeselected = null;
            OnCardPlayed = null;
            OnHandSizeChanged = null;
            OnHandCleared = null;
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// 指定タイプのカードを取得
        /// </summary>
        /// <param name="cardType">カードタイプ</param>
        /// <returns>該当するカードリスト</returns>
        public List<Card> GetCardsByType(CardType cardType)
        {
            return _cards.Where(card => card.CardData.CardType == cardType).ToList();
        }

        /// <summary>
        /// ポケモンカードを取得
        /// </summary>
        /// <returns>ポケモンカードリスト</returns>
        public List<Card> GetPokemonCards()
        {
            return GetCardsByType(CardType.Pokemon);
        }

        /// <summary>
        /// トレーナーカードを取得
        /// </summary>
        /// <returns>トレーナーカードリスト</returns>
        public List<Card> GetTrainerCards()
        {
            return GetCardsByType(CardType.Trainer);
        }

        /// <summary>
        /// エネルギーカードを取得
        /// </summary>
        /// <returns>エネルギーカードリスト</returns>
        public List<Card> GetEnergyCards()
        {
            return GetCardsByType(CardType.Energy);
        }

        /// <summary>
        /// プレイ可能なカードを取得
        /// </summary>
        /// <returns>プレイ可能なカードリスト</returns>
        public List<Card> GetPlayableCards()
        {
            // TODO: ゲームルールに基づいてプレイ可能性を判定
            return _cards.ToList();
        }

        /// <summary>
        /// 指定名のカードを検索
        /// </summary>
        /// <param name="cardName">カード名</param>
        /// <returns>該当するカード（見つからない場合null）</returns>
        public Card FindCardByName(string cardName)
        {
            return _cards.Find(card => card.CardData.CardName == cardName);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 手札の表示/非表示を設定
        /// </summary>
        /// <param name="visible">表示するか</param>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            _handContainer.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 最大手札サイズを設定
        /// </summary>
        /// <param name="maxSize">最大サイズ</param>
        public void SetMaxHandSize(int maxSize)
        {
            _maxHandSize = Mathf.Max(1, maxSize);
        }

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        /// <returns>デバッグ情報文字列</returns>
        public string GetDebugInfo()
        {
            return $"Hand - Player: {_playerId}\n" +
                   $"Cards: {Count}/{_maxHandSize}\n" +
                   $"Selected: {SelectedCount}\n" +
                   $"Pokemon: {GetPokemonCards().Count}\n" +
                   $"Trainer: {GetTrainerCards().Count}\n" +
                   $"Energy: {GetEnergyCards().Count}";
        }

        #endregion
    }
}