using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Game;
using PokemonTCG.Game.Rules;
using PokemonTCG.Cards.Runtime;
using PokemonTCG.UI;

namespace PokemonTCG.UI
{
    /// <summary>
    /// CardBattleField GameObject統合制御システム
    /// 実際のGameObjectとゲーム状態を完全同期
    /// Phase 5 - オフライン版PTCGL の中核コンポーネント
    /// </summary>
    public class CardBattleFieldController : MonoBehaviour
    {
        #region GameObject References - CardBattleField構造
        
        [Header("=== CardBattleField GameObjects ===")]
        [Space(5)]
        
        [Header("Information Display")]
        public Image cardInfo;              // CardInfo - カード詳細表示エリア
        public TextMeshProUGUI duelLog;     // DuelLog - 対戦ログ表示
        
        [Header("Player 1 Area - GameObjects")]
        public Image p1Battle;              // P1_Battle - バトル場
        public Image p1Bench1;              // P1_Bench1 - ベンチ1
        public Image p1Bench2;              // P1_Bench2 - ベンチ2  
        public Image p1Bench3;              // P1_Bench3 - ベンチ3
        public Image p1Bench4;              // P1_Bench4 - ベンチ4
        public Image p1Bench5;              // P1_Bench5 - ベンチ5
        public Image p1Deck;                // P1_Deck - デッキ
        public Image p1Trash;               // P1_Trash - トラッシュ
        public Image p1Stadium;             // P1_Stadium - スタジアム
        public Image p1Side1, p1Side2, p1Side3, p1Side4, p1Side5, p1Side6; // サイド6枚
        public Image p1Lost;                // P1_Lost - ロストゾーン
        public Image p1Hand;                // P1_Hand - 手札エリア
        public Image p1Energy;              // P1_Energy - エネルギーゾーン
        
        [Header("Player 2 Area - GameObjects")]
        public Image p2Battle;              // P2_Battle - バトル場
        public Image p2Bench1;              // P2_Bench1 - ベンチ1
        public Image p2Bench2;              // P2_Bench2 - ベンチ2
        public Image p2Bench3;              // P2_Bench3 - ベンチ3
        public Image p2Bench4;              // P2_Bench4 - ベンチ4
        public Image p2Bench5;              // P2_Bench5 - ベンチ5  
        public Image p2Deck;                // P2_Deck - デッキ
        public Image p2Trash;               // P2_Trash - トラッシュ
        public Image p2Stadium;             // P2_Stadium - スタジアム
        public Image p2Side1, p2Side2, p2Side3, p2Side4, p2Side5, p2Side6; // サイド6枚
        public Image p2Lost;                // P2_Lost - ロストゾーン
        public Image p2Hand;                // P2_Hand - 手札エリア
        public Image p2Energy;              // P2_Energy - エネルギーゾーン

        [Header("Card Display Settings")]
        public Sprite unknownCardSprite;    // unknown.jpg - 裏面・不明カード
        public Sprite emptySlotSprite;      // 空スロット表示
        public float cardAnimationSpeed = 0.5f;
        public AnimationCurve cardMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Visual Effects")]
        public Color activePlayerHighlight = new Color(0, 1, 0, 0.3f);   // アクティブプレイヤー強調
        public Color availableSlotColor = new Color(0, 1, 0, 0.5f);      // 利用可能スロット
        public Color selectedCardColor = new Color(1, 1, 0, 0.7f);       // 選択中カード
        public Color damageIndicatorColor = new Color(1, 0, 0, 0.8f);    // ダメージ表示
        
        #endregion

        #region Private Fields

        // システム参照
        private GameStateManager gameStateManager;
        private FieldUIController fieldUIController;
        private bool isInitialized = false;
        
        // カード管理
        private Dictionary<string, Card> slotCards = new Dictionary<string, Card>();
        private Dictionary<string, Image> slotImages = new Dictionary<string, Image>();
        private Dictionary<string, CardSlotData> slotData = new Dictionary<string, CardSlotData>();
        
