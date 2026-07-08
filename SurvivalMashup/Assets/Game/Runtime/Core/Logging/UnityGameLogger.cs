using System;
using UnityEngine;

namespace ToyChest.Core.Logging
{
    /// <summary>
    /// Routes <see cref="IGameLogger"/> output to the Unity console.
    /// This is the production logger; tests substitute their own recording implementation.
    /// </summary>
    public sealed class UnityGameLogger : IGameLogger
    {
        /// <inheritdoc />
        public void Info(string message)
        {
            Debug.Log(message);
        }

        /// <inheritdoc />
        public void Warning(string message)
        {
            Debug.LogWarning(message);
        }

        /// <inheritdoc />
        public void Error(string message)
        {
            Debug.LogError(message);
        }

        /// <inheritdoc />
        public void Error(string message, Exception exception)
        {
            Debug.LogError(message);
            Debug.LogException(exception);
        }
    }
}
