namespace PokemonTCG.Game.Rules
{
    /// <summary>
    /// ゲームアクションの種類
    /// ポケモンTCGで実行可能な全アクションを定義
    /// </summary>
    public enum GameActionType
    {
        // === 基本アクション ===
        /// <summary>カードをプレイ</summary>
        PlayCard = 1,
        
        /// <summary>エネルギーを添付</summary>
        AttachEnergy = 2,
        
        /// <summary>ワザを使う</summary>
        UseAttack = 3,
        
        /// <summary>にげる</summary>
        Retreat = 4,
        
        /// <summary>進化</summary>
        Evolution = 5,
        
        /// <summary>特性を使う</summary>
        UseAbility = 6,
        
        /// <summary>ターン終了</summary>
        EndTurn = 7,

        // === カード関連アクション ===
        /// <summary>カードを引く</summary>
        DrawCard = 10,
        
        /// <summary>カードを捨てる</summary>
        DiscardCard = 11,
        
        /// <summary>カードをサーチ</summary>
        SearchCard = 12,
        
        /// <summary>カードをシャッフル</summary>
        ShuffleDeck = 13,
        
        /// <summary>手札を見せる</summary>
        RevealHand = 14,

        // === ポケモン関連アクション ===
        /// <summary>ポケモンを場に出す</summary>
        PlayPokemon = 20,
        
        /// <summary>ポケモンを入れ替える</summary>
        SwitchPokemon = 21,
        
        /// <summary>ダメージを与える</summary>
        DealDamage = 22,
        
        /// <summary>ダメージを回復</summary>
        HealDamage = 23,
        
        /// <summary>状態異常を付与</summary>
        ApplyStatusCondition = 24,
        
        /// <summary>状態異常を回復</summary>
        RemoveStatusCondition = 25,

        // === エネルギー関連アクション ===
        /// <summary>エネルギーを取り除く</summary>
        RemoveEnergy = 30,
        
        /// <summary>エネルギーを移動</summary>
        MoveEnergy = 31,
        
        /// <summary>特殊エネルギー効果発動</summary>
        ActivateSpecialEnergy = 32,

        // === トレーナー関連アクション ===
        /// <summary>サポートを使う</summary>
        UseSupporter = 40,
        
        /// <summary>グッズを使う</summary>
        UseItem = 41,
        
        /// <summary>スタジアムを使う</summary>
        UseStadium = 42,

        // === 勝敗関連アクション ===
        /// <summary>サイドカードを取る</summary>
        TakePrizeCard = 50,
        
        /// <summary>きぜつ処理</summary>
        KnockOut = 51,
        
        /// <summary>降参</summary>
        Surrender = 52,

        // === ゲーム管理アクション ===
        /// <summary>ゲーム開始</summary>
        StartGame = 60,
        
        /// <summary>セットアップ</summary>
        Setup = 61,
        
        /// <summary>マリガン</summary>
        Mulligan = 62,
        
        /// <summary>コインフリップ</summary>
        CoinFlip = 63,
        
        /// <summary>ランダム選択</summary>
        RandomChoice = 64,

        // === 特殊アクション ===
        /// <summary>カスタムアクション（拡張用）</summary>
        Custom = 100,
        
        /// <summary>複合アクション</summary>
        Composite = 101,
        
        /// <summary>条件付きアクション</summary>
        Conditional = 102,

        // === エラー・無効 ===
        /// <summary>無効なアクション</summary>
        Invalid = -1,
        
        /// <summary>アクション種類不明</summary>
        Unknown = 0
    }

    /// <summary>
    /// アクションの実行タイミング
    /// </summary>
    public enum ActionTiming
    {
        /// <summary>即座に実行</summary>
        Immediate = 0,
        
        /// <summary>ターン開始時</summary>
        TurnStart = 1,
        
        /// <summary>ドローフェーズ</summary>
        DrawPhase = 2,
        
        /// <summary>メインフェーズ</summary>
        MainPhase = 3,
        
        /// <summary>アタックフェーズ</summary>
        AttackPhase = 4,
        
        /// <summary>ターン終了時</summary>
        TurnEnd = 5,
        
        /// <summary>ポケモンチェック時</summary>
        PokemonCheck = 6,
        
        /// <summary>ダメージ計算時</summary>
        DamageCalculation = 7,
        
        /// <summary>効果解決時</summary>
        EffectResolution = 8
    }

    /// <summary>
    /// アクションの優先度
    /// </summary>
    public enum ActionPriority
    {
        /// <summary>最高優先度</summary>
        Highest = 0,
        
        /// <summary>高優先度</summary>
        High = 1,
        
        /// <summary>通常優先度</summary>
        Normal = 2,
        
        /// <summary>低優先度</summary>
        Low = 3,
        
        /// <summary>最低優先度</summary>
        Lowest = 4
    }

    /// <summary>
    /// アクションの実行状態
    /// </summary>
    public enum ActionExecutionState
    {
        /// <summary>待機中</summary>
        Pending = 0,
        
        /// <summary>実行中</summary>
        Executing = 1,
        
        /// <summary>完了</summary>
        Completed = 2,
        
        /// <summary>失敗</summary>
        Failed = 3,
        
        /// <summary>キャンセル</summary>
        Cancelled = 4,
        
        /// <summary>ブロック</summary>
        Blocked = 5
    }
}