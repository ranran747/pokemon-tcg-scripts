using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;
using PokemonTCG.Game; // 修正済み
using PokemonTCG.Cards.Runtime;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.UI
{
    /// <summary>
    /// プレイヤーUI詳細制御システム
    /// プレイヤー情報、HP、エネルギー、賞金カードなどの表示を管理
    /// </summary>
    public class PlayerUIController : MonoBehaviour
    {
        [Header("Player Information")]
        public TextMeshProUGUI playerNameText;
        public Image playerAvatar;
        public TextMeshProUGUI playerIdText;
        public GameObject activePlayerIndicator;
        
        [Header("Prize Cards")]
        public Transform prizeCardsContainer;
        public GameObject prizeCardPrefab;
        public TextMeshProUGUI prizeCountText;
        public Slider prizeProgressSlider;
        
        [Header("Deck & Discard")]
        public Button deckButton;
        public TextMeshProUGUI deckCountText;
        public Button discardButton;
        public TextMeshProUGUI discardCountText;
        public GameObject deckEmptyIndicator;
        
        [Header("Energy Display")]
        public Transform energyContainer;
        public GameObject energyCounterPrefab;
        public TextMeshProUGUI totalEnergyText;
        public Dictionary<EnergyType, TextMeshProUGUI> energyCounters;
        
        [Header("Status Effects")]
        public Transform statusEffectsContainer;
        public GameObject statusEffectPrefab;
        public Image poisonIndicator;
        public Image burnIndicator;
        public Image sleepIndicator;
        public Image paralysisIndicator;
        
        [Header("Animation Settings")]
        public float updateAnimationDuration = 0.3f;
        public AnimationCurve updateCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public Color activePlayerColor = Color.green;
        public Color inactivePlayerColor = Color.gray;
        
        // Private fields
        private PlayerState currentPlayerState;
        private string playerId;
        private bool isInitialized = false;
        private List<GameObject> prizeCardObjects = new List<GameObject>();
        private Dictionary<string, GameObject> statusEffectObjects = new Dictionary<string, GameObject>();
        private Coroutine updateCoroutine;
        
        // Events
        public System.Action<string> OnDeckClicked;
        public System.Action<string> OnDiscardClicked;
        public System.Action<string, int> OnPrizeCardClicked;
        
        #region Initialization
        public void Initialize(string playerIdentifier)
        {
            if (isInitialized) return;
            
            playerId = playerIdentifier;
            
            Debug.Log($"[PlayerUIController] Initializing for player: {playerId}");
            
            // Initialize components
            InitializeComponents();
            
            // Setup event listeners
            SetupEventListeners();
            
            // Initialize energy counters
            InitializeEnergyCounters();
            
            // Initialize prize cards
            InitializePrizeCards();
            
            isInitialized = true;
            Debug.Log($"[PlayerUIController] Initialized for player: {playerId}");
        }
        
        private void InitializeComponents()
        {
            // Set player ID display
            if (playerIdText != null)
                playerIdText.text = $"Player {playerId}";
            
            // Set default player name
            if (playerNameText != null)
                playerNameText.text = $"Player {playerId}";
            
            // Initialize indicators
            if (activePlayerIndicator != null)
                activePlayerIndicator.SetActive(false);
            
            if (deckEmptyIndicator != null)
                deckEmptyIndicator.SetActive(false);
        }
        
        private void SetupEventListeners()
        {
            // Deck button
            if (deckButton != null)
                deckButton.onClick.AddListener(() => OnDeckClicked?.Invoke(playerId));
            
            // Discard button
            if (discardButton != null)
                discardButton.onClick.AddListener(() => OnDiscardClicked?.Invoke(playerId));
        }
        
        private void InitializeEnergyCounters()
        {
            energyCounters = new Dictionary<EnergyType, TextMeshProUGUI>();
            
            if (energyContainer != null && energyCounterPrefab != null)
            {
                // Create counters for each energy type
                foreach (EnergyType energyType in System.Enum.GetValues(typeof(EnergyType)))
                {
                    if (energyType == EnergyType.None) continue;
                    
                    GameObject counterObj = Instantiate(energyCounterPrefab, energyContainer);
                    var counterText = counterObj.GetComponentInChildren<TextMeshProUGUI>();
                    var counterImage = counterObj.GetComponent<Image>();
                    
                    if (counterText != null)
                    {
                        counterText.text = "0";
                        energyCounters[energyType] = counterText;
                    }
                    
                    if (counterImage != null)
                    {
                        counterImage.color = GetEnergyColor(energyType);
                    }
                    
                    // Set energy type name
                    var nameText = counterObj.transform.Find("EnergyName")?.GetComponent<TextMeshProUGUI>();
                    if (nameText != null)
                        nameText.text = energyType.ToString();
                }
            }
        }
        
        private void InitializePrizeCards()
        {
            prizeCardObjects.Clear();
            
            if (prizeCardsContainer != null && prizeCardPrefab != null)
            {
                // Create 6 prize card slots (standard Pokemon TCG)
                for (int i = 0; i < 6; i++)
                {
                    GameObject prizeObj = Instantiate(prizeCardPrefab, prizeCardsContainer);
                    prizeObj.name = $"PrizeCard_{i}";
                    
                    // Add click listener
                    var button = prizeObj.GetComponent<Button>();
                    if (button != null)
                    {
                        int index = i; // Capture for closure
                        button.onClick.AddListener(() => OnPrizeCardClicked?.Invoke(playerId, index));
                    }
                    
                    prizeCardObjects.Add(prizeObj);
                }
            }
        }
        #endregion
        
        #region Public Interface
        public void UpdatePlayerState(PlayerState playerState)
        {
            if (!isInitialized) return;
            
            currentPlayerState = playerState;
            
            // Stop previous update
            if (updateCoroutine != null)
                StopCoroutine(updateCoroutine);
            
            // Start animated update
            updateCoroutine = StartCoroutine(UpdatePlayerStateAnimated());
        }
        
        public void SetActivePlayer(bool isActive)
        {
            if (activePlayerIndicator != null)
            {
                activePlayerIndicator.SetActive(isActive);
                
                // Animate color change
                if (playerAvatar != null)
                {
                    Color targetColor = isActive ? activePlayerColor : inactivePlayerColor;
                    playerAvatar.DOColor(targetColor, updateAnimationDuration);
                }
            }
        }
        
        public void SetPlayerName(string name)
        {
            if (playerNameText != null)
            {
                playerNameText.text = name;
            }
        }
        
        public void SetPlayerAvatar(Sprite avatar)
        {
            if (playerAvatar != null)
            {
                playerAvatar.sprite = avatar;
            }
        }
        
        public void UpdateDeckCount(int count)
        {
            if (deckCountText != null)
            {
                // Animate count change
                int currentCount = 0;
                if (int.TryParse(deckCountText.text, out currentCount))
                {
                    DOTween.To(() => currentCount, x => {
                        deckCountText.text = x.ToString();
                    }, count, updateAnimationDuration);
                }
                else
                {
                    deckCountText.text = count.ToString();
                }
            }
            
            // Show deck empty indicator
            if (deckEmptyIndicator != null)
            {
                deckEmptyIndicator.SetActive(count == 0);
            }
        }
        
        public void UpdateDiscardCount(int count)
        {
            if (discardCountText != null)
            {
                // Animate count change
                int currentCount = 0;
                if (int.TryParse(discardCountText.text, out currentCount))
                {
                    DOTween.To(() => currentCount, x => {
                        discardCountText.text = x.ToString();
                    }, count, updateAnimationDuration);
                }
                else
                {
                    discardCountText.text = count.ToString();
                }
            }
        }
        
        public void UpdatePrizeCards(int remainingPrizes)
        {
            // Update prize count text
            if (prizeCountText != null)
            {
                prizeCountText.text = $"{remainingPrizes}/6";
            }
            
            // Update progress slider
            if (prizeProgressSlider != null)
            {
                float progress = (6f - remainingPrizes) / 6f;
                prizeProgressSlider.DOValue(progress, updateAnimationDuration);
            }
            
            // Update prize card visuals
            for (int i = 0; i < prizeCardObjects.Count; i++)
            {
                var prizeObj = prizeCardObjects[i];
                bool isCollected = i >= remainingPrizes;
                
                // Animate state change
                var canvasGroup = prizeObj.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.DOFade(isCollected ? 0.3f : 1f, updateAnimationDuration);
                }
                
                // Update interactability
                var button = prizeObj.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = !isCollected;
                }
            }
        }
        
        public void UpdateEnergyCount(Dictionary<EnergyType, int> energyCounts)
        {
            int totalEnergy = 0;
            
            foreach (var kvp in energyCounts)
            {
                totalEnergy += kvp.Value;
                
                if (energyCounters.ContainsKey(kvp.Key))
                {
                    var counterText = energyCounters[kvp.Key];
                    if (counterText != null)
                    {
                        // Animate count change
                        int currentCount = 0;
                        if (int.TryParse(counterText.text, out currentCount))
                        {
                            DOTween.To(() => currentCount, x => {
                                counterText.text = x.ToString();
                            }, kvp.Value, updateAnimationDuration);
                        }
                        else
                        {
                            counterText.text = kvp.Value.ToString();
                        }
                    }
                }
            }
            
            // Update total energy
            if (totalEnergyText != null)
            {
                totalEnergyText.text = $"Total: {totalEnergy}";
            }
        }
        
        public void AddStatusEffect(string effectName, Sprite effectIcon)
        {
            if (statusEffectsContainer != null && statusEffectPrefab != null)
            {
                if (!statusEffectObjects.ContainsKey(effectName))
                {
                    GameObject effectObj = Instantiate(statusEffectPrefab, statusEffectsContainer);
                    effectObj.name = $"StatusEffect_{effectName}";
                    
                    var image = effectObj.GetComponent<Image>();
                    if (image != null && effectIcon != null)
                    {
                        image.sprite = effectIcon;
                    }
                    
                    // Animate appearance
                    effectObj.transform.localScale = Vector3.zero;
                    effectObj.transform.DOScale(Vector3.one, updateAnimationDuration)
                        .SetEase(Ease.OutBack);
                    
                    statusEffectObjects[effectName] = effectObj;
                }
            }
            
            // Update specific status indicators
            UpdateStatusIndicators(effectName, true);
        }
        
        public void RemoveStatusEffect(string effectName)
        {
            if (statusEffectObjects.ContainsKey(effectName))
            {
                var effectObj = statusEffectObjects[effectName];
                
                // Animate removal
                effectObj.transform.DOScale(Vector3.zero, updateAnimationDuration)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => {
                        if (effectObj != null)
                            Destroy(effectObj);
                    });
                
                statusEffectObjects.Remove(effectName);
            }
            
            // Update specific status indicators
            UpdateStatusIndicators(effectName, false);
        }
        #endregion
        
        #region Private Methods
        private IEnumerator UpdatePlayerStateAnimated()
        {
            if (currentPlayerState == null) yield break;
            
            // Update all components with animation
            yield return StartCoroutine(AnimatePlayerStateUpdate());
        }
        
        private IEnumerator AnimatePlayerStateUpdate()
        {
            // Animate updates with staggered timing
            var tasks = new List<Coroutine>();
            
            // Update deck count
            tasks.Add(StartCoroutine(DelayedUpdate(0f, () => {
                // UpdateDeckCount(currentPlayerState.Deck?.Count ?? 0);
                UpdateDeckCount(UnityEngine.Random.Range(20, 60)); // Placeholder
            })));
            
            // Update discard count
            tasks.Add(StartCoroutine(DelayedUpdate(0.1f, () => {
                // UpdateDiscardCount(currentPlayerState.DiscardPile?.Count ?? 0);
                UpdateDiscardCount(UnityEngine.Random.Range(0, 15)); // Placeholder
            })));
            
            // Update prize cards
            tasks.Add(StartCoroutine(DelayedUpdate(0.2f, () => {
                // UpdatePrizeCards(currentPlayerState.PrizeCards?.Count ?? 6);
                UpdatePrizeCards(UnityEngine.Random.Range(2, 6)); // Placeholder
            })));
            
            // Update energy counts
            tasks.Add(StartCoroutine(DelayedUpdate(0.3f, () => {
                var energyCounts = new Dictionary<EnergyType, int>
                {
                    { EnergyType.Fire, UnityEngine.Random.Range(0, 8) },
                    { EnergyType.Water, UnityEngine.Random.Range(0, 6) },
                    { EnergyType.Electric, UnityEngine.Random.Range(0, 4) },
                    { EnergyType.Grass, UnityEngine.Random.Range(0, 5) },
                    { EnergyType.Psychic, UnityEngine.Random.Range(0, 3) }
                };
                UpdateEnergyCount(energyCounts);
            })));
            
            // Wait for all updates to complete
            foreach (var task in tasks)
            {
                yield return task;
            }
        }
        
        private IEnumerator DelayedUpdate(float delay, System.Action updateAction)
        {
            yield return new WaitForSeconds(delay);
            updateAction?.Invoke();
        }
        
        private void UpdateStatusIndicators(string effectName, bool active)
        {
            switch (effectName.ToLower())
            {
                case "poison":
                    if (poisonIndicator != null)
                        poisonIndicator.gameObject.SetActive(active);
                    break;
                case "burn":
                    if (burnIndicator != null)
                        burnIndicator.gameObject.SetActive(active);
                    break;
                case "sleep":
                    if (sleepIndicator != null)
                        sleepIndicator.gameObject.SetActive(active);
                    break;
                case "paralysis":
                    if (paralysisIndicator != null)
                        paralysisIndicator.gameObject.SetActive(active);
                    break;
            }
        }
        
        private Color GetEnergyColor(EnergyType energyType)
        {
            switch (energyType)
            {
                case EnergyType.Fire: return Color.red;
                case EnergyType.Water: return Color.blue;
                case EnergyType.Electric: return Color.yellow;
                case EnergyType.Grass: return Color.green;
                case EnergyType.Psychic: return Color.magenta;
                case EnergyType.Fighting: return new Color(0.8f, 0.4f, 0.2f); // Brown
                case EnergyType.Dark: return new Color(0.2f, 0.2f, 0.2f); // Dark gray
                case EnergyType.Steel: return Color.gray;
                case EnergyType.Fairy: return Color.pink;
                case EnergyType.Dragon: return new Color(0.5f, 0f, 0.8f); // Purple
                case EnergyType.Colorless: return Color.white;
                default: return Color.white;
            }
        }
        #endregion
        
        #region Cleanup
        public void Cleanup()
        {
            if (updateCoroutine != null)
            {
                StopCoroutine(updateCoroutine);
                updateCoroutine = null;
            }
            
            // Clean up events
            OnDeckClicked = null;
            OnDiscardClicked = null;
            OnPrizeCardClicked = null;
            
            isInitialized = false;
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
        #endregion
    }
    
    // Energy type enum
    public enum EnergyType
    {
        None,
        Fire,
        Water,
        Electric,
        Grass,
        Psychic,
        Fighting,
        Dark,
        Steel,
        Fairy,
        Dragon,
        Colorless
    }
}