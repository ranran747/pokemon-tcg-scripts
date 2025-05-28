using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonTCG.Core.Data;
using PokemonTCG.Cards.Runtime;

namespace PokemonTCG.Cards.Runtime
{
    /// <summary>
    /// 場の管理システム
    /// バトル場・ベンチ・スタジアム・エネルギーの配置と管理
    /// 従来版・ポケット版両対応のフィールドシステム
    /// </summary>
    public class Field : MonoBehaviour
    {
        #region Fields

        [Header("フィールド設定")]
        [SerializeField] private string _playerId;
        [SerializeField] private FieldType _fieldType = FieldType.Standard;
        [SerializeField] private int _maxBenchSize = 5;
        [SerializeField] private bool _hasEnergyZone = false;

        [Header("エリア配置")]
        [SerializeField] private Transform _activeZone;
        [SerializeField] private Transform _benchZone;
        [SerializeField] private Transform _stadiumZone;
        [SerializeField] private Transform _energyZone;

        [Header("配置設定")]
        [SerializeField] private Vector3 _benchSpacing = new Vector3(1.5f, 0, 0);
        [SerializeField] private float _cardScale = 1.0f;
        [SerializeField] private bool _enableZoneAnimations = true;

        [Header("ビジュアル")]
        [SerializeField] private SpriteRenderer _fieldBackground;
        [SerializeField] private List<Transform> _benchSlots = new List<Transform>();
        [SerializeField] private Transform _activeSlot;

        // フィールド状態
        private Card _activePokemon = null;
        private List<Card> _benchPokemon = new List<Card>();
        private Card _stadiumCard = null;
        private List<Card> _energyZoneCards = new List<Card>();
        private Dictionary<Card, List<Card>> _attachedCards = new Dictionary<Card, List<Card>>();

        // イベント
        public event Action<Field, Card, Card> OnActivePokemonChanged;
        public event Action<Field, Card> OnPokemonAddedToBench;
        public event Action<Field, Card> OnPokemonRemovedFromBench;
        public event Action<Field, Card> OnStadiumPlayed;
        public event Action<Field, Card> OnStadiumRemoved;
        public event Action<Field, Card, Card> OnCardAttached;
        public event Action<Field, Card, Card> OnCardDetached;
        public event Action<Field> OnFieldCleared;

        #endregion

        #region Properties

        /// <summary>プレイヤーID</summary>
        public string PlayerId => _playerId;

        /// <summary>フィールド種類</summary>
        public FieldType FieldType => _fieldType;

        /// <summary>最大ベンチサイズ</summary>
        public int MaxBenchSize => _maxBenchSize;

        /// <summary>エネルギーゾーンがあるか</summary>
        public bool HasEnergyZone => _hasEnergyZone;

        /// <summary>バトルポケモン</summary>
        public Card ActivePokemon => _activePokemon;

        /// <summary>ベンチポケモンリスト</summary>
        public List<Card> BenchPokemon => _benchPokemon;

        /// <summary>スタジアムカード</summary>
        public Card StadiumCard => _stadiumCard;

        /// <summary>エネルギーゾーンのカード</summary>
        public List<Card> EnergyZoneCards => _energyZoneCards;

        /// <summary>場にいるポケモンの総数</summary>
        public int PokemonCount => (_activePokemon != null ? 1 : 0) + _benchPokemon.Count;

        /// <summary>ベンチが満杯か</summary>
        public bool IsBenchFull => _benchPokemon.Count >= _maxBenchSize;

        /// <summary>バトルポケモンがいるか</summary>
        public bool HasActivePokemon => _activePokemon != null;

        /// <summary>ベンチポケモンがいるか</summary>
        public bool HasBenchPokemon => _benchPokemon.Count > 0;

        /// <summary>場が空か</summary>
        public bool IsEmpty => _activePokemon == null && _benchPokemon.Count == 0;

        /// <summary>スタジアムが存在するか</summary>
        public bool HasStadium => _stadiumCard != null;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeField();
        }

        private void Start()
        {
            SetupFieldZones();
        }

