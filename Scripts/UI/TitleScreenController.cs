using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Game;
using PokemonTCG.UI;

namespace PokemonTCG.UI
{
    /// <summary>
    /// Phase 5 - タイトル画面・メインメニューシステム
    /// オフライン版PTCGL完全メニュー構成
    /// 5つのゲームモード + 美しいアニメーション
    /// </summary>
    public class TitleScreenController : MonoBehaviour
    {
        #region UI References

        [Header("=== Title Screen Elements ===")]
        public Text titleText;
        public Text versionText;
        public Transform buttonContainer;
        public CanvasGroup titleCanvasGroup;

        [Header("=== Main Menu Buttons ===")]
        public Button singleModeButton;       // プレイヤーVSAI or AI VS AI
        public Button monteCarloButton;       // モンテカルロ法検証
        public Button deckBuildButton;        // デッキビルドモード
        public Button replayModeButton;       // リプレイモード
        public Button exitGameButton;         // ゲーム終了

        [Header("=== Sub Menus ===")]
        public GameObject singleModeMenu;     // シングルモード選択
        public Button playerVsAIButton;       // プレイヤーVSAI
        public Button aiVsAIButton;          // AI VS AI
        public Button backFromSingleButton;   // 戻る

        [Header("=== Animation Settings ===")]
        public float fadeInDuration = 1.0f;
        public float buttonAnimationDelay = 0.1f;
        public AnimationCurve menuCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("=== Background ===")]
        public Image backgroundImage;
        public Color backgroundColor = new Color(0.1f, 0.1f, 0.2f, 1f);

        #endregion

        #region Private Fields

        // System References
        private GameStateManager gameStateManager;
        private CardBattleFieldController battleController;
        private DeckEditorController deckEditor;

        // Animation Management
        private List<Tween> activeTweens = new List<Tween>();
        private bool isInitialized = false;

        // Menu State
        private MenuState currentMenuState = MenuState.MainMenu;

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

        #endregion

        #region Initialization

        private void InitializeReferences()
        {
            // UI参照の自動検出
            if (titleText == null)
                titleText = GameObject.Find("TitleText")?.GetComponent<Text>();

            if (versionText == null)
                versionText = GameObject.Find("VersionText")?.GetComponent<Text>();

            if (buttonContainer == null)
                buttonContainer = GameObject.Find("ButtonContainer")?.transform;

            if (titleCanvasGroup == null)
                titleCanvasGroup = GetComponent<CanvasGroup>();

            // System References
            gameStateManager = FindObjectOfType<GameStateManager>();
            battleController = FindObjectOfType<CardBattleFieldController>();
            deckEditor = FindObjectOfType<DeckEditorController>();
        }

        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("[TitleScreenController] Initializing Title Screen System...");

            // Setup UI
            SetupTitleScreen();
            SetupButtons();
            SetupBackground();

            // Initial Animation
            PlayIntroAnimation();

