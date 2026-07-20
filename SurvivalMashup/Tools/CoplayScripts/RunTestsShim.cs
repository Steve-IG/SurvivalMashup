using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Reflection shim: invokes the real EditMode runner (ToyChest.Tests.CoplayTestRunner) which lives in
/// the test assembly with the Test Runner API refs. execute_script compiles this file standalone, so it
/// must not reference the test framework directly — hence the reflection call.
/// </summary>
public static class RunTestsShim
{
    public static string Execute()
    {
        Type t = Type.GetType("ToyChest.Tests.CoplayTestRunner, ToyChest.Tests");
        if (t == null)
        {
            Debug.LogError("[RunTestsShim] CoplayTestRunner type not found.");
            return "type not found";
        }

        MethodInfo m = t.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
        object r = m.Invoke(null, null);
        Debug.Log("[RunTestsShim] " + r);
        return r as string;
    }
}
