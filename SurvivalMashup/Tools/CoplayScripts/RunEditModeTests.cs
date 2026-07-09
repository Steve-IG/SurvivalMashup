using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Runs the EditMode test suite via the Test Runner API and writes a small JSON summary to
/// Tools/CoplayScripts/test_results.json. Poll that file for completion.
/// </summary>
public static class RunEditModeTests
{
    private const string ResultPath = "Tools/CoplayScripts/test_results.json";
    private static TestRunnerApi _api;

    public static string Execute()
    {
        if (File.Exists(ResultPath))
        {
            File.Delete(ResultPath);
        }

        File.WriteAllText(ResultPath, "{\"status\":\"running\"}");

        _api = ScriptableObject.CreateInstance<TestRunnerApi>();
        _api.RegisterCallbacks(new Callbacks());
        _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        return "EditMode test run started.";
    }

    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

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
            Debug.Log("[RunEditModeTests] " + json);
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.TestStatus == TestStatus.Failed && !result.HasChildren)
            {
                Debug.LogError("[RunEditModeTests] FAILED: " + result.FullName + " -> " + result.Message);
            }
        }
    }
}