            isInitialized = true;
            Debug.Log("[TitleScreenController] Title Screen System initialized successfully");
        }

        private void SetupTitleScreen()
        {
            // タイトルテキスト設定
            if (titleText != null)
            {
                titleText.text = "ポケモンTCG\nオフライン版PTCGL";
                titleText.fontSize = 48;
                titleText.color = Color.white;
                titleText.alignment = TextAnchor.MiddleCenter;
            }

            // バージョンテキスト設定
            if (versionText != null)
            {
                versionText.text = "Phase 5 - Complete Battle System\nv1.0.0";
                versionText.fontSize = 16;
                versionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                versionText.alignment = TextAnchor.MiddleCenter;
            }

            // 初期状態は非表示
            if (titleCanvasGroup != null)
            {
                titleCanvasGroup.alpha = 0f;
            }

            // サブメニューは非表示
            if (singleModeMenu != null)
            {
                singleModeMenu.SetActive(false);
            }
        }

        private void SetupButtons()
        {
            // メインメニューボタン設定
            SetupMainMenuButtons();
            
            // サブメニューボタン設定
            SetupSubMenuButtons();

            // 初期状態ではボタンを非表示
            HideAllButtons();
        }

        private void SetupMainMenuButtons()
        {
            if (singleModeButton != null)
            {
                singleModeButton.onClick.RemoveAllListeners();
                singleModeButton.onClick.AddListener(OnSingleModeClicked);
                AddButtonText(singleModeButton, "シングルモード");
            }

            if (monteCarloButton != null)
            {
                monteCarloButton.onClick.RemoveAllListeners();
                monteCarloButton.onClick.AddListener(OnMonteCarloClicked);
                AddButtonText(monteCarloButton, "モンテカルロ検証");
            }

            if (deckBuildButton != null)
            {
                deckBuildButton.onClick.RemoveAllListeners();
                deckBuildButton.onClick.AddListener(OnDeckBuildClicked);
                AddButtonText(deckBuildButton, "デッキビルド");
            }

            if (replayModeButton != null)
            {
                replayModeButton.onClick.RemoveAllListeners();
                replayModeButton.onClick.AddListener(OnReplayModeClicked);
                AddButtonText(replayModeButton, "リプレイモード");
            }

            if (exitGameButton != null)
            {
                exitGameButton.onClick.RemoveAllListeners();
                exitGameButton.onClick.AddListener(OnExitGameClicked);
                AddButtonText(exitGameButton, "ゲーム終了");
            }
        }

        private void SetupSubMenuButtons()
        {
            if (playerVsAIButton != null)
            {
                playerVsAIButton.onClick.RemoveAllListeners();
                playerVsAIButton.onClick.AddListener(OnPlayerVsAIClicked);
                AddButtonText(playerVsAIButton, "プレイヤー VS AI");
            }

            if (aiVsAIButton != null)
            {
                aiVsAIButton.onClick.RemoveAllListeners();
                aiVsAIButton.onClick.AddListener(OnAIVsAIClicked);
                AddButtonText(aiVsAIButton, "AI VS AI");
            }

            if (backFromSingleButton != null)
            {
                backFromSingleButton.onClick.RemoveAllListeners();
                backFromSingleButton.onClick.AddListener(OnBackFromSingleClicked);
                AddButtonText(backFromSingleButton, "戻る");
            }
        }

        private void AddButtonText(Button button, string text)
        {
            var textComponent = button.GetComponentInChildren<Text>();
            if (textComponent == null)
            {
                var textObj = new GameObject("ButtonText");
                textObj.transform.SetParent(button.transform, false);
                textComponent = textObj.AddComponent<Text>();
                textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                
                var rectTransform = textObj.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
            }

            textComponent.text = text;
            textComponent.fontSize = 20;
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.MiddleCenter;
        }

        private void SetupBackground()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
            }
        }

        #endregion

        #region Animation System

        private void PlayIntroAnimation()
        {
            StopAllAnimations();

            // タイトルフェードイン
            if (titleCanvasGroup != null)
            {
                var fadeInTween = titleCanvasGroup.DOFade(1f, fadeInDuration)
                    .SetEase(Ease.OutQuad);
                activeTweens.Add(fadeInTween);
            }

            // タイトルテキストアニメーション
            if (titleText != null)
            {
                titleText.transform.localScale = Vector3.zero;
                var scaleTween = titleText.transform.DOScale(Vector3.one, fadeInDuration)
                    .SetEase(Ease.OutBack)
                    .SetDelay(0.2f);
                activeTweens.Add(scaleTween);
            }

            // ボタンの順次表示
            StartCoroutine(ShowButtonsSequentially());
        }

        private System.Collections.IEnumerator ShowButtonsSequentially()
        {
            yield return new WaitForSeconds(fadeInDuration + 0.5f);

            var buttons = new Button[] 
            { 
                singleModeButton, 
                monteCarloButton, 
                deckBuildButton, 
                replayModeButton, 
                exitGameButton 
            };

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    ShowButton(buttons[i], i * buttonAnimationDelay);
                }
                yield return new WaitForSeconds(buttonAnimationDelay);
            }
        }

        private void ShowButton(Button button, float delay)
        {
            button.gameObject.SetActive(true);
            button.transform.localScale = Vector3.zero;
            
            var scaleTween = button.transform.DOScale(Vector3.one, 0.3f)
                .SetEase(Ease.OutBack)
                .SetDelay(delay);
            activeTweens.Add(scaleTween);

            // ホバーエフェクト追加
            AddHoverEffect(button);
        }

        private void HideAllButtons()
        {
            var allButtons = new Button[] 
            { 
                singleModeButton, 
                monteCarloButton, 
                deckBuildButton, 
                replayModeButton, 
                exitGameButton,
                playerVsAIButton,
                aiVsAIButton,
                backFromSingleButton
            };

            foreach (var button in allButtons)
            {
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                }
            }
        }

        private void AddHoverEffect(Button button)
        {
            var eventTrigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }

            // マウスエンター
            var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            pointerEnter.callback.AddListener((data) => {
                button.transform.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.OutQuad);
            });
            eventTrigger.triggers.Add(pointerEnter);

            // マウスイグジット
            var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((data) => {
                button.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad);
            });
            eventTrigger.triggers.Add(pointerExit);
        }

        private void StopAllAnimations()
        {
            foreach (var tween in activeTweens)
            {
                if (tween != null && tween.IsActive())
                {
                    tween.Kill();
                }
            }
            activeTweens.Clear();
        }

        #endregion

        #region Menu Navigation

        private void ShowSingleModeMenu()
        {
            currentMenuState = MenuState.SingleModeMenu;
            
            if (singleModeMenu != null)
            {
                singleModeMenu.SetActive(true);
                
                // サブメニューボタンのアニメーション
                var subButtons = new Button[] { playerVsAIButton, aiVsAIButton, backFromSingleButton };
                for (int i = 0; i < subButtons.Length; i++)
                {
                    if (subButtons[i] != null)
                    {
                        ShowButton(subButtons[i], i * 0.1f);
                    }
                }
            }

            // メインメニューボタンを隠す
            HideMainMenuButtons();
        }

        private void HideMainMenuButtons()
        {
            var mainButtons = new Button[] 
            { 
                singleModeButton, 
                monteCarloButton, 
                deckBuildButton, 
                replayModeButton, 
                exitGameButton 
            };

            foreach (var button in mainButtons)
            {
                if (button != null)
                {
                    button.transform.DOScale(Vector3.zero, 0.2f)
                        .OnComplete(() => button.gameObject.SetActive(false));
                }
            }
        }

        private void ShowMainMenu()
        {
            currentMenuState = MenuState.MainMenu;
            
            if (singleModeMenu != null)
            {
                singleModeMenu.SetActive(false);
            }

            // メインメニューボタンを再表示
            StartCoroutine(ShowButtonsSequentially());
        }

        #endregion

        #region Button Event Handlers

        private void OnSingleModeClicked()
        {
            Debug.Log("[TitleScreen] Single Mode clicked");
            ShowSingleModeMenu();
        }

        private void OnPlayerVsAIClicked()
        {
            Debug.Log("[TitleScreen] Player VS AI selected");
            StartGame(GameMode.PlayerVsAI);
        }

        private void OnAIVsAIClicked()
        {
            Debug.Log("[TitleScreen] AI VS AI selected");
            StartGame(GameMode.AIVsAI);
        }

        private void OnMonteCarloClicked()
        {
            Debug.Log("[TitleScreen] Monte Carlo Verification Mode clicked");
            StartMonteCarloMode();
        }

        private void OnDeckBuildClicked()
        {
            Debug.Log("[TitleScreen] Deck Build Mode clicked");
            StartDeckBuildMode();
        }

        private void OnReplayModeClicked()
        {
            Debug.Log("[TitleScreen] Replay Mode clicked");
            StartReplayMode();
        }

        private void OnExitGameClicked()
        {
            Debug.Log("[TitleScreen] Exit Game clicked");
            ExitGame();
        }

        private void OnBackFromSingleClicked()
        {
            Debug.Log("[TitleScreen] Back to main menu");
            ShowMainMenu();
        }

        #endregion

        #region Game Mode Starters

        private void StartGame(GameMode mode)
        {
            // タイトル画面を隠す
            HideTitleScreen();

            // ゲームモードに応じてバトル開始
            if (battleController != null)
            {
                battleController.gameObject.SetActive(true);
                
                if (mode == GameMode.AIVsAI)
                {
                    battleController.StartDebugBattle();
                }
                else
                {
                    // Player vs AI mode implementation
                    battleController.StartDebugBattle(); // 暫定的にAI vs AI
                }
            }

            Debug.Log($"[TitleScreen] Starting {mode} mode");
        }

        private void StartMonteCarloMode()
        {
            HideTitleScreen();
            
            // モンテカルロ検証モード実装
            Debug.Log("[TitleScreen] Monte Carlo verification mode starting...");
            
            // TODO: MonteCarloControllerの実装
            ShowNotImplementedMessage("モンテカルロ検証モード");
        }

        private void StartDeckBuildMode()
        {
            HideTitleScreen();
            
            if (deckEditor != null)
            {
                deckEditor.gameObject.SetActive(true);  
                deckEditor.OpenDeckEditor();
                Debug.Log("[TitleScreen] Deck Build mode started");
            }
            else
            {
                ShowNotImplementedMessage("デッキビルドモード");
            }
        }

        private void StartReplayMode()
        {
            HideTitleScreen();
            
            // リプレイモード実装
            Debug.Log("[TitleScreen] Replay mode starting...");
            
            // TODO: ReplayControllerの実装
            ShowNotImplementedMessage("リプレイモード");
        }

        private void ExitGame()
        {
            Debug.Log("[TitleScreen] Exiting game...");
            
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        #endregion

        #region Utility Methods

        private void HideTitleScreen()
        {
            if (titleCanvasGroup != null)
            {
                var hideTween = titleCanvasGroup.DOFade(0f, 0.5f)
                    .OnComplete(() => gameObject.SetActive(false));
                activeTweens.Add(hideTween);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void ShowTitleScreen()
        {
            gameObject.SetActive(true);
            currentMenuState = MenuState.MainMenu;
            
            if (singleModeMenu != null)
            {
                singleModeMenu.SetActive(false);
            }
            
            PlayIntroAnimation();
        }

        private void ShowNotImplementedMessage(string modeName)
        {
            Debug.Log($"[TitleScreen] {modeName}は今後実装予定です");
            
            // 3秒後にタイトル画面に戻る
            StartCoroutine(ReturnToTitleAfterDelay(3f));
        }

        private System.Collections.IEnumerator ReturnToTitleAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ShowTitleScreen();
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            StopAllAnimations();
        }

        #endregion
    }

    #region Enums

    public enum MenuState
    {
        MainMenu,
        SingleModeMenu,
        Options,
        Credits
    }

    public enum GameMode
    {
        PlayerVsAI,
        AIVsAI,
        Tutorial,
        Challenge
    }

    #endregion
}