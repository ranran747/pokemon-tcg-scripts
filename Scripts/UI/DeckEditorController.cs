using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;
using PokemonTCG.Game;

namespace PokemonTCG.UI
{
    /// <summary>
    /// デッキエディターシステム - Phase 5
    /// ドラッグ&ドロップによる視覚的デッキ構築
    /// カードコレクション管理・検索・フィルタリング機能
    /// </summary>
    public class DeckEditorController : MonoBehaviour
    {
        #region UI References
        
        [Header("=== Main Panels ===")]
        public Transform collectionPanel;       // カードコレクション表示エリア
        public Transform deckBuilderPanel;      // デッキ構築エリア
        public Transform previewPanel;          // カード詳細プレビュー
        
        [Header("=== Collection UI ===")]
        public ScrollRect collectionScrollRect; // コレクションスクロール
        public GridLayoutGroup collectionGrid;  // コレクショングリッド
        public GameObject cardSlotPrefab;       // カードスロットプレハブ
        public TextMeshProUGUI collectionCountText; // コレクション枚数表示
        
        [Header("=== Deck Builder UI ===")]
        public Transform deckListContainer;     // デッキリスト表示
        public GameObject deckCardPrefab;       // デッキカード表示プレハブ
        public TextMeshProUGUI deckCountText;   // デッキ枚数表示
        public TextMeshProUGUI deckNameText;    // デッキ名表示
        public Button saveDeckButton;           // デッキ保存ボタン
        public Button loadDeckButton;           // デッキ読み込みボタン
        public Button clearDeckButton;          // デッキクリアボタン
        
        [Header("=== Search & Filter UI ===")]
        public InputField searchField;          // 検索フィールド
        public Dropdown typeFilterDropdown;     // タイプフィルター
        public Dropdown rarityFilterDropdown;   // レアリティフィルター
        public Dropdown costFilterDropdown;     // コストフィルター
        public Toggle showOnlyOwnedToggle;      // 所有カードのみ表示
        
        [Header("=== Card Preview ===")]
        public Image previewCardImage;          // プレビュー画像
        public TextMeshProUGUI previewCardName; // プレビューカード名
        public TextMeshProUGUI previewCardDesc; // プレビュー説明
        public TextMeshProUGUI previewCardStats;// プレビュー能力値
        
        [Header("=== Deck Management ===")]
        public InputField deckNameInput;        // デッキ名入力
        public Dropdown deckTemplateDropdown;   // デッキテンプレート
        public Button exportDeckButton;         // デッキエクスポート
        public Button importDeckButton;         // デッキインポート
        
        [Header("=== Settings ===")]
        public int maxDeckSize = 60;            // 最大デッキサイズ
        public int minDeckSize = 40;            // 最小デッキサイズ
        public float cardAnimationSpeed = 0.3f; // カードアニメーション速度
        
        #endregion

        #region Private Fields
        
        // Core Systems
        private CardDatabase cardDatabase;
        private GameStateManager gameStateManager;
        private bool isInitialized = false;
        
        // Card Collections
        private List<Card> allCards = new List<Card>();
        private List<Card> filteredCards = new List<Card>();
        private List<Card> ownedCards = new List<Card>();
        private Dictionary<string, int> cardCounts = new Dictionary<string, int>();
        
        // Current Deck
        private List<Card> currentDeck = new List<Card>();
        private Dictionary<string, int> deckCardCounts = new Dictionary<string, int>();
        private string currentDeckName = "New Deck";
        
        // UI State
        private List<DeckCardSlot> collectionSlots = new List<DeckCardSlot>();
        private List<DeckListItem> deckListItems = new List<DeckListItem>();
        private Card previewedCard = null;
        private DeckCardSlot draggedSlot = null;
        
        // Filters
        private string searchQuery = "";
        private CardType selectedType = CardType.All;
        private CardRarity selectedRarity = CardRarity.All;
        private int selectedCost = -1; // -1 = All
        private bool showOnlyOwned = false;
        
        #endregion

        #region Initialization
        
