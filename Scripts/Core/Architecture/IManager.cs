using System;

namespace PokemonTCG.Core.Architecture
{
    /// <summary>
    /// マネージャーの基底インターフェース
    /// 全てのマネージャークラスで共通の機能を定義
    /// Claudeによる拡張時の統一API提供
    /// </summary>
    public interface IManager
    {
        /// <summary>マネージャーが初期化済みかどうか</summary>
        bool IsInitialized { get; }

        /// <summary>マネージャーの実行優先度（低い値ほど早く初期化）</summary>
        int InitializationOrder { get; }

        /// <summary>
        /// マネージャーの初期化処理
        /// ServiceLocatorに登録される前に呼び出される
        /// </summary>
        void Initialize();

        /// <summary>
        /// マネージャーの終了処理
        /// アプリケーション終了時やシーン切り替え時に呼び出される
        /// </summary>
        void Dispose();

        /// <summary>
        /// フレーム更新処理（必要な場合のみ実装）
        /// </summary>
        void Update();

        /// <summary>
        /// 固定時間更新処理（必要な場合のみ実装）
        /// </summary>
        void FixedUpdate();
    }

    /// <summary>
    /// 初期化可能なオブジェクトのインターフェース
    /// Claude生成クラスの統一初期化API
    /// </summary>
    public interface IInitializable
    {
        /// <summary>初期化処理</summary>
        void Initialize();
    }

    /// <summary>
    /// 破棄可能なオブジェクトのインターフェース
    /// リソース管理の統一API
    /// </summary>
    public interface IDisposable
    {
        /// <summary>破棄処理</summary>
        void Dispose();
    }

    /// <summary>
    /// 更新可能なオブジェクトのインターフェース
    /// フレーム更新の統一API
    /// </summary>
    public interface IUpdatable
    {
        /// <summary>フレーム更新</summary>
        void Update();
    }

    /// <summary>
    /// 固定時間更新可能なオブジェクトのインターフェース
    /// 物理更新の統一API
    /// </summary>
    public interface IFixedUpdatable
    {
        /// <summary>固定時間更新</summary>
        void FixedUpdate();
    }
}