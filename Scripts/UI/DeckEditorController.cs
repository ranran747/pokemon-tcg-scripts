using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.UI
{
    /// <summary>
    /// Phase 5 - デッキエディターシステム統合制御
    /// オフライン版PTCGL完全デッキ構築システム
    /// ServiceLocator・EventBus・DOTween完全統合
    /// </summary>
    public class DeckEditorController : MonoBehaviour
    {
        #region GameObject References - DeckEditorUI構造

        [Header("=== Main Panels ===")]
        public Transform collectionPanel;          // CollectionPanel - カードコレクション
        public Transform deckBuilderPanel;         // DeckBuilderPanel - デッキ構築
        public Transform previewPanel;             // PreviewPanel - カードプレビュー

        [Header("=== Collection Panel Components ===")]
        public ScrollRect collectionScrollView;    // CollectionScrollView
        public Transform collectionContent;       // CollectionContent - GridLayoutGroup
        public InputField searchInputField;       // SearchInputField
        public Button pokemonFilterButton;        // PokemonFilterButton
        public Button trainerFilterButton;       // TrainerFilterButton  
        public Button energyFilterButton;        // EnergyFilterButton

        [Header("=== Deck Builder Panel Components ===")]
        public ScrollRect deckScrollView;         // DeckScrollView
        public Transform deckContent;             // DeckContent - VerticalLayoutGroup
        public Text deckCountText;                // デッキ枚数表示
        public Text deckStatsText;                // デッキ統計表示

        [Header("=== Preview Panel Components ===")]
        public Image cardPreviewImage;            // カードプレビュー画像
        public Text cardDetailsText;              // カード詳細テキスト

        [Header("=== Card Display Settings ===")]
        public GameObject cardDisplayPrefab;      // カード表示用プレハブ
        public Sprite unknownCardSprite;          // unknown.jpg
        public int cardsPerRow = 6;              // コレクション表示列数
        public Vector2 cardSize = new Vector2(120, 168); // カードサイズ
        public float cardSpacing = 10f;          // カード間隔

        [Header("=== Animation Settings ===")]
        public float cardAnimationSpeed = 0.3f;
        public AnimationCurve cardMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public Color selectedCardColor = new Color(1, 1, 0, 0.8f);
        public Color filterActiveColor = new Color(0, 1, 0, 0.8f);

        #endregion

        #region Private Fields - システム制御

        // システム参照
        private CardDatabase cardDatabase;
        private bool isInitialized = false;

        // デッキデータ
        private List<Card> currentDeck = new List<Card>();
        private Dictionary<string, int> deckCardCounts = new Dictionary<string, int>();
        private int maxDeckSize = 60;
        private int minDeckSize = 60;

        // コレクション表示
        private List<Card> allCards = new List<Card>();
        private List<Card> filteredCards = new List<Card>();
        private List<GameObject> displayedCardObjects = new List<GameObject>();

        // フィルター状態
        private CardFilterType currentFilter = CardFilterType.All;
        private string currentSearchText = "";

        // 選択状態
        private Card selectedCard;
        private GameObject selectedCardObject;

        // ドラッグ&ドロップ
        private bool isDragging = false;
        private GameObject draggedCardObject;
        private Vector3 dragOffset;

        // アニメーション管理
        private List<Tween> activeTweens = new List<Tween>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // DeckEditorController自動初期化
            StartCoroutine(DelayedInitialization());
        }

        private void Start()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }

        private System.Collections.IEnumerator DelayedInitialization()
        {
            // 他のシステムの初期化を待つ
            yield return null;
            yield return null; // 2フレーム待機
            
            if (!isInitialized)
            {
                Initialize();
            }
        }

        #endregion

        #region Initialization

        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("[DeckEditorController] Initializing Deck Editor System...");

            // システム参照取得
            InitializeSystemReferences();

            // UI参照自動検出
            AutoDetectUIReferences();

            // UI設定
            SetupUI();

            // イベント購読
            SubscribeToEvents();

            // カードデータ読み込み
            LoadCardDatabase();

            // 初期表示
            RefreshCollectionDisplay();

            isInitialized = true;
            Debug.Log("[DeckEditorController] Deck Editor System initialized successfully");

            // デッキエディター起動イベント
            EventBus.Emit(new DeckEditorOpenedEvent());
        }

        private void InitializeSystemReferences()
        {
            // CardDatabase参照
            cardDatabase = ServiceLocator.Get<CardDatabase>();
            if (cardDatabase == null)
            {
                Debug.LogWarning("[DeckEditorController] CardDatabase not found - creating temporary database");
                cardDatabase = CreateTemporaryDatabase();
            }
        }

        private void AutoDetectUIReferences()
        {
            // 自動UI参照検出 - 手動設定されていない場合のフォールバック
            if (collectionPanel == null)
                collectionPanel = transform.Find("DeckEditorManager/CollectionPanel");

            if (deckBuilderPanel == null)
                deckBuilderPanel = transform.Find("DeckEditorManager/DeckBuilderPanel");

            if (previewPanel == null)
                previewPanel = transform.Find("DeckEditorManager/PreviewPanel");

            if (collectionScrollView == null)
                collectionScrollView = collectionPanel?.GetComponentInChildren<ScrollRect>();

            if (collectionContent == null)
                collectionContent = collectionScrollView?.content;

            if (searchInputField == null)
                searchInputField = collectionPanel?.GetComponentInChildren<InputField>();

            if (deckScrollView == null)
                deckScrollView = deckBuilderPanel?.GetComponentInChildren<ScrollRect>();

            if (deckContent == null)
                deckContent = deckScrollView?.content;

            // フィルターボタン自動検出
            AutoDetectFilterButtons();
        }

        private void AutoDetectFilterButtons()
        {
            if (collectionPanel == null) return;

            var buttons = collectionPanel.GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                switch (button.name)
                {
                    case "PokemonFilterButton":
                        pokemonFilterButton = button;
                        break;
                    case "TrainerFilterButton":
                        trainerFilterButton = button;
                        break;
                    case "EnergyFilterButton":
                        energyFilterButton = button;
                        break;
                }
            }
        }

        private void SetupUI()
        {
            // GridLayoutGroup設定
            SetupCollectionGrid();

            // フィルターボタン設定
            SetupFilterButtons();

            // 検索フィールド設定
            SetupSearchField();

            // ドラッグ&ドロップ設定
            SetupDragAndDrop();

            // 初期状態設定
            SetInitialState();
        }

        private void SetupCollectionGrid()
        {
            if (collectionContent == null) return;

            var gridLayout = collectionContent.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                gridLayout.cellSize = cardSize;
                gridLayout.spacing = Vector2.one * cardSpacing;
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = cardsPerRow;
                gridLayout.childAlignment = TextAnchor.UpperLeft;
            }

            var contentSizeFitter = collectionContent.GetComponent<ContentSizeFitter>();
            if (contentSizeFitter == null)
            {
                contentSizeFitter = collectionContent.gameObject.AddComponent<ContentSizeFitter>();
            }
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void SetupFilterButtons()
        {
            if (pokemonFilterButton != null)
            {
                pokemonFilterButton.onClick.RemoveAllListeners();
                pokemonFilterButton.onClick.AddListener(() => SetFilter(CardFilterType.Pokemon));
            }

            if (trainerFilterButton != null)
            {
                trainerFilterButton.onClick.RemoveAllListeners();
                trainerFilterButton.onClick.AddListener(() => SetFilter(CardFilterType.Trainer));
            }

            if (energyFilterButton != null)
            {
                energyFilterButton.onClick.RemoveAllListeners();
                energyFilterButton.onClick.AddListener(() => SetFilter(CardFilterType.Energy));
            }
        }

        private void SetupSearchField()
        {
            if (searchInputField != null)
            {
                searchInputField.onValueChanged.RemoveAllListeners();
                searchInputField.onValueChanged.AddListener(OnSearchTextChanged);
                
                // プレースホルダー設定
                var placeholder = searchInputField.placeholder as Text;
                if (placeholder == null)
                {
                    var placeholderObj = new GameObject("Placeholder");
                    placeholderObj.transform.SetParent(searchInputField.transform, false);
                    placeholder = placeholderObj.AddComponent<Text>();
                    placeholder.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    placeholder.color = Color.gray;
                    searchInputField.placeholder = placeholder;
                }
                placeholder.text = "Search cards...";
            }
        }

        private void SetupDragAndDrop()
        {
            // ドラッグ&ドロップシステムはカード生成時に個別設定
        }

        private void SetInitialState()
        {
            // フィルター初期状態
            currentFilter = CardFilterType.All;
            currentSearchText = "";

            // デッキ初期化
            currentDeck.Clear();
            deckCardCounts.Clear();

            // プレビューパネル初期状態
            if (previewPanel != null)
            {
                previewPanel.gameObject.SetActive(false);
            }
        }

        private void SubscribeToEvents()
        {
            EventBus.On<CardDatabaseLoadedEvent>(OnCardDatabaseLoaded);
            EventBus.On<DeckValidationRequestEvent>(OnDeckValidationRequested);
        }

        #endregion

        #region Card Database Management

        private void LoadCardDatabase()
        {
            if (cardDatabase == null) return;

            try
            {
                // CardDatabaseから全カードデータを取得
                var allCardData = cardDatabase.AllCards;
                allCards.Clear();
                
                // BaseCardDataからCardオブジェクトを作成
                foreach (var cardData in allCardData)
                {
                    // Card GameObjectを作成
                    GameObject cardObj = new GameObject($"Card_{cardData.CardName}");
                    var card = cardObj.AddComponent<Card>();
                    
                    // Cardを初期化（InstanceIdは適当に設定）
                    int instanceId = allCards.Count + 1;
                    card.Initialize(cardData, "Editor", instanceId);
                    
                    allCards.Add(card);
                }
                
                Debug.Log($"[DeckEditorController] Loaded {allCards.Count} cards from database");

                // フィルタリング適用
                ApplyCurrentFilter();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DeckEditorController] Failed to load card database: {ex.Message}");
                CreateSampleCards();
            }
        }

        private CardDatabase CreateTemporaryDatabase()
        {
            // CardDatabaseが見つからない場合、一時的なものを作成
            Debug.LogWarning("[DeckEditorController] CardDatabase not found - creating temporary database");
            return null; // 暫定的にnullを返してサンプルカード作成へ
        }

        private void CreateSampleCards()
        {
            // Phase 5完成まで使用する最小限のサンプルカード
            allCards.Clear();

            // シンプルなダミーカードを作成
            for (int i = 1; i <= 44; i++)
            {
                var dummyCard = CreateDummyCard($"Sample Card {i}", i);
                allCards.Add(dummyCard);
            }

            Debug.Log($"[DeckEditorController] Created {allCards.Count} sample cards");
        }

        private Card CreateDummyCard(string name, int id)
        {
            // 最小限のダミーカードを作成
            GameObject cardObj = new GameObject($"DummyCard_{name}");
            var card = cardObj.AddComponent<Card>();
            
            // ダミーのBaseCardDataを作成（実際にはScriptableObjectが必要）
            // 暫定的に初期化をスキップ
            
            return card;
        }

        #endregion

        #region Collection Display

        public void RefreshCollectionDisplay()
        {
            if (!isInitialized || collectionContent == null) return;

            // 既存カード表示をクリア
            ClearDisplayedCards();

            // フィルタリング済みカードを表示
            DisplayFilteredCards();

            Debug.Log($"[DeckEditorController] Displaying {filteredCards.Count} cards");
        }

        private void ClearDisplayedCards()
        {
            foreach (var cardObj in displayedCardObjects)
            {
                if (cardObj != null)
                {
                    DestroyImmediate(cardObj);
                }
            }
            displayedCardObjects.Clear();
        }

        private void DisplayFilteredCards()
        {
            for (int i = 0; i < filteredCards.Count; i++)
            {
                var card = filteredCards[i];
                var cardObject = CreateCardDisplayObject(card, i);
                displayedCardObjects.Add(cardObject);
            }
        }

        private GameObject CreateCardDisplayObject(Card card, int index)
        {
            // カード表示オブジェクト作成
            GameObject cardObj = new GameObject($"Card_{card.CardData?.CardName ?? "Unknown"}_{index}");
            cardObj.transform.SetParent(collectionContent, false);

            // Image コンポーネント追加
            var image = cardObj.AddComponent<Image>();
            image.sprite = unknownCardSprite; // Phase 5では高解像度画像システム実装予定
            image.preserveAspect = true;

            // Button コンポーネント追加
            var button = cardObj.AddComponent<Button>();
            button.onClick.AddListener(() => OnCardClicked(card, cardObj));

            // ドラッグハンドラー追加
            var dragHandler = cardObj.AddComponent<CardDragHandler>();
            dragHandler.Initialize(card, this);

            // RectTransform設定
            var rectTransform = cardObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = cardSize;

            // カード情報表示（テキスト追加）
            AddCardInfoText(cardObj, card);

            return cardObj;
        }

        private void AddCardInfoText(GameObject cardObj, Card card)
        {
            // カード名表示
            GameObject textObj = new GameObject("CardNameText");
            textObj.transform.SetParent(cardObj.transform, false);

            var text = textObj.AddComponent<Text>();
            text.text = card.CardData?.CardName ?? "Unknown Card";
            text.fontSize = 12;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 0.2f);
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
        }

        #endregion

        #region Filtering & Search

        public void SetFilter(CardFilterType filterType)
        {
            currentFilter = filterType;
            ApplyCurrentFilter();
            UpdateFilterButtonStates();
            RefreshCollectionDisplay();

            Debug.Log($"[DeckEditorController] Filter changed to: {filterType}");
        }

        private void ApplyCurrentFilter()
        {
            filteredCards.Clear();

            foreach (var card in allCards)
            {
                if (PassesFilter(card) && PassesSearch(card))
                {
                    filteredCards.Add(card);
                }
            }

            // 名前順でソート
            filteredCards = filteredCards.OrderBy(c => c.CardData?.CardName ?? "").ToList();
        }

        private bool PassesFilter(Card card)
        {
            if (card?.CardData == null) return false;
            
            switch (currentFilter)
            {
                case CardFilterType.Pokemon:
                    return card.CardData.CardType == CardType.Pokemon;
                case CardFilterType.Trainer:
                    return card.CardData.CardType == CardType.Trainer;
                case CardFilterType.Energy:
                    return card.CardData.CardType == CardType.Energy;
                case CardFilterType.All:
                default:
                    return true;
            }
        }

        private bool PassesSearch(Card card)
        {
            if (string.IsNullOrEmpty(currentSearchText) || card?.CardData == null)
                return true;

            return card.CardData.CardName.ToLower().Contains(currentSearchText.ToLower());
        }

        private void UpdateFilterButtonStates()
        {
            // フィルターボタンの視覚状態更新
            SetButtonState(pokemonFilterButton, currentFilter == CardFilterType.Pokemon);
            SetButtonState(trainerFilterButton, currentFilter == CardFilterType.Trainer);
            SetButtonState(energyFilterButton, currentFilter == CardFilterType.Energy);
        }

        private void SetButtonState(Button button, bool isActive)
        {
            if (button == null) return;

            var colors = button.colors;
            colors.normalColor = isActive ? filterActiveColor : Color.white;
            button.colors = colors;
        }

        private void OnSearchTextChanged(string searchText)
        {
            currentSearchText = searchText;
            ApplyCurrentFilter();
            RefreshCollectionDisplay();
        }

        #endregion

        #region Card Selection & Preview

        public void OnCardClicked(Card card, GameObject cardObject)
        {
            // 前の選択をクリア
            ClearCardSelection();

            // 新しい選択を設定
            selectedCard = card;
            selectedCardObject = cardObject;

            // 視覚的選択表示
            var image = cardObject.GetComponent<Image>();
            if (image != null)
            {
                image.color = selectedCardColor;
            }

            // プレビュー表示更新
            UpdatePreviewPanel(card);

            Debug.Log($"[DeckEditorController] Card selected: {card.CardData?.CardName ?? "Unknown"}");
        }

        private void ClearCardSelection()
        {
            if (selectedCardObject != null)
            {
                var image = selectedCardObject.GetComponent<Image>();
                if (image != null)
                {
                    image.color = Color.white;
                }
            }

            selectedCard = null;
            selectedCardObject = null;
        }

        private void UpdatePreviewPanel(Card card)
        {
            if (previewPanel == null) return;

            previewPanel.gameObject.SetActive(true);

            // プレビュー画像更新
            if (cardPreviewImage != null)
            {
                cardPreviewImage.sprite = unknownCardSprite; // Phase 5で高解像度画像実装
            }

            // カード詳細テキスト更新
            if (cardDetailsText != null)
            {
                cardDetailsText.text = GetCardDetailsText(card);
            }
        }

        private string GetCardDetailsText(Card card)
        {
            if (card?.CardData == null) return "No card data";
            
            string details = $"{card.CardData.CardName}\n\n";
            details += $"Type: {card.CardData.CardType}\n";
            details += $"Rarity: {card.CardData.Rarity}\n";
            details += $"\n{card.CardData.RulesText}";

            return details;
        }

        #endregion

        #region Deck Management

        public void AddCardToDeck(Card card)
        {
            if (card == null) return;

            // デッキサイズチェック
            if (currentDeck.Count >= maxDeckSize)
            {
                Debug.LogWarning("[DeckEditorController] Deck is full!");
                return;
            }

            // 同名カード枚数制限チェック（通常4枚まで）
            string cardName = card.CardData?.CardName ?? "Unknown";
            int currentCount = deckCardCounts.GetValueOrDefault(cardName, 0);
            
            if (currentCount >= 4)
            {
                Debug.LogWarning($"[DeckEditorController] Cannot add more {cardName} - limit reached");
                return;
            }

            // デッキに追加
            currentDeck.Add(card);
            deckCardCounts[cardName] = currentCount + 1;

            // デッキ表示更新
            RefreshDeckDisplay();

            // アニメーション
            AnimateCardAddToDeck(card);

            Debug.Log($"[DeckEditorController] Added {cardName} to deck ({currentDeck.Count}/{maxDeckSize})");

            // イベント通知
            EventBus.Emit(new CardAddedToDeckEvent { Card = card, DeckSize = currentDeck.Count });
        }

        public void RemoveCardFromDeck(Card card)
        {
            if (card == null || !currentDeck.Contains(card)) return;

            // デッキから削除
            currentDeck.Remove(card);
            string cardName = card.CardData?.CardName ?? "Unknown";
            int currentCount = deckCardCounts.GetValueOrDefault(cardName, 0);
            
            if (currentCount > 1)
            {
                deckCardCounts[cardName] = currentCount - 1;
            }
            else
            {
                deckCardCounts.Remove(cardName);
            }

            // デッキ表示更新
            RefreshDeckDisplay();

            Debug.Log($"[DeckEditorController] Removed {cardName} from deck ({currentDeck.Count}/{maxDeckSize})");

            // イベント通知
            EventBus.Emit(new CardRemovedFromDeckEvent { Card = card, DeckSize = currentDeck.Count });
        }

        private void RefreshDeckDisplay()
        {
            if (deckContent == null) return;

            // 既存デッキ表示をクリア
            ClearDeckDisplay();

            // デッキカードを種類別に表示
            DisplayDeckCards();

            // デッキ統計更新
            UpdateDeckStats();
        }

        private void ClearDeckDisplay()
        {
            for (int i = deckContent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(deckContent.GetChild(i).gameObject);
            }
        }

        private void DisplayDeckCards()
        {
            var groupedCards = deckCardCounts.GroupBy(kvp => kvp.Key)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var group in groupedCards)
            {
                string cardName = group.Key;
                int count = group.First().Value;
                var card = currentDeck.FirstOrDefault(c => (c.CardData?.CardName ?? "Unknown") == cardName);

                if (card != null)
                {
                    CreateDeckCardEntry(card, count);
                }
            }
        }

        private void CreateDeckCardEntry(Card card, int count)
        {
            GameObject entryObj = new GameObject($"DeckEntry_{card.CardData?.CardName ?? "Unknown"}");
            entryObj.transform.SetParent(deckContent, false);

            // 水平レイアウト
            var horizontalLayout = entryObj.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.childControlWidth = false;
            horizontalLayout.childControlHeight = false;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;

            // カウントテキスト
            CreateDeckEntryText(entryObj, $"{count}x", 40);
            
            // カード名テキスト
            CreateDeckEntryText(entryObj, card.CardData?.CardName ?? "Unknown", 200);

            // 削除ボタン
            CreateDeckEntryRemoveButton(entryObj, card);

            // RectTransform設定
            var rectTransform = entryObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(300, 30);
        }

        private void CreateDeckEntryText(GameObject parent, string text, float width)
        {
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(parent.transform, false);

            var textComponent = textObj.AddComponent<Text>();
            textComponent.text = text;
            textComponent.fontSize = 14;
            textComponent.color = Color.white;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var rectTransform = textObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(width, 30);
        }

        private void CreateDeckEntryRemoveButton(GameObject parent, Card card)
        {
            GameObject buttonObj = new GameObject("RemoveButton");
            buttonObj.transform.SetParent(parent.transform, false);

            var button = buttonObj.AddComponent<Button>();
            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(1, 0.2f, 0.2f, 0.8f);

            button.onClick.AddListener(() => RemoveCardFromDeck(card));

            // ボタンテキスト
            GameObject buttonTextObj = new GameObject("ButtonText");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            
            var buttonText = buttonTextObj.AddComponent<Text>();
            buttonText.text = "-";
            buttonText.fontSize = 16;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(30, 25);
        }

        private void UpdateDeckStats()
        {
            if (deckCountText != null)
            {
                deckCountText.text = $"Deck: {currentDeck.Count}/{maxDeckSize}";
                
                // 色分け（完成デッキは緑、未完成は赤）
                if (currentDeck.Count == maxDeckSize)
                {
                    deckCountText.color = Color.green;
                }
                else if (currentDeck.Count < minDeckSize)
                {
                    deckCountText.color = Color.red;
                }
                else
                {
                    deckCountText.color = Color.yellow;
                }
            }

            if (deckStatsText != null)
            {
                var pokemonCount = currentDeck.Count(c => c.CardData?.CardType == CardType.Pokemon);
                var trainerCount = currentDeck.Count(c => c.CardData?.CardType == CardType.Trainer);
                var energyCount = currentDeck.Count(c => c.CardData?.CardType == CardType.Energy);

                deckStatsText.text = $"Pokemon: {pokemonCount} | Trainer: {trainerCount} | Energy: {energyCount}";
            }
        }

        #endregion

        #region Animation System

        private void AnimateCardAddToDeck(Card card)
        {
            // カードがデッキに追加されるアニメーション
            if (selectedCardObject != null)
            {
                var cardTransform = selectedCardObject.transform;
                var originalPos = cardTransform.position;
                var targetPos = deckBuilderPanel.position;

                // 一時的なアニメーション用オブジェクト作成
                var animObj = Instantiate(selectedCardObject, cardTransform.parent);
                animObj.transform.position = originalPos;

                var tween = animObj.transform.DOMove(targetPos, cardAnimationSpeed)
                    .SetEase(cardMoveCurve)
                    .OnComplete(() => {
                        Destroy(animObj);
                    });

                activeTweens.Add(tween);
            }
        }

        #endregion

        #region Event Handlers

        private void OnCardDatabaseLoaded(CardDatabaseLoadedEvent evt)
        {
            LoadCardDatabase();
            RefreshCollectionDisplay();
        }

        private void OnDeckValidationRequested(DeckValidationRequestEvent evt)
        {
            ValidateDeck();
        }

        #endregion

        #region Deck Validation

        public bool ValidateDeck()
        {
            var validationResults = new List<string>();

            // サイズチェック
            if (currentDeck.Count != maxDeckSize)
            {
                validationResults.Add($"Deck must contain exactly {maxDeckSize} cards (current: {currentDeck.Count})");
            }

            // カード枚数制限チェック
            foreach (var kvp in deckCardCounts)
            {
                if (kvp.Value > 4)
                {
                    validationResults.Add($"Too many {kvp.Key} ({kvp.Value}/4)");
                }
            }

            // 結果通知
            bool isValid = validationResults.Count == 0;
            EventBus.Emit(new DeckValidationResultEvent 
            { 
                IsValid = isValid, 
                ValidationErrors = validationResults 
            });

            return isValid;
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// デッキエディターを開く
        /// </summary>
        public void OpenDeckEditor()
        {
            gameObject.SetActive(true);
            if (!isInitialized)
            {
                Initialize();
            }
            RefreshCollectionDisplay();
        }

        /// <summary>
        /// デッキエディターを閉じる
        /// </summary>
        public void CloseDeckEditor()
        {
            gameObject.SetActive(false);
            EventBus.Emit(new DeckEditorClosedEvent());
        }

        /// <summary>
        /// 新しいデッキを作成
        /// </summary>
        public void CreateNewDeck()
        {
            currentDeck.Clear();
            deckCardCounts.Clear();
            RefreshDeckDisplay();
            ClearCardSelection();
            
            Debug.Log("[DeckEditorController] New deck created");
        }

        /// <summary>
        /// 現在のデッキを取得
        /// </summary>
        public List<Card> GetCurrentDeck()
        {
            return new List<Card>(currentDeck);
        }

        #endregion

        #region Cleanup

        public void Cleanup()
        {
            // イベント購読解除
            EventBus.Off<CardDatabaseLoadedEvent>(OnCardDatabaseLoaded);
            EventBus.Off<DeckValidationRequestEvent>(OnDeckValidationRequested);

            // アニメーション停止
            foreach (var tween in activeTweens)
            {
                if (tween != null && tween.IsActive())
                {
                    tween.Kill();
                }
            }
            activeTweens.Clear();

            // データクリア
            currentDeck.Clear();
            deckCardCounts.Clear();
            allCards.Clear();
            filteredCards.Clear();
            displayedCardObjects.Clear();

            isInitialized = false;
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        #endregion
    }

    #region Support Classes

    /// <summary>
    /// カードドラッグハンドラー
    /// </summary>
    public class CardDragHandler : MonoBehaviour, 
        UnityEngine.EventSystems.IBeginDragHandler,
        UnityEngine.EventSystems.IDragHandler,
        UnityEngine.EventSystems.IEndDragHandler
    {
        private Card card;
        private DeckEditorController controller;
        private Vector3 originalPosition;
        private Transform originalParent;

        public void Initialize(Card card, DeckEditorController controller)
        {
            this.card = card;
            this.controller = controller;
        }

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            originalPosition = transform.position;
            originalParent = transform.parent;
            
            // ドラッグ中は最前面に表示
            transform.SetParent(controller.transform.root, true);
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            transform.position = eventData.position;
        }

        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // ドロップ先判定
            if (IsOverDeckArea(eventData.position))
            {
                controller.AddCardToDeck(card);
            }

            // 元の位置に戻す
            transform.SetParent(originalParent, true);
            transform.position = originalPosition;
        }

        private bool IsOverDeckArea(Vector2 position)
        {
            var deckRect = controller.deckBuilderPanel.GetComponent<RectTransform>();
            return RectTransformUtility.RectangleContainsScreenPoint(deckRect, position);
        }
    }

    /// <summary>
    /// カードフィルタータイプ
    /// </summary>
    public enum CardFilterType
    {
        All,
        Pokemon,
        Trainer,
        Energy
    }

    #endregion

    #region Event Classes

    public class DeckEditorOpenedEvent { }
    public class DeckEditorClosedEvent { }

    public class CardAddedToDeckEvent 
    { 
        public Card Card { get; set; }
        public int DeckSize { get; set; }
    }

    public class CardRemovedFromDeckEvent 
    { 
        public Card Card { get; set; }
        public int DeckSize { get; set; }
    }

    public class DeckValidationRequestEvent { }

    public class DeckValidationResultEvent 
    { 
        public bool IsValid { get; set; }
        public List<string> ValidationErrors { get; set; }
    }

    public class CardDatabaseLoadedEvent { }

    #endregion
}