        public void Initialize()
        {
            if (isInitialized) return;
            
            Debug.Log("[DeckEditorController] Initializing Deck Editor...");
            
            // Get system references
            InitializeSystemReferences();
            
            // Initialize UI components
            InitializeUIComponents();
            
            // Load card database
            LoadCardDatabase();
            
            // Setup event listeners
            SetupEventListeners();
            
            // Initialize default deck
            InitializeNewDeck();
            
            isInitialized = true;
            Debug.Log("[DeckEditorController] Deck Editor initialized successfully");
        }
        
        private void InitializeSystemReferences()
        {
            cardDatabase = ServiceLocator.Get<CardDatabase>();
            gameStateManager = ServiceLocator.Get<GameStateManager>();
            
            if (cardDatabase == null)
            {
                Debug.LogWarning("[DeckEditorController] CardDatabase not found - creating default");
                CreateDefaultCardDatabase();
            }
        }
        
        private void InitializeUIComponents()
        {
            // Initialize collection grid
            if (collectionGrid != null)
            {
                collectionGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                collectionGrid.constraintCount = 6; // 6 columns
                collectionGrid.spacing = new Vector2(10f, 10f);
                collectionGrid.cellSize = new Vector2(120f, 168f); // Card aspect ratio
            }
            
            // Initialize dropdowns
            InitializeDropdowns();
            
            // Initialize text displays
            UpdateCountDisplays();
        }
        
        private void InitializeDropdowns()
        {
            // Type filter dropdown
            if (typeFilterDropdown != null)
            {
                typeFilterDropdown.ClearOptions();
                List<string> typeOptions = Enum.GetNames(typeof(CardType)).ToList();
                typeFilterDropdown.AddOptions(typeOptions);
            }
            
            // Rarity filter dropdown
            if (rarityFilterDropdown != null)
            {
                rarityFilterDropdown.ClearOptions();
                List<string> rarityOptions = Enum.GetNames(typeof(CardRarity)).ToList();
                rarityFilterDropdown.AddOptions(rarityOptions);
            }
            
            // Cost filter dropdown
            if (costFilterDropdown != null)
            {
                costFilterDropdown.ClearOptions();
                List<string> costOptions = new List<string> { "All", "0", "1", "2", "3", "4", "5+" };
                costFilterDropdown.AddOptions(costOptions);
            }
        }
        
        private void LoadCardDatabase()
        {
            if (cardDatabase != null)
            {
                // Load all cards from database
                allCards = cardDatabase.GetAllCards();
                
                // Simulate owned cards (in real implementation, load from save data)
                ownedCards = allCards.Take(allCards.Count / 2).ToList(); // Own half of all cards
                
                // Initialize card counts
                foreach (var card in ownedCards)
                {
                    string cardId = card.CardData.CardId;
                    cardCounts[cardId] = UnityEngine.Random.Range(1, 4); // 1-3 copies of each card
                }
            }
            else
            {
                CreateSampleCards();
            }
            
            // Apply initial filtering
            ApplyFilters();
        }
        
        private void CreateDefaultCardDatabase()
        {
            // Create a temporary card database for testing
            // In real implementation, this would be loaded from resources
            Debug.Log("[DeckEditorController] Creating default card database for testing");
            CreateSampleCards();
        }
        
