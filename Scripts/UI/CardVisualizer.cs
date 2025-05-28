using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PokemonTCG.Cards.Runtime;
using PokemonTCG.Core.Data;

namespace PokemonTCG.UI
{
    /// <summary>
    /// カード表示システム - カードデータからUI表示への変換とアニメーション
    /// 個別のカードオブジェクトの表示・操作を管理
    /// </summary>
    public class CardVisualizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [Header("Card Visual Components")]
        public Image cardBackground;
        public Image cardArt;
        public TextMeshProUGUI cardName;
        public TextMeshProUGUI cardCost;
        public TextMeshProUGUI cardDescription;
        public TextMeshProUGUI cardType;
        public TextMeshProUGUI cardHP;
        public TextMeshProUGUI cardAttack;
        public TextMeshProUGUI cardDefense;
        
        [Header("Card State Visuals")]
        public Image cardFrame;
        public Image glowEffect;
        public GameObject selectedIndicator;
        public GameObject tappedIndicator;
        public GameObject damageOverlay;
        public Slider hpSlider;
        
        [Header("Animation Settings")]
        public float hoverScale = 1.1f;
        public float hoverDuration = 0.2f;
        public float selectScale = 1.05f;
        public float dragScale = 0.9f;
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Card Colors")]
        public Color normalColor = Color.white;
        public Color hoverColor = Color.yellow;
        public Color selectedColor = Color.green;
        public Color unavailableColor = Color.gray;
        
        // Private fields
        private Card cardData;
        private CardVisualizerState currentState;
        private Vector3 originalScale;
        private Vector3 originalPosition;
        private Transform originalParent;
        private Canvas parentCanvas;
        private GraphicRaycaster parentRaycaster;
        private bool isDragging = false;
        private bool isHovered = false;
        private bool isSelected = false;
        private bool isInteractable = true;
        
        // Animation
        private Tween currentScaleTweener;
        private Tween currentMoveTweener;
        private Coroutine glowCoroutine;
        
        // Events
        public System.Action<CardVisualizer> OnCardClicked;
        public System.Action<CardVisualizer> OnCardHovered;
        public System.Action<CardVisualizer> OnCardUnhovered;
        public System.Action<CardVisualizer, Vector2> OnCardDragStarted;
        public System.Action<CardVisualizer, Vector2> OnCardDragging;
        public System.Action<CardVisualizer, Vector2> OnCardDragEnded;
        
        #region Card State Management
        public enum CardVisualizerState
        {
            Normal,
            Hovered,
            Selected,
            Dragging,
            Tapped,
            Unavailable,
            InPlay,
            InHand
        }
        
        public Card CardData => cardData;
        public CardVisualizerState CurrentState => currentState;
        public bool IsInteractable => isInteractable;
        #endregion
        
        #region Initialization
        private void Awake()
        {
            originalScale = transform.localScale;
            parentCanvas = GetComponentInParent<Canvas>();
            parentRaycaster = GetComponentInParent<GraphicRaycaster>();
            
            // Initialize visual components if not assigned
            FindVisualComponents();
        }
        
        private void Start()
        {
            // Setup initial state
            SetCardState(CardVisualizerState.Normal);
        }
        
        private void FindVisualComponents()
        {
            if (cardBackground == null) cardBackground = transform.Find("Background")?.GetComponent<Image>();
            if (cardArt == null) cardArt = transform.Find("CardArt")?.GetComponent<Image>();
            if (cardName == null) cardName = GetComponentInChildren<TextMeshProUGUI>();
            if (cardFrame == null) cardFrame = transform.Find("Frame")?.GetComponent<Image>();
            if (glowEffect == null) glowEffect = transform.Find("GlowEffect")?.GetComponent<Image>();
            if (selectedIndicator == null) selectedIndicator = transform.Find("SelectedIndicator")?.gameObject;
            if (hpSlider == null) hpSlider = GetComponentInChildren<Slider>();
        }
        #endregion
        
        #region Card Data Binding
        public void SetCardData(Card card)
        {
            cardData = card;
            UpdateVisualDisplay();
        }
        
        private void UpdateVisualDisplay()
        {
            if (cardData == null) return;
            
            // Update text elements (Fixed property access)
            if (cardName != null) cardName.text = cardData.CardData.CardName;
            if (cardDescription != null) cardDescription.text = cardData.CardData.RulesText; // Fixed: Use RulesText instead of Description
            if (cardType != null) cardType.text = GetCardTypeString();
            
            // Update Pokemon-specific data (Fixed property access)
            if (cardData.IsPokemonCard)
            {
                var pokemonData = cardData.GetPokemonData();
                if (pokemonData != null)
                {
                    if (cardHP != null) cardHP.text = pokemonData.HP.ToString(); // Fixed: HP instead of MaxHP
                    if (cardAttack != null) cardAttack.text = pokemonData.Attacks.Count > 0 ? 
                        pokemonData.Attacks[0].Damage.ToString() : "0";
                    
                    // Update HP slider
                    UpdateHPDisplay();
                }
            }
            
            // Update card art
            UpdateCardArt();
            
            // Update card frame based on type
            UpdateCardFrame();
        }
        
