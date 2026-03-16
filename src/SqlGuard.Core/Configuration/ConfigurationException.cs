using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Configuration
{
    /// <summary>
    /// Thrown when configuration is invalid or cannot be loaded.
    /// </summary>
    public sealed class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception inner) : base(message, inner) { }
    }
}