        // ビジュアル状態
        private string currentActivePlayer = "";
        private GamePhase currentPhase = GamePhase.Setup;
        private List<string> highlightedSlots = new List<string>();
        private string selectedCard = "";
        
        // アニメーション制御
        private List<Tween> activeTweens = new List<Tween>();
        
        #endregion

        #region Unity Lifecycle - 自動初期化

        private void Awake()
        {
            // CardBattleFieldController自動初期化
            // GameStateManagerの初期化を待つため、少し遅延させる
            StartCoroutine(DelayedInitialization());
        }

        private void Start()
        {
            // 確実に初期化されるようにStart()でも確認
            if (!isInitialized)
            {
                Initialize();
            }
        }

        private System.Collections.IEnumerator DelayedInitialization()
        {
            // 1フレーム待ってから初期化（他のシステムの初期化を待つ）
            yield return null;
            
            // まだ初期化されていない場合のみ実行
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
            
            Debug.Log("[CardBattleFieldController] Initializing CardBattleField Controller...");
            
            // システム参照取得
            InitializeSystemReferences();
            
            // GameObject参照セットアップ
            InitializeGameObjectReferences();
            
            // スロットデータ初期化
            InitializeSlotData();
            
            // イベント購読
            SubscribeToEvents();
            
            // 初期状態設定
            SetInitialState();
            
            isInitialized = true;
            Debug.Log("[CardBattleFieldController] CardBattleField Controller initialized successfully");
        }
        
        private void InitializeSystemReferences()
        {
            // GameStateManager参照
            gameStateManager = ServiceLocator.Get<GameStateManager>();
            if (gameStateManager == null)
            {
                Debug.LogError("[CardBattleFieldController] GameStateManager not found!");
                return;
            }
            
            // FieldUIController参照
            fieldUIController = FindObjectOfType<FieldUIController>();
            if (fieldUIController == null)
            {
                Debug.LogWarning("[CardBattleFieldController] FieldUIController not found - some features may be limited");
            }
        }
        
        private void InitializeGameObjectReferences()
        {
            // Player 1 スロット参照セットアップ
            RegisterSlot("P1_Battle", p1Battle, SlotType.BattleField, "Player1");
            RegisterSlot("P1_Bench1", p1Bench1, SlotType.Bench, "Player1");
            RegisterSlot("P1_Bench2", p1Bench2, SlotType.Bench, "Player1");
            RegisterSlot("P1_Bench3", p1Bench3, SlotType.Bench, "Player1");
            RegisterSlot("P1_Bench4", p1Bench4, SlotType.Bench, "Player1");
            RegisterSlot("P1_Bench5", p1Bench5, SlotType.Bench, "Player1");
            RegisterSlot("P1_Deck", p1Deck, SlotType.Deck, "Player1");
            RegisterSlot("P1_Trash", p1Trash, SlotType.Trash, "Player1");
            RegisterSlot("P1_Stadium", p1Stadium, SlotType.Stadium, "Player1");
            RegisterSlot("P1_Side1", p1Side1, SlotType.PrizeCard, "Player1");
            RegisterSlot("P1_Side2", p1Side2, SlotType.PrizeCard, "Player1");
            RegisterSlot("P1_Side3", p1Side3, SlotType.PrizeCard, "Player1");
            RegisterSlot("P1_Side4", p1Side4, SlotType.PrizeCard, "Player1");
            RegisterSlot("P1_Side5", p1Side5, SlotType.PrizeCard, "Player1");
            RegisterSlot("P1_Side6", p1Side6, SlotType.PrizeCard, "Player1");
            RegisterSlot("P1_Lost", p1Lost, SlotType.LostZone, "Player1");
            RegisterSlot("P1_Hand", p1Hand, SlotType.Hand, "Player1");
            RegisterSlot("P1_Energy", p1Energy, SlotType.Energy, "Player1");
            
            // Player 2 スロット参照セットアップ
            RegisterSlot("P2_Battle", p2Battle, SlotType.BattleField, "Player2");
            RegisterSlot("P2_Bench1", p2Bench1, SlotType.Bench, "Player2");
            RegisterSlot("P2_Bench2", p2Bench2, SlotType.Bench, "Player2");
            RegisterSlot("P2_Bench3", p2Bench3, SlotType.Bench, "Player2");
            RegisterSlot("P2_Bench4", p2Bench4, SlotType.Bench, "Player2");
            RegisterSlot("P2_Bench5", p2Bench5, SlotType.Bench, "Player2");
            RegisterSlot("P2_Deck", p2Deck, SlotType.Deck, "Player2");
            RegisterSlot("P2_Trash", p2Trash, SlotType.Trash, "Player2");
            RegisterSlot("P2_Stadium", p2Stadium, SlotType.Stadium, "Player2");
            RegisterSlot("P2_Side1", p2Side1, SlotType.PrizeCard, "Player2");
            RegisterSlot("P2_Side2", p2Side2, SlotType.PrizeCard, "Player2");
            RegisterSlot("P2_Side3", p2Side3, SlotType.PrizeCard, "Player2");
            RegisterSlot("P2_Side4", p2Side4, SlotType.PrizeCard, "Player2");
            RegisterSlot("P2_Side5", p2Side5, SlotType.PrizeCard, "Player2");
            RegisterSlot("P2_Side6", p2Side6, SlotType.PrizeCard, "Player2");
            RegisterSlot("P2_Lost", p2Lost, SlotType.LostZone, "Player2");
            RegisterSlot("P2_Hand", p2Hand, SlotType.Hand, "Player2");
            RegisterSlot("P2_Energy", p2Energy, SlotType.Energy, "Player2");
        }
        
