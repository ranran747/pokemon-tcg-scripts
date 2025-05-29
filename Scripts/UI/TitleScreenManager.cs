using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Game;
using PokemonTCG.Game.Rules;
using PokemonTCG.UI;

namespace PokemonTCG.UI
{
    /// <summary>
    /// タイトル画面統合管理システム
    /// ServiceLocator/EventBus統合、DOTweenアニメーション、シーン遷移制御
    /// 既存アーキテクチャに完全準拠した高品質実装
    /// </summary>
    public class TitleScreenManager : MonoBehaviour, IManager
    {
        [Header("UI References")]
        [SerializeField] private Canvas titleCanvas;
        [SerializeField] private CanvasGroup titleCanvasGroup;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Image backgroundImage;
        
        [Header("Button References")]
        [SerializeField] private Button singleModeButton;
        [SerializeField] private Button monteCarloButton;
        [SerializeField] private Button deckBuildButton;
        [SerializeField] private Button replayModeButton;
        [SerializeField] private Button exitGameButton;
        
        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 1.5f;
        [SerializeField] private float buttonAnimationDelay = 0.1f;
        [SerializeField] private Ease titleTextEase = Ease.OutBounce;
        [SerializeField] private Ease buttonEase = Ease.OutQuart;
        
        [Header("Audio Settings")]
        [SerializeField] private AudioClip bgmClip;
        [SerializeField] private AudioClip buttonHoverSound;
        [SerializeField] private AudioClip buttonClickSound;
        
        [ContextMenu("Test Single Player Transition")]
        public void TestSinglePlayerTransition()
        {
            OnSingleModeClicked();
        }

        [ContextMenu("Test Deck Builder Transition")]
        public void TestDeckBuilderTransition()
        {
            OnDeckBuildClicked();
        }
        // Private fields
        private GameUI gameUI;
        private bool isInitialized = false;
        private List<Button> allButtons = new List<Button>();
        private Dictionary<string, System.Action> buttonActions = new Dictionary<string, System.Action>();
        private Sequence titleAnimationSequence;
        private AudioSource audioSource;
        
        // Events
        public System.Action<string> OnModeSelected;
        public System.Action OnTitleScreenClosed;
        
        #region IManager Implementation
        public bool IsInitialized => isInitialized;
        public string ManagerName => "TitleScreenManager";
        public int InitializationOrder => 100; // UI managers initialize early
        
        public void Initialize()
        {
            if (isInitialized) return;
            
            Debug.Log("[TitleScreenManager] Initializing title screen system...");
            
            // Service Locator registration
            ServiceLocator.Register<TitleScreenManager>(this);
            
            // Get dependencies
            gameUI = ServiceLocator.Get<GameUI>();
            
            // Initialize components
            InitializeUIComponents();
            InitializeAudioSystem();
            InitializeButtonActions();
            SetupEventListeners();
            
            // Play entrance animation
            PlayEntranceAnimation();
            
            isInitialized = true;
            Debug.Log("[TitleScreenManager] Title screen system initialized successfully");
            
            // Publish initialization event
            EventBus.Emit(new TitleScreenInitializedEvent { manager = this });
        }
        
        public void Dispose()
        {
            if (!isInitialized) return;
            
            // Kill animations
            titleAnimationSequence?.Kill();
            DOTween.Kill(transform);
            
            // Cleanup audio
            if (audioSource != null)
            {
                audioSource.Stop();
            }
            
            // Unsubscribe events
            UnsubscribeFromEvents();
            
            // Service cleanup
            ServiceLocator.Get<ServiceLocator>()?.UnregisterService<TitleScreenManager>();
            
            isInitialized = false;
            Debug.Log("[TitleScreenManager] Title screen system disposed");
        }
        
        void IManager.Update()
        {
            if (!isInitialized) return;
            
            HandleTitleScreenInput();
        }
        
        void IManager.FixedUpdate()
        {
            // No fixed update needed for title screen
        }
        #endregion
        
        #region Unity Lifecycle
        private void Awake()
        {
            // Auto-find references if not assigned
            FindUIReferences();
            
            // Auto-initialize if ServiceLocator is ready
            if (ServiceLocator.Get<ServiceLocator>() != null)
            {
                Initialize();
            }
        }
        
        private void Start()
        {
            // Ensure initialization
            if (!isInitialized)
            {
                Initialize();
            }
        }
        
        private void OnDestroy()
        {
            Dispose();
        }
        #endregion
        
