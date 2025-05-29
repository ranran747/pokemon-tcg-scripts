using UnityEngine;

namespace PokemonTCG.UI
{
    /// <summary>
    /// シンプルなボタンクリック処理テスト用スクリプト
    /// Unity Inspector から直接ボタンに設定して使用
    /// </summary>
    public class SimpleButtonHandler : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject titleScreenUI;
        public GameObject gameCanvas;
        public GameObject deckEditorUI;

        void Start()
        {
            Debug.Log("[SimpleButtonHandler] Button handler initialized");
            
            // 自動参照取得
            if (titleScreenUI == null)
                titleScreenUI = GameObject.Find("TitleScreenUI");
            if (gameCanvas == null)
                gameCanvas = GameObject.Find("GameCanvas");
            if (deckEditorUI == null)
                deckEditorUI = GameObject.Find("DeckEditorUI");
                
            Debug.Log($"[SimpleButtonHandler] References - Title:{titleScreenUI != null}, Game:{gameCanvas != null}, Deck:{deckEditorUI != null}");
        }

        // SinglePlayer ボタン用
        public void OnSinglePlayerClicked()
        {
            Debug.Log("[SimpleButtonHandler] Single Player button clicked!");
            
            if (titleScreenUI != null) titleScreenUI.SetActive(false);
            if (gameCanvas != null) gameCanvas.SetActive(true);
            
            Debug.Log("[SimpleButtonHandler] Switched to Game Canvas");
        }

        // DeckBuilder ボタン用
        public void OnDeckBuilderClicked()
        {
            Debug.Log("[SimpleButtonHandler] Deck Builder button clicked!");
            
            if (titleScreenUI != null) titleScreenUI.SetActive(false);
            if (deckEditorUI != null) deckEditorUI.SetActive(true);
            
            Debug.Log("[SimpleButtonHandler] Switched to Deck Editor");
        }

        // MonteCarlo ボタン用
        public void OnMonteCarloClicked()
        {
            Debug.Log("[SimpleButtonHandler] Monte Carlo button clicked!");
            
            if (titleScreenUI != null) titleScreenUI.SetActive(false);
            if (gameCanvas != null) gameCanvas.SetActive(true);
            
            Debug.Log("[SimpleButtonHandler] Switched to Game Canvas (Monte Carlo Mode)");
        }

        // ReplayMode ボタン用
        public void OnReplayModeClicked()
        {
            Debug.Log("[SimpleButtonHandler] Replay Mode button clicked!");
            Debug.Log("[SimpleButtonHandler] Replay Mode - Coming Soon!");
        }

        // ExitGame ボタン用
        public void OnExitGameClicked()
        {
            Debug.Log("[SimpleButtonHandler] Exit Game button clicked!");
            
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        // タイトル画面に戻る
        public void ReturnToTitle()
        {
            Debug.Log("[SimpleButtonHandler] Returning to Title Screen");
            
            if (gameCanvas != null) gameCanvas.SetActive(false);
            if (deckEditorUI != null) deckEditorUI.SetActive(false);
            if (titleScreenUI != null) titleScreenUI.SetActive(true);
            
            Debug.Log("[SimpleButtonHandler] Returned to Title Screen");
        }

        // テスト用: すべての画面状態を表示
        [ContextMenu("Debug UI States")]
        public void DebugUIStates()
        {
            Debug.Log($"[SimpleButtonHandler] UI States:");
            Debug.Log($"  - TitleScreenUI: {(titleScreenUI != null ? titleScreenUI.activeSelf.ToString() : "null")}");
            Debug.Log($"  - GameCanvas: {(gameCanvas != null ? gameCanvas.activeSelf.ToString() : "null")}");
            Debug.Log($"  - DeckEditorUI: {(deckEditorUI != null ? deckEditorUI.activeSelf.ToString() : "null")}");
        }
    }
}