        private void RegisterSlot(string slotId, Image slotImage, SlotType slotType, string playerId)
        {
            if (slotImage == null)
            {
                Debug.LogWarning($"[CardBattleFieldController] Slot image is null: {slotId}");
                return;
            }
            
            slotImages[slotId] = slotImage;
            slotData[slotId] = new CardSlotData
            {
                SlotId = slotId,
                SlotType = slotType,
                PlayerId = playerId,
                IsEmpty = true,
                Card = null
            };
            
            // クリックイベント設定
            SetupSlotClickHandler(slotId, slotImage);
        }
        
        private void SetupSlotClickHandler(string slotId, Image slotImage)
        {
            // Buttonコンポーネント追加/取得
            Button button = slotImage.GetComponent<Button>();
            if (button == null)
            {
                button = slotImage.gameObject.AddComponent<Button>();
            }
            
            // クリックイベント設定
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnSlotClicked(slotId));
        }
        
        private void InitializeSlotData()
        {
            // 全スロットを初期状態に設定
            foreach (var kvp in slotImages)
            {
                SetSlotEmpty(kvp.Key);
            }
        }
        
        private void SubscribeToEvents()
        {
            // GameStateManager イベント購読
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged += OnGameStateChanged;
                gameStateManager.OnPlayerStateChanged += OnPlayerStateChanged;
                gameStateManager.OnPhaseChanged += OnPhaseChanged;
                gameStateManager.OnTurnChanged += OnTurnChanged;
                gameStateManager.OnGameEnded += OnGameEnded;
            }
            
