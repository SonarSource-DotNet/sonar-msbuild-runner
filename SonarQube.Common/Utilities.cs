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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace SonarQube.Common
{
    public static class Utilities
    {
        #region Public methods

        /// <summary>
        /// Retries the specified operation until the specified timeout period expires
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="op">The operation to perform. Should return true if the operation succeeded, otherwise false.</param>
        /// <returns>True if the operation succeed, otherwise false</returns>
        public static bool Retry(int timeoutInMilliseconds, int pauseBetweenTriesInMilliseconds, ILogger logger, Func<bool> op)
        {
            if (timeoutInMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException("timeoutInMilliseconds");
            }
            if (pauseBetweenTriesInMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException("pauseBetweenTriesInMilliseconds");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (op == null)
            {
                throw new ArgumentNullException("op");
            }

            logger.LogDebug(Resources.MSG_BeginningRetry, timeoutInMilliseconds, pauseBetweenTriesInMilliseconds);

            Stopwatch timer = Stopwatch.StartNew();
            bool succeeded = op();

            while (!succeeded && timer.ElapsedMilliseconds < timeoutInMilliseconds)
            {
                logger.LogDebug(Resources.MSG_RetryingOperation);
                System.Threading.Thread.Sleep(pauseBetweenTriesInMilliseconds);
                succeeded = op();
            }

            timer.Stop();

            if (succeeded)
            {
                logger.LogDebug(Resources.MSG_RetryOperationSucceeded, timer.ElapsedMilliseconds);
            }
            else
            {
                logger.LogDebug(Resources.MSG_RetryOperationFailed, timer.ElapsedMilliseconds);
            }
            return succeeded;
        }

        /// <summary>
        /// Ensures that the specified directory exists
        /// </summary>
        public static void EnsureDirectoryExists(string directory, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentNullException("directory");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            if (Directory.Exists(directory))
            {
                logger.LogDebug(Resources.MSG_DirectoryAlreadyExists, directory);
            }
            else
            {
                logger.LogDebug(Resources.MSG_CreatingDirectory, directory);
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Ensures that the specified directory exists and is empty
        /// </summary>
        public static void EnsureEmptyDirectory(string directory, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentNullException("directory");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            if (Directory.Exists(directory))
            {
                logger.LogDebug(Resources.MSG_DeletingDirectory, directory);
                Directory.Delete(directory, true);
            }
            logger.LogDebug(Resources.MSG_CreatingDirectory, directory);
            Directory.CreateDirectory(directory);
        }

        /// <summary>
        /// Attempts to ensure the specified empty directories exist.
        /// Handles the common types of failure and logs a more helpful error message.
        /// </summary>
        public static bool TryEnsureEmptyDirectories(ILogger logger, params string[] directories)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            Debug.Assert(directories.Length > 0);

            foreach (string directory in directories)
            {
                try
                {
                    EnsureEmptyDirectory(directory, logger);
                }
                catch (Exception ex)
                {
                    if (ex is UnauthorizedAccessException || ex is IOException)
                    {
                        logger.LogError(Resources.ERROR_CannotCreateEmptyDirectory, directory, ex.Message);
                        return false;
                    }
                    throw;
                }
            }
            return true;
        }

        public static bool IsSecuredServerProperty(string s)
        {
            return s.EndsWith(".secured", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Common logic for handling web exceptions when connecting to the SonarQube server. Common exceptions
        /// are handled by logging user friendly errors.
        /// </summary>
        /// <returns>True if the exception was handled</returns>
        public static bool HandleHostUrlWebException(WebException ex, string hostUrl, ILogger logger)
        {
            var response = ex.Response as HttpWebResponse;
            if (response != null && response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogError(Resources.ERROR_FileNotFound, response.ResponseUri);
                return true;
            }

            if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
            {
                logger.LogError(Resources.ERROR_UnauthorizedConnection, response.ResponseUri);
                return true;
            }

            if (ex.Status == WebExceptionStatus.NameResolutionFailure)
            {
                logger.LogError(Resources.ERROR_UrlNameResolutionFailed, hostUrl);
                return true;
            }

            if (ex.Status == WebExceptionStatus.ConnectFailure)
            {
                logger.LogError(Resources.ERROR_ConnectionFailed, hostUrl);
                return true;
            }

            if (ex.Status == WebExceptionStatus.TrustFailure)
            {
                logger.LogError(Resources.ERROR_TrustFailure, hostUrl);
                return true;
            }

            return false;
        }

        public static void LogAssemblyVersion(ILogger logger, System.Reflection.Assembly assembly, string description)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (assembly == null)
            {
                throw new ArgumentNullException("assembly");
            }
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentNullException("description");
            }

            logger.LogInfo("{0} {1}", description, assembly.GetName().Version.ToDisplayString());
        }

        /// <summary>
        /// Disposes the supplied object if it can be disposed. Null objects are ignored.
        /// </summary>
        public static void SafeDispose(object instance)
        {
            if (instance != null)
            {
                IDisposable disposable = instance as IDisposable;
                disposable?.Dispose();
            }
        }

        public static string ToDisplayString(this Version version)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(version.Major);
            sb.Append(".");
            sb.Append(version.Minor);

            if (version.Build != 0 || version.Revision != 0)
            {
                sb.Append(".");
                sb.Append(version.Build);
            }

            if (version.Revision != 0)
            {
                sb.Append(".");
                sb.Append(version.Revision);
            }

            return sb.ToString();
        }

        #endregion

    }
}