        private string GetCardTypeString()
        {
            if (cardData == null) return "Unknown";
            
            if (cardData.IsPokemonCard) return "Pokemon";
            if (cardData.IsTrainerCard) return "Trainer";
            if (cardData.IsEnergyCard) return "Energy";
            return "Unknown";
        }
        
        private void UpdateCardArt()
        {
            if (cardArt != null && cardData != null)
            {
                // Load card art from CardData
                if (cardData.CardData.CardArt != null)
                {
                    cardArt.sprite = cardData.CardData.CardArt;
                }
                else
                {
                    // Use placeholder or default art
                    cardArt.color = GetCardTypeColor();
                }
            }
        }
        
        private void UpdateCardFrame()
        {
            if (cardFrame != null && cardData != null)
            {
                cardFrame.color = GetCardTypeColor();
            }
        }
        
        private void UpdateHPDisplay()
        {
            if (hpSlider != null && cardData?.IsPokemonCard == true)
            {
                var pokemonData = cardData.GetPokemonData();
                if (pokemonData != null)
                {
                    // Note: We need to get current HP from somewhere, using HP as placeholder
                    float currentHP = pokemonData.HP; // This should come from game state
                    float maxHP = pokemonData.HP; // Fixed: Use HP instead of MaxHP
                    hpSlider.value = currentHP / maxHP;
                    
                    // Show damage overlay if damaged
                    if (damageOverlay != null)
                    {
                        damageOverlay.SetActive(currentHP < maxHP);
                    }
                }
            }
        }
        
        private Color GetCardTypeColor()
        {
            if (cardData == null) return Color.white;
            
            if (cardData.IsPokemonCard) return Color.yellow;
            if (cardData.IsTrainerCard) return Color.blue;
            if (cardData.IsEnergyCard) return Color.green;
            return Color.white;
        }
        #endregion
        
        #region Card State Management
        public void SetCardState(CardVisualizerState newState)
        {
            CardVisualizerState previousState = currentState;
            currentState = newState;
            
            UpdateVisualForState(newState, previousState);
        }
        
        private void UpdateVisualForState(CardVisualizerState newState, CardVisualizerState previousState)
        {
            // Stop current animations
            StopCurrentAnimations();
            
            switch (newState)
            {
                case CardVisualizerState.Normal:
                    SetNormalVisual();
                    break;
                case CardVisualizerState.Hovered:
                    SetHoveredVisual();
                    break;
                case CardVisualizerState.Selected:
                    SetSelectedVisual();
                    break;
                case CardVisualizerState.Dragging:
                    SetDraggingVisual();
                    break;
                case CardVisualizerState.Tapped:
                    SetTappedVisual();
                    break;
                case CardVisualizerState.Unavailable:
                    SetUnavailableVisual();
                    break;
            }
        }
        
        private void SetNormalVisual()
        {
            AnimateScale(originalScale, hoverDuration);
            SetCardColor(normalColor);
            SetGlow(false);
            SetSelectedIndicator(false);
        }
        
        private void SetHoveredVisual()
        {
            AnimateScale(originalScale * hoverScale, hoverDuration);
            SetCardColor(hoverColor);
            SetGlow(true);
        }
        
        private void SetSelectedVisual()
        {
            AnimateScale(originalScale * selectScale, hoverDuration);
            SetCardColor(selectedColor);
            SetGlow(true);
            SetSelectedIndicator(true);
        }
        
        private void SetDraggingVisual()
        {
            AnimateScale(originalScale * dragScale, hoverDuration * 0.5f);
            SetCardColor(hoverColor);
            SetGlow(true);
        }
        
        private void SetTappedVisual()
        {
            // Rotate card to indicate tapped state
            AnimateRotation(new Vector3(0, 0, 90), hoverDuration);
            SetCardColor(normalColor);
            if (tappedIndicator != null) tappedIndicator.SetActive(true);
        }
        
        private void SetUnavailableVisual()
        {
            SetCardColor(unavailableColor);
            SetGlow(false);
            isInteractable = false;
        }
        #endregion
        
        #region Animation Methods
        private void AnimateScale(Vector3 targetScale, float duration)
        {
            currentScaleTweener?.Kill();
            currentScaleTweener = transform.DOScale(targetScale, duration)
                .SetEase(scaleCurve);
        }
        
        private void AnimateRotation(Vector3 targetRotation, float duration)
        {
            transform.DORotate(targetRotation, duration)
                .SetEase(Ease.OutBack);
        }
        
        private void AnimatePosition(Vector3 targetPosition, float duration)
        {
            currentMoveTweener?.Kill();
            currentMoveTweener = transform.DOMove(targetPosition, duration)
                .SetEase(Ease.OutQuad);
        }
        
        private void SetCardColor(Color color)
        {
            if (cardBackground != null)
            {
                cardBackground.DOColor(color, hoverDuration * 0.5f);
            }
        }
        
        private void SetGlow(bool enabled)
        {
            if (glowEffect != null)
            {
                if (enabled)
                {
                    glowEffect.gameObject.SetActive(true);
                    StartGlowEffect();
                }
                else
                {
                    glowEffect.gameObject.SetActive(false);
                    StopGlowEffect();
                }
            }
        }
        
