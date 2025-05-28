using UnityEngine;
using PokemonTCG.Core.Architecture;

namespace PokemonTCG.Test
{
    /// <summary>
    /// Core Architecture基盤の動作確認テスト
    /// ServiceLocator, EventBus, Singletonの統合テスト
    /// </summary>
    public class ArchitectureTest : MonoBehaviourSingleton<ArchitectureTest>
    {
        #region Test Events

        /// <summary>テスト用イベントクラス</summary>
        public class TestEvent
        {
            public string Message { get; set; }
            public int Value { get; set; }
        }

        /// <summary>テスト用サービスインターフェース</summary>
        public interface ITestService
        {
            void DoSomething();
            string GetStatus();
        }

        /// <summary>テスト用サービス実装</summary>
        public class TestService : ITestService
        {
            public void DoSomething()
            {
                Debug.Log("[TestService] DoSomething called!");
            }

            public string GetStatus()
            {
                return "Test Service is running";
            }
        }

        #endregion

        #region Fields

        /// <summary>初期化順序</summary>
        public override int InitializationOrder => 100; // 他のシステム後に初期化

        /// <summary>テスト結果</summary>
        private bool _serviceLocatorTest = false;
        private bool _eventBusTest = false;
        private bool _singletonTest = false;

        #endregion

        #region Unity Lifecycle

        protected override void OnInitialize()
        {
            Debug.Log("=== Pokemon TCG Core Architecture Test Started ===");
            
            // テスト実行
            TestSingleton();
            TestServiceLocator();
            TestEventBus();
            
            // 結果表示
            ShowTestResults();
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// Singletonパターンのテスト
        /// </summary>
        private void TestSingleton()
        {
            Debug.Log("[Test] Testing Singleton Pattern...");
            
            try
            {
                // Singletonインスタンステスト
                var instance1 = ArchitectureTest.Instance;
                var instance2 = ArchitectureTest.Instance;
                
                if (instance1 == instance2 && instance1 != null)
                {
                    _singletonTest = true;
                    Debug.Log("[Test] ✅ Singleton test passed");
                }
                else
                {
                    Debug.LogError("[Test] ❌ Singleton test failed");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test] ❌ Singleton test exception: {ex.Message}");
            }
        }

        /// <summary>
        /// ServiceLocatorのテスト
        /// </summary>
        private void TestServiceLocator()
        {
            Debug.Log("[Test] Testing ServiceLocator...");
            
            try
            {
                // サービス登録
                var testService = new TestService();
                ServiceLocator.Register<ITestService>(testService);
                
                // サービス取得
                var retrievedService = ServiceLocator.Get<ITestService>();
                
                if (retrievedService != null && retrievedService == testService)
                {
                    retrievedService.DoSomething();
                    string status = retrievedService.GetStatus();
                    
                    if (!string.IsNullOrEmpty(status))
                    {
                        _serviceLocatorTest = true;
                        Debug.Log("[Test] ✅ ServiceLocator test passed");
                        Debug.Log($"[Test] Service status: {status}");
                    }
                }
                else
                {
                    Debug.LogError("[Test] ❌ ServiceLocator test failed - service retrieval");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test] ❌ ServiceLocator test exception: {ex.Message}");
            }
        }

        /// <summary>
        /// EventBusのテスト
        /// </summary>
        private void TestEventBus()
        {
            Debug.Log("[Test] Testing EventBus...");
            
            try
            {
                bool eventReceived = false;
                string receivedMessage = "";
                
                // イベント購読
                EventBus.On<TestEvent>(evt =>
                {
                    eventReceived = true;
                    receivedMessage = evt.Message;
                    Debug.Log($"[Test] Received event: {evt.Message}, Value: {evt.Value}");
                });
                
                // イベント発行
                EventBus.Emit(new TestEvent 
                { 
                    Message = "Hello from EventBus!", 
                    Value = 42 
                });
                
                // 結果確認（次フレームで確認するため少し待つ）
                if (eventReceived && receivedMessage == "Hello from EventBus!")
                {
                    _eventBusTest = true;
                    Debug.Log("[Test] ✅ EventBus test passed");
                }
                else
                {
                    Debug.LogError("[Test] ❌ EventBus test failed - event not received properly");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test] ❌ EventBus test exception: {ex.Message}");
            }
        }

        /// <summary>
        /// テスト結果表示
        /// </summary>
        private void ShowTestResults()
        {
            Debug.Log("=== Test Results ===");
            Debug.Log($"Singleton Pattern: {(_singletonTest ? "✅ PASS" : "❌ FAIL")}");
            Debug.Log($"ServiceLocator: {(_serviceLocatorTest ? "✅ PASS" : "❌ FAIL")}");
            Debug.Log($"EventBus: {(_eventBusTest ? "✅ PASS" : "❌ FAIL")}");
            
            bool allTestsPassed = _singletonTest && _serviceLocatorTest && _eventBusTest;
            
            if (allTestsPassed)
            {
                Debug.Log("🎉 All Core Architecture tests PASSED! Ready for Phase 1 Day 2.");
            }
            else
            {
                Debug.LogWarning("⚠️ Some tests FAILED. Please check the implementation.");
            }
            
            Debug.Log("=== Pokemon TCG Core Architecture Test Completed ===");
        }

        #endregion

        #region Context Menu Methods

        /// <summary>
        /// テストを手動実行
        /// </summary>
        [ContextMenu("Run Architecture Tests")]
        public void RunTests()
        {
            TestSingleton();
            TestServiceLocator();
            TestEventBus();
            ShowTestResults();
        }

        /// <summary>
        /// 現在のシステム状態を表示
        /// </summary>
        [ContextMenu("Show System Status")]
        public void ShowSystemStatus()
        {
            Debug.Log("=== System Status ===");
            Debug.Log($"ArchitectureTest Instance: {(HasInstance ? "✅ Active" : "❌ Not Active")}");
            Debug.Log($"ServiceLocator Instance: {(ServiceLocator.HasInstance ? "✅ Active" : "❌ Not Active")}");
            Debug.Log($"EventBus Instance: {(EventBus.HasInstance ? "✅ Active" : "❌ Not Active")}");
            
            // 登録サービス情報表示
            if (ServiceLocator.HasInstance)
            {
                ServiceLocator.Instance.LogRegisteredServices();
            }
            
            // イベントリスナー情報表示
            if (EventBus.HasInstance)
            {
                EventBus.Instance.LogEventListeners();
            }
        }

        #endregion
    }
}