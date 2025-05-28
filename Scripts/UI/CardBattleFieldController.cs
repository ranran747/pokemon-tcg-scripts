using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;
using PokemonTCG.Game;
using PokemonTCG.Game.Rules;
using PokemonTCG.UI;

namespace PokemonTCG.UI
{
    /// <summary>
    /// Phase 5 - カードバトルフィールド統合制御システム（安定版）
    /// 実戦対戦・AI対戦・ゲーム進行とビジュアル同期の核心
    /// コンソール不具合修正版
    /// </summary>
    public class CardBattleFieldController : MonoBehaviour
    {
        #region UI References

        [Header("=== Battle Field UI References ===")]
        public FieldUIController fieldUIController;
        public Transform player1Area;
        public Transform player2Area;
        
        [Header("=== Battle Information Display ===")]
        public Text battleStatusText;
        public Text currentPhaseText;    
        public Text currentPlayerText;
        public Button nextPhaseButton;
        public Button endTurnButton;

        [Header("=== Card Areas ===")]
        public Transform cardInfoArea;
        public Transform duelLogArea;
        public Text cardInfoText;
        public Text duelLogText;

        [Header("=== Debug Controls ===")]
        public Button startBattleButton;
        public Button autoPlayButton;
        public Toggle aiVsAiToggle;
        public Slider gameSpeedSlider;

        #endregion

        #region Private Fields

        // System References
        private GameStateManager gameStateManager;
        private bool isInitialized = false;

        // Battle State
        private GameState currentGameState;
        private bool battleInProgress = false;
        private bool isAutoPlay = false;
        private float gameSpeed = 1.0f;

        // Players and Decks
        private Dictionary<string, List<Card>> playerDecks = new Dictionary<string, List<Card>>();
        private Dictionary<string, List<Card>> playerHands = new Dictionary<string, List<Card>>();
        private Dictionary<string, List<Card>> playerFields = new Dictionary<string, List<Card>>();
        private Dictionary<string, Card> activePokemon = new Dictionary<string, Card>();

        // Sample Cards (けつばん & デバッグエネルギー)
        private PokemonCardData ketsbanData;
        private EnergyCardData debugEnergyData;

        // Battle Log
        private List<string> battleLog = new List<string>();
        private const int MAX_LOG_ENTRIES = 20;

        // Animation
        private List<Tween> activeTweens = new List<Tween>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeReferences();
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (battleInProgress && isAutoPlay)
            {
                ProcessAutoPlay();
            }
        }

        #endregion

        #region Initialization

        private void InitializeReferences()
        {
            // UI参照の自動検出
            if (fieldUIController == null)
                fieldUIController = FindObjectOfType<FieldUIController>();

            if (player1Area == null)
                player1Area = GameObject.Find("Player1Area")?.transform;

            if (player2Area == null)
                player2Area = GameObject.Find("Player2Area")?.transform;
        }

        public void Initialize()
        {
            if (isInitialized) return;

            try
            {
                Debug.Log("[CardBattleFieldController] Initializing Battle System...");

                // System References
                gameStateManager = ServiceLocator.Get<GameStateManager>();

                // Create Sample Cards
                CreateSampleCards();

                // Setup UI Events
                SetupUIEvents();

                // Subscribe to Events
                SubscribeToEvents();

                // Initialize Field UI
                if (fieldUIController != null)
                {
                    fieldUIController.Initialize();
                }

                isInitialized = true;
                Debug.Log("[CardBattleFieldController] Battle System initialized successfully");

                // 初期UI更新
                UpdateBattleUI();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Initialization failed: {ex.Message}");
            }
        }

        private void SetupUIEvents()
        {
            try
            {
                if (nextPhaseButton != null)
                {
                    nextPhaseButton.onClick.RemoveAllListeners();
                    nextPhaseButton.onClick.AddListener(NextPhase);
                }

                if (endTurnButton != null)
                {
                    endTurnButton.onClick.RemoveAllListeners();
                    endTurnButton.onClick.AddListener(EndTurn);
                }

                if (startBattleButton != null)
                {
                    startBattleButton.onClick.RemoveAllListeners();
                    startBattleButton.onClick.AddListener(StartDebugBattle);
                }

                if (autoPlayButton != null)
                {
                    autoPlayButton.onClick.RemoveAllListeners();
                    autoPlayButton.onClick.AddListener(ToggleAutoPlay);
                }

                if (gameSpeedSlider != null)
                {
                    gameSpeedSlider.onValueChanged.RemoveAllListeners();
                    gameSpeedSlider.onValueChanged.AddListener(OnGameSpeedChanged);
                    gameSpeedSlider.value = 1.0f;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] UI setup failed: {ex.Message}");
            }
        }