        private void OnDestroy()
        {
            ClearEventListeners();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// フィールドを初期化
        /// </summary>
        private void InitializeField()
        {
            if (string.IsNullOrEmpty(_playerId))
            {
                _playerId = $"Player_{GetInstanceID()}";
            }

            _benchPokemon = new List<Card>();
            _energyZoneCards = new List<Card>();
            _attachedCards = new Dictionary<Card, List<Card>>();
        }

        /// <summary>
        /// フィールドゾーンを設定
        /// </summary>
        private void SetupFieldZones()
        {
            CreateZoneIfMissing(ref _activeZone, "ActiveZone", Vector3.zero);
            CreateZoneIfMissing(ref _benchZone, "BenchZone", new Vector3(0, -2f, 0));
            CreateZoneIfMissing(ref _stadiumZone, "StadiumZone", new Vector3(0, 3f, 0));
            
            if (_hasEnergyZone)
            {
                CreateZoneIfMissing(ref _energyZone, "EnergyZone", new Vector3(-4f, 0, 0));
            }

            SetupBenchSlots();
        }

        /// <summary>
        /// ゾーンが存在しない場合は作成
        /// </summary>
        private void CreateZoneIfMissing(ref Transform zone, string zoneName, Vector3 position)
        {
            if (zone == null)
            {
                var zoneGO = new GameObject(zoneName);
                zoneGO.transform.SetParent(transform);
                zoneGO.transform.localPosition = position;
                zone = zoneGO.transform;
            }
        }

        /// <summary>
        /// ベンチスロットを設定
        /// </summary>
        private void SetupBenchSlots()
        {
            // 既存スロットをクリア
            foreach (Transform slot in _benchSlots)
            {
                if (slot != null)
                    DestroyImmediate(slot.gameObject);
            }
            _benchSlots.Clear();

            // ベンチスロットを作成
            for (int i = 0; i < _maxBenchSize; i++)
            {
                var slotGO = new GameObject($"BenchSlot_{i}");
                slotGO.transform.SetParent(_benchZone);
                
                float offsetX = (i - (_maxBenchSize - 1) / 2f) * _benchSpacing.x;
                slotGO.transform.localPosition = new Vector3(offsetX, 0, 0);
                
                _benchSlots.Add(slotGO.transform);
            }

            // アクティブスロットを設定
            if (_activeSlot == null)
            {
                var activeSlotGO = new GameObject("ActiveSlot");
                activeSlotGO.transform.SetParent(_activeZone);
                activeSlotGO.transform.localPosition = Vector3.zero;
                _activeSlot = activeSlotGO.transform;
            }
        }

        /// <summary>
        /// プレイヤーIDを設定
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        public void SetPlayerId(string playerId)
        {
            _playerId = playerId;
        }

        /// <summary>
        /// フィールドタイプを設定
        /// </summary>
        /// <param name="fieldType">フィールドタイプ</param>
        public void SetFieldType(FieldType fieldType)
        {
            _fieldType = fieldType;
            
            switch (fieldType)
            {
                case FieldType.Standard:
                    _maxBenchSize = 5;
                    _hasEnergyZone = false;
                    break;
                case FieldType.Pocket:
                    _maxBenchSize = 3;
                    _hasEnergyZone = true;
                    break;
            }
            
            SetupFieldZones();
            Debug.Log($"[Field] Set field type to {fieldType}");
        }

        #endregion

        #region Pokemon Management

        /// <summary>
        /// バトルポケモンを設定
        /// </summary>
        /// <param name="pokemon">ポケモンカード</param>
        /// <returns>設定成功した場合true</returns>
        public bool SetActivePokemon(Card pokemon)
        {
            if (pokemon == null)
                return false;

            if (!pokemon.IsPokemonCard)
            {
                Debug.LogWarning($"[Field] Cannot set non-Pokemon card as active: {pokemon.CardData.CardName}");
                return false;
            }

            var oldActive = _activePokemon;
            _activePokemon = pokemon;

            // カードを配置
            pokemon.SetZone(CardZone.Active);
            pokemon.transform.SetParent(_activeSlot);
            pokemon.transform.localPosition = Vector3.zero;
            pokemon.transform.localScale = Vector3.one * _cardScale;

            // イベント発火
            OnActivePokemonChanged?.Invoke(this, oldActive, pokemon);

            Debug.Log($"[Field] Set active Pokemon: {pokemon.CardData.CardName}");
            return true;
        }

        /// <summary>
        /// ベンチにポケモンを追加
        /// </summary>
        /// <param name="pokemon">ポケモンカード</param>
        /// <returns>追加成功した場合true</returns>
        public bool AddToBench(Card pokemon)
        {
            if (pokemon == null)
                return false;

            if (!pokemon.IsPokemonCard)
            {
                Debug.LogWarning($"[Field] Cannot add non-Pokemon card to bench: {pokemon.CardData.CardName}");
                return false;
            }

            if (IsBenchFull)
            {
                Debug.LogWarning($"[Field] Cannot add to bench: bench is full ({_maxBenchSize})");
                return false;
            }

            if (_benchPokemon.Contains(pokemon))
            {
                Debug.LogWarning($"[Field] Pokemon {pokemon.CardData.CardName} is already on bench");
                return false;
            }

            // ベンチに追加
            _benchPokemon.Add(pokemon);
            var slotIndex = _benchPokemon.Count - 1;

            // カードを配置
            pokemon.SetZone(CardZone.Bench);
            pokemon.transform.SetParent(_benchSlots[slotIndex]);
            pokemon.transform.localPosition = Vector3.zero;
            pokemon.transform.localScale = Vector3.one * _cardScale;

            // イベント発火
            OnPokemonAddedToBench?.Invoke(this, pokemon);

            Debug.Log($"[Field] Added {pokemon.CardData.CardName} to bench (Position: {slotIndex})");
            return true;
        }

        /// <summary>
        /// ベンチからポケモンを削除
        /// </summary>
        /// <param name="pokemon">ポケモンカード</param>
        /// <returns>削除成功した場合true</returns>
        public bool RemoveFromBench(Card pokemon)
        {
            if (pokemon == null || !_benchPokemon.Contains(pokemon))
                return false;

            _benchPokemon.Remove(pokemon);

            // 添付されたカードも削除
            RemoveAllAttachedCards(pokemon);

            // ベンチの配置を更新
            RearrangeBench();

            // イベント発火
            OnPokemonRemovedFromBench?.Invoke(this, pokemon);

            Debug.Log($"[Field] Removed {pokemon.CardData.CardName} from bench");
            return true;
        }

        /// <summary>
        /// バトルポケモンとベンチポケモンを入れ替え
        /// </summary>
        /// <param name="benchPokemon">入れ替えるベンチポケモン</param>
        /// <returns>入れ替え成功した場合true</returns>
        public bool SwitchActivePokemon(Card benchPokemon)
        {
            if (benchPokemon == null || !_benchPokemon.Contains(benchPokemon))
                return false;

            if (_activePokemon == null)
            {
                // バトル場が空の場合は単純にアクティブに設定
                RemoveFromBench(benchPokemon);
                SetActivePokemon(benchPokemon);
                return true;
            }

            var currentActive = _activePokemon;

            // ベンチポケモンをアクティブに
            RemoveFromBench(benchPokemon);
            SetActivePokemon(benchPokemon);

            // 元のアクティブをベンチに
            AddToBench(currentActive);

            Debug.Log($"[Field] Switched active Pokemon: {currentActive.CardData.CardName} <-> {benchPokemon.CardData.CardName}");
            return true;
        }

        /// <summary>
        /// ベンチを再配置
        /// </summary>
        private void RearrangeBench()
        {
            for (int i = 0; i < _benchPokemon.Count; i++)
            {
                var pokemon = _benchPokemon[i];
                pokemon.transform.SetParent(_benchSlots[i]);
                pokemon.transform.localPosition = Vector3.zero;
            }
        }

        #endregion

        #region Card Attachment

        /// <summary>
        /// ポケモンにカードを添付
        /// </summary>
        /// <param name="targetPokemon">対象ポケモン</param>
        /// <param name="attachCard">添付するカード</param>
        /// <returns>添付成功した場合true</returns>
        public bool AttachCard(Card targetPokemon, Card attachCard)
        {
            if (targetPokemon == null || attachCard == null)
                return false;

            if (!IsOnField(targetPokemon))
            {
                Debug.LogWarning($"[Field] Target Pokemon {targetPokemon.CardData.CardName} is not on field");
                return false;
            }

            // 添付可能性チェック
            if (!CanAttachCard(targetPokemon, attachCard))
                return false;

            // 添付リストに追加
            if (!_attachedCards.ContainsKey(targetPokemon))
            {
                _attachedCards[targetPokemon] = new List<Card>();
            }
            _attachedCards[targetPokemon].Add(attachCard);

            // カードを配置
            attachCard.SetZone(CardZone.Attached);
            attachCard.transform.SetParent(targetPokemon.transform);
            
            // エネルギーカードの場合は横に配置
            var attachedCount = _attachedCards[targetPokemon].Count;
            var offset = new Vector3(0.3f * attachedCount, -0.2f, -0.01f * attachedCount);
            attachCard.transform.localPosition = offset;
            attachCard.transform.localScale = Vector3.one * 0.7f;

            // イベント発火
            OnCardAttached?.Invoke(this, targetPokemon, attachCard);

            Debug.Log($"[Field] Attached {attachCard.CardData.CardName} to {targetPokemon.CardData.CardName}");
            return true;
        }

        /// <summary>
        /// 添付されたカードを取り外し
        /// </summary>
        /// <param name="targetPokemon">対象ポケモン</param>
        /// <param name="attachedCard">取り外すカード</param>
        /// <returns>取り外し成功した場合true</returns>
        public bool DetachCard(Card targetPokemon, Card attachedCard)
        {
            if (targetPokemon == null || attachedCard == null)
                return false;

            if (!_attachedCards.ContainsKey(targetPokemon))
                return false;

            var attachedList = _attachedCards[targetPokemon];
            if (!attachedList.Contains(attachedCard))
                return false;

            attachedList.Remove(attachedCard);

            // リストが空になったら削除
            if (attachedList.Count == 0)
            {
                _attachedCards.Remove(targetPokemon);
            }

            // イベント発火
            OnCardDetached?.Invoke(this, targetPokemon, attachedCard);

            Debug.Log($"[Field] Detached {attachedCard.CardData.CardName} from {targetPokemon.CardData.CardName}");
            return true;
        }

        /// <summary>
        /// カードを添付できるかチェック
        /// </summary>
        /// <param name="targetPokemon">対象ポケモン</param>
        /// <param name="attachCard">添付するカード</param>
        /// <returns>添付可能な場合true</returns>
        public bool CanAttachCard(Card targetPokemon, Card attachCard)
        {
            if (targetPokemon == null || attachCard == null)
                return false;

            // エネルギーカードの場合の制限チェック
            if (attachCard.IsEnergyCard)
            {
                var energyData = attachCard.GetEnergyData();
                if (targetPokemon.IsPokemonCard)
                {
                    var pokemonData = targetPokemon.GetPokemonData();
                    return energyData.CanAttachTo(pokemonData);
                }
            }

            // ポケモンのどうぐの場合の制限チェック
            if (attachCard.IsTrainerCard)
            {
                var trainerData = attachCard.GetTrainerData();
                if (trainerData.TrainerType == TrainerType.Tool)
                {
                    // ツールカードは1匹につき1枚まで
                    var attachedCards = GetAttachedCards(targetPokemon);
                    return !attachedCards.Any(card => card.IsTrainerCard && 
                                                     card.GetTrainerData().TrainerType == TrainerType.Tool);
                }
            }

            return true;
        }

        /// <summary>
        /// 添付されたカードをすべて削除
        /// </summary>
        /// <param name="targetPokemon">対象ポケモン</param>
        public void RemoveAllAttachedCards(Card targetPokemon)
        {
            if (targetPokemon == null || !_attachedCards.ContainsKey(targetPokemon))
                return;

            var attachedCards = new List<Card>(_attachedCards[targetPokemon]);
            foreach (var card in attachedCards)
            {
                DetachCard(targetPokemon, card);
            }

            _attachedCards.Remove(targetPokemon);
        }

        /// <summary>
        /// 添付されたカードを取得
        /// </summary>
        /// <param name="targetPokemon">対象ポケモン</param>
        /// <returns>添付されたカードリスト</returns>
        public List<Card> GetAttachedCards(Card targetPokemon)
        {
            if (targetPokemon == null || !_attachedCards.ContainsKey(targetPokemon))
                return new List<Card>();

            return new List<Card>(_attachedCards[targetPokemon]);
        }

        /// <summary>
        /// 添付されたエネルギーカードを取得
        /// </summary>
        /// <param name="targetPokemon">対象ポケモン</param>
        /// <returns>エネルギーカードリスト</returns>
        public List<Card> GetAttachedEnergy(Card targetPokemon)
        {
            return GetAttachedCards(targetPokemon).Where(card => card.IsEnergyCard).ToList();
        }

        #endregion

        #region Stadium Management

        /// <summary>
        /// スタジアムをプレイ
        /// </summary>
        /// <param name="stadium">スタジアムカード</param>
        /// <returns>プレイ成功した場合true</returns>
        public bool PlayStadium(Card stadium)
        {
            if (stadium == null)
                return false;

            if (!stadium.IsTrainerCard || stadium.GetTrainerData().TrainerType != TrainerType.Stadium)
            {
                Debug.LogWarning($"[Field] Cannot play non-Stadium card: {stadium.CardData.CardName}");
                return false;
            }

            // 既存のスタジアムを削除
            if (_stadiumCard != null)
            {
                RemoveStadium();
            }

            _stadiumCard = stadium;

            // カードを配置
            stadium.SetZone(CardZone.Stadium);
            stadium.transform.SetParent(_stadiumZone);
            stadium.transform.localPosition = Vector3.zero;
            stadium.transform.localScale = Vector3.one * _cardScale;

            // イベント発火
            OnStadiumPlayed?.Invoke(this, stadium);

            Debug.Log($"[Field] Played Stadium: {stadium.CardData.CardName}");
            return true;
        }

        /// <summary>
        /// スタジアムを削除
        /// </summary>
        /// <returns>削除成功した場合true</returns>
        public bool RemoveStadium()
        {
            if (_stadiumCard == null)
                return false;

            var removedStadium = _stadiumCard;
            _stadiumCard = null;

            // イベント発火
            OnStadiumRemoved?.Invoke(this, removedStadium);

            Debug.Log($"[Field] Removed Stadium: {removedStadium.CardData.CardName}");
            return true;
        }

        #endregion

        #region Energy Zone Management (Pocket TCG)

        /// <summary>
        /// エネルギーゾーンにエネルギーを追加
        /// </summary>
        /// <param name="energy">エネルギーカード</param>
        /// <returns>追加成功した場合true</returns>
        public bool AddToEnergyZone(Card energy)
        {
            if (!_hasEnergyZone)
            {
                Debug.LogWarning("[Field] Energy zone is not available for this field type");
                return false;
            }

            if (energy == null || !energy.IsEnergyCard)
                return false;

            _energyZoneCards.Add(energy);

            // カードを配置
            energy.SetZone(CardZone.Attached); // エネルギーゾーンの専用ゾーンがない場合
            energy.transform.SetParent(_energyZone);
            
            var position = new Vector3(0, _energyZoneCards.Count * 0.1f, 0);
            energy.transform.localPosition = position;
            energy.transform.localScale = Vector3.one * 0.8f;

            Debug.Log($"[Field] Added {energy.CardData.CardName} to energy zone");
            return true;
        }

        /// <summary>
        /// エネルギーゾーンの供給量を取得
        /// </summary>
        /// <param name="energyType">エネルギータイプ</param>
        /// <returns>供給量</returns>
        public int GetEnergyZoneSupply(PokemonType energyType)
        {
            if (!_hasEnergyZone)
                return 0;

            int supply = 0;
            foreach (var energy in _energyZoneCards)
            {
                var energyData = energy.GetEnergyData();
                if (energyData.ProvidesEnergyType(energyType))
                {
                    supply += energyData.EnergyValue;
                }
            }

            return supply;
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// カードが場にいるかチェック
        /// </summary>
        /// <param name="card">チェックするカード</param>
        /// <returns>場にいる場合true</returns>
        public bool IsOnField(Card card)
        {
            if (card == null)
                return false;

            return card == _activePokemon || _benchPokemon.Contains(card);
        }

        /// <summary>
        /// 場のすべてのポケモンを取得
        /// </summary>
        /// <returns>ポケモンカードリスト</returns>
        public List<Card> GetAllPokemon()
        {
            var allPokemon = new List<Card>();
            
            if (_activePokemon != null)
                allPokemon.Add(_activePokemon);
            
            allPokemon.AddRange(_benchPokemon);
            
            return allPokemon;
        }

        /// <summary>
        /// 指定タイプのポケモンを取得
        /// </summary>
        /// <param name="pokemonType">ポケモンタイプ</param>
        /// <returns>該当するポケモンリスト</returns>
        public List<Card> GetPokemonByType(PokemonType pokemonType)
        {
            return GetAllPokemon().Where(pokemon => 
            {
                var pokemonData = pokemon.GetPokemonData();
                return pokemonData != null && pokemonData.PokemonType == pokemonType;
            }).ToList();
        }

        /// <summary>
        /// ダメージを受けているポケモンを取得
        /// </summary>
        /// <returns>ダメージを受けているポケモンリスト</returns>
        public List<Card> GetDamagedPokemon()
        {
            // TODO: ダメージシステム実装後に修正
            return new List<Card>();
        }

        /// <summary>
        /// 進化可能なポケモンを取得
        /// </summary>
        /// <returns>進化可能なポケモンリスト</returns>
        public List<Card> GetEvolvablePokemon()
        {
            // TODO: 進化システム実装後に修正
            return GetAllPokemon();
        }

        #endregion

        #region Field Operations

        /// <summary>
        /// フィールドをクリア
        /// </summary>
        public void Clear()
        {
            // すべてのポケモンの添付カードを削除
            foreach (var pokemon in GetAllPokemon())
            {
                RemoveAllAttachedCards(pokemon);
            }

            _activePokemon = null;
            _benchPokemon.Clear();
            _stadiumCard = null;
            _energyZoneCards.Clear();
            _attachedCards.Clear();

            OnFieldCleared?.Invoke(this);
            Debug.Log("[Field] Cleared field");
        }

        /// <summary>
        /// イベントリスナーをクリア
        /// </summary>
        private void ClearEventListeners()
        {
            OnActivePokemonChanged = null;
            OnPokemonAddedToBench = null;
            OnPokemonRemovedFromBench = null;
            OnStadiumPlayed = null;
            OnStadiumRemoved = null;
            OnCardAttached = null;
            OnCardDetached = null;
            OnFieldCleared = null;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// デバッグ情報を取得
        /// </summary>
        /// <returns>デバッグ情報文字列</returns>
        public string GetDebugInfo()
        {
            return $"Field - Player: {_playerId}\n" +
                   $"Type: {_fieldType}\n" +
                   $"Active: {(_activePokemon?.CardData.CardName ?? "None")}\n" +
                   $"Bench: {_benchPokemon.Count}/{_maxBenchSize}\n" +
                   $"Stadium: {(_stadiumCard?.CardData.CardName ?? "None")}\n" +
                   $"Energy Zone: {(_hasEnergyZone ? _energyZoneCards.Count.ToString() : "N/A")}";
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// フィールド種類
    /// </summary>
    public enum FieldType
    {
        Standard = 0,   // 従来版（ベンチ5体）
        Pocket = 1,     // ポケット版（ベンチ3体＋エネルギーゾーン）
        Custom = 99     // カスタム
    }

    #endregion
}