        private void CreateSampleCards()
        {
            // Create sample cards for testing
            allCards.Clear();
            ownedCards.Clear();
            
            // Sample Pokemon cards
            for (int i = 1; i <= 20; i++)
            {
                var pokemonData = ScriptableObject.CreateInstance<PokemonCardData>();
                pokemonData.CardId = $"POKEMON_{i:D3}";
                pokemonData.CardName = $"Test Pokemon {i}";
                pokemonData.HP = 100 + (i * 10);
                pokemonData.Type = (PokemonType)(i % 8); // Cycle through types
                
                var pokemonCard = new Card();
                pokemonCard.SetCardData(pokemonData);
                allCards.Add(pokemonCard);
                
                if (i <= 15) // Own 15 out of 20 Pokemon
                {
                    ownedCards.Add(pokemonCard);
                    cardCounts[pokemonData.CardId] = UnityEngine.Random.Range(1, 4);
                }
            }
            
            // Sample Trainer cards
            for (int i = 1; i <= 15; i++)
            {
                var trainerData = ScriptableObject.CreateInstance<TrainerCardData>();
                trainerData.CardId = $"TRAINER_{i:D3}";
                trainerData.CardName = $"Test Trainer {i}";
                trainerData.TrainerType = (TrainerType)(i % 3);
                
                var trainerCard = new Card();
                trainerCard.SetCardData(trainerData);
                allCards.Add(trainerCard);
                
                if (i <= 10) // Own 10 out of 15 Trainers
                {
                    ownedCards.Add(trainerCard);
                    cardCounts[trainerData.CardId] = UnityEngine.Random.Range(1, 4);
                }
            }
            
            // Sample Energy cards
            for (int i = 1; i <= 12; i++)
            {
                var energyData = ScriptableObject.CreateInstance<EnergyCardData>();
                energyData.CardId = $"ENERGY_{i:D3}";
                energyData.CardName = $"Test Energy {i}";
                energyData.EnergyType = (EnergyType)(i % 9);
                
                var energyCard = new Card();
                energyCard.SetCardData(energyData);
                allCards.Add(energyCard);
                
                ownedCards.Add(energyCard); // Own all energy cards
                cardCounts[energyData.CardId] = UnityEngine.Random.Range(2, 6);
            }
            
            Debug.Log($"[DeckEditorController] Created {allCards.Count} sample cards ({ownedCards.Count} owned)");
        }
        
        private void SetupEventListeners()
        {
            // Search field
            if (searchField != null)
                searchField.onValueChanged.AddListener(OnSearchChanged);
            
            // Filter dropdowns
            if (typeFilterDropdown != null)
                typeFilterDropdown.onValueChanged.AddListener(OnTypeFilterChanged);
            
            if (rarityFilterDropdown != null)
                rarityFilterDropdown.onValueChanged.AddListener(OnRarityFilterChanged);
                
            if (costFilterDropdown != null)
                costFilterDropdown.onValueChanged.AddListener(OnCostFilterChanged);
            
            // Show only owned toggle
            if (showOnlyOwnedToggle != null)
                showOnlyOwnedToggle.onValueChanged.AddListener(OnShowOnlyOwnedChanged);
            
            // Deck management buttons
            if (saveDeckButton != null)
                saveDeckButton.onClick.AddListener(SaveCurrentDeck);
                
            if (loadDeckButton != null)
                loadDeckButton.onClick.AddListener(LoadDeck);
                
            if (clearDeckButton != null)
                clearDeckButton.onClick.AddListener(ClearDeck);
                
            if (exportDeckButton != null)
                exportDeckButton.onClick.AddListener(ExportDeck);
                
            if (importDeckButton != null)
                importDeckButton.onClick.AddListener(ImportDeck);
        }
        
        private void InitializeNewDeck()
        {
            currentDeck.Clear();
            deckCardCounts.Clear();
            currentDeckName = "New Deck";
            
            if (deckNameInput != null)
                deckNameInput.text = currentDeckName;
                
            UpdateDeckDisplay();
            UpdateCountDisplays();
        }
        
        #endregion

        #region Public Interface
        
        public void ShowDeckEditor()
        {
            gameObject.SetActive(true);
            if (!isInitialized)
                Initialize();
            
            RefreshCollectionDisplay();
        }
        
        public void HideDeckEditor()
        {
            gameObject.SetActive(false);
        }
        
        public void AddCardToDeck(Card card)
        {
            if (card == null || currentDeck.Count >= maxDeckSize) return;
            
            string cardId = card.CardData.CardId;
            
            // Check if we have copies of this card
            if (!cardCounts.ContainsKey(cardId) || cardCounts[cardId] <= 0) return;
            
            // Check deck limit for this card (max 4 copies in standard TCG)
            int currentCount = deckCardCounts.GetValueOrDefault(cardId, 0);
            if (currentCount >= 4) return;
            
            // Add card to deck
            currentDeck.Add(card);
            deckCardCounts[cardId] = currentCount + 1;
            
            // Update displays
            UpdateDeckDisplay();
            UpdateCountDisplays();
            
            // Animation
            AnimateCardAddToDeck(card);
            
            Debug.Log($"[DeckEditorController] Added {card.CardData.CardName} to deck ({deckCardCounts[cardId]}/4)");
        }
        
