using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using DG.Tweening;
using PokemonTCG.Cards.Runtime;
using PokemonTCG.Game.Rules;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.UI
{
    /// <summary>
    /// フィールドUI詳細制御システム
    /// バトル場、ベンチ、アクティブポケモン、エネルギー付与状況を管理
    /// </summary>
    public class FieldUIController : MonoBehaviour
    {
        [Header("Battle Field Layout")]
        public Transform player1BattleField;
        public Transform player2BattleField;
        public Transform player1Bench;
        public Transform player2Bench;
        public Transform centerField;
        
        [Header("Active Pokemon Slots")]
        public Transform player1ActiveSlot;
        public Transform player2ActiveSlot;
        public GameObject pokemonSlotPrefab;
        public float slotSpacing = 150f;
        
        [Header("Battle Indicators")]
        public GameObject battleArrow;
        public TextMeshProUGUI battleStatusText;
        public Image battlePhaseIndicator;
        public ParticleSystem battleEffects;
        
        [Header("Field Cards")]
        public Transform stadiumCardSlot;
        public GameObject stadiumCardFrame;
        public TextMeshProUGUI stadiumNameText;
        public TextMeshProUGUI stadiumEffectText;
        
        [Header("Turn Indicators")]
        public GameObject player1TurnIndicator;
        public GameObject player2TurnIndicator;
        public Image turnTimerFill;
        public TextMeshProUGUI turnTimeText;
        
        [Header("Damage Counters")]
        public GameObject damageCounterPrefab;
        public Transform damageCountersPool;
        public int damageCounterPoolSize = 50;
        
        [Header("Animation Settings")]
        public float cardMoveSpeed = 1f;
        public float slotHighlightDuration = 0.5f;
        public AnimationCurve cardMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public Color availableSlotColor = Color.green;
        public Color occupiedSlotColor = Color.yellow;
        public Color unavailableSlotColor = Color.red;
        public Color activePlayerColor = Color.green; // 修正: フィールド追加
        
        // Private fields
        private Dictionary<string, PokemonSlot> pokemonSlots = new Dictionary<string, PokemonSlot>();
        private List<GameObject> damageCountersPool_objects = new List<GameObject>();
        private Dictionary<string, List<GameObject>> assignedDamageCounters = new Dictionary<string, List<GameObject>>();
        private Card currentStadiumCard;
        private bool isInitialized = false;
        private string currentActivePlayer;
        private GamePhase currentPhase;
        
        // Events
        public System.Action<string, Vector2> OnSlotClicked;
        public System.Action<string, Card> OnPokemonPlayed;
        public System.Action<string, Card> OnPokemonRetreated;
        public System.Action<Card> OnStadiumPlayed;
        
        #region Initialization
        public void Initialize()
        {
            if (isInitialized) return;
            
            Debug.Log("[FieldUIController] Initializing Field UI Controller...");
            
            // Initialize components
            InitializeComponents();
            
            // Create pokemon slots
            CreatePokemonSlots();
            
            // Initialize damage counter pool
            InitializeDamageCounterPool();
            
            // Subscribe to events
            SubscribeToEvents();
            
            isInitialized = true;
            Debug.Log("[FieldUIController] Field UI Controller initialized successfully");
        }
        
        private void InitializeComponents()
        {
            // Initialize battle indicators
            if (battleArrow != null)
                battleArrow.SetActive(false);
            
            if (battleStatusText != null)
                battleStatusText.text = "Waiting for game to start...";
            
            // Initialize turn indicators
            if (player1TurnIndicator != null)
                player1TurnIndicator.SetActive(false);
            
            if (player2TurnIndicator != null)
                player2TurnIndicator.SetActive(false);
            
            // Initialize stadium card slot
            if (stadiumCardFrame != null)
                stadiumCardFrame.SetActive(false);
        }
        
        private void CreatePokemonSlots()
        {
            // Create Player 1 Active Slot
            CreateSlot("Player1_Active", player1ActiveSlot, PokemonSlotType.Active, "Player1");
            
            // Create Player 2 Active Slot
            CreateSlot("Player2_Active", player2ActiveSlot, PokemonSlotType.Active, "Player2");
            
            // Create Player 1 Bench Slots (5 slots)
            for (int i = 0; i < 5; i++)
            {
                Vector3 benchPos = new Vector3(i * slotSpacing - 2 * slotSpacing, 0, 0);
                CreateSlot($"Player1_Bench_{i}", player1Bench, PokemonSlotType.Bench, "Player1", benchPos);
            }
            
            // Create Player 2 Bench Slots (5 slots)
            for (int i = 0; i < 5; i++)
            {
                Vector3 benchPos = new Vector3(i * slotSpacing - 2 * slotSpacing, 0, 0);
                CreateSlot($"Player2_Bench_{i}", player2Bench, PokemonSlotType.Bench, "Player2", benchPos);
            }
        }
        
        private void CreateSlot(string slotId, Transform parent, PokemonSlotType slotType, string playerId, Vector3? localPosition = null)
        {
            if (pokemonSlotPrefab == null || parent == null) return;
            
            GameObject slotObj = Instantiate(pokemonSlotPrefab, parent);
            slotObj.name = slotId;
            
            if (localPosition.HasValue)
                slotObj.transform.localPosition = localPosition.Value;
            
            var pokemonSlot = slotObj.GetComponent<PokemonSlot>();
            if (pokemonSlot == null)
                pokemonSlot = slotObj.AddComponent<PokemonSlot>();
            
            pokemonSlot.Initialize(slotId, slotType, playerId);
            pokemonSlot.OnSlotClicked += HandleSlotClicked;
            
            pokemonSlots[slotId] = pokemonSlot;
        }
        
        private void InitializeDamageCounterPool()
        {
            if (damageCounterPrefab == null || damageCountersPool == null) return;
            
            for (int i = 0; i < damageCounterPoolSize; i++)
            {
                GameObject counter = Instantiate(damageCounterPrefab, damageCountersPool);
                counter.SetActive(false);
                damageCountersPool_objects.Add(counter);
            }
        }
        
        private void SubscribeToEvents()
        {
            // Subscribe to EventBus events
            EventBus.On<GamePhaseChangedEvent>(OnGamePhaseChanged);
            EventBus.On<PlayerTurnChangedEvent>(OnPlayerTurnChanged);
            EventBus.On<PokemonPlayedEvent>(OnPokemonPlayed_Event);
            EventBus.On<PokemonDamagedEvent>(OnPokemonDamaged);
            EventBus.On<StadiumPlayedEvent>(OnStadiumPlayed_Event);
        }
        #endregion
        
        #region Public Interface
        public void UpdateFieldState(Field player1Field, Field player2Field)
        {
            if (!isInitialized) return;
            
            // Update Player 1 field
            UpdatePlayerField("Player1", player1Field);
            
            // Update Player 2 field
            UpdatePlayerField("Player2", player2Field);
            
            // Update battle indicators
            UpdateBattleIndicators();
        }
        
        public void SetActivePlayer(string playerId)
        {
            currentActivePlayer = playerId;
            
            // Update turn indicators
            if (player1TurnIndicator != null)
                player1TurnIndicator.SetActive(playerId == "Player1");
            
            if (player2TurnIndicator != null)
                player2TurnIndicator.SetActive(playerId == "Player2");
            
            // Highlight active player's field
            HighlightPlayerField(playerId, true);
        }
        
        public void SetGamePhase(GamePhase phase)
        {
            currentPhase = phase;
            
            if (battlePhaseIndicator != null)
            {
                battlePhaseIndicator.color = GetPhaseColor(phase);
            }
            
            if (battleStatusText != null)
            {
                battleStatusText.text = GetPhaseText(phase);
            }
        }
        
        public void PlayPokemonToSlot(string slotId, Card pokemonCard)
        {
            if (pokemonSlots.ContainsKey(slotId))
            {
                var slot = pokemonSlots[slotId];
                slot.PlacePokemon(pokemonCard);
                
                // Animate card placement
                AnimateCardPlacement(slot.transform, pokemonCard);
                
                OnPokemonPlayed?.Invoke(slotId, pokemonCard);
            }
        }
        
        public void RemovePokemonFromSlot(string slotId)
        {
            if (pokemonSlots.ContainsKey(slotId))
            {
                var slot = pokemonSlots[slotId];
                var pokemon = slot.GetPokemon();
                
                // Animate card removal
                if (pokemon != null)
                {
                    AnimateCardRemoval(slot.transform, () => {
                        slot.RemovePokemon();
                        OnPokemonRetreated?.Invoke(slotId, pokemon);
                    });
                }
            }
        }
        
        public void ApplyDamage(string slotId, int damage)
        {
            if (pokemonSlots.ContainsKey(slotId))
            {
                var slot = pokemonSlots[slotId];
                slot.ApplyDamage(damage);
                
                // Add damage counters visually
                AddDamageCounters(slot, damage);
                
                // Animate damage effect
                AnimateDamageEffect(slot.transform);
            }
        }
        
        public void HealPokemon(string slotId, int healAmount)
        {
            if (pokemonSlots.ContainsKey(slotId))
            {
                var slot = pokemonSlots[slotId];
                slot.HealDamage(healAmount);
                
                // Remove damage counters visually
                RemoveDamageCounters(slot, healAmount);
                
                // Animate heal effect
                AnimateHealEffect(slot.transform);
            }
        }
        
        public void PlayStadiumCard(Card stadiumCard)
        {
            currentStadiumCard = stadiumCard;
            
            if (stadiumCardFrame != null)
            {
                stadiumCardFrame.SetActive(true);
                
                // Update stadium card display
                if (stadiumNameText != null)
                    stadiumNameText.text = stadiumCard.CardData.CardName;
                
                if (stadiumEffectText != null)
                    stadiumEffectText.text = stadiumCard.CardData.RulesText;
                
                // Animate stadium card appearance
                stadiumCardFrame.transform.localScale = Vector3.zero;
                stadiumCardFrame.transform.DOScale(Vector3.one, cardMoveSpeed)
                    .SetEase(Ease.OutBack);
            }
            
            OnStadiumPlayed?.Invoke(stadiumCard);
        }
        
        public void RemoveStadiumCard()
        {
            if (stadiumCardFrame != null && currentStadiumCard != null)
            {
                // Animate stadium card removal
                stadiumCardFrame.transform.DOScale(Vector3.zero, cardMoveSpeed * 0.5f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => {
                        stadiumCardFrame.SetActive(false);
                        currentStadiumCard = null;
                    });
            }
        }
        
        public void HighlightAvailableSlots(List<string> availableSlotIds)
        {
            foreach (var kvp in pokemonSlots)
            {
                var slot = kvp.Value;
                bool isAvailable = availableSlotIds.Contains(kvp.Key);
                
                slot.SetHighlight(isAvailable, isAvailable ? availableSlotColor : Color.white);
            }
        }
        
        public void ClearSlotHighlights()
        {
            foreach (var slot in pokemonSlots.Values)
            {
                slot.SetHighlight(false, Color.white);
            }
        }
        
        public void StartBattle(string attacker, string defender)
        {
            if (battleArrow != null)
            {
                battleArrow.SetActive(true);
                
                // Position arrow between attacker and defender
                var attackerSlot = pokemonSlots.Values.FirstOrDefault(s => s.SlotId == attacker);
                var defenderSlot = pokemonSlots.Values.FirstOrDefault(s => s.SlotId == defender);
                
                if (attackerSlot != null && defenderSlot != null)
                {
                    Vector3 midPoint = (attackerSlot.transform.position + defenderSlot.transform.position) / 2;
                    battleArrow.transform.position = midPoint;
                    
                    // Animate arrow
                    battleArrow.transform.localScale = Vector3.zero;
                    battleArrow.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBounce);
                }
            }
            
            if (battleEffects != null)
                battleEffects.Play();
        }
        
        public void EndBattle()
        {
            if (battleArrow != null)
            {
                battleArrow.transform.DOScale(Vector3.zero, 0.2f)
                    .OnComplete(() => battleArrow.SetActive(false));
            }
            
            if (battleEffects != null)
                battleEffects.Stop();
        }
        #endregion
        
        #region Private Methods
        private void UpdatePlayerField(string playerId, Field playerField)
        {
            // Update active Pokemon
            string activeSlotId = $"{playerId}_Active";
            if (pokemonSlots.ContainsKey(activeSlotId))
            {
                // var activePokemon = playerField?.ActivePokemon;
                // pokemonSlots[activeSlotId].UpdatePokemon(activePokemon);
                
                // Placeholder: Random update for demonstration
                if (UnityEngine.Random.value > 0.8f)
                {
                    pokemonSlots[activeSlotId].UpdateHealthDisplay(UnityEngine.Random.Range(10, 120));
                }
            }
            
            // Update bench Pokemon
            for (int i = 0; i < 5; i++)
            {
                string benchSlotId = $"{playerId}_Bench_{i}";
                if (pokemonSlots.ContainsKey(benchSlotId))
                {
                    // var benchPokemon = playerField?.BenchPokemon?[i];
                    // pokemonSlots[benchSlotId].UpdatePokemon(benchPokemon);
                    
                    // Placeholder: Random update for demonstration
                    if (UnityEngine.Random.value > 0.9f)
                    {
                        pokemonSlots[benchSlotId].UpdateHealthDisplay(UnityEngine.Random.Range(30, 100));
                    }
                }
            }
        }
        
        private void UpdateBattleIndicators()
        {
            if (battleStatusText != null)
            {
                string statusText = $"Phase: {currentPhase}";
                if (!string.IsNullOrEmpty(currentActivePlayer))
                    statusText += $"\nActive Player: {currentActivePlayer}";
                
                battleStatusText.text = statusText;
            }
        }
        
        private void HighlightPlayerField(string playerId, bool highlight)
        {
            Color highlightColor = highlight ? activePlayerColor : Color.white;
            
            // Highlight active slot
            string activeSlotId = $"{playerId}_Active";
            if (pokemonSlots.ContainsKey(activeSlotId))
            {
                pokemonSlots[activeSlotId].SetFieldHighlight(highlight, highlightColor);
            }
            
            // Highlight bench slots
            for (int i = 0; i < 5; i++)
            {
                string benchSlotId = $"{playerId}_Bench_{i}";
                if (pokemonSlots.ContainsKey(benchSlotId))
                {
                    pokemonSlots[benchSlotId].SetFieldHighlight(highlight, highlightColor);
                }
            }
        }
        
        private void AnimateCardPlacement(Transform slot, Card card)
        {
            // Animate card dropping into slot
            var cardVisualizer = slot.GetComponentInChildren<CardVisualizer>();
            if (cardVisualizer != null)
            {
                cardVisualizer.transform.localPosition = Vector3.up * 200f;
                cardVisualizer.transform.DOLocalMove(Vector3.zero, cardMoveSpeed)
                    .SetEase(cardMoveCurve);
            }
        }
        
        private void AnimateCardRemoval(Transform slot, System.Action onComplete)
        {
            var cardVisualizer = slot.GetComponentInChildren<CardVisualizer>();
            if (cardVisualizer != null)
            {
                cardVisualizer.transform.DOLocalMove(Vector3.up * 200f, cardMoveSpeed * 0.5f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() => onComplete?.Invoke());
            }
            else
            {
                onComplete?.Invoke();
            }
        }
        
        private void AnimateDamageEffect(Transform target)
        {
            // Screen shake effect
            target.DOPunchPosition(Vector3.one * 10f, 0.3f, 5, 0.5f);
            
            // Red flash
            var image = target.GetComponent<Image>();
            if (image != null)
            {
                Color originalColor = image.color;
                image.DOColor(Color.red, 0.1f)
                    .OnComplete(() => image.DOColor(originalColor, 0.2f));
            }
        }
        
        private void AnimateHealEffect(Transform target)
        {
            // Green glow effect
            var image = target.GetComponent<Image>();
            if (image != null)
            {
                Color originalColor = image.color;
                image.DOColor(Color.green, 0.2f)
                    .OnComplete(() => image.DOColor(originalColor, 0.3f));
            }
        }
        
        private void AddDamageCounters(PokemonSlot slot, int damage)
        {
            string slotId = slot.SlotId;
            int countersNeeded = damage / 10; // 10 damage per counter
            
            if (!assignedDamageCounters.ContainsKey(slotId))
                assignedDamageCounters[slotId] = new List<GameObject>();
            
            for (int i = 0; i < countersNeeded; i++)
            {
                var counter = GetDamageCounterFromPool();
                if (counter != null)
                {
                    counter.transform.SetParent(slot.transform);
                    counter.transform.localPosition = Vector3.zero + Vector3.right * (assignedDamageCounters[slotId].Count * 20f);
                    counter.SetActive(true);
                    
                    assignedDamageCounters[slotId].Add(counter);
                    
                    // Animate counter appearance
                    counter.transform.localScale = Vector3.zero;
                    counter.transform.DOScale(Vector3.one, 0.2f).SetDelay(i * 0.05f);
                }
            }
        }
        
        private void RemoveDamageCounters(PokemonSlot slot, int healAmount)
        {
            string slotId = slot.SlotId;
            int countersToRemove = healAmount / 10;
            
            if (assignedDamageCounters.ContainsKey(slotId))
            {
                var counters = assignedDamageCounters[slotId];
                int actualRemoval = Mathf.Min(countersToRemove, counters.Count);
                
                for (int i = 0; i < actualRemoval; i++)
                {
                    var counter = counters[counters.Count - 1 - i];
                    counter.transform.DOScale(Vector3.zero, 0.2f)
                        .OnComplete(() => ReturnDamageCounterToPool(counter));
                }
                
                counters.RemoveRange(counters.Count - actualRemoval, actualRemoval);
            }
        }
        
        private GameObject GetDamageCounterFromPool()
        {
            return damageCountersPool_objects.FirstOrDefault(c => !c.activeInHierarchy);
        }
        
        private void ReturnDamageCounterToPool(GameObject counter)
        {
            counter.SetActive(false);
            counter.transform.SetParent(damageCountersPool);
        }
        
        private Color GetPhaseColor(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Setup: return Color.blue;
                case GamePhase.DrawPhase: return Color.cyan;
                case GamePhase.MainPhase: return Color.green;
                case GamePhase.AttackPhase: return Color.red;
                case GamePhase.GameEnd: return Color.gray;
                default: return Color.white;
            }
        }
        
        private string GetPhaseText(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Setup: return "Game Setup";
                case GamePhase.DrawPhase: return "Draw Phase";
                case GamePhase.MainPhase: return "Main Phase";
                case GamePhase.AttackPhase: return "Attack Phase";
                case GamePhase.GameEnd: return "Game Over";
                default: return "Unknown Phase";
            }
        }
        #endregion
        
        #region Event Handlers
        private void HandleSlotClicked(string slotId, Vector2 position)
        {
            OnSlotClicked?.Invoke(slotId, position);
        }
        
        private void OnGamePhaseChanged(GamePhaseChangedEvent evt)
        {
            SetGamePhase(evt.NewPhase);
        }
        
        private void OnPlayerTurnChanged(PlayerTurnChangedEvent evt)
        {
            SetActivePlayer(evt.CurrentPlayerId);
        }
        
        private void OnPokemonPlayed_Event(PokemonPlayedEvent evt)
        {
            PlayPokemonToSlot(evt.SlotId, evt.Pokemon);
        }
        
        private void OnPokemonDamaged(PokemonDamagedEvent evt)
        {
            ApplyDamage(evt.SlotId, evt.Damage);
        }
        
        private void OnStadiumPlayed_Event(StadiumPlayedEvent evt)
        {
            PlayStadiumCard(evt.StadiumCard);
        }
        #endregion
        
        #region Cleanup
        public void Cleanup()
        {
            // Unsubscribe from events
            EventBus.Off<GamePhaseChangedEvent>(OnGamePhaseChanged);
            EventBus.Off<PlayerTurnChangedEvent>(OnPlayerTurnChanged);
            EventBus.Off<PokemonPlayedEvent>(OnPokemonPlayed_Event);
            EventBus.Off<PokemonDamagedEvent>(OnPokemonDamaged);
            EventBus.Off<StadiumPlayedEvent>(OnStadiumPlayed_Event);
            
            // Clean up events
            OnSlotClicked = null;
            OnPokemonPlayed = null;
            OnPokemonRetreated = null;
            OnStadiumPlayed = null;
            
            isInitialized = false;
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
        #endregion
    }
    
    // Pokemon slot component
    public class PokemonSlot : MonoBehaviour
    {
        public string SlotId { get; private set; }
        public PokemonSlotType SlotType { get; private set; }
        public string PlayerId { get; private set; }
        
        private Card currentPokemon;
        private int currentDamage = 0;
        private Image slotImage;
        private TextMeshProUGUI healthText;
        private Button slotButton;
        
        public System.Action<string, Vector2> OnSlotClicked;
        
        public void Initialize(string slotId, PokemonSlotType slotType, string playerId)
        {
            SlotId = slotId;
            SlotType = slotType;
            PlayerId = playerId;
            
            slotImage = GetComponent<Image>();
            healthText = GetComponentInChildren<TextMeshProUGUI>();
            slotButton = GetComponent<Button>();
            
            if (slotButton != null)
            {
                slotButton.onClick.AddListener(() => OnSlotClicked?.Invoke(SlotId, transform.position));
            }
        }
        
        public void PlacePokemon(Card pokemon)
        {
            currentPokemon = pokemon;
            currentDamage = 0;
            UpdateDisplay();
        }
        
        public void RemovePokemon()
        {
            currentPokemon = null;
            currentDamage = 0;
            UpdateDisplay();
        }
        
        public Card GetPokemon() => currentPokemon;
        
        public void ApplyDamage(int damage)
        {
            currentDamage += damage;
            UpdateDisplay();
        }
        
        public void HealDamage(int healAmount)
        {
            currentDamage = Mathf.Max(0, currentDamage - healAmount);
            UpdateDisplay();
        }
        
        public void UpdateHealthDisplay(int health)
        {
            if (healthText != null)
            {
                healthText.text = $"HP: {health}";
            }
        }
        
        public void SetHighlight(bool highlight, Color color)
        {
            if (slotImage != null)
            {
                slotImage.color = highlight ? color : Color.white;
            }
        }
        
        public void SetFieldHighlight(bool highlight, Color color)
        {
            if (slotImage != null)
            {
                Color targetColor = highlight ? color : Color.white;
                slotImage.DOColor(targetColor, 0.3f);
            }
        }
        
        private void UpdateDisplay()
        {
            if (currentPokemon != null && healthText != null)
            {
                var pokemonData = currentPokemon.GetPokemonData();
                if (pokemonData != null)
                {
                    int remainingHP = pokemonData.HP - currentDamage;
                    healthText.text = $"HP: {remainingHP}/{pokemonData.HP}";
                }
            }
            else if (healthText != null)
            {
                healthText.text = "Empty";
            }
        }
    }
    
    // Enums and Events
    public enum PokemonSlotType
    {
        Active,
        Bench
    }
    
    public class GamePhaseChangedEvent
    {
        public GamePhase OldPhase { get; set; }
        public GamePhase NewPhase { get; set; }
    }
    
    public class PlayerTurnChangedEvent
    {
        public string PreviousPlayerId { get; set; }
        public string CurrentPlayerId { get; set; }
    }
    
    public class PokemonPlayedEvent
    {
        public string SlotId { get; set; }
        public Card Pokemon { get; set; }
        public string PlayerId { get; set; }
    }
    
    public class PokemonDamagedEvent
    {
        public string SlotId { get; set; }
        public int Damage { get; set; }
        public string Source { get; set; }
    }
    
    public class StadiumPlayedEvent
    {
        public Card StadiumCard { get; set; }
        public string PlayerId { get; set; }
    }
}