        private void SetSelectedIndicator(bool enabled)
        {
            if (selectedIndicator != null)
            {
                selectedIndicator.SetActive(enabled);
            }
        }
        
        private void StartGlowEffect()
        {
            if (glowCoroutine != null) StopCoroutine(glowCoroutine);
            glowCoroutine = StartCoroutine(GlowAnimation());
        }
        
        private void StopGlowEffect()
        {
            if (glowCoroutine != null)
            {
                StopCoroutine(glowCoroutine);
                glowCoroutine = null;
            }
        }
        
        private IEnumerator GlowAnimation()
        {
            float time = 0;
            while (true)
            {
                float alpha = Mathf.Sin(time * 2f) * 0.5f + 0.5f;
                if (glowEffect != null)
                {
                    var color = glowEffect.color;
                    color.a = alpha * 0.8f;
                    glowEffect.color = color;
                }
                time += Time.deltaTime;
                yield return null;
            }
        }
        
        private void StopCurrentAnimations()
        {
            currentScaleTweener?.Kill();
            currentMoveTweener?.Kill();
            StopGlowEffect();
        }
        #endregion
        
        #region Event Handlers
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isInteractable || isDragging) return;
            
            isHovered = true;
            if (!isSelected)
            {
                SetCardState(CardVisualizerState.Hovered);
            }
            
            OnCardHovered?.Invoke(this);
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isInteractable || isDragging) return;
            
            isHovered = false;
            if (!isSelected)
            {
                SetCardState(CardVisualizerState.Normal);
            }
            
            OnCardUnhovered?.Invoke(this);
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isInteractable || isDragging) return;
            
            isSelected = !isSelected;
            SetCardState(isSelected ? CardVisualizerState.Selected : 
                         (isHovered ? CardVisualizerState.Hovered : CardVisualizerState.Normal));
            
            OnCardClicked?.Invoke(this);
        }
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isInteractable) return;
            
            isDragging = true;
            originalPosition = transform.position;
            originalParent = transform.parent;
            
            // Move to top of hierarchy for rendering
            if (parentCanvas != null)
            {
                transform.SetParent(parentCanvas.transform, true);
            }
            
            SetCardState(CardVisualizerState.Dragging);
            OnCardDragStarted?.Invoke(this, eventData.position);
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            if (!isInteractable || !isDragging) return;
            
            // Update card position to follow cursor
            Vector2 screenPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out screenPosition);
            
            transform.localPosition = screenPosition;
            OnCardDragging?.Invoke(this, eventData.position);
        }
        
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isInteractable || !isDragging) return;
            
            isDragging = false;
            
            // Return to original parent
            if (originalParent != null)
            {
                transform.SetParent(originalParent, true);
            }
            
            // Animate back to original position or new valid position
            AnimatePosition(originalPosition, hoverDuration);
            
            SetCardState(isHovered ? CardVisualizerState.Hovered : CardVisualizerState.Normal);
            OnCardDragEnded?.Invoke(this, eventData.position);
        }
        #endregion
        
        #region Public Interface
        public void PlayCard()
        {
            // Animate card being played
            StartCoroutine(PlayCardAnimation());
        }
        
        public void TapCard()
        {
            SetCardState(CardVisualizerState.Tapped);
        }
        
        public void UntapCard()
        {
            AnimateRotation(Vector3.zero, hoverDuration);
            if (tappedIndicator != null) tappedIndicator.SetActive(false);
            SetCardState(CardVisualizerState.Normal);
        }
        
        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;
            if (!interactable)
            {
                SetCardState(CardVisualizerState.Unavailable);
            }
            else
            {
                SetCardState(CardVisualizerState.Normal);
            }
        }
        
        public void UpdateCardData(Card newCardData)
        {
            SetCardData(newCardData);
        }
        
        public void DestroyCard()
        {
            StartCoroutine(DestroyCardAnimation());
        }
        
        private IEnumerator PlayCardAnimation()
        {
            // Animate card being played (scale up and fade)
            var originalColor = cardBackground.color;
            
            transform.DOScale(originalScale * 1.5f, 0.3f);
            cardBackground.DOFade(0.7f, 0.3f);
            
            yield return new WaitForSeconds(0.3f);
            
            transform.DOScale(originalScale, 0.2f);
            cardBackground.DOColor(originalColor, 0.2f);
        }
        
        private IEnumerator DestroyCardAnimation()
        {
            // Animate card destruction (spin and shrink)
            transform.DORotate(new Vector3(0, 0, 720), 0.5f, RotateMode.FastBeyond360);
            transform.DOScale(Vector3.zero, 0.5f).SetEase(Ease.InBack);
            cardBackground.DOFade(0, 0.5f);
            
            yield return new WaitForSeconds(0.5f);
            
            Destroy(gameObject);
        }
        #endregion
        
        #region Cleanup
        private void OnDestroy()
        {
            StopCurrentAnimations();
            StopGlowEffect();
        }
        #endregion
    }
}