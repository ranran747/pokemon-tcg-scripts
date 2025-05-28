using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Game;
using PokemonTCG.Game.Rules;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.UI
{
    /// <summary>
    /// ゲーム全体のUI基盤システム
    /// ServiceLocatorパターンでアクセス可能な統合UIマネージャー
    /// </summary>
    public class GameUI : MonoBehaviour, IManager
    {
        [Header("UI References")]
        public Canvas gameCanvas;
        public RectTransform uiRoot;
        
        [Header("Player UI")]
        public GameObject player1UI;
        public GameObject player2UI;
        
        [Header("Field UI")]
        public GameObject fieldUI;
        
        [Header("Hand UI")]
        public GameObject handUI;
        
        [Header("Effect UI")]
        public GameObject effectUI;
        
        [Header("AI Battle UI")]
        public GameObject aiBattleUI;
        
        [Header("Debug UI")]
        public GameObject debugPanel;
        public TextMeshProUGUI debugText;
        
        // Private fields
        private GameStateManager gameStateManager;
        private bool isInitialized = false;
        private Dictionary<string, GameObject> uiPanels = new Dictionary<string, GameObject>();
        
        // Events
        public System.Action<string> OnUIStateChanged;
        public System.Action<Vector2> OnScreenResolutionChanged;
        
        #region IManager Implementation
        public bool IsInitialized => isInitialized;
        public string ManagerName => "GameUI";
        public int InitializationOrder => 200; // UI should initialize after game systems
        
        public void Initialize()
        {
            if (isInitialized) return;
            
            Debug.Log("[GameUI] Initializing UI system...");
            
            // Service Locator registration
            ServiceLocator.Register<GameUI>(this);
            
            // Get dependencies
            gameStateManager = ServiceLocator.Get<GameStateManager>();
            
            // Initialize UI components
            InitializeUIComponents();
            
            // Subscribe to events
            SubscribeToEvents();
            
            // Setup initial UI state
            SetupInitialUIState();
            
            isInitialized = true;
            Debug.Log("[GameUI] UI system initialized successfully");
        }
        
        public void Dispose()
        {
            if (!isInitialized) return;
            
            UnsubscribeFromEvents();
            CleanupUIComponents();
            
            ServiceLocator.Get<ServiceLocator>()?.UnregisterService<GameUI>();
            isInitialized = false;
        }
        
        void IManager.Update()
        {
            if (!isInitialized) return;
            
            // Handle input
            HandleUIInput();
            
            // Update debug display
            UpdateDebugDisplay();
        }
        
        void IManager.FixedUpdate()
        {
            // No fixed update needed for UI
        }
        #endregion
        
        #region Unity Lifecycle
        private void Awake()
        {
            // Find required references if not assigned
            if (gameCanvas == null)
                gameCanvas = GetComponentInParent<Canvas>();
            
            if (uiRoot == null)
                uiRoot = GetComponent<RectTransform>();
            
            // Auto-initialize if possible
            if (ServiceLocator.Get<ServiceLocator>() != null)
                Initialize();
        }
        
        private void Start()
        {
            // Ensure initialization
            if (!isInitialized)
                Initialize();
        }
        
        private void OnDestroy()
        {
            Dispose();
        }
        
        private void Update()
        {
            if (!isInitialized) return;
            
            // Handle input
            HandleUIInput();
            
            // Update debug display
            UpdateDebugDisplay();
        }
        #endregion
        
        #region Initialization
        private void InitializeUIComponents()
        {
            // Initialize UI controllers
            InitializePlayerUI();
            InitializeFieldUI();
            InitializeHandUI();
            InitializeEffectUI();
            InitializeAIBattleUI();
            InitializeDebugUI();
        }
        
        private void InitializePlayerUI()
        {
            if (player1UI != null) player1UI.SetActive(true);
            if (player2UI != null) player2UI.SetActive(true);
        }
        
        private void InitializeFieldUI()
        {
            if (fieldUI != null) fieldUI.SetActive(true);
        }
        
        private void InitializeHandUI()
        {
            if (handUI != null) handUI.SetActive(true);
        }
        
        private void InitializeEffectUI()
        {
            if (effectUI != null) effectUI.SetActive(true);
        }
        
        private void InitializeAIBattleUI()
        {
            if (aiBattleUI != null) aiBattleUI.SetActive(true);
        }
        
        private void InitializeDebugUI()
        {
            if (debugPanel != null)
            {
                debugPanel.SetActive(Application.isEditor || Debug.isDebugBuild);
            }
        }
        
        private void SetupInitialUIState()
        {
            // Set default UI state
            ShowUI("MainMenu");
            
            // Configure canvas settings
            ConfigureCanvasSettings();
        }
        
        private void ConfigureCanvasSettings()
        {
            if (gameCanvas != null)
            {
                gameCanvas.sortingOrder = 0;
                
                var canvasScaler = gameCanvas.GetComponent<CanvasScaler>();
                if (canvasScaler != null)
                {
                    canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    canvasScaler.referenceResolution = new Vector2(1920, 1080);
                    canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    canvasScaler.matchWidthOrHeight = 0.5f;
                }
            }
        }
        #endregion
        
        #region Event Management
        private void SubscribeToEvents()
        {
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged += OnGameStateChanged;
                gameStateManager.OnTurnChanged += OnPlayerTurnChanged;
                gameStateManager.OnPhaseChanged += OnPhaseChanged;
                gameStateManager.OnGameEnded += OnGameEnded;
            }
            
            // Subscribe to EventBus events (Fixed: class instead of struct)
            EventBus.On<GameStartedEvent>(OnGameStarted);
            EventBus.On<GameEndedEvent>(OnGameEndedEvent);
            EventBus.On<CardPlayedEvent>(OnCardPlayed);
            EventBus.On<EffectTriggeredEvent>(OnEffectTriggered);
        }
        
        private void UnsubscribeFromEvents()
        {
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged -= OnGameStateChanged;
                gameStateManager.OnTurnChanged -= OnPlayerTurnChanged;
                gameStateManager.OnPhaseChanged -= OnPhaseChanged;
                gameStateManager.OnGameEnded -= OnGameEnded;
            }
            
            EventBus.Off<GameStartedEvent>(OnGameStarted);
            EventBus.Off<GameEndedEvent>(OnGameEndedEvent);
            EventBus.Off<CardPlayedEvent>(OnCardPlayed);
            EventBus.Off<EffectTriggeredEvent>(OnEffectTriggered);
        }
        #endregion
        
        #region Event Handlers
        private void OnGameStateChanged(GameState gameState)
        {
            Debug.Log($"[GameUI] Game state changed to: {gameState.CurrentPhase}");
            UpdateUIForGameState(gameState);
        }
        
        private void OnPlayerTurnChanged(string previousPlayer, string currentPlayer)
        {
            Debug.Log($"[GameUI] Player turn changed: {previousPlayer} -> {currentPlayer}");
            UpdateUIForPlayerTurn(currentPlayer);
        }
        
        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            Debug.Log($"[GameUI] Phase changed: {oldPhase} -> {newPhase}");
            UpdateUIForPhase(newPhase);
        }
        
        private void OnGameEnded(string winnerId, VictoryReason reason)
        {
            Debug.Log($"[GameUI] Game ended: Winner {winnerId}, Reason: {reason}");
            DisplayGameResult(winnerId, reason.ToString());
        }
        
        private void OnGameStarted(GameStartedEvent eventData)
        {
            ShowUI("GamePlay");
            Debug.Log($"[GameUI] Game started with mode: {eventData.gameMode}");
        }
        
        private void OnGameEndedEvent(GameEndedEvent eventData)
        {
            ShowUI("GameResult");
            DisplayGameResult(eventData.winner.ToString(), eventData.winCondition);
        }
        
        private void OnCardPlayed(CardPlayedEvent eventData)
        {
            Debug.Log($"[GameUI] Card played: {eventData.card.CardData.CardName} by Player {eventData.player}");
        }
        
        private void OnEffectTriggered(EffectTriggeredEvent eventData)
        {
            Debug.Log($"[GameUI] Effect triggered: {eventData.effectName}");
        }
        #endregion
        
        #region UI State Management
        public void ShowUI(string uiName)
        {
            HideAllUI();
            
            if (uiPanels.ContainsKey(uiName))
            {
                uiPanels[uiName].SetActive(true);
                OnUIStateChanged?.Invoke(uiName);
            }
            else
            {
                Debug.LogWarning($"[GameUI] UI panel '{uiName}' not found");
            }
        }
        
        public void HideUI(string uiName)
        {
            if (uiPanels.ContainsKey(uiName))
            {
                uiPanels[uiName].SetActive(false);
            }
        }
        
        public void HideAllUI()
        {
            foreach (var panel in uiPanels.Values)
            {
                panel.SetActive(false);
            }
        }
        
        public void RegisterUIPanel(string name, GameObject panel)
        {
            uiPanels[name] = panel;
        }
        
        public void UnregisterUIPanel(string name)
        {
            uiPanels.Remove(name);
        }
        
        private void UpdateUIForGameState(GameState gameState)
        {
            // Use existing GamePhase values
            switch (gameState.CurrentPhase)
            {
                case GamePhase.Setup:
                case GamePhase.GameStart:
                    ShowUI("MainMenu");
                    break;
                case GamePhase.MainPhase:
                case GamePhase.DrawPhase:
                case GamePhase.AttackPhase:
                    ShowUI("GamePlay");
                    break;
                case GamePhase.GameEnd:
                    ShowUI("GameResult");
                    break;
                default:
                    ShowUI("GamePlay");
                    break;
            }
        }
        
        private void UpdateUIForPlayerTurn(string currentPlayer)
        {
            // Update player UI highlights - simplified for now
            Debug.Log($"[GameUI] Current player turn: {currentPlayer}");
            
            // TODO: Implement detailed player turn UI updates when PlayerUIController is created
        }
        
        private void UpdateUIForPhase(GamePhase newPhase)
        {
            Debug.Log($"[GameUI] UI updated for phase: {newPhase}");
            // TODO: Implement phase-specific UI updates
        }
        #endregion
        
        #region Input Handling
        private void HandleUIInput()
        {
            // ESC key handling
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscapeKey();
            }
            
            // Debug key handling
            if (Input.GetKeyDown(KeyCode.F1))
            {
                ToggleDebugUI();
            }
            
            // AI battle toggle
            if (Input.GetKeyDown(KeyCode.F2))
            {
                ToggleAIBattleUI();
            }
        }
        
        private void HandleEscapeKey()
        {
            if (gameStateManager?.CurrentGameState != null)
            {
                var currentPhase = gameStateManager.CurrentGameState.CurrentPhase;
                if (currentPhase == GamePhase.MainPhase || 
                    currentPhase == GamePhase.AttackPhase || 
                    currentPhase == GamePhase.DrawPhase)
                {
                    ShowUI("PauseMenu");
                }
                else
                {
                    ShowUI("GamePlay");
                }
            }
        }
        
        private void ToggleDebugUI()
        {
            if (debugPanel != null)
            {
                debugPanel.SetActive(!debugPanel.activeSelf);
            }
        }
        
        private void ToggleAIBattleUI()
        {
            if (aiBattleUI != null)
            {
                aiBattleUI.SetActive(!aiBattleUI.activeSelf);
            }
        }
        #endregion
        
        #region UI Updates
        private void UpdateDebugDisplay()
        {
            if (debugText != null && debugPanel != null && debugPanel.activeSelf)
            {
                UpdateDebugText();
            }
        }
        
        private void UpdateDebugText()
        {
            var sb = new System.Text.StringBuilder();
            
            // Game state info
            if (gameStateManager != null && gameStateManager.CurrentGameState != null)
            {
                var gameState = gameStateManager.CurrentGameState;
                sb.AppendLine($"Game State: {gameState.CurrentPhase}");
                sb.AppendLine($"Current Player: {gameState.CurrentPlayerId}");
                sb.AppendLine($"Turn: {gameState.CurrentTurn}");
            }
            
            // Performance info
            sb.AppendLine($"FPS: {(1f / Time.unscaledDeltaTime):F1}");
            sb.AppendLine($"Memory: {(System.GC.GetTotalMemory(false) / 1024f / 1024f):F1} MB");
            
            debugText.text = sb.ToString();
        }
        
        private void DisplayGameResult(string winner, string winCondition)
        {
            // Implement game result display
            Debug.Log($"[GameUI] Game ended - Winner: {winner}, Condition: {winCondition}");
        }
        #endregion
        
        #region Utility Methods
        public void CleanupUIComponents()
        {
            // Cleanup UI controllers - simplified for now
            Debug.Log("[GameUI] Cleaning up UI components");
            
            // TODO: Implement detailed cleanup when specific UI controllers are created
        }
        
        public T GetUIController<T>() where T : MonoBehaviour
        {
            return GetComponentInChildren<T>();
        }
        
        public void SetUIActive(bool active)
        {
            if (gameCanvas != null)
                gameCanvas.gameObject.SetActive(active);
        }
        #endregion
    }
    
    // Event classes for EventBus (Fixed: class instead of struct)
    public class GameStartedEvent
    {
        public string gameMode;
        public int player1Id;
        public int player2Id;
    }
    
    public class GameEndedEvent
    {
        public int winner;
        public string winCondition;
        public float gameDuration;
    }
    
    public class CardPlayedEvent
    {
        public Card card;
        public int player;
        public Vector3 position;
    }
    
    public class EffectTriggeredEvent
    {
        public string effectName;
        public GameObject source;
        public GameObject target;
        public float duration;
    }
}