        private void SubscribeToEvents()
        {
            try
            {
                if (gameStateManager != null)
                {
                    gameStateManager.OnGameStateChanged += OnGameStateChanged;
                    gameStateManager.OnPhaseChanged += OnPhaseChanged;
                    gameStateManager.OnTurnChanged += OnTurnChanged;
                }

                EventBus.On<CardPlayedEvent>(OnCardPlayed);
                EventBus.On<PokemonAttackedEvent>(OnPokemonAttacked);
                EventBus.On<EnergyAttachedEvent>(OnEnergyAttached);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Event subscription failed: {ex.Message}");
            }
        }

        #endregion

        #region Sample Card Creation

        private void CreateSampleCards()
        {
            try
            {
                CreateKetsbanCard();
                CreateDebugEnergyCard();
                Debug.Log("[CardBattleFieldController] Sample cards created successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Sample card creation failed: {ex.Message}");
            }
        }

        private void CreateKetsbanCard()
        {
            ketsbanData = ScriptableObject.CreateInstance<PokemonCardData>();
            
            // けつばんの基本設定
            ketsbanData.name = "Ketsban";
            var cardNameField = typeof(BaseCardData).GetField("_cardName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cardNameField?.SetValue(ketsbanData, "けつばん");
            
            var cardIdField = typeof(BaseCardData).GetField("_cardID", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cardIdField?.SetValue(ketsbanData, "DEBUG_001");

            // ポケモン設定
            var hpField = typeof(PokemonCardData).GetField("_hp", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hpField?.SetValue(ketsbanData, 60);

            var pokemonTypeField = typeof(PokemonCardData).GetField("_pokemonType", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            pokemonTypeField?.SetValue(ketsbanData, PokemonType.Colorless);

            var evolutionStageField = typeof(PokemonCardData).GetField("_evolutionStage", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            evolutionStageField?.SetValue(evolutionStageField, EvolutionStage.Basic);

            // デバッグ攻撃の作成
            var debugAttack = new PokemonAttack();
            var attackNameField = typeof(PokemonAttack).GetField("_attackName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            attackNameField?.SetValue(debugAttack, "デバッグアタック");

            var energyCostField = typeof(PokemonAttack).GetField("_energyCost", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            energyCostField?.SetValue(debugAttack, new List<PokemonType> { PokemonType.Colorless });

            var damageField = typeof(PokemonAttack).GetField("_damage", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            damageField?.SetValue(debugAttack, 30);

            var attacksField = typeof(PokemonCardData).GetField("_attacks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            attacksField?.SetValue(ketsbanData, new List<PokemonAttack> { debugAttack });
        }

        private void CreateDebugEnergyCard()
        {
            debugEnergyData = ScriptableObject.CreateInstance<EnergyCardData>();
            
            // デバッグエネルギーの基本設定
            debugEnergyData.name = "DebugEnergy";
            var cardNameField = typeof(BaseCardData).GetField("_cardName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cardNameField?.SetValue(debugEnergyData, "デバッグエネルギー");
            
            var cardIdField = typeof(BaseCardData).GetField("_cardID", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cardIdField?.SetValue(debugEnergyData, "DEBUG_002");

            // エネルギー設定
            var energyTypeField = typeof(EnergyCardData).GetField("_energyType", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            energyTypeField?.SetValue(debugEnergyData, EnergyType.Basic);

            var providedTypesField = typeof(EnergyCardData).GetField("_providedTypes", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            providedTypesField?.SetValue(debugEnergyData, new List<PokemonType> { PokemonType.Colorless });

            var energyValueField = typeof(EnergyCardData).GetField("_energyValue", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            energyValueField?.SetValue(debugEnergyData, 1);
        }

        #endregion

        #region Deck Creation

        private void CreateDebugDecks()
        {
            try
            {
                playerDecks.Clear();
                
                // Player1のデッキ（けつばん30枚、デバッグエネルギー30枚）
                var player1Deck = new List<Card>();
                
                // けつばん30枚
                for (int i = 0; i < 30; i++)
                {
                    var cardObj = new GameObject($"Ketsban_{i}");
                    var card = cardObj.AddComponent<Card>();
                    card.Initialize(ketsbanData, "Player1", i);
                    player1Deck.Add(card);
                }
                
                // デバッグエネルギー30枚
                for (int i = 0; i < 30; i++)
                {
                    var cardObj = new GameObject($"DebugEnergy_{i}");
                    var card = cardObj.AddComponent<Card>();
                    card.Initialize(debugEnergyData, "Player1", i + 30);
                    player1Deck.Add(card);
                }
                
                playerDecks["Player1"] = player1Deck;

                // Player2のデッキ（同じ構成）
                var player2Deck = new List<Card>();
                
                for (int i = 0; i < 30; i++)
                {
                    var cardObj = new GameObject($"Ketsban_{i}_P2");
                    var card = cardObj.AddComponent<Card>();
                    card.Initialize(ketsbanData, "Player2", i + 1000);
                    player2Deck.Add(card);
                }
                
                for (int i = 0; i < 30; i++)
                {
                    var cardObj = new GameObject($"DebugEnergy_{i}_P2");
                    var card = cardObj.AddComponent<Card>();
                    card.Initialize(debugEnergyData, "Player2", i + 1030);
                    player2Deck.Add(card);
                }
                
                playerDecks["Player2"] = player2Deck;

                Debug.Log("[CardBattleFieldController] Debug decks created successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Deck creation failed: {ex.Message}");
            }
        }

        #endregion

        #region Battle Control

        public void StartDebugBattle()
        {
            if (battleInProgress)
            {
                Debug.LogWarning("[CardBattleFieldController] Battle already in progress");
                return;
            }

            try
            {
                Debug.Log("[CardBattleFieldController] Starting けつばん Battle");

                // Create Debug Decks
                CreateDebugDecks();

                // Initialize Game State
                var playerIds = new List<string> { "Player1", "Player2" };
                var classicRule = ScriptableObject.CreateInstance<ClassicTCGRule>();
                
                if (gameStateManager != null)
                {
                    bool gameStarted = gameStateManager.StartNewGame(classicRule, playerIds);
                    if (gameStarted)
                    {
                        currentGameState = gameStateManager.CurrentGameState;
                        battleInProgress = true;
                        
                        // 初期手札配布
                        DealInitialHands();
                        
                        // 最初のけつばんをバトル場に
                        SetupInitialBattleField();
                        
                        UpdateBattleUI();
                        AddToBattleLog("けつばんバトル開始");
                        AddToBattleLog("Player1のターン開始");
                    }
                    else
                    {
                        Debug.LogError("[CardBattleFieldController] Failed to start game");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Battle start failed: {ex.Message}");
            }
        }

        private void DealInitialHands()
        {
            playerHands.Clear();
            playerFields.Clear();

            foreach (var playerId in playerDecks.Keys)
            {
                var deck = playerDecks[playerId];
                var hand = new List<Card>();
                
                // 初期手札7枚
                for (int i = 0; i < 7; i++)
                {
                    if (deck.Count > 0)
                    {
                        var card = deck[0];
                        deck.RemoveAt(0);
                        hand.Add(card);
                        card.SetZone(CardZone.Hand);
                    }
                }
                
                playerHands[playerId] = hand;
                playerFields[playerId] = new List<Card>();
            }
        }

        private void SetupInitialBattleField()
        {
            foreach (var playerId in playerHands.Keys)
            {
                var hand = playerHands[playerId];
                
                // 手札からけつばんを探してバトル場に置く
                var ketsban = hand.FirstOrDefault(c => c.CardData is PokemonCardData);
                if (ketsban != null)
                {
                    hand.Remove(ketsban);
                    ketsban.SetZone(CardZone.Active);
                    activePokemon[playerId] = ketsban;
                    
                    AddToBattleLog($"{playerId}のけつばんがバトル場に登場");
                }
            }
        }

        public void NextPhase()
        {
            if (!battleInProgress || currentGameState == null) return;

            try
            {
                var currentPhase = currentGameState.CurrentPhase;
                GamePhase nextPhase;

                switch (currentPhase)
                {
                    case GamePhase.Setup:
                        nextPhase = GamePhase.DrawPhase;
                        break;
                    case GamePhase.DrawPhase:
                        nextPhase = GamePhase.MainPhase;
                        DrawCard(currentGameState.CurrentPlayerId);
                        break;
                    case GamePhase.MainPhase:
                        nextPhase = GamePhase.AttackPhase;
                        break;
                    case GamePhase.AttackPhase:
                        nextPhase = GamePhase.DrawPhase;
                        ExecuteAIAction(currentGameState.CurrentPlayerId);
                        break;
                    default:
                        nextPhase = GamePhase.MainPhase;
                        break;
                }

                if (nextPhase == GamePhase.DrawPhase && currentPhase == GamePhase.AttackPhase)
                {
                    EndTurn();
                }
                else
                {
                    gameStateManager.ChangePhase(nextPhase);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Phase change failed: {ex.Message}");
            }
        }

        public void EndTurn() 
        {
            if (!battleInProgress || currentGameState == null) return;

            try
            {
                string currentPlayer = currentGameState.CurrentPlayerId;
                string nextPlayer = currentPlayer == "Player1" ? "Player2" : "Player1";
                
                AddToBattleLog($"{currentPlayer}のターン終了");
                gameStateManager.AdvanceTurn(nextPlayer);
                gameStateManager.ChangePhase(GamePhase.DrawPhase);
                AddToBattleLog($"{nextPlayer}のターン開始");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Turn end failed: {ex.Message}");
            }
        }

        #endregion

        #region AI Actions

        private void ExecuteAIAction(string playerId)
        {
            if (!battleInProgress) return;

            try
            {
                // シンプルAI戦略: エネルギーをつけて攻撃
                var hand = playerHands.GetValueOrDefault(playerId, new List<Card>());
                var activePokemon = this.activePokemon.GetValueOrDefault(playerId);

                if (activePokemon != null)
                {
                    // 1. エネルギーをつける
                    var energy = hand.FirstOrDefault(c => c.CardData is EnergyCardData);
                    if (energy != null)
                    {
                        AttachEnergy(playerId, energy, activePokemon);
                    }

                    // 2. 攻撃する
                    AttackWithActivePokemon(playerId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] AI action failed: {ex.Message}");
            }
        }

        private void AttachEnergy(string playerId, Card energy, Card pokemon)
        {
            var hand = playerHands[playerId];
            hand.Remove(energy);
            energy.SetZone(CardZone.Attached);

            AddToBattleLog($"{playerId}がエネルギーを付与");
            
            EventBus.Emit(new EnergyAttachedEvent
            {
                PlayerId = playerId,
                EnergyCard = energy,
                TargetPokemon = pokemon
            });
        }

        private void AttackWithActivePokemon(string playerId)
        {
            var attackingPokemon = activePokemon.GetValueOrDefault(playerId);
            if (attackingPokemon?.CardData is PokemonCardData pokemonData)
            {
                string opponentId = playerId == "Player1" ? "Player2" : "Player1";
                var defendingPokemon = activePokemon.GetValueOrDefault(opponentId);

                if (defendingPokemon != null && pokemonData.Attacks.Count > 0)
                {
                    var attack = pokemonData.Attacks[0];
                    int damage = attack.Damage;

                    AddToBattleLog($"{playerId}の攻撃! ダメージ: {damage}");
                    
                    EventBus.Emit(new PokemonAttackedEvent
                    {
                        AttackerId = playerId,
                        AttackingPokemon = attackingPokemon,
                        DefendingPokemon = defendingPokemon,
                        Damage = damage
                    });

                    // 簡易勝利判定
                    CheckForVictory(opponentId, damage);
                }
            }
        }

        private void DrawCard(string playerId)
        {
            var deck = playerDecks.GetValueOrDefault(playerId);
            var hand = playerHands.GetValueOrDefault(playerId);

            if (deck != null && hand != null && deck.Count > 0)
            {
                var card = deck[0];
                deck.RemoveAt(0);
                hand.Add(card);
                card.SetZone(CardZone.Hand);

                AddToBattleLog($"{playerId}がドロー");
            }
        }

        private void CheckForVictory(string defendingPlayerId, int damage)
        {
            // 簡易勝利判定: 5回攻撃を受けると負け
            if (damage >= 30)
            {
                string winnerId = defendingPlayerId == "Player1" ? "Player2" : "Player1";
                AddToBattleLog($"{winnerId}の勝利!");
                EndBattle(winnerId);
            }
        }

        #endregion

        #region Auto Play

        public void ToggleAutoPlay()
        {
            isAutoPlay = !isAutoPlay;
            AddToBattleLog(isAutoPlay ? "オートプレイ開始" : "オートプレイ停止");
        }

        private void ProcessAutoPlay()
        {
            if (!battleInProgress || currentGameState == null) return;

            // オートプレイの間隔を1秒に調整（観戦しやすく）
            if (Time.time % (1.0f / gameSpeed) < Time.deltaTime)
            {
                NextPhase();
            }
        }

        private void OnGameSpeedChanged(float value)
        {
            gameSpeed = Mathf.Clamp(value, 0.1f, 5.0f);
            Time.timeScale = gameSpeed;
        }

        #endregion

        #region Battle End

        private void EndBattle(string winnerId)
        {
            if (!battleInProgress) return;

            try
            {
                battleInProgress = false;
                isAutoPlay = false;
                
                if (gameStateManager != null)
                {
                    gameStateManager.EndGame(winnerId, VictoryReason.KnockOut);
                }

                AddToBattleLog("バトル終了");
                UpdateBattleUI();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Battle end failed: {ex.Message}");
            }
        }

        #endregion

        #region UI Updates

        private void UpdateBattleUI()
        {
            try
            {
                if (battleStatusText != null)
                {
                    string status = battleInProgress ? "バトル中" : "待機中";
                    battleStatusText.text = $"Status: {status}";
                }

                if (currentPhaseText != null && currentGameState != null)
                {
                    currentPhaseText.text = $"Phase: {currentGameState.CurrentPhase}";
                }

                if (currentPlayerText != null && currentGameState != null)
                {
                    currentPlayerText.text = $"Current: {currentGameState.CurrentPlayerId}";
                }

                UpdateCardInfo();
                UpdateDuelLog();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] UI update failed: {ex.Message}");
            }
        }

        private void UpdateCardInfo()
        {
            if (cardInfoText == null) return;

            try
            {
                string info = "=== Battle Info ===\n";
                
                foreach (var kvp in activePokemon)
                {
                    var pokemon = kvp.Value;
                    if (pokemon?.CardData != null)
                    {
                        info += $"{kvp.Key}: {pokemon.CardData.CardName}\n";
                    }
                }

                foreach (var kvp in playerHands)
                {
                    info += $"{kvp.Key} Hand: {kvp.Value.Count} cards\n";
                }

                foreach (var kvp in playerDecks)
                {
                    info += $"{kvp.Key} Deck: {kvp.Value.Count} cards\n";
                }

                cardInfoText.text = info;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Card info update failed: {ex.Message}");
            }
        }

        private void UpdateDuelLog()
        {
            if (duelLogText == null) return;

            try
            {
                string logText = string.Join("\n", battleLog.TakeLast(10));
                duelLogText.text = logText;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Duel log update failed: {ex.Message}");
            }
        }

        private void AddToBattleLog(string message)
        {
            try
            {
                string timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
                battleLog.Add(timestampedMessage);
                
                // ログサイズ制限
                while (battleLog.Count > MAX_LOG_ENTRIES)
                {
                    battleLog.RemoveAt(0);
                }

                UpdateDuelLog();
                
                // 安全なログ出力
                Debug.Log($"[Battle] {message}");
            }
            catch (Exception ex)
            {
                // ログ出力でエラーが起きても安全に処理
                Debug.LogError($"[CardBattleFieldController] Log failed: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnGameStateChanged(GameState newGameState)
        {
            currentGameState = newGameState;
            UpdateBattleUI();
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            AddToBattleLog($"フェーズ: {oldPhase} → {newPhase}");
            UpdateBattleUI();
        }

        private void OnTurnChanged(string previousPlayer, string currentPlayer)
        {
            UpdateBattleUI();
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            AddToBattleLog($"{evt.PlayerId}がカードプレイ");
        }

        private void OnPokemonAttacked(PokemonAttackedEvent evt)
        {
            AddToBattleLog($"攻撃処理完了");
        }

        private void OnEnergyAttached(EnergyAttachedEvent evt)
        {
            AddToBattleLog($"エネルギー付与完了");
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            try
            {
                // Unsubscribe from events
                if (gameStateManager != null)
                {
                    gameStateManager.OnGameStateChanged -= OnGameStateChanged;
                    gameStateManager.OnPhaseChanged -= OnPhaseChanged;
                    gameStateManager.OnTurnChanged -= OnTurnChanged;
                }

                EventBus.Off<CardPlayedEvent>(OnCardPlayed);
                EventBus.Off<PokemonAttackedEvent>(OnPokemonAttacked);
                EventBus.Off<EnergyAttachedEvent>(OnEnergyAttached);

                // Stop animations
                foreach (var tween in activeTweens)
                {
                    if (tween != null && tween.IsActive())
                    {
                        tween.Kill();
                    }
                }
                activeTweens.Clear();

                // Reset time scale
                Time.timeScale = 1.0f;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardBattleFieldController] Cleanup failed: {ex.Message}");
            }
        }

        #endregion
    }

    #region Event Classes

    public class CardPlayedEvent
    {
        public string PlayerId { get; set; }
        public Card Card { get; set; }
    }

    public class PokemonAttackedEvent
    {
        public string AttackerId { get; set; }
        public Card AttackingPokemon { get; set; }
        public Card DefendingPokemon { get; set; }
        public int Damage { get; set; }
    }

    public class EnergyAttachedEvent
    {
        public string PlayerId { get; set; }
        public Card EnergyCard { get; set; }
        public Card TargetPokemon { get; set; }
    }

    #endregion
}