        #region Initialization
        private void FindUIReferences()
        {
            if (titleCanvas == null)
                titleCanvas = GetComponent<Canvas>();
            
            if (titleCanvasGroup == null)
                titleCanvasGroup = GetComponent<CanvasGroup>();
            
            // Auto-find components if not assigned
            if (titleText == null)
                titleText = GetComponentInChildren<TextMeshProUGUI>();
            
            // Find buttons by name
            if (singleModeButton == null)
                singleModeButton = GameObject.Find("SingleModeButton")?.GetComponent<Button>();
            if (monteCarloButton == null)
                monteCarloButton = GameObject.Find("MonteCarloButton")?.GetComponent<Button>();
            if (deckBuildButton == null)
                deckBuildButton = GameObject.Find("DeckBuildButton")?.GetComponent<Button>();
            if (replayModeButton == null)
                replayModeButton = GameObject.Find("ReplayModeButton")?.GetComponent<Button>();
            if (exitGameButton == null)
                exitGameButton = GameObject.Find("ExitGameButton")?.GetComponent<Button>();
        }
        
        private void InitializeUIComponents()
        {
            // Configure canvas settings
            if (titleCanvas != null)
            {
                titleCanvas.sortingOrder = 10; // Above game UI
            }
            
            // Initialize canvas group for animations
            if (titleCanvasGroup == null)
            {
                titleCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            titleCanvasGroup.alpha = 0f;
            
            // Setup version text
            if (versionText != null)
            {
                versionText.text = $"Pokemon TCG Simulator v{Application.version}\nPhase 5 - Offline PTCGL Edition";
            }
            
            // Collect all buttons
            CollectButtons();
            
            // Setup button hover effects
            SetupButtonAnimations();
        }
        
        private void InitializeAudioSystem()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            
            // Play BGM if available
            if (bgmClip != null)
            {
                audioSource.clip = bgmClip;
                audioSource.Play();
            }
        }
        
        private void InitializeButtonActions()
        {
            buttonActions.Clear();
            
            buttonActions["SingleMode"] = () => OnSingleModeClicked();
            buttonActions["MonteCarlo"] = () => OnMonteCarloClicked();
            buttonActions["DeckBuild"] = () => OnDeckBuildClicked();
            buttonActions["ReplayMode"] = () => OnReplayModeClicked();
            buttonActions["ExitGame"] = () => OnExitGameClicked();
        }
        
        private void CollectButtons()
        {
            allButtons.Clear();
            
            if (singleModeButton != null) allButtons.Add(singleModeButton);
            if (monteCarloButton != null) allButtons.Add(monteCarloButton);
            if (deckBuildButton != null) allButtons.Add(deckBuildButton);
            if (replayModeButton != null) allButtons.Add(replayModeButton);
            if (exitGameButton != null) allButtons.Add(exitGameButton);
        }
        
        private void SetupButtonAnimations()
        {
            foreach (var button in allButtons)
            {
                if (button == null) continue;
                
                // Set initial state
                button.transform.localScale = Vector3.zero;
                
                // Setup hover effects
                var buttonTransform = button.transform;
                
                button.onClick.AddListener(() => PlayButtonClickSound());
                
                // Add hover animations
                var eventTrigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                if (eventTrigger == null)
                {
                    eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                }
                
                // Hover enter
                var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
                pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
                pointerEnter.callback.AddListener((data) => OnButtonHover(buttonTransform, true));
                eventTrigger.triggers.Add(pointerEnter);
                
                // Hover exit
                var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
                pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
                pointerExit.callback.AddListener((data) => OnButtonHover(buttonTransform, false));
                eventTrigger.triggers.Add(pointerExit);
            }
        }
        
        private void SetupEventListeners()
        {
            // Button click listeners
            singleModeButton?.onClick.AddListener(() => ExecuteButtonAction("SingleMode"));
            monteCarloButton?.onClick.AddListener(() => ExecuteButtonAction("MonteCarlo"));
            deckBuildButton?.onClick.AddListener(() => ExecuteButtonAction("DeckBuild"));
            replayModeButton?.onClick.AddListener(() => ExecuteButtonAction("ReplayMode"));
            exitGameButton?.onClick.AddListener(() => ExecuteButtonAction("ExitGame"));
            
            // Subscribe to EventBus events
            EventBus.On<GameModeSelectedEvent>(OnGameModeSelected);
            EventBus.On<DeckEditorClosedEvent>(OnDeckEditorClosed);
        }
        
        private void UnsubscribeFromEvents()
        {
            EventBus.Off<GameModeSelectedEvent>(OnGameModeSelected);
            EventBus.Off<DeckEditorClosedEvent>(OnDeckEditorClosed);
        }
        #endregion
        
