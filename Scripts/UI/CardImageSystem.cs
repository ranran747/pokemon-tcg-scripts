using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using DG.Tweening;
using PokemonTCG.Core.Architecture;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.UI
{
    /// <summary>
    /// Phase 5.2 - 高解像度カード画像システム
    /// 動的画像読み込み・キャッシュ・プレビュー機能
    /// メモリ効率最適化・高品質表示システム
    /// </summary>
    public class CardImageSystem : MonoBehaviourSingleton<CardImageSystem>
    {
        #region Configuration

        [Header("=== Image Loading Settings ===")]
        [SerializeField] private int maxCacheSize = 200;              // 最大キャッシュ数
        [SerializeField] private int maxConcurrentLoads = 5;          // 同時読み込み数
        [SerializeField] private float imageLoadTimeout = 10f;       // 読み込みタイムアウト
        [SerializeField] private bool enableMemoryOptimization = true;// メモリ最適化

        [Header("=== Image Quality Settings ===")]
        [SerializeField] private Vector2 thumbnailSize = new Vector2(120, 168);   // サムネイルサイズ
        [SerializeField] private Vector2 previewSize = new Vector2(240, 336);     // プレビューサイズ
        [SerializeField] private Vector2 fullSize = new Vector2(480, 672);        // フルサイズ
        [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;     // フィルタモード

        [Header("=== Fallback Images ===")]
        [SerializeField] private Sprite unknownCardSprite;           // 不明カード画像
        [SerializeField] private Sprite loadingSprite;               // 読み込み中画像
        [SerializeField] private Sprite errorSprite;                 // エラー画像

        [Header("=== Animation Settings ===")]
        [SerializeField] private float fadeInDuration = 0.3f;       // フェードイン時間
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("=== Image Paths ===")]
        [SerializeField] private string imageBasePath = "CardImages/";    // 基本パス
        [SerializeField] private string thumbnailPath = "Thumbnails/";    // サムネイルパス
        [SerializeField] private string previewPath = "Previews/";        // プレビューパス
        [SerializeField] private string fullPath = "Full/";               // フルサイズパス

        #endregion

        #region Private Fields

        // キャッシュシステム
        private Dictionary<string, CachedImage> imageCache = new Dictionary<string, CachedImage>();
        private Queue<string> cacheAccessOrder = new Queue<string>();
        private Dictionary<string, CardImageRequest> activeRequests = new Dictionary<string, CardImageRequest>();

        // 読み込み管理
        private Queue<CardImageRequest> loadQueue = new Queue<CardImageRequest>();
        private int currentConcurrentLoads = 0;
        private bool isInitialized = false;

        // プレビューシステム
        private CardPreviewWindow previewWindow;
        private GameObject previewWindowPrefab;

        #endregion

        #region Properties

        /// <summary>初期化順序</summary>
        public override int InitializationOrder => 100;

        /// <summary>キャッシュサイズ</summary>
        public int CacheSize => imageCache.Count;

        /// <summary>読み込み待ちキューサイズ</summary>
        public int QueueSize => loadQueue.Count;

        /// <summary>現在の同時読み込み数</summary>
        public int CurrentLoads => currentConcurrentLoads;

        #endregion

        #region Initialization

        protected override void OnInitialize()
        {
            // サービス登録
            ServiceLocator.Register<CardImageSystem>(this);

            // プレビューウィンドウ作成
            CreatePreviewWindow();

            // イベント購読
            SubscribeToEvents();

            isInitialized = true;
            Debug.Log("[CardImageSystem] Card Image System initialized");
        }

        protected override void OnDispose()
        {
            // キャッシュクリア
            ClearCache();

            // アクティブリクエストキャンセル
            CancelAllRequests();

            // イベント解除
            UnsubscribeFromEvents();

            Debug.Log("[CardImageSystem] Card Image System disposed");
        }

        private void CreatePreviewWindow()
        {
            // プレビューウィンドウプレハブ作成
            // 実際のプロジェクトでは別途プレハブを用意
            GameObject previewWindowObj = new GameObject("CardPreviewWindow");
            previewWindowObj.transform.SetParent(transform);
            previewWindowObj.SetActive(false);

            // Canvas設定
            var canvas = previewWindowObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var canvasScaler = previewWindowObj.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            previewWindowObj.AddComponent<GraphicRaycaster>();

            // プレビューウィンドウコンポーネント追加
            previewWindow = previewWindowObj.AddComponent<CardPreviewWindow>();
            previewWindow.Initialize(this);
        }

        private void SubscribeToEvents()
        {
            EventBus.On<CardImageRequestEvent>(OnCardImageRequested);
            EventBus.On<CardPreviewRequestEvent>(OnCardPreviewRequested);
            EventBus.On<MemoryPressureEvent>(OnMemoryPressure);
        }

        private void UnsubscribeFromEvents()
        {
            EventBus.Off<CardImageRequestEvent>(OnCardImageRequested);
            EventBus.Off<CardPreviewRequestEvent>(OnCardPreviewRequested);
            EventBus.Off<MemoryPressureEvent>(OnMemoryPressure);
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// カード画像を要求（非同期）
        /// </summary>
        /// <param name="card">カード</param>
        /// <param name="imageType">画像タイプ</param>
        /// <param name="targetImage">対象Image</param>
        /// <param name="onComplete">完了コールバック</param>
        public void RequestCardImage(Card card, CardImageType imageType, Image targetImage, 
            System.Action<Sprite> onComplete = null)
        {
            if (card == null || targetImage == null)
            {
                onComplete?.Invoke(unknownCardSprite);
                return;
            }

            string imageKey = GetImageKey(card, imageType);

            // キャッシュから取得
            if (imageCache.ContainsKey(imageKey))
            {
                var cachedImage = imageCache[imageKey];
                cachedImage.lastAccessTime = Time.time;
                UpdateCacheAccessOrder(imageKey);

                SetImageWithAnimation(targetImage, cachedImage.sprite);
                onComplete?.Invoke(cachedImage.sprite);
                return;
            }

            // 読み込み中の場合
            if (activeRequests.ContainsKey(imageKey))
            {
                activeRequests[imageKey].callbacks.Add(onComplete);
                activeRequests[imageKey].targetImages.Add(targetImage);
                return;
            }

            // 新規読み込み要求
            var request = new CardImageRequest
            {
                card = card,
                imageType = imageType,
                imageKey = imageKey,
                targetImages = new List<Image> { targetImage },
                callbacks = new List<System.Action<Sprite>> { onComplete },
                requestTime = Time.time
            };

            // ローディング画像設定
            SetImageWithAnimation(targetImage, loadingSprite);

            // キューに追加
            loadQueue.Enqueue(request);
            activeRequests[imageKey] = request;

            // 読み込み開始
            ProcessLoadQueue();
        }

        /// <summary>
        /// カードプレビューを表示
        /// </summary>
        /// <param name="card">カード</param>
        /// <param name="position">表示位置</param>
        public void ShowCardPreview(Card card, Vector3 position)
        {
            if (previewWindow != null)
            {
                previewWindow.ShowPreview(card, position);
            }
        }

        /// <summary>
        /// カードプレビューを隠す
        /// </summary>
        public void HideCardPreview()
        {
            if (previewWindow != null)
            {
                previewWindow.HidePreview();
            }
        }

        /// <summary>
        /// キャッシュから画像を取得
        /// </summary>
        /// <param name="card">カード</param>
        /// <param name="imageType">画像タイプ</param>
        /// <returns>画像（キャッシュにない場合null）</returns>
        public Sprite GetCachedImage(Card card, CardImageType imageType)
        {
            string imageKey = GetImageKey(card, imageType);
            if (imageCache.ContainsKey(imageKey))
            {
                var cachedImage = imageCache[imageKey];
                cachedImage.lastAccessTime = Time.time;
                UpdateCacheAccessOrder(imageKey);
                return cachedImage.sprite;
            }
            return null;
        }

        /// <summary>
        /// プリロード（事前読み込み）
        /// </summary>
        /// <param name="cards">プリロードするカード</param>
        /// <param name="imageType">画像タイプ</param>
        /// <param name="onProgress">進捗コールバック</param>
        public void PreloadImages(List<Card> cards, CardImageType imageType, 
            System.Action<float> onProgress = null)
        {
            StartCoroutine(PreloadImagesCoroutine(cards, imageType, onProgress));
        }

        #endregion

        #region Image Loading

        private void ProcessLoadQueue()
        {
            while (loadQueue.Count > 0 && currentConcurrentLoads < maxConcurrentLoads)
            {
                var request = loadQueue.Dequeue();
                StartCoroutine(LoadImageCoroutine(request));
                currentConcurrentLoads++;
            }
        }

        private IEnumerator LoadImageCoroutine(CardImageRequest request)
        {
            string imagePath = GetImagePath(request.card, request.imageType);
            Sprite loadedSprite = null;

            // ローカルファイルから読み込み
            if (System.IO.File.Exists(imagePath))
            {
                yield return LoadImageFromFile(imagePath, request);
            }
            // リソースから読み込み
            else
            {
                yield return LoadImageFromResources(request);
            }

            // 読み込み完了処理
            currentConcurrentLoads--;
            CompleteImageRequest(request);

            // キューの次の要求を処理
            ProcessLoadQueue();
        }

        private IEnumerator LoadImageFromFile(string path, CardImageRequest request)
        {
            UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture("file://" + path);
            webRequest.timeout = (int)imageLoadTimeout;

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                if (texture != null)
                {
                    request.loadedSprite = CreateSpriteFromTexture(texture, request.imageType);
                    request.isSuccess = true;
                }
            }
            else
            {
                Debug.LogWarning($"[CardImageSystem] Failed to load image from file: {path}");
                request.isSuccess = false;
            }

            webRequest.Dispose();
        }

        private IEnumerator LoadImageFromResources(CardImageRequest request)
        {
            string resourcePath = GetResourcePath(request.card, request.imageType);
            ResourceRequest resourceRequest = Resources.LoadAsync<Sprite>(resourcePath);

            yield return resourceRequest;

            if (resourceRequest.asset != null)
            {
                request.loadedSprite = resourceRequest.asset as Sprite;
                request.isSuccess = true;
            }
            else
            {
                // フォールバック: unknown.jpg
                request.loadedSprite = unknownCardSprite;
                request.isSuccess = false;
                Debug.LogWarning($"[CardImageSystem] Image not found: {resourcePath}");
            }
        }

        private void CompleteImageRequest(CardImageRequest request)
        {
            Sprite finalSprite = request.isSuccess ? request.loadedSprite : errorSprite;

            // キャッシュに保存
            if (request.isSuccess && finalSprite != null)
            {
                CacheImage(request.imageKey, finalSprite);
            }

            // 全ての対象Imageに適用
            foreach (var targetImage in request.targetImages)
            {
                if (targetImage != null)
                {
                    SetImageWithAnimation(targetImage, finalSprite);
                }
            }

            // コールバック実行
            foreach (var callback in request.callbacks)
            {
                callback?.Invoke(finalSprite);
            }

            // アクティブリクエストから削除
            activeRequests.Remove(request.imageKey);
        }

        private IEnumerator PreloadImagesCoroutine(List<Card> cards, CardImageType imageType, 
            System.Action<float> onProgress)
        {
            int totalCards = cards.Count;
            int loadedCount = 0;

            foreach (var card in cards)
            {
                // 既にキャッシュにある場合はスキップ
                string imageKey = GetImageKey(card, imageType);
                if (imageCache.ContainsKey(imageKey))
                {
                    loadedCount++;
                    continue;
                }

                // プリロード用の仮想リクエスト作成
                var dummyImage = new GameObject("DummyImage").AddComponent<Image>();
                dummyImage.gameObject.SetActive(false);

                bool isCompleted = false;
                RequestCardImage(card, imageType, dummyImage, (_) => {
                    isCompleted = true;
                    loadedCount++;
                });

                // 完了を待つ
                yield return new WaitUntil(() => isCompleted);

                // 仮想オブジェクト削除
                DestroyImmediate(dummyImage.gameObject);

                // 進捗通知
                float progress = (float)loadedCount / totalCards;
                onProgress?.Invoke(progress);

                // フレーム間の間隔
                yield return null;
            }

            Debug.Log($"[CardImageSystem] Preloaded {loadedCount} images");
        }

        #endregion

        #region Cache Management

        private void CacheImage(string imageKey, Sprite sprite)
        {
            // キャッシュサイズ制限チェック
            if (imageCache.Count >= maxCacheSize)
            {
                EvictOldestCacheEntry();
            }

            // キャッシュに追加
            var cachedImage = new CachedImage
            {
                sprite = sprite,
                imageKey = imageKey,
                cacheTime = Time.time,
                lastAccessTime = Time.time,
                memorySize = EstimateMemorySize(sprite)
            };

            imageCache[imageKey] = cachedImage;
            cacheAccessOrder.Enqueue(imageKey);
        }

        private void EvictOldestCacheEntry()
        {
            if (cacheAccessOrder.Count > 0)
            {
                string oldestKey = cacheAccessOrder.Dequeue();
                if (imageCache.ContainsKey(oldestKey))
                {
                    var cachedImage = imageCache[oldestKey];
                    imageCache.Remove(oldestKey);

                    // メモリクリーンアップ
                    if (cachedImage.sprite != null && cachedImage.sprite.texture != null)
                    {
                        DestroyImmediate(cachedImage.sprite.texture);
                        DestroyImmediate(cachedImage.sprite);
                    }
                }
            }
        }

        private void UpdateCacheAccessOrder(string imageKey)
        {
            // アクセス順序を更新（簡易的な実装）
            var tempQueue = new Queue<string>();
            while (cacheAccessOrder.Count > 0)
            {
                string key = cacheAccessOrder.Dequeue();
                if (key != imageKey)
                {
                    tempQueue.Enqueue(key);
                }
            }
            cacheAccessOrder = tempQueue;
            cacheAccessOrder.Enqueue(imageKey);
        }

        private void ClearCache()
        {
            foreach (var cachedImage in imageCache.Values)
            {
                if (cachedImage.sprite != null && cachedImage.sprite.texture != null)
                {
                    DestroyImmediate(cachedImage.sprite.texture);
                    DestroyImmediate(cachedImage.sprite);
                }
            }
            imageCache.Clear();
            cacheAccessOrder.Clear();
        }

        private int EstimateMemorySize(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return 0;
            
            var texture = sprite.texture;
            return texture.width * texture.height * 4; // RGBA = 4 bytes
        }

        #endregion

        #region Utility Methods

        private string GetImageKey(Card card, CardImageType imageType)
        {
            return $"{card.CardData.CardID}_{imageType}";
        }

        private string GetImagePath(Card card, CardImageType imageType)
        {
            string baseDir = Application.streamingAssetsPath + "/" + imageBasePath;
            string typeDir = "";

            switch (imageType)
            {
                case CardImageType.Thumbnail:
                    typeDir = thumbnailPath;
                    break;
                case CardImageType.Preview:
                    typeDir = previewPath;
                    break;
                case CardImageType.Full:
                    typeDir = fullPath;
                    break;
            }

            return baseDir + typeDir + card.CardData.CardID + ".png";
        }

        private string GetResourcePath(Card card, CardImageType imageType)
        {
            string typeDir = "";

            switch (imageType)
            {
                case CardImageType.Thumbnail:
                    typeDir = thumbnailPath;
                    break;
                case CardImageType.Preview:
                    typeDir = previewPath;
                    break;
                case CardImageType.Full:
                    typeDir = fullPath;
                    break;
            }

            return imageBasePath + typeDir + card.CardData.CardID;
        }

        private Sprite CreateSpriteFromTexture(Texture2D texture, CardImageType imageType)
        {
            if (texture == null) return null;

            // テクスチャ品質設定
            texture.filterMode = filterMode;
            texture.wrapMode = TextureWrapMode.Clamp;

            // スプライト作成
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), 
                Vector2.one * 0.5f, 100f);

            return sprite;
        }

        private void SetImageWithAnimation(Image targetImage, Sprite sprite)
        {
            if (targetImage == null) return;

            // アニメーション付きで画像設定
            targetImage.sprite = sprite;
            
            if (sprite != null)
            {
                // フェードインアニメーション
                var canvasGroup = targetImage.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = targetImage.gameObject.AddComponent<CanvasGroup>();
                }

                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeCurve);
            }
        }

        private void CancelAllRequests()
        {
            // アクティブリクエストをキャンセル
            foreach (var request in activeRequests.Values)
            {
                StopCoroutine(LoadImageCoroutine(request));
            }
            activeRequests.Clear();

            // キューをクリア
            loadQueue.Clear();
            currentConcurrentLoads = 0;
        }

        #endregion

        #region Event Handlers

        private void OnCardImageRequested(CardImageRequestEvent evt)
        {
            RequestCardImage(evt.Card, evt.ImageType, evt.TargetImage, evt.OnComplete);
        }

        private void OnCardPreviewRequested(CardPreviewRequestEvent evt)
        {
            ShowCardPreview(evt.Card, evt.Position);
        }

        private void OnMemoryPressure(MemoryPressureEvent evt)
        {
            if (enableMemoryOptimization)
            {
                // メモリ圧迫時の緊急対応
                int targetCacheSize = maxCacheSize / 2;
                while (imageCache.Count > targetCacheSize)
                {
                    EvictOldestCacheEntry();
                }
                
                // ガベージコレクション実行
                System.GC.Collect();
                
                Debug.Log($"[CardImageSystem] Memory pressure response: cache reduced to {imageCache.Count}");
            }
        }

        #endregion

        #region Debug Methods

        /// <summary>
        /// キャッシュ統計を取得
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            int totalMemory = 0;
            foreach (var cached in imageCache.Values)
            {
                totalMemory += cached.memorySize;
            }

            return new CacheStatistics
            {
                CacheSize = imageCache.Count,
                MaxCacheSize = maxCacheSize,
                TotalMemoryUsage = totalMemory,
                ActiveRequests = activeRequests.Count,
                QueuedRequests = loadQueue.Count
            };
        }

        [ContextMenu("Log Cache Statistics")]
        private void LogCacheStatistics()
        {
            var stats = GetCacheStatistics();
            Debug.Log($"=== CardImageSystem Cache Statistics ===");
            Debug.Log($"Cache Size: {stats.CacheSize}/{stats.MaxCacheSize}");
            Debug.Log($"Memory Usage: {stats.TotalMemoryUsage / 1024 / 1024:F2} MB");
            Debug.Log($"Active Requests: {stats.ActiveRequests}");
            Debug.Log($"Queued Requests: {stats.QueuedRequests}");
        }

        #endregion
    }

    #region Support Classes

    /// <summary>
    /// カード画像リクエスト
    /// </summary>
    public class CardImageRequest
    {
        public Card card;
        public CardImageType imageType;
        public string imageKey;
        public List<Image> targetImages = new List<Image>();
        public List<System.Action<Sprite>> callbacks = new List<System.Action<Sprite>>();
        public float requestTime;
        public Sprite loadedSprite;
        public bool isSuccess;
    }

    /// <summary>
    /// キャッシュされた画像
    /// </summary>
    public class CachedImage
    {
        public Sprite sprite;
        public string imageKey;
        public float cacheTime;
        public float lastAccessTime;
        public int memorySize;
    }

    /// <summary>
    /// カード画像タイプ
    /// </summary>
    public enum CardImageType
    {
        Thumbnail,  // サムネイル（120x168）
        Preview,    // プレビュー（240x336）
        Full        // フルサイズ（480x672）
    }

    /// <summary>
    /// キャッシュ統計
    /// </summary>
    public class CacheStatistics
    {
        public int CacheSize;
        public int MaxCacheSize;
        public int TotalMemoryUsage;
        public int ActiveRequests;
        public int QueuedRequests;
    }

    /// <summary>
    /// カードプレビューウィンドウ
    /// </summary>
    public class CardPreviewWindow : MonoBehaviour
    {
        private CardImageSystem imageSystem;
        private Image previewImage;
        private Text cardInfoText;  // Changed from TextMeshProUGUI to Text
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;

        public void Initialize(CardImageSystem imageSystem)
        {
            this.imageSystem = imageSystem;
            SetupUI();
        }

        private void SetupUI()
        {
            // Canvas Group設定
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            rectTransform = GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(300, 400);

            // 背景パネル
            var backgroundPanel = new GameObject("BackgroundPanel");
            backgroundPanel.transform.SetParent(transform, false);
            
            var backgroundImage = backgroundPanel.AddComponent<Image>();
            backgroundImage.color = new Color(0, 0, 0, 0.8f);
            
            var backgroundRect = backgroundPanel.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.sizeDelta = Vector2.zero;

            // プレビュー画像
            var previewImageObj = new GameObject("PreviewImage");
            previewImageObj.transform.SetParent(transform, false);
            
            previewImage = previewImageObj.AddComponent<Image>();
            previewImage.preserveAspect = true;
            
            var previewRect = previewImageObj.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.1f, 0.3f);
            previewRect.anchorMax = new Vector2(0.9f, 0.9f);
            previewRect.sizeDelta = Vector2.zero;

            // カード情報テキスト
            var cardInfoObj = new GameObject("CardInfoText");
            cardInfoObj.transform.SetParent(transform, false);
            
            cardInfoText = cardInfoObj.AddComponent<Text>();  // Changed from TextMeshProUGUI to Text
            cardInfoText.fontSize = 12;
            cardInfoText.color = Color.white;
            cardInfoText.alignment = TextAnchor.MiddleCenter;  // Changed from TextAlignmentOptions.Center
            cardInfoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");  // Added font reference
            
            var textRect = cardInfoObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.1f);
            textRect.anchorMax = new Vector2(0.9f, 0.3f);
            textRect.sizeDelta = Vector2.zero;
        }

        public void ShowPreview(Card card, Vector3 position)
        {
            if (card == null) return;

            // 位置設定
            transform.position = position;

            // カード画像要求
            imageSystem.RequestCardImage(card, CardImageType.Preview, previewImage);

            // カード情報設定
            cardInfoText.text = GetCardPreviewText(card);

            // フェードイン
            gameObject.SetActive(true);
            canvasGroup.DOFade(1f, 0.2f);
        }

        public void HidePreview()
        {
            canvasGroup.DOFade(0f, 0.2f).OnComplete(() => {
                gameObject.SetActive(false);
            });
        }

        private string GetCardPreviewText(Card card)
        {
            return $"{card.CardData.CardName}\n" +  // Removed <b> tags as Text doesn't support rich text by default
                   $"Type: {card.CardData.CardType}\n" +
                   $"Rarity: {card.CardData.Rarity}";
        }
    }

    #endregion

    #region Event Classes

    public class CardImageRequestEvent
    {
        public Card Card { get; set; }
        public CardImageType ImageType { get; set; }
        public Image TargetImage { get; set; }
        public System.Action<Sprite> OnComplete { get; set; }
    }

    public class CardPreviewRequestEvent
    {
        public Card Card { get; set; }
        public Vector3 Position { get; set; }
    }

    public class MemoryPressureEvent
    {
        public float MemoryUsage { get; set; }
        public float MemoryLimit { get; set; }
    }

    #endregion
}