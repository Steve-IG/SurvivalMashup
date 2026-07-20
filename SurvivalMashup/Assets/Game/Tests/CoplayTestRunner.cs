#if UNITY_INCLUDE_TESTS
using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ToyChest.Tests
{
    /// <summary>
    /// Editor-only helper to run the EditMode suite headlessly and write a JSON summary that can be
    /// polled from outside the Editor. Lives in the test assembly so it has the Test Runner API refs.
    /// </summary>
    public static class CoplayTestRunner
    {
        private const string ResultPath = "Tools/CoplayScripts/test_results.json";
        private static TestRunnerApi _api;

        public static string Execute()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "{\"status\":\"running\"}");

            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.RegisterCallbacks(new Callbacks());
            _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
            return "EditMode test run started.";
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                string json =
                    "{\"status\":\"finished\"," +
                    "\"passed\":" + result.PassCount + "," +
                    "\"failed\":" + result.FailCount + "," +
                    "\"skipped\":" + result.SkipCount + "," +
                    "\"inconclusive\":" + result.InconclusiveCount + "," +
                    "\"result\":\"" + result.TestStatus + "\"}";
                File.WriteAllText(ResultPath, json);
                Debug.Log("[CoplayTestRunner] " + json);
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed && !result.HasChildren)
                {
                    Debug.LogError("[CoplayTestRunner] FAILED: " + result.FullName + " -> " + result.Message);
                }
            }
        }
    }
}
#endif