        #region Animation System
        private void PlayEntranceAnimation()
        {
            titleAnimationSequence?.Kill();
            titleAnimationSequence = DOTween.Sequence();
            
            // Fade in canvas
            titleAnimationSequence.Append(titleCanvasGroup.DOFade(1f, fadeInDuration).SetEase(Ease.OutQuart));
            
            // Animate title text
            if (titleText != null)
            {
                titleText.transform.localScale = Vector3.zero;
                titleAnimationSequence.Join(titleText.transform.DOScale(1f, fadeInDuration * 0.8f)
                    .SetEase(titleTextEase).SetDelay(0.2f));
            }
            
            // Animate buttons in sequence
            for (int i = 0; i < allButtons.Count; i++)
            {
                if (allButtons[i] == null) continue;
                
                var button = allButtons[i];
                var delay = fadeInDuration * 0.5f + (i * buttonAnimationDelay);
                
                titleAnimationSequence.Join(button.transform.DOScale(1f, 0.6f)
                    .SetEase(buttonEase).SetDelay(delay));
            }
            
            // Animation complete callback
            titleAnimationSequence.OnComplete(() => 
            {
                Debug.Log("[TitleScreenManager] Entrance animation completed");
                EventBus.Emit(new TitleScreenAnimationCompleteEvent { animationType = "Entrance" });
            });
        }
        
        private void PlayExitAnimation(System.Action onComplete = null)
        {
            titleAnimationSequence?.Kill();
            titleAnimationSequence = DOTween.Sequence();
            
            // Animate buttons out
            for (int i = allButtons.Count - 1; i >= 0; i--)
            {
                if (allButtons[i] == null) continue;
                
                var button = allButtons[i];
                var delay = (allButtons.Count - 1 - i) * (buttonAnimationDelay * 0.5f);
                
                titleAnimationSequence.Join(button.transform.DOScale(0f, 0.4f)
                    .SetEase(Ease.InBack).SetDelay(delay));
            }
            
            // Fade out canvas
            titleAnimationSequence.Append(titleCanvasGroup.DOFade(0f, 0.5f).SetEase(Ease.InQuart));
            
            titleAnimationSequence.OnComplete(() => 
            {
                onComplete?.Invoke();
                EventBus.Emit(new TitleScreenAnimationCompleteEvent { animationType = "Exit" });
            });
        }
        
        private void OnButtonHover(Transform buttonTransform, bool isHovering)
        {
            if (buttonTransform == null) return;
            
            buttonTransform.DOKill();
            
            if (isHovering)
            {
                buttonTransform.DOScale(1.1f, 0.2f).SetEase(Ease.OutBack);
                PlayButtonHoverSound();
            }
            else
            {
                buttonTransform.DOScale(1f, 0.2f).SetEase(Ease.OutQuart);
            }
        }
        #endregion
        
        #region Button Actions
        private void ExecuteButtonAction(string actionName)
        {
            if (buttonActions.TryGetValue(actionName, out var action))
            {
                Debug.Log($"[TitleScreenManager] Executing action: {actionName}");
                action?.Invoke();
            }
            else
            {
                Debug.LogWarning($"[TitleScreenManager] Action '{actionName}' not found");
            }
        }
        
        private void OnSingleModeClicked()
        {
            OnModeSelected?.Invoke("SinglePlayer");
            EventBus.Emit(new GameModeSelectedEvent { mode = "SinglePlayer", difficulty = "Normal" });
            
            TransitionToGameMode("SinglePlayer");
        }
        
        private void OnMonteCarloClicked()
        {
            Debug.Log("[TitleScreenManager] Monte Carlo mode selected");
            OnModeSelected?.Invoke("MonteCarlo");
            EventBus.Emit(new GameModeSelectedEvent { mode = "MonteCarlo", difficulty = "AI_vs_AI" });
            
            TransitionToGameMode("MonteCarlo");
        }
        
        private void OnDeckBuildClicked()
        {
            Debug.Log("[TitleScreenManager] Deck Builder selected");
            OnModeSelected?.Invoke("DeckBuilder");
            EventBus.Emit(new DeckEditorRequestEvent { requestSource = "TitleScreen" });
            
            TransitionToDeckEditor();
        }
        
        private void OnReplayModeClicked()
        {
            Debug.Log("[TitleScreenManager] Replay mode selected");
            OnModeSelected?.Invoke("ReplayMode");
            EventBus.Emit(new GameModeSelectedEvent { mode = "ReplayMode", difficulty = "Review" });
            
            // TODO: Implement replay system
            ShowNotImplementedMessage("Replay Mode");
        }
        
        private void OnExitGameClicked()
        {
            Debug.Log("[TitleScreenManager] Exit game requested");
            EventBus.Emit(new ApplicationExitRequestEvent { exitReason = "UserRequest" });
            
            PlayExitAnimation(() => 
            {
                #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                #else
                    Application.Quit();
                #endif
            });
        }
        #endregion
        
        #region Scene Transitions
        private void TransitionToGameMode(string mode)
        {
            Debug.Log($"[TitleScreenManager] Transitioning to {mode}");
            
            // Hide title screen
            gameObject.SetActive(false);
            
            // Show game UI
            var gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas != null)
            {
                gameCanvas.SetActive(true);
                Debug.Log("[TitleScreenManager] Game mode activated!");
            }
        }
        
