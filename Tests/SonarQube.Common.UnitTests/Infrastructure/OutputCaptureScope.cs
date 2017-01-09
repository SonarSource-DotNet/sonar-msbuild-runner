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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace SonarQube.Common.UnitTests
{
    /// <summary>
    /// Utility class to capture the standard console output and error streams.
    /// Disposing the class resets the console to use the standard streams.
    /// </summary>
    public sealed class OutputCaptureScope : IDisposable
    {
        private StringWriter outputWriter;
        private StringWriter errorWriter;

        public OutputCaptureScope()
        {
            this.outputWriter = new StringWriter();
            Console.SetOut(this.outputWriter);

            this.errorWriter = new StringWriter();
            Console.SetError(this.errorWriter);
        }

        public string GetLastErrorMessage()
        {
            return GetLastMessage(this.errorWriter);
        }

        public string GetLastOutputMessage()
        {
            return GetLastMessage(this.outputWriter);
        }

        #region Assertions

        public void AssertExpectedLastMessage(string expected)
        {
            string lastMessage = GetLastMessage(this.outputWriter);
            Assert.AreEqual(expected, lastMessage, "Expected message was not logged");
        }

        public void AssertLastMessageEndsWith(string expected)
        {
            string lastMessage = GetLastMessage(this.outputWriter);

            Assert.IsTrue(lastMessage.EndsWith(expected, StringComparison.CurrentCulture), "Message does not end with the expected string: '{0}'", lastMessage);
            Assert.IsTrue(lastMessage.Length > expected.Length, "Expecting the message to be prefixed with timestamp text");
        }

        public void AssertExpectedLastError(string expected)
        {
            string last = GetLastMessage(this.errorWriter);
            Assert.AreEqual(expected, last, "Expected error was not logged");
        }

        public void AssertLastErrorEndsWith(string expected)
        {
            string last = GetLastMessage(this.errorWriter);

            Assert.IsTrue(last.EndsWith(expected, StringComparison.CurrentCulture), "Error does not end with the expected string: '{0}'", last);
            Assert.IsTrue(last.Length > expected.Length, "Expecting the error to be prefixed with timestamp text");
        }

        #endregion

        #region IDisposable implementation

        private bool disposed;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                this.disposed = true;

                StreamWriter standardError = new StreamWriter(Console.OpenStandardError());
                standardError.AutoFlush = true;
                Console.SetError(standardError);

                StreamWriter standardOut = new StreamWriter(Console.OpenStandardOutput());
                standardOut.AutoFlush = true;
                Console.SetOut(standardOut);

                this.outputWriter.Dispose();
                this.outputWriter = null;

                this.errorWriter.Dispose();
                this.errorWriter = null;
            }
        }

        #endregion

        #region Private methods

        private static string GetLastMessage(StringWriter writer)
        {
            writer.Flush();
            string allText = writer.GetStringBuilder().ToString();
            string[] lines = allText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            Assert.IsTrue(lines.Length > 1, "No output written");

            // There will always be at least one entry in the array, even in an empty string.
            // The last line should be an empty string that follows the final new line character.
            Assert.AreEqual(string.Empty, lines[lines.Length - 1], "Test logic error: expecting the last array entry to be an empty string");

            return lines[lines.Length - 2];
        }

        #endregion

    }
}