            // EventBus イベント購読
            EventBus.On<CardPlayedEvent>(OnCardPlayed);
            EventBus.On<CardMovedEvent>(OnCardMoved);
            EventBus.On<DamageDealtEvent>(OnDamageDealt);
            EventBus.On<CardDrawnEvent>(OnCardDrawn);
        }
        
        private void SetInitialState()
        {
            // unknown.jpg を全スロットに設定
            foreach (var kvp in slotImages)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.sprite = unknownCardSprite;
                    kvp.Value.color = Color.white;
                }
            }
            
            // ログ初期化
            if (duelLog != null)
            {
                duelLog.text = "Game Ready - Waiting for players...";
            }
        }

        #endregion

        #region Public Interface - Main API

        /// <summary>
        /// 指定スロットにカードを配置
        /// </summary>
        public void PlaceCardInSlot(string slotId, Card card, bool animate = true)
        {
            if (!slotImages.ContainsKey(slotId))
            {
                Debug.LogWarning($"[CardBattleFieldController] Unknown slot: {slotId}");
                return;
            }
            
            var slotImage = slotImages[slotId];
            var slotInfo = slotData[slotId];
            
            // カード情報更新
            slotCards[slotId] = card;
            slotInfo.Card = card;
            slotInfo.IsEmpty = false;
            
            // カード画像更新
            UpdateSlotVisual(slotId, card);
            
            // アニメーション
            if (animate)
            {
                AnimateCardPlacement(slotImage);
            }
            
            // ログ更新
            LogCardAction($"Card placed in {slotId}: {card.CardData.CardName}");
            
            Debug.Log($"[CardBattleFieldController] Card placed: {card.CardData.CardName} -> {slotId}");
        }
        
        /// <summary>
        /// 指定スロットからカードを削除
        /// </summary>
        public void RemoveCardFromSlot(string slotId, bool animate = true)
        {
            if (!slotImages.ContainsKey(slotId))
            {
                Debug.LogWarning($"[CardBattleFieldController] Unknown slot: {slotId}");
                return;
            }
            
            var slotImage = slotImages[slotId];
            var slotInfo = slotData[slotId];
            var card = slotCards.GetValueOrDefault(slotId, null);
            
            // アニメーション
            if (animate && card != null)
            {
                AnimateCardRemoval(slotImage, () => {
                    // アニメーション完了後の処理
                    CompleteCardRemoval(slotId);
                });
            }
            else
            {
                CompleteCardRemoval(slotId);
            }
            
            // ログ更新
            if (card != null)
            {
                LogCardAction($"Card removed from {slotId}: {card.CardData.CardName}");
            }
        }
        
        /// <summary>
        /// スロット状態を空に設定
        /// </summary>
        public void SetSlotEmpty(string slotId)
        {
            if (!slotImages.ContainsKey(slotId) || !slotData.ContainsKey(slotId))
                return;
                
            var slotImage = slotImages[slotId];
            var slotInfo = slotData[slotId];
            
            // 状態更新
            slotCards.Remove(slotId);
            slotInfo.Card = null;
            slotInfo.IsEmpty = true;
            
            // 視覚更新
            slotImage.sprite = emptySlotSprite ?? unknownCardSprite;
            slotImage.color = Color.white;
        }
        
        /// <summary>
        /// プレイヤーの全フィールドを更新
        /// </summary>
        public void UpdatePlayerField(string playerId)
        {
            if (gameStateManager == null) return;
            
            var playerState = gameStateManager.GetPlayerState(playerId);
            if (playerState == null) return;
            
            // 各スロットタイプ毎の更新
            UpdateBattleFieldSlots(playerId, playerState);
            UpdateBenchSlots(playerId, playerState);
            UpdateDeckSlots(playerId, playerState);
            UpdatePrizeSlots(playerId, playerState);
            UpdateOtherSlots(playerId, playerState);
        }
        
        /// <summary>
        /// フィールド全体の同期更新
        /// </summary>
        public void SyncWithGameState()
        {
            if (gameStateManager == null || !gameStateManager.GameInProgress) return;
            
            // 両プレイヤーのフィールド更新
            UpdatePlayerField("Player1");
            UpdatePlayerField("Player2");
            
            // ゲームフェーズ表示更新
            UpdatePhaseDisplay();
            
            // アクティブプレイヤー強調
            UpdateActivePlayerHighlight();
        }

        #endregion

        #region Visual Update Methods

        private void UpdateSlotVisual(string slotId, Card card)
        {
            if (!slotImages.ContainsKey(slotId)) return;
            
            var slotImage = slotImages[slotId];
            
            if (card != null)
            {
                // カード画像があれば設定、なければunknown.jpg
                // Phase 5では高解像度画像システムを実装予定
                slotImage.sprite = unknownCardSprite; // 暫定: unknown.jpg
                slotImage.color = Color.white;
            }
            else
            {
                // 空スロット
                slotImage.sprite = emptySlotSprite ?? unknownCardSprite;
                slotImage.color = Color.gray;
            }
        }
        
        private void UpdateBattleFieldSlots(string playerId, PlayerState playerState)
        {
            string battleSlotId = $"{playerId.Replace("Player", "P")}_Battle";
            
            // バトル場のポケモン取得（実装は後のフェーズで詳細化）
            // 現在は基本構造のみ
            if (slotData.ContainsKey(battleSlotId))
            {
                // TODO: playerState.ActivePokemon から実際のカード取得
                // PlaceCardInSlot(battleSlotId, activePokemon, false);
            }
        }
        
        private void UpdateBenchSlots(string playerId, PlayerState playerState)
        {
            string playerPrefix = playerId.Replace("Player", "P");
            
            for (int i = 1; i <= 5; i++)
            {
                string benchSlotId = $"{playerPrefix}_Bench{i}";
                
                // ベンチポケモン取得（実装は後のフェーズで詳細化）
                if (slotData.ContainsKey(benchSlotId))
                {
                    // TODO: playerState.BenchPokemon[i-1] から実際のカード取得
                }
            }
        }
        
        private void UpdateDeckSlots(string playerId, PlayerState playerState)
        {
            string playerPrefix = playerId.Replace("Player", "P");
            string deckSlotId = $"{playerPrefix}_Deck";
            string trashSlotId = $"{playerPrefix}_Trash";
            
            // デッキ枚数表示（カード裏面で枚数表現）
            if (slotData.ContainsKey(deckSlotId))
            {
                // TODO: playerState.Deck.Cards.Count による枚数表示
            }
            
            // トラッシュ一番上のカード表示
            if (slotData.ContainsKey(trashSlotId))
            {
                // TODO: playerState.Trash.TopCard による表示
            }
        }
        
        private void UpdatePrizeSlots(string playerId, PlayerState playerState)
        {
            string playerPrefix = playerId.Replace("Player", "P");
            
            for (int i = 1; i <= 6; i++)
            {
                string prizeSlotId = $"{playerPrefix}_Side{i}";
                
                if (slotData.ContainsKey(prizeSlotId))
                {
                    // TODO: playerState.PrizeCards[i-1] による表示
                    // 取得済みプライズは空に、未取得はカード裏面
                }
            }
        }
        
        private void UpdateOtherSlots(string playerId, PlayerState playerState)
        {
            string playerPrefix = playerId.Replace("Player", "P");
            
            // Hand表示
            string handSlotId = $"{playerPrefix}_Hand";
            if (slotData.ContainsKey(handSlotId))
            {
                // TODO: 手札枚数表示
            }
            
            // Energy表示  
            string energySlotId = $"{playerPrefix}_Energy";
            if (slotData.ContainsKey(energySlotId))
            {
                // TODO: エネルギー残量表示
            }
            
            // Lost Zone表示
            string lostSlotId = $"{playerPrefix}_Lost";
            if (slotData.ContainsKey(lostSlotId))
            {
                // TODO: ロストゾーン枚数表示
            }
        }
        
        private void UpdatePhaseDisplay()
        {
            if (duelLog != null)
            {
                string phaseText = $"Phase: {currentPhase}\nActive Player: {currentActivePlayer}";
                duelLog.text = phaseText;
            }
        }
        
        private void UpdateActivePlayerHighlight()
        {
            // 全プレイヤーのハイライトをリセット
            ClearPlayerHighlights();
            
            // アクティブプレイヤーのフィールドをハイライト
            if (!string.IsNullOrEmpty(currentActivePlayer))
            {
                HighlightPlayerField(currentActivePlayer, true);
            }
        }

        #endregion

        #region Animation Methods

        private void AnimateCardPlacement(Image slotImage)
        {
            if (slotImage == null) return;
            
            // カード落下アニメーション
            Vector3 originalPos = slotImage.transform.localPosition;
            slotImage.transform.localPosition = originalPos + Vector3.up * 200f;
            
            var tween = slotImage.transform.DOLocalMove(originalPos, cardAnimationSpeed)
                .SetEase(cardMoveCurve)
                .OnComplete(() => {
                    // アニメーション完了処理
                });
                
            activeTweens.Add(tween);
        }
        
        private void AnimateCardRemoval(Image slotImage, Action onComplete = null)
        {
            if (slotImage == null)
            {
                onComplete?.Invoke();
                return;
            }
            
            // カードが上に飛んでいくアニメーション
            Vector3 targetPos = slotImage.transform.localPosition + Vector3.up * 200f;
            
            var tween = slotImage.transform.DOLocalMove(targetPos, cardAnimationSpeed * 0.5f)
                .SetEase(Ease.InQuad)
                .OnComplete(() => {
                    onComplete?.Invoke();
                });
                
            activeTweens.Add(tween);
        }
        
        private void AnimateDamageEffect(Image slotImage)
        {
            if (slotImage == null) return;
            
            // ダメージフラッシュエフェクト
            Color originalColor = slotImage.color;
            
            var tween = slotImage.DOColor(damageIndicatorColor, 0.1f)
                .SetLoops(3, LoopType.Yoyo)
                .OnComplete(() => {
                    slotImage.color = originalColor;
                });
                
            activeTweens.Add(tween);
        }
        
        private void CompleteCardRemoval(string slotId)
        {
            // カードデータクリア
            slotCards.Remove(slotId);
            
            // スロット情報更新
            if (slotData.ContainsKey(slotId))
            {
                var slotInfo = slotData[slotId];
                slotInfo.Card = null;
                slotInfo.IsEmpty = true;
            }
            
            // 視覚状態リセット
            SetSlotEmpty(slotId);
        }

        #endregion

        #region Highlight System

        public void HighlightAvailableSlots(List<string> slotIds)
        {
            ClearAllHighlights();
            
            foreach (string slotId in slotIds)
            {
                if (slotImages.ContainsKey(slotId))
                {
                    var slotImage = slotImages[slotId];
                    slotImage.color = availableSlotColor;
                    highlightedSlots.Add(slotId);
                }
            }
        }
        
        public void ClearAllHighlights()
        {
            foreach (string slotId in highlightedSlots)
            {
                if (slotImages.ContainsKey(slotId))
                {
                    var slotImage = slotImages[slotId];
                    slotImage.color = Color.white;
                }
            }
            highlightedSlots.Clear();
        }
        
        private void HighlightPlayerField(string playerId, bool highlight)
        {
            string playerPrefix = playerId.Replace("Player", "P");
            Color highlightColor = highlight ? activePlayerHighlight : Color.white;
            
            // バトル場・ベンチのハイライト
            List<string> fieldSlots = new List<string>
            {
                $"{playerPrefix}_Battle",
                $"{playerPrefix}_Bench1", $"{playerPrefix}_Bench2", $"{playerPrefix}_Bench3",
                $"{playerPrefix}_Bench4", $"{playerPrefix}_Bench5"
            };
            
            foreach (string slotId in fieldSlots)
            {
                if (slotImages.ContainsKey(slotId))
                {
                    var slotImage = slotImages[slotId];
                    if (highlight)
                    {
                        slotImage.color = Color.Lerp(slotImage.color, highlightColor, 0.5f);
                    }
                    else
                    {
                        slotImage.color = Color.white;
                    }
                }
            }
        }
        
        private void ClearPlayerHighlights()
        {
            HighlightPlayerField("Player1", false);
            HighlightPlayerField("Player2", false);
        }

        #endregion

        #region Event Handlers

        private void OnSlotClicked(string slotId)
        {
            Debug.Log($"[CardBattleFieldController] Slot clicked: {slotId}");
            
            // カード情報表示更新
            UpdateCardInfoDisplay(slotId);
            
            // 選択状態管理
            HandleSlotSelection(slotId);
            
            // FieldUIController へ転送
            if (fieldUIController != null)
            {
                var slotImage = slotImages[slotId];
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, slotImage.transform.position);
                // fieldUIController.OnSlotClicked?.Invoke(slotId, screenPos);
            }
        }
        
        private void UpdateCardInfoDisplay(string slotId)
        {
            if (cardInfo == null) return;
            
            var card = slotCards.GetValueOrDefault(slotId, null);
            if (card != null)
            {
                // カード詳細表示（Phase 5で詳細実装）
                cardInfo.sprite = unknownCardSprite; // 暫定
                Debug.Log($"[CardBattleFieldController] Displaying card info: {card.CardData.CardName}");
            }
            else
            {
                // 空スロット情報表示
                cardInfo.sprite = emptySlotSprite ?? unknownCardSprite;
            }
        }
        
        private void HandleSlotSelection(string slotId)
        {
            // 既に選択中のカードがあれば解除
            if (!string.IsNullOrEmpty(selectedCard) && slotImages.ContainsKey(selectedCard))
            {
                slotImages[selectedCard].color = Color.white;
            }
            
            // 新しいカードを選択
            selectedCard = slotId;
            if (slotImages.ContainsKey(slotId))
            {
                slotImages[slotId].color = selectedCardColor;
            }
        }
        
        private void OnGameStateChanged(GameState newGameState)
        {
            Debug.Log("[CardBattleFieldController] Game state changed - syncing field");
            SyncWithGameState();
        }
        
        private void OnPlayerStateChanged(PlayerState playerState)
        {
            Debug.Log($"[CardBattleFieldController] Player state changed: {playerState.PlayerId}");
            UpdatePlayerField(playerState.PlayerId);
        }
        
        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            currentPhase = newPhase;
            UpdatePhaseDisplay();
            Debug.Log($"[CardBattleFieldController] Phase changed: {oldPhase} -> {newPhase}");
        }
        
        private void OnTurnChanged(string previousPlayer, string currentPlayer)
        {
            currentActivePlayer = currentPlayer;
            UpdateActivePlayerHighlight();
            LogCardAction($"Turn changed to {currentPlayer}");
            Debug.Log($"[CardBattleFieldController] Turn changed: {previousPlayer} -> {currentPlayer}");
        }
        
        private void OnGameEnded(string winnerId, VictoryReason reason)
        {
            LogCardAction($"Game ended - Winner: {winnerId} ({reason})");
            Debug.Log($"[CardBattleFieldController] Game ended: {winnerId} wins by {reason}");
        }
        
        // EventBus イベントハンドラー - 既存イベントクラス定義に合わせて修正
        private void OnCardPlayed(CardPlayedEvent evt)
        {
            // GameUI.cs定義: { Card card; int player; Vector3 position; }
            string slotId = $"P{evt.player}_Battle"; // 簡略化されたマッピング
            PlaceCardInSlot(slotId, evt.card, true);
        }
        
        private void OnCardMoved(CardMovedEvent evt)
        {
            RemoveCardFromSlot(evt.FromSlot, true);
            PlaceCardInSlot(evt.ToSlot, evt.Card, true);
        }
        
        private void OnDamageDealt(DamageDealtEvent evt)
        {
            // EffectUIController.cs定義: { int Damage; Vector3 TargetPosition; bool IsCritical; string Source; }
            // TargetPositionから最も近いスロットを見つける（簡略化）
            string targetSlot = FindClosestSlot(evt.TargetPosition);
            if (slotImages.ContainsKey(targetSlot))
            {
                AnimateDamageEffect(slotImages[targetSlot]);
                LogCardAction($"Damage dealt: {evt.Damage} to {targetSlot}" + (evt.IsCritical ? " (CRITICAL!)" : ""));
            }
        }
        
        private void OnCardDrawn(CardDrawnEvent evt)
        {
            // HandUIController.cs定義: { string PlayerId; Card Card; }
            LogCardAction($"{evt.PlayerId} drew a card: {evt.Card?.CardData?.CardName ?? "Unknown"}");
        }

        #endregion

        #region Utility Methods

        private void LogCardAction(string message)
        {
            if (duelLog != null)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                duelLog.text = $"[{timestamp}] {message}\n{duelLog.text}";
                
                // ログ長さ制限
                string[] lines = duelLog.text.Split('\n');
                if (lines.Length > 20)
                {
                    duelLog.text = string.Join("\n", lines.Take(20));
                }
            }
        }
        
        public string GetSlotInfo(string slotId)
        {
            if (!slotData.ContainsKey(slotId)) return "Unknown slot";
            
            var slotInfo = slotData[slotId];
            var card = slotCards.GetValueOrDefault(slotId, null);
            
            return $"Slot: {slotId}\n" +
                   $"Type: {slotInfo.SlotType}\n" +
                   $"Player: {slotInfo.PlayerId}\n" +
                   $"Empty: {slotInfo.IsEmpty}\n" +
                   $"Card: {(card?.CardData.CardName ?? "None")}";
        }
        
        public List<string> GetEmptySlots(string playerId, SlotType slotType)
        {
            return slotData.Values
                .Where(s => s.PlayerId == playerId && s.SlotType == slotType && s.IsEmpty)
                .Select(s => s.SlotId)
                .ToList();
        }
        
        private string FindClosestSlot(Vector3 worldPosition)
        {
            string closestSlot = "";
            float closestDistance = float.MaxValue;
            
            foreach (var kvp in slotImages)
            {
                if (kvp.Value != null)
                {
                    float distance = Vector3.Distance(worldPosition, kvp.Value.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestSlot = kvp.Key;
                    }
                }
            }
            
            return closestSlot;
        }

        #endregion

        #region Cleanup

        public void Cleanup()
        {
            // イベント購読解除
            if (gameStateManager != null)
            {
                gameStateManager.OnGameStateChanged -= OnGameStateChanged;
                gameStateManager.OnPlayerStateChanged -= OnPlayerStateChanged;
                gameStateManager.OnPhaseChanged -= OnPhaseChanged;
                gameStateManager.OnTurnChanged -= OnTurnChanged;
                gameStateManager.OnGameEnded -= OnGameEnded;
            }
            
            EventBus.Off<CardPlayedEvent>(OnCardPlayed);
            EventBus.Off<CardMovedEvent>(OnCardMoved);
            EventBus.Off<DamageDealtEvent>(OnDamageDealt);
            EventBus.Off<CardDrawnEvent>(OnCardDrawn);
            
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
            slotCards.Clear();
            slotImages.Clear();
            slotData.Clear();
            highlightedSlots.Clear();
            
            isInitialized = false;
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }

        #endregion
    }

    #region Support Classes

    [System.Serializable]
    public class CardSlotData
    {
        public string SlotId;
        public SlotType SlotType;
        public string PlayerId;
        public bool IsEmpty;
        public Card Card;
    }
    
    public enum SlotType
    {
        BattleField,    // バトル場
        Bench,          // ベンチ
        Deck,           // デッキ
        Trash,          // トラッシュ
        PrizeCard,      // サイドカード
        Hand,           // 手札
        Stadium,        // スタジアム
        Energy,         // エネルギー
        LostZone        // ロストゾーン
    }

    #endregion

    #region Event Classes

    // CardMovedEvent - 新規イベント（重複なし）
    public class CardMovedEvent
    {
        public string FromSlot { get; set; }
        public string ToSlot { get; set; }
        public Card Card { get; set; }
        public string PlayerId { get; set; }
    }

    // 注意: CardPlayedEvent, DamageDealtEvent, CardDrawnEvent は
    // 既存ファイル（GameUI.cs, EffectUIController.cs, HandUIController.cs）で定義済み
    // 重複を避けるため、ここでは定義しない

    #endregion
}