        public void RemoveCardFromDeck(Card card)
        {
            if (card == null) return;
            
            string cardId = card.CardData.CardId;
            
            if (currentDeck.Remove(card))
            {
                int currentCount = deckCardCounts.GetValueOrDefault(cardId, 0);
                if (currentCount > 1)
                {
                    deckCardCounts[cardId] = currentCount - 1;
                }
                else
                {
                    deckCardCounts.Remove(cardId);
                }
                
                // Update displays
                UpdateDeckDisplay();
                UpdateCountDisplays();
                
                // Animation
                AnimateCardRemoveFromDeck(card);
                
                Debug.Log($"[DeckEditorController] Removed {card.CardData.CardName} from deck");
            }
        }
        
        public bool IsDeckValid()
        {
            // Check deck size
            if (currentDeck.Count < minDeckSize || currentDeck.Count > maxDeckSize)
                return false;
            
            // Check for required card ratios (simplified)
            int pokemonCount = currentDeck.Count(c => c.IsPokemonCard);
            int energyCount = currentDeck.Count(c => c.IsEnergyCard);
            
            // Must have at least some Pokemon and Energy
            return pokemonCount >= 8 && energyCount >= 8;
        }
        
        public List<Card> GetCurrentDeck()
        {
            return new List<Card>(currentDeck);
        }
        
        public void LoadDeckFromList(List<Card> deckCards, string deckName = "Loaded Deck")
        {
            ClearDeck();
            
            foreach (var card in deckCards)
            {
                AddCardToDeck(card);
            }
            
            currentDeckName = deckName;
            if (deckNameInput != null)
                deckNameInput.text = currentDeckName;
                
            UpdateCountDisplays();
        }
        
        #endregion

        #region Filtering & Search
        
        private void ApplyFilters()
        {
            filteredCards.Clear();
            
            var cardsToFilter = showOnlyOwned ? ownedCards : allCards;
            
            foreach (var card in cardsToFilter)
            {
                if (PassesFilters(card))
                {
                    filteredCards.Add(card);
                }
            }
            
            // Sort filtered cards
            SortFilteredCards();
            
            // Update collection display
            RefreshCollectionDisplay();
            
            Debug.Log($"[DeckEditorController] Applied filters: {filteredCards.Count} cards match criteria");
        }
        
        private bool PassesFilters(Card card)
        {
            // Search query filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                if (!card.CardData.CardName.ToLower().Contains(searchQuery.ToLower()))
                    return false;
            }
            
            // Type filter
            if (selectedType != CardType.All)
            {
                if (!CardMatchesType(card, selectedType))
                    return false;
            }
            
            // Cost filter
            if (selectedCost >= 0)
            {
                int cardCost = GetCardCost(card);
                if (selectedCost == 5 && cardCost < 5) return false; // 5+ filter
                if (selectedCost < 5 && cardCost != selectedCost) return false;
            }
            
            return true;
        }
        
        private bool CardMatchesType(Card card, CardType type)
        {
            if (card.IsPokemonCard) return type == CardType.Pokemon;
            if (card.IsTrainerCard) return type == CardType.Trainer;
            if (card.IsEnergyCard) return type == CardType.Energy;
            return false;
        }
        
        private int GetCardCost(Card card)
        {
            if (card.IsPokemonCard)
            {
                var pokemonData = card.GetPokemonData();
                return pokemonData?.Attacks?.FirstOrDefault()?.EnergyCost?.Count ?? 0;
            }
            return 0;
        }
        
        private void SortFilteredCards()
        {
            filteredCards = filteredCards.OrderBy(c => c.CardData.CardName).ToList();
        }
        
        #endregion

        #region UI Updates
        
        private void RefreshCollectionDisplay()
        {
            if (collectionPanel == null) return;
            
            // Clear existing slots
            ClearCollectionSlots();
            
            // Create slots for filtered cards
            foreach (var card in filteredCards)
            {
                CreateCollectionSlot(card);
            }
            
            UpdateCountDisplays();
        }
        
        private void ClearCollectionSlots()
        {
            foreach (var slot in collectionSlots)
            {
                if (slot != null && slot.gameObject != null)
                    DestroyImmediate(slot.gameObject);
            }
            collectionSlots.Clear();
        }
        
