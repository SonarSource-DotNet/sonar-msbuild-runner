﻿/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Globalization;

namespace SonarQube.Common
{
    /// <summary>
    /// Simple logger implementation that logs output to the console
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private const LoggerVerbosity DefaultVerbosity = VerbosityCalculator.InitialLoggingVerbosity;

        #region Public methods

        public ConsoleLogger() : this(includeTimestamp: false)
        {
        }

        public ConsoleLogger(bool includeTimestamp)
        {
            this.IncludeTimestamp = includeTimestamp;
            this.Verbosity = DefaultVerbosity;
        }

        /// <summary>
        /// Indicates whether logged messages should be prefixed with timestamps or not
        /// </summary>
        public bool IncludeTimestamp { get; set; }

        #endregion Public methods

        #region ILogger interface

        public void LogWarning(string message, params object[] args)
        {
            string finalMessage = this.GetFormattedMessage(Resources.Logger_WarningPrefix + message, args);
            Console.WriteLine(finalMessage);
        }

        public void LogError(string message, params object[] args)
        {
            string finalMessage = this.GetFormattedMessage(message, args);
            Console.Error.WriteLine(finalMessage);
        }

        public void LogDebug(string message, params object[] args)
        {
            LogMessage(LoggerVerbosity.Debug, message, args);
        }

        public void LogInfo(string message, params object[] args)
        {
            LogMessage(LoggerVerbosity.Info, message, args);
        }

        public LoggerVerbosity Verbosity
        {
            get; set;
        }

        #endregion ILogger interface

        #region Private methods

        private string GetFormattedMessage(string message, params object[] args)
        {
            string finalMessage = message;
            if (args != null && args.Length > 0)
            {
                finalMessage = string.Format(CultureInfo.CurrentCulture, finalMessage ?? string.Empty, args);
            }

            if (this.IncludeTimestamp)
            {
                finalMessage = string.Format(CultureInfo.CurrentCulture, "{0}  {1}", System.DateTime.Now.ToString("HH:mm:ss.FFF", CultureInfo.InvariantCulture), finalMessage);
            }
            return finalMessage;
        }

        private void LogMessage(LoggerVerbosity messageVerbosity, string message, params object[] args)
        {
            if (messageVerbosity == LoggerVerbosity.Info || 
                (messageVerbosity == LoggerVerbosity.Debug && this.Verbosity == LoggerVerbosity.Debug))
            {
                string finalMessage = this.GetFormattedMessage(message, args);
                Console.WriteLine(finalMessage);
            }
        }

        #endregion Private methods
    }
}