        private void TransitionToDeckEditor()
        {
            Debug.Log("[TitleScreenManager] Transitioning to Deck Editor");
            
            // Hide title screen
            gameObject.SetActive(false);
            
            // Show deck editor
            var deckEditorUI = GameObject.Find("DeckEditorUI");
            if (deckEditorUI != null)
            {
                deckEditorUI.SetActive(true);
                Debug.Log("[TitleScreenManager] Deck Editor activated!");
            }
        }
        
        public void ReturnToTitleScreen()
        {
            Debug.Log("[TitleScreenManager] Returning to title screen");
            
            // Hide other UIs
            var gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas != null)
            {
                gameCanvas.SetActive(false);
                Debug.Log("[TitleScreenManager] GameCanvas deactivated");
            }
            
            var deckEditorUI = GameObject.Find("DeckEditorUI");
            if (deckEditorUI != null)
            {
                deckEditorUI.SetActive(false);
                Debug.Log("[TitleScreenManager] DeckEditorUI deactivated");
            }
            
            // Show title screen
            gameObject.SetActive(true);
            PlayEntranceAnimation();
            Debug.Log("[TitleScreenManager] Title screen reactivated");
        }

        // 緊急用の即座遷移メソッド（アニメーションなしのテスト用）
        public void QuickTransitionToGameMode()
        {
            Debug.Log("[TitleScreenManager] Quick transition to game mode");
            
            gameObject.SetActive(false);
            var gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas != null)
            {
                gameCanvas.SetActive(true);
                Debug.Log("[TitleScreenManager] Quick transition successful");
            }
        }

        public void QuickTransitionToDeckEditor()
        {
            Debug.Log("[TitleScreenManager] Quick transition to deck editor");
            
            gameObject.SetActive(false);
            var deckEditorUI = GameObject.Find("DeckEditorUI");
            if (deckEditorUI != null)
            {
                deckEditorUI.SetActive(true);
                Debug.Log("[TitleScreenManager] Quick transition successful");
            }
        }
        #endregion
        
        #region Audio System
        private void PlayButtonHoverSound()
        {
            if (buttonHoverSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(buttonHoverSound, 0.5f);
            }
        }
        
        private void PlayButtonClickSound()
        {
            if (buttonClickSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(buttonClickSound, 0.7f);
            }
        }
        #endregion
        
        #region Input Handling
        private void HandleTitleScreenInput()
        {
            // ESC key to exit
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnExitGameClicked();
            }
            
            // Hotkeys for quick access
            if (Input.GetKeyDown(KeyCode.Alpha1)) OnSingleModeClicked();
            if (Input.GetKeyDown(KeyCode.Alpha2)) OnMonteCarloClicked();
            if (Input.GetKeyDown(KeyCode.Alpha3)) OnDeckBuildClicked();
            if (Input.GetKeyDown(KeyCode.Alpha4)) OnReplayModeClicked();
        }
        #endregion
        
        #region Event Handlers
        private void OnGameModeSelected(GameModeSelectedEvent eventData)
        {
            Debug.Log($"[TitleScreenManager] Game mode selected: {eventData.mode}");
        }
        
        private void OnDeckEditorClosed(DeckEditorClosedEvent eventData)
        {
            Debug.Log("[TitleScreenManager] Deck editor closed, returning to title");
            ReturnToTitleScreen();
        }
        #endregion
        
        #region Utility Methods
        private void ShowNotImplementedMessage(string featureName)
        {
            Debug.Log($"[TitleScreenManager] {featureName} - Coming Soon!");
            
            // TODO: Show proper notification UI
            // For now, just log the message
        }
        
        public void SetButtonInteractable(string buttonName, bool interactable)
        {
            Button targetButton = buttonName switch
            {
                "SingleMode" => singleModeButton,
                "MonteCarlo" => monteCarloButton,
                "DeckBuild" => deckBuildButton,
                "ReplayMode" => replayModeButton,
                "ExitGame" => exitGameButton,
                _ => null
            };
            
            if (targetButton != null)
            {
                targetButton.interactable = interactable;
            }
        }
        
        public void UpdateVersionText(string version)
        {
            if (versionText != null)
            {
                versionText.text = version;
            }
        }
        #endregion
    }
    
    // Event classes for EventBus integration
    public class TitleScreenInitializedEvent
    {
        public TitleScreenManager manager;
    }
    
    public class TitleScreenAnimationCompleteEvent
    {
        public string animationType;
    }
    
    public class GameModeSelectedEvent
    {
        public string mode;
        public string difficulty;
    }
    
    public class DeckEditorRequestEvent
    {
        public string requestSource;
    }
    
    public class ApplicationExitRequestEvent
    {
        public string exitReason;
    }
}