        private void CreateCollectionSlot(Card card)
        {
            if (cardSlotPrefab == null || collectionPanel == null) return;
            
            GameObject slotObj = Instantiate(cardSlotPrefab, collectionPanel);
            var slot = slotObj.GetComponent<DeckCardSlot>();
            
            if (slot == null)
                slot = slotObj.AddComponent<DeckCardSlot>();
            
            // Initialize slot
            slot.Initialize(card, this);
            
            // Set card count
            string cardId = card.CardData.CardId;
            int ownedCount = cardCounts.GetValueOrDefault(cardId, 0);
            int usedCount = deckCardCounts.GetValueOrDefault(cardId, 0);
            slot.SetCardCount(ownedCount, usedCount);
            
            collectionSlots.Add(slot);
        }
        
        private void UpdateDeckDisplay()
        {
            if (deckListContainer == null) return;
            
            // Clear existing items
            ClearDeckListItems();
            
            // Group cards by type for better organization
            var groupedCards = currentDeck.GroupBy(c => c.CardData.CardId)
                                         .OrderBy(g => g.First().CardData.CardName);
            
            // Create deck list items
            foreach (var cardGroup in groupedCards)
            {
                CreateDeckListItem(cardGroup.First(), cardGroup.Count());
            }
        }
        
        private void ClearDeckListItems()
        {
            foreach (var item in deckListItems)
            {
                if (item != null && item.gameObject != null)
                    DestroyImmediate(item.gameObject);
            }
            deckListItems.Clear();
        }
        
        private void CreateDeckListItem(Card card, int count)
        {
            if (deckCardPrefab == null || deckListContainer == null) return;
            
            GameObject itemObj = Instantiate(deckCardPrefab, deckListContainer);
            var item = itemObj.GetComponent<DeckListItem>();
            
            if (item == null)
                item = itemObj.AddComponent<DeckListItem>();
                
            item.Initialize(card, count, this);
            deckListItems.Add(item);
        }
        
        private void UpdateCountDisplays()
        {
            // Collection count
            if (collectionCountText != null)
            {
                int totalCards = showOnlyOwned ? ownedCards.Count : allCards.Count;
                collectionCountText.text = $"Collection: {filteredCards.Count}/{totalCards}";
            }
            
            // Deck count
            if (deckCountText != null)
            {
                Color textColor = IsDeckValid() ? Color.green : Color.red;
                deckCountText.text = $"Deck: {currentDeck.Count}/{maxDeckSize}";
                deckCountText.color = textColor;
            }
            
            // Deck name
            if (deckNameText != null)
            {
                deckNameText.text = currentDeckName;
            }
        }
        
        #endregion

        #region Animations
        
        private void AnimateCardAddToDeck(Card card)
        {
            // Find the collection slot for this card
            var slot = collectionSlots.FirstOrDefault(s => s.GetCard() == card);
            if (slot != null)
            {
                AnimateCardMovement(slot.transform, deckBuilderPanel, 0.3f);
            }
        }
        
        private void AnimateCardRemoveFromDeck(Card card)
        {
            // Animate card returning to collection
            if (deckListItems.Count > 0)
            {
                AnimateCardMovement(deckListItems[0].transform, collectionPanel, 0.3f);
            }
        }
        
        private void AnimateCardMovement(Transform from, Transform to, float duration)
        {
            if (from == null || to == null) return;
            
            // Create temporary visual for animation
            GameObject animCard = new GameObject("AnimCard");
            Image cardImage = animCard.AddComponent<Image>();
            cardImage.sprite = null; // Would use actual card sprite
            cardImage.color = Color.yellow; // Placeholder color
            
            RectTransform cardRect = animCard.GetComponent<RectTransform>();
            cardRect.SetParent(transform);
            cardRect.position = from.position;
            cardRect.sizeDelta = new Vector2(80, 112);
            
            // Animate movement
            cardRect.DOMove(to.position, duration)
                   .SetEase(Ease.OutQuad)
                   .OnComplete(() => {
                       if (animCard != null)
                           Destroy(animCard);
                   });
                   
            cardRect.DOScale(0.8f, duration);
        }
        
        #endregion

        #region Event Handlers
        
