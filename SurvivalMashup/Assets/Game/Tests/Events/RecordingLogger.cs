using System;
using System.Collections.Generic;
using ToyChest.Core.Logging;

namespace ToyChest.Tests.Events
{
    /// <summary>
    /// Test double that records log output so tests can assert on failure reporting
    /// without touching the Unity console.
    /// </summary>
    internal sealed class RecordingLogger : IGameLogger
    {
        public readonly List<string> Infos = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<Exception> Exceptions = new List<Exception>();

        public void Info(string message)
        {
            Infos.Add(message);
        }

        public void Warning(string message)
        {
            Warnings.Add(message);
        }

        public void Error(string message)
        {
            Errors.Add(message);
        }

        public void Error(string message, Exception exception)
        {
            Errors.Add(message);
            Exceptions.Add(exception);
        }
    }
}
