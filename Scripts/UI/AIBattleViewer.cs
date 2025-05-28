using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using PokemonTCG.Game.AI;
using PokemonTCG.Game;
using PokemonTCG.Game.Rules;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.UI
{
    /// <summary>
    /// AI対戦観戦システム - AI同士の対戦を可視化し、統計情報を表示
    /// AIの思考過程、ゲーム進行、統計データをリアルタイムで表示
    /// </summary>
    public class AIBattleViewer : MonoBehaviour
    {
        [Header("Battle Control")]
        public Button startBattleButton;
        public Button pauseBattleButton;
        public Button stopBattleButton;
        public Button speedToggleButton;
        public Slider speedSlider;
        public TextMeshProUGUI speedLabel;
        
        [Header("AI Information")]
        public TextMeshProUGUI ai1NameText;
        public TextMeshProUGUI ai2NameText;
        public TextMeshProUGUI ai1StatusText;
        public TextMeshProUGUI ai2StatusText;
        public Image ai1Avatar;
        public Image ai2Avatar;
        public ProgressBar ai1ThinkingProgress;
        public ProgressBar ai2ThinkingProgress;
        
        [Header("Game State Display")]
        public TextMeshProUGUI gameStateText;
        public TextMeshProUGUI currentTurnText;
        public TextMeshProUGUI turnCountText;
        public TextMeshProUGUI timerText;
        public TextMeshProUGUI winConditionText;
        
        [Header("Statistics")]
        public TextMeshProUGUI ai1StatsText;
        public TextMeshProUGUI ai2StatsText;
        public TextMeshProUGUI battleStatsText;
        public LineGraph winRateGraph;
        public LineGraph performanceGraph;
        
        [Header("Action Log")]
        public ScrollRect actionLogScrollRect;
        public TextMeshProUGUI actionLogText;
        public InputField filterInput;
        public Toggle showAIThoughtsToggle;
        public Toggle showGameEventsToggle;
        
        [Header("Battle Queue")]
        public ScrollRect battleQueueScrollRect;
        public Transform battleQueueContent;
        public GameObject battleQueueItemPrefab;
        
        [Header("Visual Settings")]
        public Color ai1Color = Color.blue;
        public Color ai2Color = Color.red;
        public Color neutralColor = Color.white;
        public Color highlightColor = Color.yellow;
        
        // Private fields
        private AIManager aiManager;
        private GameStateManager gameStateManager;
        private TurnManager turnManager;
        private bool isInitialized = false;
        private string currentBattleId;
        private Dictionary<string, AIStatistics> aiStatistics = new Dictionary<string, AIStatistics>();
        private List<string> actionLog = new List<string>();
        private float gameSpeed = 1.0f;
        private bool showAIThoughts = true;
        private bool showGameEvents = true;
        private Coroutine battleUpdateCoroutine;
        
        // Battle information - Fixed: Added missing currentBattle field
        private BattleSession currentBattle;
        
        // Battle statistics
        private struct BattleStatistics
        {
            public int totalBattles;
            public int ai1Wins;
            public int ai2Wins;
            public float averageBattleDuration;
            public float averageTurnsPerBattle;
            public Dictionary<string, int> winConditions;
        }
        
        private BattleStatistics battleStats;
        
        // Battle session class - Fixed: Added missing class
        private class BattleSession
        {
            public string battleId;
            public string ai1Name;
            public string ai2Name;
            public float startTime;
            public bool isActive;
        }
        
        #region Initialization
        public void Initialize()
        {
            if (isInitialized) return;
            
            Debug.Log("[AIBattleViewer] Initializing AI Battle Viewer...");
            
            // Get service dependencies
            aiManager = ServiceLocator.Get<AIManager>();
            gameStateManager = ServiceLocator.Get<GameStateManager>();
            turnManager = ServiceLocator.Get<TurnManager>();
            
            // Setup UI components
            SetupUIComponents();
            
            // Subscribe to events
            SubscribeToEvents();
            
            // Initialize statistics
            InitializeStatistics();
            
            isInitialized = true;
            Debug.Log("[AIBattleViewer] AI Battle Viewer initialized successfully");
        }
        
        private void SetupUIComponents()
        {
            // Setup battle control buttons
            if (startBattleButton != null)
                startBattleButton.onClick.AddListener(StartBattle);
            
            if (pauseBattleButton != null)
                pauseBattleButton.onClick.AddListener(TogglePause);
            
            if (stopBattleButton != null)
                stopBattleButton.onClick.AddListener(StopBattle);
            
            if (speedToggleButton != null)
                speedToggleButton.onClick.AddListener(ToggleSpeed);
            
            if (speedSlider != null)
            {
                speedSlider.onValueChanged.AddListener(OnSpeedChanged);
                speedSlider.value = gameSpeed;
            }
            
            // Setup toggles
            if (showAIThoughtsToggle != null)
            {
                showAIThoughtsToggle.isOn = showAIThoughts;
                showAIThoughtsToggle.onValueChanged.AddListener(OnShowAIThoughtsChanged);
            }
            
            if (showGameEventsToggle != null)
            {
                showGameEventsToggle.isOn = showGameEvents;
                showGameEventsToggle.onValueChanged.AddListener(OnShowGameEventsChanged);
            }
            
            // Setup filter input
            if (filterInput != null)
                filterInput.onValueChanged.AddListener(OnFilterChanged);
            
            // Initialize displays
            UpdateBattleControlButtons();
            ClearActionLog();
        }
        
        private void InitializeStatistics()
        {
            battleStats = new BattleStatistics
            {
                totalBattles = 0,
                ai1Wins = 0,
                ai2Wins = 0,
                averageBattleDuration = 0,
                averageTurnsPerBattle = 0,
                winConditions = new Dictionary<string, int>()
            };
        }
        #endregion
        
        #region Event Management
        private void SubscribeToEvents()
        {
            // Fixed: Use EventBus for TurnManager events instead of direct static events
            EventBus.On<TurnStartedEvent>(OnTurnStarted);
            EventBus.On<TurnEndedEvent>(OnTurnEnded);
            
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged += OnGameStateChanged;
            }
            
            // Subscribe to EventBus events (Fixed: class events)
            EventBus.On<CardPlayedEvent>(OnCardPlayed);
            EventBus.On<EffectTriggeredEvent>(OnEffectTriggered);
        }
        
        private void UnsubscribeFromEvents()
        {
            // Fixed: Use EventBus for unsubscribing
            EventBus.Off<TurnStartedEvent>(OnTurnStarted);
            EventBus.Off<TurnEndedEvent>(OnTurnEnded);
            
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged -= OnGameStateChanged;
            }
            
            EventBus.Off<CardPlayedEvent>(OnCardPlayed);
            EventBus.Off<EffectTriggeredEvent>(OnEffectTriggered);
        }
        #endregion
        
        #region Event Handlers
        private void OnBattleStarted(string battleId)
        {
            currentBattleId = battleId;
            currentBattle = new BattleSession
            {
                battleId = battleId,
                ai1Name = "AI Player 1",
                ai2Name = "AI Player 2",
                startTime = Time.time,
                isActive = true
            };
            
            LogAction($"🎮 Battle Started: {battleId}", AILogType.System);
            
            UpdateAIInformation();
            UpdateBattleControlButtons();
            
            // Start battle update coroutine
            if (battleUpdateCoroutine != null) StopCoroutine(battleUpdateCoroutine);
            battleUpdateCoroutine = StartCoroutine(BattleUpdateLoop());
        }
        
        private void OnBattleEnded(string battleId, string winner, string winCondition)
        {
            LogAction($"🏆 Battle Ended: {winner} wins by {winCondition}", AILogType.System);
            
            UpdateStatistics(battleId, winner, winCondition);
            UpdateBattleControlButtons();
            
            // Stop battle update coroutine
            if (battleUpdateCoroutine != null)
            {
                StopCoroutine(battleUpdateCoroutine);
                battleUpdateCoroutine = null;
            }
            
            currentBattleId = null;
            if (currentBattle != null)
            {
                currentBattle.isActive = false;
            }
        }
        
        private void OnAIActionExecuted(string aiName, string actionType, string actionDescription)
        {
            Color aiColor = GetAIColor(aiName);
            LogAction($"🤖 {aiName}: {actionType} - {actionDescription}", AILogType.AIAction, aiColor);
        }
        
        private void OnAIThinkingStarted(string aiName, string thinkingType)
        {
            if (showAIThoughts)
            {
                LogAction($"💭 {aiName} thinking: {thinkingType}...", AILogType.AIThought);
            }
            
            UpdateAIThinkingProgress(aiName, 0f, true);
        }
        
        private void OnAIThinkingCompleted(string aiName, string decision, float thinkingTime)
        {
            if (showAIThoughts)
            {
                LogAction($"💡 {aiName} decided: {decision} ({thinkingTime:F2}s)", AILogType.AIThought);
            }
            
            UpdateAIThinkingProgress(aiName, 1f, false);
        }
        
        private void OnGameStateChanged(GameState newState)
        {
            if (showGameEvents)
            {
                LogAction($"🎯 Game State: → {newState.CurrentPhase}", AILogType.GameEvent);
            }
            
            UpdateGameStateDisplay();
        }
        
        private void OnTurnStarted(TurnStartedEvent evt)
        {
            if (showGameEvents)
            {
                string playerName = GetPlayerName(evt.PlayerId);
                LogAction($"▶️ Turn {evt.TurnNumber}: {playerName}", AILogType.GameEvent);
            }
            
            UpdateTurnDisplay();
        }
        
        private void OnTurnEnded(TurnEndedEvent evt)
        {
            if (showGameEvents)
            {
                string playerName = GetPlayerName(evt.PlayerId);
                LogAction($"⏹️ Turn {evt.TurnNumber} ended: {playerName}", AILogType.GameEvent);
            }
        }
        
        private void OnCardPlayed(CardPlayedEvent eventData)
        {
            if (showGameEvents)
            {
                LogAction($"🃏 Card Played: {eventData.card.CardData.CardName} by Player {eventData.player}", AILogType.GameEvent);
            }
        }
        
        private void OnEffectTriggered(EffectTriggeredEvent eventData)
        {
            if (showGameEvents)
            {
                LogAction($"✨ Effect: {eventData.effectName} ({eventData.source.name} → {eventData.target.name})", AILogType.GameEvent);
            }
        }
        #endregion
        
        #region UI Updates
        private void UpdateAIInformation()
        {
            if (string.IsNullOrEmpty(currentBattleId)) return;
            
            // Update AI names - simplified for now
            if (ai1NameText != null) ai1NameText.text = currentBattle?.ai1Name ?? "AI Player 1";
            if (ai2NameText != null) ai2NameText.text = currentBattle?.ai2Name ?? "AI Player 2";
            
            // Update AI status
            UpdateAIStatus();
            
            // Update avatars
            UpdateAIAvatars();
        }
        
        private void UpdateAIStatus()
        {
            if (string.IsNullOrEmpty(currentBattleId)) return;
            
            // Get AI status from AIManager - simplified for now
            string ai1Status = "Active";
            string ai2Status = "Active";
            
            if (ai1StatusText != null) ai1StatusText.text = ai1Status;
            if (ai2StatusText != null) ai2StatusText.text = ai2Status;
        }
        
        private void UpdateAIAvatars()
        {
            // Set AI avatar colors
            if (ai1Avatar != null) ai1Avatar.color = ai1Color;
            if (ai2Avatar != null) ai2Avatar.color = ai2Color;
        }
        
        private void UpdateAIThinkingProgress(string aiName, float progress, bool thinking)
        {
            ProgressBar progressBar = null;
            
            // Simplified - we don't have battle session to determine which AI
            if (aiName.Contains("1") || aiName.Contains("AI1"))
                progressBar = ai1ThinkingProgress;
            else if (aiName.Contains("2") || aiName.Contains("AI2"))
                progressBar = ai2ThinkingProgress;
            
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(thinking);
                progressBar.SetProgress(progress);
            }
        }
        
        private void UpdateGameStateDisplay()
        {
            if (gameStateText != null && gameStateManager != null && gameStateManager.CurrentGameState != null)
            {
                gameStateText.text = $"State: {gameStateManager.CurrentGameState.CurrentPhase}";
            }
        }
        
        private void UpdateTurnDisplay()
        {
            if (gameStateManager != null && gameStateManager.CurrentGameState != null)
            {
                var gameState = gameStateManager.CurrentGameState;
                if (currentTurnText != null)
                    currentTurnText.text = $"Current: {gameState.CurrentPlayerId}";
                
                if (turnCountText != null)
                    turnCountText.text = $"Turn: {gameState.CurrentTurn}";
            }
        }
        
        private void UpdateStatisticsDisplay()
        {
            UpdateAIStatistics();
            UpdateBattleStatistics();
            UpdateGraphs();
        }
        
        private void UpdateAIStatistics()
        {
            var sb1 = new StringBuilder();
            var sb2 = new StringBuilder();
            
            if (currentBattle != null)
            {
                // AI 1 statistics
                if (aiStatistics.ContainsKey(currentBattle.ai1Name))
                {
                    var stats = aiStatistics[currentBattle.ai1Name];
                    sb1.AppendLine($"Wins: {stats.wins}");
                    sb1.AppendLine($"Losses: {stats.losses}");
                    sb1.AppendLine($"Win Rate: {stats.winRate:P1}");
                    sb1.AppendLine($"Avg Think Time: {stats.averageThinkTime:F2}s");
                }
                
                // AI 2 statistics
                if (aiStatistics.ContainsKey(currentBattle.ai2Name))
                {
                    var stats = aiStatistics[currentBattle.ai2Name];
                    sb2.AppendLine($"Wins: {stats.wins}");
                    sb2.AppendLine($"Losses: {stats.losses}");
                    sb2.AppendLine($"Win Rate: {stats.winRate:P1}");
                    sb2.AppendLine($"Avg Think Time: {stats.averageThinkTime:F2}s");
                }
            }
            
            if (ai1StatsText != null) ai1StatsText.text = sb1.ToString();
            if (ai2StatsText != null) ai2StatsText.text = sb2.ToString();
        }
        
        private void UpdateBattleStatistics()
        {
            if (battleStatsText != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Total Battles: {battleStats.totalBattles}");
                sb.AppendLine($"AI1 Wins: {battleStats.ai1Wins}");
                sb.AppendLine($"AI2 Wins: {battleStats.ai2Wins}");
                sb.AppendLine($"Avg Duration: {battleStats.averageBattleDuration:F1}s");
                sb.AppendLine($"Avg Turns: {battleStats.averageTurnsPerBattle:F1}");
                
                battleStatsText.text = sb.ToString();
            }
        }
        
        private void UpdateGraphs()
        {
            // Update win rate graph
            if (winRateGraph != null && currentBattle != null)
            {
                var ai1WinRate = aiStatistics.ContainsKey(currentBattle.ai1Name) ? 
                    aiStatistics[currentBattle.ai1Name].winRate : 0f;
                var ai2WinRate = aiStatistics.ContainsKey(currentBattle.ai2Name) ? 
                    aiStatistics[currentBattle.ai2Name].winRate : 0f;
                
                winRateGraph.AddDataPoint(ai1WinRate, ai1Color);
                winRateGraph.AddDataPoint(ai2WinRate, ai2Color);
            }
        }
        
        private void UpdateBattleControlButtons()
        {
            bool battleActive = currentBattle != null && currentBattle.isActive;
            
            if (startBattleButton != null)
                startBattleButton.interactable = !battleActive;
            
            if (pauseBattleButton != null)
                pauseBattleButton.interactable = battleActive;
            
            if (stopBattleButton != null)
                stopBattleButton.interactable = battleActive;
        }
        #endregion
        
        #region Action Logging
        private enum AILogType
        {
            System,
            AIAction,
            AIThought,
            GameEvent,
            Error
        }
        
        private void LogAction(string message, AILogType logType, Color? color = null)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";
            
            actionLog.Add(logEntry);
            
            // Limit log size
            if (actionLog.Count > 1000)
            {
                actionLog.RemoveAt(0);
            }
            
            // Update log display
            UpdateActionLogDisplay();
        }
        
        private void UpdateActionLogDisplay()
        {
            if (actionLogText == null) return;
            
            var sb = new StringBuilder();
            var filteredLog = FilterActionLog();
            
            foreach (var entry in filteredLog)
            {
                sb.AppendLine(entry);
            }
            
            actionLogText.text = sb.ToString();
            
            // Auto-scroll to bottom
            if (actionLogScrollRect != null)
            {
                StartCoroutine(ScrollToBottom());
            }
        }
        
        private List<string> FilterActionLog()
        {
            var filtered = new List<string>();
            string filter = filterInput?.text?.ToLower() ?? "";
            
            foreach (var entry in actionLog)
            {
                if (string.IsNullOrEmpty(filter) || entry.ToLower().Contains(filter))
                {
                    filtered.Add(entry);
                }
            }
            
            return filtered;
        }
        
        private IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame();
            actionLogScrollRect.normalizedPosition = new Vector2(0, 0);
        }
        
        private void ClearActionLog()
        {
            actionLog.Clear();
            if (actionLogText != null) actionLogText.text = "";
        }
        #endregion
        
        #region Battle Control
        private void StartBattle()
        {
            if (aiManager != null)
            {
                // Start a default battle or show battle setup UI
                LogAction("🚀 Starting AI Battle...", AILogType.System);
                // Simulate battle start
                OnBattleStarted(System.Guid.NewGuid().ToString());
                Debug.Log("[AIBattleViewer] Battle start requested");
            }
        }
        
        private void TogglePause()
        {
            if (!string.IsNullOrEmpty(currentBattleId))
            {
                // Toggle battle pause state
                LogAction("⏸️ Battle Paused/Resumed", AILogType.System);
                // TODO: Implement pause logic in AIManager
            }
        }
        
        private void StopBattle()
        {
            if (!string.IsNullOrEmpty(currentBattleId) && currentBattle != null)
            {
                LogAction("🛑 Battle Stopped", AILogType.System);
                OnBattleEnded(currentBattleId, "System", "Manual Stop");
            }
        }
        
        private void ToggleSpeed()
        {
            gameSpeed = gameSpeed == 1.0f ? 2.0f : gameSpeed == 2.0f ? 0.5f : 1.0f;
            Time.timeScale = gameSpeed;
            
            if (speedSlider != null) speedSlider.value = gameSpeed;
            UpdateSpeedLabel();
        }
        
        private void OnSpeedChanged(float value)
        {
            gameSpeed = value;
            Time.timeScale = gameSpeed;
            UpdateSpeedLabel();
        }
        
        private void UpdateSpeedLabel()
        {
            if (speedLabel != null)
            {
                speedLabel.text = $"Speed: {gameSpeed:F1}x";
            }
        }
        
        private void OnShowAIThoughtsChanged(bool value)
        {
            showAIThoughts = value;
        }
        
        private void OnShowGameEventsChanged(bool value)
        {
            showGameEvents = value;
        }
        
        private void OnFilterChanged(string filter)
        {
            UpdateActionLogDisplay();
        }
        #endregion
        
        #region Statistics Management
        private void UpdateStatistics(string battleId, string winner, string winCondition)
        {
            battleStats.totalBattles++;
            
            if (winner.Contains("1") || winner.Contains("AI1"))
                battleStats.ai1Wins++;
            else if (winner.Contains("2") || winner.Contains("AI2"))
                battleStats.ai2Wins++;
            
            // Update win condition statistics
            if (!battleStats.winConditions.ContainsKey(winCondition))
                battleStats.winConditions[winCondition] = 0;
            battleStats.winConditions[winCondition]++;
            
            // Update individual AI statistics
            UpdateAIStatisticsSingle("AI1", winner.Contains("1") || winner.Contains("AI1"));
            UpdateAIStatisticsSingle("AI2", winner.Contains("2") || winner.Contains("AI2"));
            
            // Update display
            UpdateStatisticsDisplay();
        }
        
        private void UpdateAIStatisticsSingle(string aiName, bool won)
        {
            if (!aiStatistics.ContainsKey(aiName))
            {
                aiStatistics[aiName] = new AIStatistics();
            }
            
            var stats = aiStatistics[aiName];
            stats.totalBattles++;
            
            if (won)
                stats.wins++;
            else
                stats.losses++;
            
            stats.winRate = (float)stats.wins / stats.totalBattles;
            
            aiStatistics[aiName] = stats;
        }
        
        private struct AIStatistics
        {
            public int totalBattles;
            public int wins;
            public int losses;
            public float winRate;
            public float averageThinkTime;
            public float totalThinkTime;
            public int thinkingCount;
        }
        #endregion
        
        #region Battle Update Loop
        private IEnumerator BattleUpdateLoop()
        {
            while (!string.IsNullOrEmpty(currentBattleId))
            {
                UpdateAIStatus();
                UpdateGameStateDisplay();
                UpdateTurnDisplay();
                UpdateStatisticsDisplay();
                
                yield return new WaitForSeconds(0.5f);
            }
        }
        #endregion
        
        #region Utility Methods
        private Color GetAIColor(string aiName)
        {
            if (aiName.Contains("1") || aiName.Contains("AI1")) return ai1Color;
            if (aiName.Contains("2") || aiName.Contains("AI2")) return ai2Color;
            return neutralColor;
        }
        
        private string GetPlayerName(string playerId)
        {
            return $"Player {playerId}";
        }
        #endregion
        
        #region Cleanup
        public void Cleanup()
        {
            UnsubscribeFromEvents();
            
            if (battleUpdateCoroutine != null)
            {
                StopCoroutine(battleUpdateCoroutine);
                battleUpdateCoroutine = null;
            }
            
            isInitialized = false;
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
        #endregion
    }
    
    // Helper classes for UI components
    [System.Serializable]
    public class ProgressBar : MonoBehaviour
    {
        public Image fillImage;
        public TextMeshProUGUI progressText;
        
        public void SetProgress(float progress)
        {
            if (fillImage != null)
                fillImage.fillAmount = progress;
            
            if (progressText != null)
                progressText.text = $"{progress:P0}";
        }
    }
    
    [System.Serializable]
    public class LineGraph : MonoBehaviour
    {
        public List<Vector2> dataPoints = new List<Vector2>();
        public LineRenderer lineRenderer;
        
        public void AddDataPoint(float value, Color color)
        {
            dataPoints.Add(new Vector2(dataPoints.Count, value));
            UpdateGraph();
        }
        
        private void UpdateGraph()
        {
            if (lineRenderer != null && dataPoints.Count > 1)
            {
                lineRenderer.positionCount = dataPoints.Count;
                for (int i = 0; i < dataPoints.Count; i++)
                {
                    lineRenderer.SetPosition(i, new Vector3(dataPoints[i].x, dataPoints[i].y, 0));
                }
            }
        }
    }

    // Event classes for TurnManager
    public class TurnStartedEvent
    {
        public int TurnNumber { get; set; }
        public string PlayerId { get; set; }
        public int PlayerIndex { get; set; }
    }
    
    public class TurnEndedEvent
    {
        public int TurnNumber { get; set; }
        public string PlayerId { get; set; }
        public float TurnDuration { get; set; }
        public int ActionsExecuted { get; set; }
    }
}