        private void OnSearchChanged(string query)
        {
            searchQuery = query;
            ApplyFilters();
        }
        
        private void OnTypeFilterChanged(int index)
        {
            selectedType = (CardType)index;
            ApplyFilters();
        }
        
        private void OnRarityFilterChanged(int index)
        {
            selectedRarity = (CardRarity)index;
            ApplyFilters();
        }
        
        private void OnCostFilterChanged(int index)
        {
            selectedCost = index - 1; // -1 for "All"
            ApplyFilters();
        }
        
        private void OnShowOnlyOwnedChanged(bool showOwned)
        {
            showOnlyOwned = showOwned;
            ApplyFilters();
        }
        
        public void OnCardSlotClicked(DeckCardSlot slot)
        {
            var card = slot.GetCard();
            if (card != null)
            {
                ShowCardPreview(card);
                AddCardToDeck(card);
            }
        }
        
        public void OnDeckListItemClicked(DeckListItem item)
        {
            var card = item.GetCard();
            if (card != null)
            {
                ShowCardPreview(card);
            }
        }
        
        public void OnDeckListItemRemove(DeckListItem item)
        {
            var card = item.GetCard();
            if (card != null)
            {
                RemoveCardFromDeck(card);
            }
        }
        
        private void ShowCardPreview(Card card)
        {
            if (card == null || previewPanel == null) return;
            
            previewedCard = card;
            
            // Update preview UI
            if (previewCardName != null)
                previewCardName.text = card.CardData.CardName;
                
            if (previewCardDesc != null)
                previewCardDesc.text = card.CardData.RulesText ?? "No description available";
            
            if (previewCardStats != null)
            {
                string stats = GetCardStatsText(card);
                previewCardStats.text = stats;
            }
            
            // Show preview panel
            previewPanel.gameObject.SetActive(true);
        }
        
        private string GetCardStatsText(Card card)
        {
            if (card.IsPokemonCard)
            {
                var pokemonData = card.GetPokemonData();
                if (pokemonData != null)
                {
                    return $"HP: {pokemonData.HP}\nType: {pokemonData.Type}";
                }
            }
            else if (card.IsEnergyCard)
            {
                var energyData = card.GetEnergyData();
                if (energyData != null)
                {
                    return $"Energy Type: {energyData.EnergyType}";
                }
            }
            
            return "Card Stats";
        }
        
        #endregion

        #region Deck Management
        
        private void SaveCurrentDeck()
        {
            if (string.IsNullOrEmpty(currentDeckName))
                currentDeckName = "Unnamed Deck";
            
            // In real implementation, save to persistent storage
            Debug.Log($"[DeckEditorController] Saving deck: {currentDeckName} ({currentDeck.Count} cards)");
            
            // Simulate save success
            ShowTemporaryMessage("Deck saved successfully!", Color.green);
        }
        
        private void LoadDeck()
        {
            // In real implementation, show deck selection UI
            Debug.Log("[DeckEditorController] Load deck functionality - would show deck selection UI");
            
            // For now, just clear and show message
            ShowTemporaryMessage("Load deck feature coming soon!", Color.blue);
        }
        
        private void ClearDeck()
        {
            currentDeck.Clear();
            deckCardCounts.Clear();
            
            UpdateDeckDisplay();
            UpdateCountDisplays();
            RefreshCollectionDisplay(); // Refresh to update available counts
            
            Debug.Log("[DeckEditorController] Deck cleared");
            ShowTemporaryMessage("Deck cleared", Color.yellow);
        }
        
        private void ExportDeck()
        {
            if (currentDeck.Count == 0)
            {
                ShowTemporaryMessage("No cards in deck to export", Color.red);
                return;
            }
            
            // Create JSON representation of deck
            var deckData = new DeckExportData
            {
                DeckName = currentDeckName,
                Cards = currentDeck.Select(c => c.CardData.CardId).ToList()
            };
            
            string json = JsonUtility.ToJson(deckData, true);
            Debug.Log($"[DeckEditorController] Exported deck JSON:\n{json}");
            
            ShowTemporaryMessage("Deck exported to console", Color.green);
        }
        
        private void ImportDeck()
        {
            Debug.Log("[DeckEditorController] Import deck functionality - would show file browser");
            ShowTemporaryMessage("Import deck feature coming soon!", Color.blue);
        }
        
