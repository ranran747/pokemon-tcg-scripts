using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using DG.Tweening;
using PokemonTCG.Cards.Runtime;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.UI
{
    /// <summary>
    /// 手札UI制御システム
    /// プレイヤーの手札表示、カード選択、ドラッグ&ドロップ、手札管理を行う
    /// </summary>
    public class HandUIController : MonoBehaviour
    {
        [Header("Hand Layout")]
        public Transform handContainer;
        public GameObject cardSlotPrefab;
        public int maxHandSize = 7;
        public float cardSpacing = 120f;
        public float handCurveRadius = 800f;
        
        [Header("Card Display")]
        public Transform cardPreviewArea;
        public GameObject cardPreviewPrefab;
        public TextMeshProUGUI handCountText;
        public Button sortHandButton;
        public Button filterHandButton;
        
        [Header("Hand Interaction")]
        public LayerMask cardLayerMask = -1;
        public float hoverHeight = 30f;
        public float selectedHeight = 50f;
        public GameObject playableIndicator;
        public Color playableColor = Color.green;
        public Color unplayableColor = Color.gray;
        
        [Header("Sorting & Filtering")]
        public Dropdown sortModeDropdown;
        public Dropdown filterTypeDropdown;
        public Toggle showOnlyPlayableToggle;
        public InputField searchField;
        
        [Header("Animation Settings")]
        public float cardDrawSpeed = 0.8f;
        public float cardPlaySpeed = 0.6f;
        public float cardArrangeSpeed = 0.4f;
        public AnimationCurve cardMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve cardScaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 修正: EaseOutBackを削除
        
        // Private fields
        private List<HandCardSlot> handSlots = new List<HandCardSlot>();
        private List<Card> currentHand = new List<Card>();
        private HandCardSlot selectedCard;
        private HandCardSlot hoveredCard;
        private CardVisualizer previewCard;
        private bool isDragging = false;
        private string playerId;
        private bool isInitialized = false;
        
        // Sorting and filtering
        private SortMode currentSortMode = SortMode.ManaCost;
        private FilterType currentFilterType = FilterType.All;
        private bool showOnlyPlayable = false;
        private string searchQuery = "";
        
        // Events
        public System.Action<Card, Vector2> OnCardPlayed;
        public System.Action<Card> OnCardSelected;
        public System.Action<Card> OnCardHovered;
        public System.Action<Card> OnCardUnhovered;
        public System.Action<List<Card>> OnHandUpdated;
        
        #region Initialization
        public void Initialize(string playerIdentifier)
        {
            if (isInitialized) return;
            
            playerId = playerIdentifier;
            
            Debug.Log($"[HandUIController] Initializing for player: {playerId}");
            
            // Initialize components
            InitializeComponents();
            
            // Create hand slots
            CreateHandSlots();
            
            // Setup event listeners
            SetupEventListeners();
            
            // Subscribe to events
            SubscribeToEvents();
            
            isInitialized = true;
            Debug.Log($"[HandUIController] Initialized for player: {playerId}");
        }
        
        private void InitializeComponents()
        {
            // Initialize hand count display
            UpdateHandCountDisplay();
            
            // Initialize preview area
            if (cardPreviewArea != null && cardPreviewPrefab != null)
            {
                GameObject previewObj = Instantiate(cardPreviewPrefab, cardPreviewArea);
                previewCard = previewObj.GetComponent<CardVisualizer>();
                if (previewCard == null)
                    previewCard = previewObj.AddComponent<CardVisualizer>();
                
                previewObj.SetActive(false);
            }
            
            // Initialize sorting dropdown
            if (sortModeDropdown != null)
            {
                sortModeDropdown.ClearOptions();
                var sortOptions = System.Enum.GetNames(typeof(SortMode)).ToList();
                sortModeDropdown.AddOptions(sortOptions);
                sortModeDropdown.value = (int)currentSortMode;
            }
            
            // Initialize filter dropdown
            if (filterTypeDropdown != null)
            {
                filterTypeDropdown.ClearOptions();
                var filterOptions = System.Enum.GetNames(typeof(FilterType)).ToList();
                filterTypeDropdown.AddOptions(filterOptions);
                filterTypeDropdown.value = (int)currentFilterType;
            }
        }
        
        private void CreateHandSlots()
        {
            handSlots.Clear();
            
            if (handContainer == null || cardSlotPrefab == null) return;
            
            for (int i = 0; i < maxHandSize; i++)
            {
                GameObject slotObj = Instantiate(cardSlotPrefab, handContainer);
                slotObj.name = $"HandSlot_{i}";
                
                var handSlot = slotObj.GetComponent<HandCardSlot>();
                if (handSlot == null)
                    handSlot = slotObj.AddComponent<HandCardSlot>();
                
                handSlot.Initialize(i, playerId);
                handSlot.OnCardClicked += HandleCardClicked;
                handSlot.OnCardHover += HandleCardHover;
                handSlot.OnCardUnhover += HandleCardUnhover;
                handSlot.OnCardDragStart += HandleCardDragStart;
                handSlot.OnCardDrag += HandleCardDrag;
                handSlot.OnCardDragEnd += HandleCardDragEnd;
                
                handSlots.Add(handSlot);
                
                // Initially hide slot
                slotObj.SetActive(false);
            }
            
            ArrangeHandSlots();
        }
        
        private void SetupEventListeners()
        {
            // Sort button
            if (sortHandButton != null)
                sortHandButton.onClick.AddListener(SortHand);
            
            // Filter button
            if (filterHandButton != null)
                filterHandButton.onClick.AddListener(ApplyFilters);
            
            // Sort mode dropdown
            if (sortModeDropdown != null)
                sortModeDropdown.onValueChanged.AddListener(OnSortModeChanged);
            
            // Filter type dropdown
            if (filterTypeDropdown != null)
                filterTypeDropdown.onValueChanged.AddListener(OnFilterTypeChanged);
            
            // Playable toggle
            if (showOnlyPlayableToggle != null)
                showOnlyPlayableToggle.onValueChanged.AddListener(OnShowPlayableChanged);
            
            // Search field
            if (searchField != null)
                searchField.onValueChanged.AddListener(OnSearchChanged);
        }
        
        private void SubscribeToEvents()
        {
            // Subscribe to EventBus events
            EventBus.On<CardDrawnEvent>(OnCardDrawn);
            EventBus.On<CardPlayedFromHandEvent>(OnCardPlayedFromHand);
            EventBus.On<HandUpdatedEvent>(OnHandUpdated_Event);
        }
        #endregion
        
        #region Public Interface
        public void UpdateHand(List<Card> newHand)
        {
            if (!isInitialized) return;
            
            currentHand = new List<Card>(newHand);
            
            // Apply current filters and sorting
            var displayHand = ApplyFiltersAndSorting(currentHand);
            
            // Update hand slots
            UpdateHandSlots(displayHand);
            
            // Update display
            UpdateHandCountDisplay();
            ArrangeHandSlots();
            
            OnHandUpdated?.Invoke(currentHand);
        }
        
        public void DrawCard(Card card)
        {
            if (!isInitialized) return;
            
            currentHand.Add(card);
            
            // Animate card draw
            AnimateCardDraw(card);
            
            // Update display
            UpdateHand(currentHand);
        }
        
        public void PlayCard(Card card)
        {
            if (!isInitialized) return;
            
            currentHand.Remove(card);
            
            // Animate card play
            AnimateCardPlay(card);
            
            // Update display
            UpdateHand(currentHand);
        }
        
        public void SetCardPlayability(Card card, bool isPlayable)
        {
            var slot = handSlots.FirstOrDefault(s => s.GetCard() == card);
            if (slot != null)
            {
                slot.SetPlayable(isPlayable);
            }
        }
        
        public void HighlightPlayableCards()
        {
            foreach (var slot in handSlots)
            {
                var card = slot.GetCard();
                if (card != null)
                {
                    // Check if card is playable (simplified logic)
                    bool isPlayable = IsCardPlayable(card);
                    slot.SetPlayable(isPlayable);
                    slot.SetHighlight(isPlayable, playableColor);
                }
            }
        }
        
        public void ClearHighlights()
        {
            foreach (var slot in handSlots)
            {
                slot.SetHighlight(false, Color.white);
            }
        }
        
        public void SelectCard(Card card)
        {
            // Deselect previous card
            if (selectedCard != null)
                selectedCard.SetSelected(false);
            
            // Select new card
            selectedCard = handSlots.FirstOrDefault(s => s.GetCard() == card);
            if (selectedCard != null)
            {
                selectedCard.SetSelected(true);
                ShowCardPreview(card);
            }
            
            OnCardSelected?.Invoke(card);
        }
        
        public void DeselectCard()
        {
            if (selectedCard != null)
            {
                selectedCard.SetSelected(false);
                selectedCard = null;
                HideCardPreview();
            }
        }
        
        public Card GetSelectedCard()
        {
            return selectedCard?.GetCard();
        }
        
        public List<Card> GetCurrentHand()
        {
            return new List<Card>(currentHand);
        }
        
        public int GetHandCount()
        {
            return currentHand.Count;
        }
        #endregion
        
        #region Private Methods
        private void UpdateHandSlots(List<Card> displayHand)
        {
            // Show/hide slots based on hand size
            for (int i = 0; i < handSlots.Count; i++)
            {
                if (i < displayHand.Count)
                {
                    handSlots[i].gameObject.SetActive(true);
                    handSlots[i].SetCard(displayHand[i]);
                }
                else
                {
                    handSlots[i].gameObject.SetActive(false);
                    handSlots[i].SetCard(null);
                }
            }
        }
        
        private void ArrangeHandSlots()
        {
            int activeSlots = handSlots.Count(s => s.gameObject.activeSelf);
            if (activeSlots == 0) return;
            
            // Calculate positions in arc
            float totalWidth = (activeSlots - 1) * cardSpacing;
            float startX = -totalWidth / 2f;
            
            int activeIndex = 0;
            for (int i = 0; i < handSlots.Count; i++)
            {
                if (!handSlots[i].gameObject.activeSelf) continue;
                
                float normalizedPos = activeSlots > 1 ? (float)activeIndex / (activeSlots - 1) : 0.5f;
                float angle = (normalizedPos - 0.5f) * 30f; // Max 15 degrees each side
                
                Vector3 position = new Vector3(startX + activeIndex * cardSpacing, 0, 0);
                
                // Apply curve
                if (handCurveRadius > 0)
                {
                    float curveHeight = Mathf.Sin(normalizedPos * Mathf.PI) * 20f;
                    position.y += curveHeight;
                }
                
                // Animate to position
                handSlots[i].transform.DOLocalMove(position, cardArrangeSpeed);
                handSlots[i].transform.DOLocalRotate(new Vector3(0, 0, angle), cardArrangeSpeed);
                
                activeIndex++;
            }
        }
        
        private List<Card> ApplyFiltersAndSorting(List<Card> originalHand)
        {
            var filteredHand = new List<Card>(originalHand);
            
            // Apply search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                filteredHand = filteredHand.Where(card => 
                    card.CardData.CardName.ToLower().Contains(searchQuery.ToLower())).ToList();
            }
            
            // Apply type filter
            if (currentFilterType != FilterType.All)
            {
                filteredHand = filteredHand.Where(card => 
                    GetFilterTypeForCard(card) == currentFilterType).ToList();
            }
            
            // Apply playable filter
            if (showOnlyPlayable)
            {
                filteredHand = filteredHand.Where(card => IsCardPlayable(card)).ToList();
            }
            
            // Apply sorting
            filteredHand = SortCards(filteredHand, currentSortMode);
            
            return filteredHand;
        }
        
        private List<Card> SortCards(List<Card> cards, SortMode sortMode)
        {
            switch (sortMode)
            {
                case SortMode.Name:
                    return cards.OrderBy(c => c.CardData.CardName).ToList();
                case SortMode.Type:
                    return cards.OrderBy(c => GetCardTypePriority(c)).ThenBy(c => c.CardData.CardName).ToList();
                case SortMode.ManaCost:
                    return cards.OrderBy(c => GetCardCost(c)).ThenBy(c => c.CardData.CardName).ToList();
                case SortMode.Rarity:
                    return cards.OrderBy(c => GetCardRarity(c)).ThenBy(c => c.CardData.CardName).ToList();
                default:
                    return cards;
            }
        }
        
        private bool IsCardPlayable(Card card)
        {
            // Simplified playability check
            // In real implementation, this would check energy requirements, field state, etc.
            return true; // Placeholder
        }
        
        private FilterType GetFilterTypeForCard(Card card)
        {
            if (card.IsPokemonCard) return FilterType.Pokemon;
            if (card.IsTrainerCard) return FilterType.Trainer;
            if (card.IsEnergyCard) return FilterType.Energy;
            return FilterType.All;
        }
        
        private int GetCardTypePriority(Card card)
        {
            if (card.IsPokemonCard) return 1;
            if (card.IsTrainerCard) return 2;
            if (card.IsEnergyCard) return 3;
            return 4;
        }
        
        private int GetCardCost(Card card)
        {
            // Simplified cost calculation
            if (card.IsPokemonCard)
            {
                var pokemonData = card.GetPokemonData();
                return pokemonData?.Attacks?.FirstOrDefault()?.EnergyCost?.Count ?? 0;
            }
            return 0;
        }
        
        private int GetCardRarity(Card card)
        {
            // Placeholder rarity system
            return UnityEngine.Random.Range(1, 5);
        }
        
        private void UpdateHandCountDisplay()
        {
            if (handCountText != null)
            {
                handCountText.text = $"Hand: {currentHand.Count}/{maxHandSize}";
            }
        }
        
        private void ShowCardPreview(Card card)
        {
            if (previewCard != null)
            {
                previewCard.SetCardData(card);
                previewCard.gameObject.SetActive(true);
                
                // Animate preview appearance - 修正: DOTweenのEase使用
                previewCard.transform.localScale = Vector3.zero;
                previewCard.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
        }
        
        private void HideCardPreview()
        {
            if (previewCard != null)
            {
                previewCard.transform.DOScale(Vector3.zero, 0.2f)
                    .OnComplete(() => previewCard.gameObject.SetActive(false));
            }
        }
        
        private void AnimateCardDraw(Card card)
        {
            // Find empty slot or add new slot
            var emptySlot = handSlots.FirstOrDefault(s => !s.gameObject.activeSelf);
            if (emptySlot != null)
            {
                emptySlot.gameObject.SetActive(true);
                emptySlot.SetCard(card);
                
                // Animate from deck position
                emptySlot.transform.localPosition = new Vector3(0, -200f, 0);
                emptySlot.transform.localScale = Vector3.zero;
                
                emptySlot.transform.DOLocalMove(Vector3.zero, cardDrawSpeed).SetEase(cardMoveCurve);
                emptySlot.transform.DOScale(Vector3.one, cardDrawSpeed).SetEase(Ease.OutBack); // 修正: DOTweenのEase使用
            }
        }
        
        private void AnimateCardPlay(Card card)
        {
            var slot = handSlots.FirstOrDefault(s => s.GetCard() == card);
            if (slot != null)
            {
                // Animate card leaving hand
                slot.transform.DOLocalMove(new Vector3(0, 200f, 0), cardPlaySpeed);
                slot.transform.DOScale(Vector3.zero, cardPlaySpeed)
                    .OnComplete(() => {
                        slot.gameObject.SetActive(false);
                        slot.SetCard(null);
                    });
            }
        }
        
        #region Event Handlers
        private void HandleCardClicked(int slotIndex, Card card)
        {
            if (card != null)
            {
                SelectCard(card);
            }
        }
        
        private void HandleCardHover(int slotIndex, Card card)
        {
            if (card != null)
            {
                hoveredCard = handSlots[slotIndex];
                hoveredCard.SetHovered(true);
                OnCardHovered?.Invoke(card);
            }
        }
        
        private void HandleCardUnhover(int slotIndex, Card card)
        {
            if (hoveredCard != null)
            {
                hoveredCard.SetHovered(false);
                hoveredCard = null;
                if (card != null)
                    OnCardUnhovered?.Invoke(card);
            }
        }
        
        private void HandleCardDragStart(int slotIndex, Card card, Vector2 position)
        {
            if (card != null)
            {
                isDragging = true;
                SelectCard(card);
            }
        }
        
        private void HandleCardDrag(int slotIndex, Card card, Vector2 position)
        {
            // Handle drag logic here
            // Could show drop zones, update cursor, etc.
        }
        
        private void HandleCardDragEnd(int slotIndex, Card card, Vector2 position)
        {
            if (card != null && isDragging)
            {
                isDragging = false;
                
                // Check if dropped in valid play area
                if (IsValidDropZone(position))
                {
                    OnCardPlayed?.Invoke(card, position);
                    PlayCard(card);
                }
            }
        }
        
        private bool IsValidDropZone(Vector2 position)
        {
            // Check if position is in valid play area
            // Simplified check
            return position.y > 0; // Above hand area
        }
        
        private void OnSortModeChanged(int value)
        {
            currentSortMode = (SortMode)value;
            SortHand();
        }
        
        private void OnFilterTypeChanged(int value)
        {
            currentFilterType = (FilterType)value;
            ApplyFilters();
        }
        
        private void OnShowPlayableChanged(bool value)
        {
            showOnlyPlayable = value;
            ApplyFilters();
        }
        
        private void OnSearchChanged(string query)
        {
            searchQuery = query;
            ApplyFilters();
        }
        
        private void SortHand()
        {
            UpdateHand(currentHand);
        }
        
        private void ApplyFilters()
        {
            UpdateHand(currentHand);
        }
        
        private void OnCardDrawn(CardDrawnEvent evt)
        {
            if (evt.PlayerId == playerId)
            {
                DrawCard(evt.Card);
            }
        }
        
        private void OnCardPlayedFromHand(CardPlayedFromHandEvent evt)
        {
            if (evt.PlayerId == playerId)
            {
                PlayCard(evt.Card);
            }
        }
        
        private void OnHandUpdated_Event(HandUpdatedEvent evt)
        {
            if (evt.PlayerId == playerId)
            {
                UpdateHand(evt.NewHand);
            }
        }
        #endregion
        #endregion
        
        #region Cleanup
        public void Cleanup()
        {
            // Unsubscribe from events
            EventBus.Off<CardDrawnEvent>(OnCardDrawn);
            EventBus.Off<CardPlayedFromHandEvent>(OnCardPlayedFromHand);
            EventBus.Off<HandUpdatedEvent>(OnHandUpdated_Event);
            
            // Clean up events
            OnCardPlayed = null;
            OnCardSelected = null;
            OnCardHovered = null;
            OnCardUnhovered = null;
            OnHandUpdated = null;
            
            isInitialized = false;
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
        #endregion
    }
    
    // Hand card slot component
    public class HandCardSlot : MonoBehaviour
    {
        public int SlotIndex { get; private set; }
        public string PlayerId { get; private set; }
        
        private Card currentCard;
        private CardVisualizer cardVisualizer;
        private bool isSelected = false;
        private bool isHovered = false;
        private bool isPlayable = true;
        
        public System.Action<int, Card> OnCardClicked;
        public System.Action<int, Card> OnCardHover;
        public System.Action<int, Card> OnCardUnhover;
        public System.Action<int, Card, Vector2> OnCardDragStart;
        public System.Action<int, Card, Vector2> OnCardDrag;
        public System.Action<int, Card, Vector2> OnCardDragEnd;
        
        public void Initialize(int slotIndex, string playerId)
        {
            SlotIndex = slotIndex;
            PlayerId = playerId;
            
            cardVisualizer = GetComponentInChildren<CardVisualizer>();
            if (cardVisualizer == null)
            {
                // Create card visualizer if not present
                GameObject cardObj = new GameObject("CardVisualizer");
                cardObj.transform.SetParent(transform);
                cardVisualizer = cardObj.AddComponent<CardVisualizer>();
            }
            
            // Setup card visualizer events
            if (cardVisualizer != null)
            {
                cardVisualizer.OnCardClicked += HandleCardClicked;
                cardVisualizer.OnCardHovered += HandleCardHovered;
                cardVisualizer.OnCardUnhovered += HandleCardUnhovered;
            }
        }
        
        public void SetCard(Card card)
        {
            currentCard = card;
            
            if (cardVisualizer != null)
            {
                if (card != null)
                {
                    cardVisualizer.SetCardData(card);
                    cardVisualizer.gameObject.SetActive(true);
                }
                else
                {
                    cardVisualizer.gameObject.SetActive(false);
                }
            }
        }
        
        public Card GetCard() => currentCard;
        
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisualState();
        }
        
        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            UpdateVisualState();
        }
        
        public void SetPlayable(bool playable)
        {
            isPlayable = playable;
            UpdateVisualState();
        }
        
        public void SetHighlight(bool highlight, Color color)
        {
            if (cardVisualizer != null)
            {
                // Apply highlight to card visualizer
                var image = cardVisualizer.GetComponent<Image>();
                if (image != null)
                {
                    image.color = highlight ? color : Color.white;
                }
            }
        }
        
        private void UpdateVisualState()
        {
            if (cardVisualizer == null) return;
            
            // Update card position based on state
            Vector3 targetPosition = Vector3.zero;
            Vector3 targetScale = Vector3.one;
            
            if (isSelected)
            {
                targetPosition.y += 50f;
                targetScale *= 1.1f;
            }
            else if (isHovered)
            {
                targetPosition.y += 30f;
                targetScale *= 1.05f;
            }
            
            // Apply playability state
            Color targetColor = isPlayable ? Color.white : Color.gray;
            
            // Animate changes
            cardVisualizer.transform.DOLocalMove(targetPosition, 0.2f);
            cardVisualizer.transform.DOScale(targetScale, 0.2f);
            
            var image = cardVisualizer.GetComponent<Image>();
            if (image != null)
            {
                image.DOColor(targetColor, 0.2f);
            }
        }
        
        private void HandleCardClicked(CardVisualizer visualizer)
        {
            OnCardClicked?.Invoke(SlotIndex, currentCard);
        }
        
        private void HandleCardHovered(CardVisualizer visualizer)
        {
            OnCardHover?.Invoke(SlotIndex, currentCard);
        }
        
        private void HandleCardUnhovered(CardVisualizer visualizer)
        {
            OnCardUnhover?.Invoke(SlotIndex, currentCard);
        }
    }
    
    // Enums
    public enum SortMode
    {
        Name,
        Type,
        ManaCost,
        Rarity
    }
    
    public enum FilterType
    {
        All,
        Pokemon,
        Trainer,
        Energy
    }
    
    // Events
    public class CardDrawnEvent
    {
        public string PlayerId { get; set; }
        public Card Card { get; set; }
    }
    
    public class CardPlayedFromHandEvent
    {
        public string PlayerId { get; set; }
        public Card Card { get; set; }
        public Vector2 Position { get; set; }
    }
    
    public class HandUpdatedEvent
    {
        public string PlayerId { get; set; }
        public List<Card> NewHand { get; set; }
    }
}