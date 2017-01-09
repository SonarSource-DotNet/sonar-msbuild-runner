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

using SonarQube.Common;
using System;
using System.Diagnostics;
using System.IO;

namespace SonarQube.MSBuild.Tasks
{
    /// <summary>
    /// Helper methods used by multiple tasks
    /// </summary>
    public static class TaskUtilities
    {
        // Workaround for the file locking issue: retry after a short period.
        public const int MaxConfigRetryPeriodInMilliseconds = 2500; // Maximum time to spend trying to access the config file

        public const int DelayBetweenRetriesInMilliseconds = 499; // Period to wait between retries

        #region Public methods

        /// <summary>
        /// Attempts to load the analysis config from the specified directory.
        /// Will retry if the file is locked.
        /// </summary>
        /// <returns>The loaded configuration, or null if the file does not exist or could not be
        /// loaded e.g. due to being locked</returns>
        public static AnalysisConfig TryGetConfig(string configDir, ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            AnalysisConfig config = null;
            if (string.IsNullOrEmpty(configDir)) // not specified
            {
                return null;
            }

            string fullAnalysisPath = Path.Combine(configDir, FileConstants.ConfigFileName);
            logger.LogDebug(Resources.Shared_ReadingConfigFile, fullAnalysisPath);
            if (!File.Exists(fullAnalysisPath))
            {
                logger.LogDebug(Resources.Shared_ConfigFileNotFound);
                return null;
            }

            bool succeeded = Utilities.Retry(MaxConfigRetryPeriodInMilliseconds, DelayBetweenRetriesInMilliseconds, logger,
                () => DoLoadConfig(fullAnalysisPath, logger, out config));
            if (succeeded)
            {
                logger.LogDebug(Resources.Shared_ReadingConfigSucceeded, fullAnalysisPath);
            }
            else
            {
                logger.LogError(Resources.Shared_ReadingConfigFailed, fullAnalysisPath);
            }
            return config;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Attempts to load the config file, suppressing any IO errors that occur.
        /// This method is expected to be called inside a "retry"
        /// </summary>
        private static bool DoLoadConfig(string filePath, ILogger logger, out AnalysisConfig config)
        {
            Debug.Assert(File.Exists(filePath), "Expecting the config file to exist: " + filePath);
            config = null;

            try
            {
                config = AnalysisConfig.Load(filePath);
            }
            catch (IOException e)
            {
                // Log this as a message for info. We'll log an error if all of the re-tries failed
                logger.LogDebug(Resources.Shared_ErrorReadingConfigFile, e.Message);
                return false;
            }
            return true;
        }

        #endregion
    }
}