        private void ShowTemporaryMessage(string message, Color color)
        {
            Debug.Log($"[DeckEditorController] {message}");
            // In real implementation, show UI toast message
        }
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            // Auto-initialize when the deck editor is first created
            if (!isInitialized)
            {
                Initialize();
            }
        }
        
        private void OnDestroy()
        {
            // Cleanup
            ClearCollectionSlots();
            ClearDeckListItems();
        }
        
        #endregion
    }

    #region Support Classes
    
    // Deck card slot component for collection display
    public class DeckCardSlot : MonoBehaviour
    {
        private Card card;
        private DeckEditorController deckEditor;
        private Image cardImage;
        private TextMeshProUGUI cardNameText;
        private TextMeshProUGUI cardCountText;
        private Button clickButton;
        
        public void Initialize(Card cardData, DeckEditorController editor)
        {
            card = cardData;
            deckEditor = editor;
            
            // Get UI components
            cardImage = GetComponent<Image>();
            cardNameText = GetComponentInChildren<TextMeshProUGUI>();
            clickButton = GetComponent<Button>();
            
            if (clickButton == null)
                clickButton = gameObject.AddComponent<Button>();
            
            // Set up click handler
            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(() => deckEditor.OnCardSlotClicked(this));
            
            // Update display
            UpdateDisplay();
        }
        
        public void SetCardCount(int owned, int used)
        {
            if (cardCountText != null)
            {
                cardCountText.text = $"{owned - used}/{owned}";
                cardCountText.color = (owned - used > 0) ? Color.white : Color.red;
            }
        }
        
        private void UpdateDisplay()
        {
            if (card == null) return;
            
            // Set card name
            if (cardNameText != null)
                cardNameText.text = card.CardData.CardName;
            
            // Set placeholder image (in real implementation, load actual card image)
            if (cardImage != null)
            {
                cardImage.color = GetCardTypeColor(card);
            }
        }
        
        private Color GetCardTypeColor(Card card)
        {
            if (card.IsPokemonCard) return new Color(1f, 0.8f, 0.8f); // Light red
            if (card.IsTrainerCard) return new Color(0.8f, 1f, 0.8f); // Light green  
            if (card.IsEnergyCard) return new Color(0.8f, 0.8f, 1f); // Light blue
            return Color.white;
        }
        
        public Card GetCard() => card;
    }
    
    // Deck list item component for deck display
    public class DeckListItem : MonoBehaviour
    {
        private Card card;
        private int count;
        private DeckEditorController deckEditor;
        private TextMeshProUGUI cardNameText;
        private TextMeshProUGUI cardCountText;
        private Button removeButton;
        
        public void Initialize(Card cardData, int cardCount, DeckEditorController editor)
        {
            card = cardData;
            count = cardCount;
            deckEditor = editor;
            
            // Get UI components
            cardNameText = GetComponentsInChildren<TextMeshProUGUI>()[0];
            if (GetComponentsInChildren<TextMeshProUGUI>().Length > 1)
                cardCountText = GetComponentsInChildren<TextMeshProUGUI>()[1];
                
            removeButton = GetComponentInChildren<Button>();
            
            // Set up click handlers
            if (removeButton != null)
            {
                removeButton.onClick.RemoveAllListeners();
                removeButton.onClick.AddListener(() => deckEditor.OnDeckListItemRemove(this));
            }
            
            // Update display
            UpdateDisplay();
        }
        
        private void UpdateDisplay()
        {
            if (card == null) return;
            
            if (cardNameText != null)
                cardNameText.text = card.CardData.CardName;
                
            if (cardCountText != null)
                cardCountText.text = $"x{count}";
        }
        
        public Card GetCard() => card;
        public int GetCount() => count;
    }
    
    // Enums for filtering
    public enum CardType
    {
        All,
        Pokemon,
        Trainer,
        Energy
    }
    
    public enum CardRarity
    {
        All,
        Common,
        Uncommon,
        Rare,
        UltraRare
    }
    
    // Data classes
    [System.Serializable]
    public class DeckExportData
    {
        public string DeckName;
        public List<string> Cards;
    }
    
    #endregion
}