using System;

namespace ToyChest.Core.Logging
{
    /// <summary>
    /// Central logging capability for all ToyChest code.
    /// Core exposes this capability; it owns no gameplay meaning.
    /// Error messages should explain both what happened and what to check.
    /// </summary>
    public interface IGameLogger
    {
        /// <summary>Records informational output useful during development.</summary>
        void Info(string message);

        /// <summary>Records a recoverable problem that deserves attention.</summary>
        void Warning(string message);

        /// <summary>Records a failure. The message must state what happened and what to check.</summary>
        void Error(string message);

        /// <summary>Records a failure caused by an exception, preserving the stack trace.</summary>
        void Error(string message, Exception exception);
    }
}
