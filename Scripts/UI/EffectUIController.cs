using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using DG.Tweening;
using PokemonTCG.Cards.Effects;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.UI
{
    /// <summary>
    /// エフェクトUI制御システム
    /// 視覚エフェクト、パーティクル、アニメーション、フローティングテキストを管理
    /// </summary>
    public class EffectUIController : MonoBehaviour
    {
        [Header("Effect Systems")]
        public Transform effectContainer;
        public Transform particleContainer;
        public Transform floatingTextContainer;
        public Transform screenEffectsContainer;
        
        [Header("Particle Prefabs")]
        public GameObject damageParticlePrefab;
        public GameObject healParticlePrefab;
        public GameObject criticalHitParticlePrefab;
        public GameObject statusEffectParticlePrefab;
        public GameObject energyParticlePrefab;
        public GameObject evolutionParticlePrefab;
        
        [Header("Floating Text")]
        public GameObject floatingTextPrefab;
        public Font floatingTextFont;
        public Color damageTextColor = Color.red;
        public Color healTextColor = Color.green;
        public Color criticalTextColor = Color.yellow;
        public Color statusTextColor = Color.purple;
        
        [Header("Screen Effects")]
        public Image screenFlashOverlay;
        public Image vignetteOverlay;
        public GameObject shakeCamera;
        public RectTransform uiShakeRoot;
        
        [Header("Animation Settings")]
        public float particleLifetime = 3f;
        public float floatingTextSpeed = 100f;
        public float floatingTextLifetime = 2f;
        public float screenFlashDuration = 0.2f;
        public float shakeIntensity = 10f;
        public float shakeDuration = 0.5f;
        
        [Header("Effect Pools")]
        public int particlePoolSize = 20;
        public int floatingTextPoolSize = 10;
        
        // Private fields
        private Dictionary<string, Queue<GameObject>> particlePools = new Dictionary<string, Queue<GameObject>>();
        private Queue<GameObject> floatingTextPool = new Queue<GameObject>();
        private List<ActiveEffect> activeEffects = new List<ActiveEffect>();
        private bool isInitialized = false;
        private Coroutine shakeCoroutine;
        
        // Effect tracking
        private struct ActiveEffect
        {
            public string effectId;
            public GameObject effectObject;
            public float startTime;
            public float duration;
            public EffectType effectType;
        }
        
        // Events
        public System.Action<string, Vector3> OnEffectStarted;
        public System.Action<string> OnEffectCompleted;
        public System.Action<string, float> OnEffectProgress;
        
        #region Initialization
        public void Initialize()
        {
            if (isInitialized) return;
            
            Debug.Log("[EffectUIController] Initializing Effect UI Controller...");
            
            // Initialize components
            InitializeComponents();
            
            // Initialize object pools
            InitializeObjectPools();
            
            // Subscribe to events
            SubscribeToEvents();
            
            isInitialized = true;
            Debug.Log("[EffectUIController] Effect UI Controller initialized successfully");
        }
        
        private void InitializeComponents()
        {
            // Initialize screen effects
            if (screenFlashOverlay != null)
            {
                screenFlashOverlay.color = new Color(1, 1, 1, 0);
                screenFlashOverlay.gameObject.SetActive(false);
            }
            
            if (vignetteOverlay != null)
            {
                vignetteOverlay.color = new Color(0, 0, 0, 0);
                vignetteOverlay.gameObject.SetActive(false);
            }
        }
        
        private void InitializeObjectPools()
        {
            // Initialize particle pools
            InitializeParticlePool("Damage", damageParticlePrefab);
            InitializeParticlePool("Heal", healParticlePrefab);
            InitializeParticlePool("Critical", criticalHitParticlePrefab);
            InitializeParticlePool("Status", statusEffectParticlePrefab);
            InitializeParticlePool("Energy", energyParticlePrefab);
            InitializeParticlePool("Evolution", evolutionParticlePrefab);
            
            // Initialize floating text pool
            InitializeFloatingTextPool();
        }
        
        private void InitializeParticlePool(string poolName, GameObject prefab)
        {
            if (prefab == null) return;
            
            Queue<GameObject> pool = new Queue<GameObject>();
            
            for (int i = 0; i < particlePoolSize; i++)
            {
                GameObject particle = Instantiate(prefab, particleContainer);
                particle.SetActive(false);
                pool.Enqueue(particle);
            }
            
            particlePools[poolName] = pool;
        }
        
        private void InitializeFloatingTextPool()
        {
            if (floatingTextPrefab == null) return;
            
            for (int i = 0; i < floatingTextPoolSize; i++)
            {
                GameObject textObj = Instantiate(floatingTextPrefab, floatingTextContainer);
                textObj.SetActive(false);
                floatingTextPool.Enqueue(textObj);
            }
        }
        
        private void SubscribeToEvents()
        {
            // Subscribe to EventBus events
            EventBus.On<EffectTriggeredEvent>(OnEffectTriggered);
            EventBus.On<DamageDealtEvent>(OnDamageDealt);
            EventBus.On<HealingAppliedEvent>(OnHealingApplied);
            EventBus.On<StatusEffectAppliedEvent>(OnStatusEffectApplied);
            EventBus.On<CriticalHitEvent>(OnCriticalHit);
            EventBus.On<EnergyAttachedEvent>(OnEnergyAttached);
            EventBus.On<EvolutionEvent>(OnEvolution);
        }
        #endregion
        
        #region Public Interface
        public string PlayDamageEffect(Vector3 position, int damage, bool isCritical = false)
        {
            string effectId = System.Guid.NewGuid().ToString();
            
            // Spawn damage particle
            var particle = GetParticleFromPool(isCritical ? "Critical" : "Damage");
            if (particle != null)
            {
                particle.transform.position = position;
                particle.SetActive(true);
                
                var particleSystem = particle.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    particleSystem.Play();
                }
                
                RegisterActiveEffect(effectId, particle, particleLifetime, EffectType.Damage);
            }
            
            // Spawn floating damage text
            ShowFloatingText(position, $"-{damage}", isCritical ? criticalTextColor : damageTextColor);
            
            // Screen effects for critical hits
            if (isCritical)
            {
                StartScreenFlash(Color.yellow);
                StartScreenShake(shakeIntensity * 1.5f, shakeDuration);
            }
            else
            {
                StartScreenShake(shakeIntensity, shakeDuration * 0.5f);
            }
            
            OnEffectStarted?.Invoke(effectId, position);
            return effectId;
        }
        
        public string PlayHealEffect(Vector3 position, int healAmount)
        {
            string effectId = System.Guid.NewGuid().ToString();
            
            // Spawn heal particle
            var particle = GetParticleFromPool("Heal");
            if (particle != null)
            {
                particle.transform.position = position;
                particle.SetActive(true);
                
                var particleSystem = particle.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    particleSystem.Play();
                }
                
                RegisterActiveEffect(effectId, particle, particleLifetime, EffectType.Heal);
            }
            
            // Spawn floating heal text
            ShowFloatingText(position, $"+{healAmount}", healTextColor);
            
            OnEffectStarted?.Invoke(effectId, position);
            return effectId;
        }
        
        public string PlayStatusEffect(Vector3 position, string statusName, Color statusColor)
        {
            string effectId = System.Guid.NewGuid().ToString();
            
            // Spawn status particle
            var particle = GetParticleFromPool("Status");
            if (particle != null)
            {
                particle.transform.position = position;
                particle.SetActive(true);
                
                var particleSystem = particle.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    var main = particleSystem.main;
                    main.startColor = statusColor;
                    particleSystem.Play();
                }
                
                RegisterActiveEffect(effectId, particle, particleLifetime, EffectType.Status);
            }
            
            // Spawn floating status text
            ShowFloatingText(position, statusName, statusTextColor);
            
            OnEffectStarted?.Invoke(effectId, position);
            return effectId;
        }
        
        public string PlayEnergyEffect(Vector3 position, Color energyColor)
        {
            string effectId = System.Guid.NewGuid().ToString();
            
            // Spawn energy particle
            var particle = GetParticleFromPool("Energy");
            if (particle != null)
            {
                particle.transform.position = position;
                particle.SetActive(true);
                
                var particleSystem = particle.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    var main = particleSystem.main;
                    main.startColor = energyColor;
                    particleSystem.Play();
                }
                
                RegisterActiveEffect(effectId, particle, particleLifetime, EffectType.Energy);
            }
            
            OnEffectStarted?.Invoke(effectId, position);
            return effectId;
        }
        
        public string PlayEvolutionEffect(Vector3 position)
        {
            string effectId = System.Guid.NewGuid().ToString();
            
            // Spawn evolution particle
            var particle = GetParticleFromPool("Evolution");
            if (particle != null)
            {
                particle.transform.position = position;
                particle.SetActive(true);
                
                var particleSystem = particle.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    particleSystem.Play();
                }
                
                RegisterActiveEffect(effectId, particle, particleLifetime * 2f, EffectType.Evolution);
            }
            
            // Evolution screen effects
            StartScreenFlash(Color.white);
            ShowFloatingText(position, "EVOLUTION!", Color.white);
            
            OnEffectStarted?.Invoke(effectId, position);
            return effectId;
        }
        
        public void StartScreenFlash(Color flashColor)
        {
            if (screenFlashOverlay != null)
            {
                screenFlashOverlay.gameObject.SetActive(true);
                screenFlashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0);
                
                // Flash animation
                var sequence = DOTween.Sequence();
                sequence.Append(screenFlashOverlay.DOFade(0.8f, screenFlashDuration * 0.3f));
                sequence.Append(screenFlashOverlay.DOFade(0f, screenFlashDuration * 0.7f));
                sequence.OnComplete(() => screenFlashOverlay.gameObject.SetActive(false));
            }
        }
        
        public void StartScreenShake(float intensity, float duration)
        {
            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);
            
            shakeCoroutine = StartCoroutine(ScreenShakeCoroutine(intensity, duration));
        }
        
        public void ShowFloatingText(Vector3 worldPosition, string text, Color color)
        {
            var textObj = GetFloatingTextFromPool();
            if (textObj != null)
            {
                textObj.transform.position = worldPosition;
                textObj.SetActive(true);
                
                var textComponent = textObj.GetComponent<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = text;
                    textComponent.color = color;
                    
                    // Animate floating text
                    StartCoroutine(FloatingTextAnimation(textObj, textComponent));
                }
            }
        }
        
        public void StartVignetteEffect(float intensity, float duration)
        {
            if (vignetteOverlay != null)
            {
                vignetteOverlay.gameObject.SetActive(true);
                vignetteOverlay.color = new Color(0, 0, 0, 0);
                
                var sequence = DOTween.Sequence();
                sequence.Append(vignetteOverlay.DOFade(intensity, duration * 0.2f));
                sequence.AppendInterval(duration * 0.6f);
                sequence.Append(vignetteOverlay.DOFade(0f, duration * 0.2f));
                sequence.OnComplete(() => vignetteOverlay.gameObject.SetActive(false));
            }
        }
        
        public void StopEffect(string effectId)
        {
            var effect = activeEffects.FirstOrDefault(e => e.effectId == effectId);
            if (effect.effectObject != null)
            {
                // Stop particle system
                var particleSystem = effect.effectObject.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    particleSystem.Stop();
                }
                
                // Return to pool after a delay
                StartCoroutine(ReturnToPoolDelayed(effect.effectObject, GetPoolNameForEffect(effect.effectType), 1f));
                
                activeEffects.RemoveAll(e => e.effectId == effectId);
                OnEffectCompleted?.Invoke(effectId);
            }
        }
        
        public void StopAllEffects()
        {
            foreach (var effect in activeEffects)
            {
                StopEffect(effect.effectId);
            }
            activeEffects.Clear();
        }
        
        public List<string> GetActiveEffects()
        {
            return activeEffects.Select(e => e.effectId).ToList();
        }
        
        public float GetEffectProgress(string effectId)
        {
            var effect = activeEffects.FirstOrDefault(e => e.effectId == effectId);
            if (effect.effectObject != null)
            {
                float elapsed = Time.time - effect.startTime;
                return Mathf.Clamp01(elapsed / effect.duration);
            }
            return 1f;
        }
        #endregion
        
        #region Private Methods
        private GameObject GetParticleFromPool(string poolName)
        {
            if (particlePools.ContainsKey(poolName) && particlePools[poolName].Count > 0)
            {
                return particlePools[poolName].Dequeue();
            }
            return null;
        }
        
        private GameObject GetFloatingTextFromPool()
        {
            if (floatingTextPool.Count > 0)
            {
                return floatingTextPool.Dequeue();
            }
            return null;
        }
        
        private void ReturnParticleToPool(GameObject particle, string poolName)
        {
            if (particle != null && particlePools.ContainsKey(poolName))
            {
                particle.SetActive(false);
                particlePools[poolName].Enqueue(particle);
            }
        }
        
        private void ReturnFloatingTextToPool(GameObject textObj)
        {
            if (textObj != null)
            {
                textObj.SetActive(false);
                floatingTextPool.Enqueue(textObj);
            }
        }
        
        private void RegisterActiveEffect(string effectId, GameObject effectObject, float duration, EffectType effectType)
        {
            var effect = new ActiveEffect
            {
                effectId = effectId,
                effectObject = effectObject,
                startTime = Time.time,
                duration = duration,
                effectType = effectType
            };
            
            activeEffects.Add(effect);
            
            // Auto-cleanup after duration
            StartCoroutine(EffectCleanupCoroutine(effectId, duration));
        }
        
        private IEnumerator EffectCleanupCoroutine(string effectId, float duration)
        {
            yield return new WaitForSeconds(duration);
            
            var effect = activeEffects.FirstOrDefault(e => e.effectId == effectId);
            if (effect.effectObject != null)
            {
                string poolName = GetPoolNameForEffect(effect.effectType);
                
                if (poolName == "FloatingText")
                {
                    ReturnFloatingTextToPool(effect.effectObject);
                }
                else
                {
                    ReturnParticleToPool(effect.effectObject, poolName);
                }
                
                activeEffects.RemoveAll(e => e.effectId == effectId);
                OnEffectCompleted?.Invoke(effectId);
            }
        }
        
        private IEnumerator ReturnToPoolDelayed(GameObject obj, string poolName, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (poolName == "FloatingText")
            {
                ReturnFloatingTextToPool(obj);
            }
            else
            {
                ReturnParticleToPool(obj, poolName);
            }
        }
        
        private string GetPoolNameForEffect(EffectType effectType)
        {
            switch (effectType)
            {
                case EffectType.Damage: return "Damage";
                case EffectType.Heal: return "Heal";
                case EffectType.Status: return "Status";
                case EffectType.Energy: return "Energy";
                case EffectType.Evolution: return "Evolution";
                case EffectType.Critical: return "Critical";
                default: return "Damage";
            }
        }
        
        private IEnumerator ScreenShakeCoroutine(float intensity, float duration)
        {
            Transform shakeTarget = uiShakeRoot != null ? uiShakeRoot : 
                                  shakeCamera != null ? shakeCamera.transform : null;
            
            if (shakeTarget == null) yield break;
            
            Vector3 originalPosition = shakeTarget.localPosition;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                float currentIntensity = intensity * (1f - progress); // Fade out over time
                
                Vector3 randomOffset = new Vector3(
                    Random.Range(-currentIntensity, currentIntensity),
                    Random.Range(-currentIntensity, currentIntensity),
                    0f
                );
                
                shakeTarget.localPosition = originalPosition + randomOffset;
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Return to original position
            shakeTarget.localPosition = originalPosition;
            shakeCoroutine = null;
        }
        
        private IEnumerator FloatingTextAnimation(GameObject textObj, TextMeshProUGUI textComponent)
        {
            Vector3 startPosition = textObj.transform.position;
            Color startColor = textComponent.color;
            
            float elapsed = 0f;
            while (elapsed < floatingTextLifetime)
            {
                float progress = elapsed / floatingTextLifetime;
                
                // Move upward
                Vector3 currentPosition = startPosition + Vector3.up * (floatingTextSpeed * elapsed);
                textObj.transform.position = currentPosition;
                
                // Fade out
                Color currentColor = startColor;
                currentColor.a = 1f - progress;
                textComponent.color = currentColor;
                
                // Scale animation
                float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.2f;
                textObj.transform.localScale = Vector3.one * scale;
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Return to pool
            ReturnFloatingTextToPool(textObj);
        }
        
        private void Update()
        {
            if (!isInitialized) return;
            
            // Update effect progress
            foreach (var effect in activeEffects)
            {
                float progress = GetEffectProgress(effect.effectId);
                OnEffectProgress?.Invoke(effect.effectId, progress);
            }
        }
        #endregion
        
        #region Event Handlers
        private void OnEffectTriggered(EffectTriggeredEvent evt)
        {
            Vector3 position = evt.source != null ? evt.source.transform.position : Vector3.zero;
            
            switch (evt.effectName.ToLower())
            {
                case "damage":
                    PlayDamageEffect(position, 20);
                    break;
                case "heal":
                    PlayHealEffect(position, 15);
                    break;
                case "poison":
                    PlayStatusEffect(position, "POISON", Color.green);
                    break;
                case "burn":
                    PlayStatusEffect(position, "BURN", Color.red);
                    break;
                case "evolution":
                    PlayEvolutionEffect(position);
                    break;
            }
        }
        
        private void OnDamageDealt(DamageDealtEvent evt)
        {
            PlayDamageEffect(evt.TargetPosition, evt.Damage, evt.IsCritical);
        }
        
        private void OnHealingApplied(HealingAppliedEvent evt)
        {
            PlayHealEffect(evt.TargetPosition, evt.HealAmount);
        }
        
        private void OnStatusEffectApplied(StatusEffectAppliedEvent evt)
        {
            Color statusColor = GetStatusEffectColor(evt.StatusName);
            PlayStatusEffect(evt.TargetPosition, evt.StatusName, statusColor);
        }
        
        private void OnCriticalHit(CriticalHitEvent evt)
        {
            PlayDamageEffect(evt.TargetPosition, evt.Damage, true);
        }
        
        private void OnEnergyAttached(EnergyAttachedEvent evt)
        {
            Color energyColor = GetEnergyColor(evt.EnergyType);
            PlayEnergyEffect(evt.TargetPosition, energyColor);
        }
        
        private void OnEvolution(EvolutionEvent evt)
        {
            PlayEvolutionEffect(evt.TargetPosition);
        }
        
        private Color GetStatusEffectColor(string statusName)
        {
            switch (statusName.ToLower())
            {
                case "poison": return Color.green;
                case "burn": return Color.red;
                case "sleep": return Color.blue;
                case "paralysis": return Color.yellow;
                default: return Color.purple;
            }
        }
        
        private Color GetEnergyColor(string energyType)
        {
            switch (energyType.ToLower())
            {
                case "fire": return Color.red;
                case "water": return Color.blue;
                case "electric": return Color.yellow;
                case "grass": return Color.green;
                case "psychic": return Color.magenta;
                default: return Color.white;
            }
        }
        #endregion
        
        #region Cleanup
        public void Cleanup()
        {
            // Stop all effects
            StopAllEffects();
            
            // Stop shake coroutine
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
                shakeCoroutine = null;
            }
            
            // Unsubscribe from events
            EventBus.Off<EffectTriggeredEvent>(OnEffectTriggered);
            EventBus.Off<DamageDealtEvent>(OnDamageDealt);
            EventBus.Off<HealingAppliedEvent>(OnHealingApplied);
            EventBus.Off<StatusEffectAppliedEvent>(OnStatusEffectApplied);
            EventBus.Off<CriticalHitEvent>(OnCriticalHit);
            EventBus.Off<EnergyAttachedEvent>(OnEnergyAttached);
            EventBus.Off<EvolutionEvent>(OnEvolution);
            
            // Clean up events
            OnEffectStarted = null;
            OnEffectCompleted = null;
            OnEffectProgress = null;
            
            isInitialized = false;
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
        #endregion
    }
    
    // Enums
    public enum EffectType
    {
        Damage,
        Heal,
        Status,
        Energy,
        Evolution,
        Critical
    }
    
    // Event classes
    public class DamageDealtEvent
    {
        public int Damage { get; set; }
        public Vector3 TargetPosition { get; set; }
        public bool IsCritical { get; set; }
        public string Source { get; set; }
    }
    
    public class HealingAppliedEvent
    {
        public int HealAmount { get; set; }
        public Vector3 TargetPosition { get; set; }
        public string Source { get; set; }
    }
    
    public class StatusEffectAppliedEvent
    {
        public string StatusName { get; set; }
        public Vector3 TargetPosition { get; set; }
        public string Source { get; set; }
    }
    
    public class CriticalHitEvent
    {
        public int Damage { get; set; }
        public Vector3 TargetPosition { get; set; }
        public string Source { get; set; }
    }
    
    public class EnergyAttachedEvent
    {
        public string EnergyType { get; set; }
        public Vector3 TargetPosition { get; set; }
        public string Source { get; set; }
    }
    
    public class EvolutionEvent
    {
        public Vector3 TargetPosition { get; set; }
        public string PokemonName { get; set; }
    }
}