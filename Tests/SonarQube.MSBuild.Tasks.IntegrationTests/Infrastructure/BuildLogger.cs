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

using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Simple implementation of <see cref="ILogger"/> that writes all
    /// event information to the console
    /// </summary>
    internal class BuildLogger : ILogger
    {
        private IEventSource eventSource;

        private List<TargetStartedEventArgs> executedTargets;
        private List<TaskStartedEventArgs> executedTasks;
        private List<BuildErrorEventArgs> errors;
        private List<BuildWarningEventArgs> warnings;


        #region Public properties

        public IReadOnlyList<BuildWarningEventArgs> Warnings { get { return this.warnings.AsReadOnly(); } }

        public IReadOnlyList<BuildErrorEventArgs> Errors { get { return this.errors.AsReadOnly(); } }

        #endregion

        #region ILogger interface

        string ILogger.Parameters
        {
            get; set;
        }

        LoggerVerbosity ILogger.Verbosity
        {
            get; set;
        }

        void ILogger.Initialize(IEventSource eventSource)
        {
            this.eventSource = eventSource;
            this.executedTargets = new List<TargetStartedEventArgs>();
            this.executedTasks = new List<TaskStartedEventArgs>();

            this.warnings = new List<BuildWarningEventArgs>();
            this.errors = new List<BuildErrorEventArgs>();
            
            this.RegisterEvents(this.eventSource);
        }


        void ILogger.Shutdown()
        {
            this.UnregisterEvents(this.eventSource);
        }

        #endregion

        #region Private methods

        private void RegisterEvents(IEventSource source)
        {
            source.AnyEventRaised += source_AnyEventRaised;
            source.TargetStarted += source_TargetStarted;
            source.TaskStarted += source_TaskStarted;
            source.ErrorRaised += source_ErrorRaised;
            source.WarningRaised += source_WarningRaised;
        }

        private void UnregisterEvents(IEventSource source)
        {
            source.AnyEventRaised -= source_AnyEventRaised;
            source.TargetStarted -= source_TargetStarted;
            source.TaskStarted -= source_TaskStarted;
            source.ErrorRaised -= source_ErrorRaised;
            source.WarningRaised -= source_WarningRaised;
        }

        void source_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            this.executedTargets.Add(e);
        }

        void source_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            this.executedTasks.Add(e);
        }

        void source_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            this.errors.Add(e);
        }

        void source_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            this.warnings.Add(e);
        }

        private static void source_AnyEventRaised(object sender, BuildEventArgs e)
        {
            Log("{0}: {1}: {2}", e.Timestamp, e.SenderName, e.Message);
        }

        private static void Log(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        #endregion

        #region Assertions

        public TargetStartedEventArgs AssertTargetExecuted(string targetName)
        {
            TargetStartedEventArgs found = this.executedTargets.FirstOrDefault(t => t.TargetName.Equals(targetName, StringComparison.InvariantCulture));
            Assert.IsNotNull(found, "Specified target was not executed: {0}", targetName);
            return found;
        }

        public void AssertTargetNotExecuted(string targetName)
        {
            TargetStartedEventArgs found = this.executedTargets.FirstOrDefault(t => t.TargetName.Equals(targetName, StringComparison.InvariantCulture));
            Assert.IsNull(found, "Not expecting the target to have been executed: {0}", targetName);
        }

        public TaskStartedEventArgs AssertTaskExecuted(string taskName)
        {
            TaskStartedEventArgs found = this.executedTasks.FirstOrDefault(t => t.TaskName.Equals(taskName, StringComparison.InvariantCulture));
            Assert.IsNotNull(found, "Specified task was not executed: {0}", taskName);
            return found;
        }

        public void AssertTaskNotExecuted(string taskName)
        {
            TaskStartedEventArgs found = this.executedTasks.FirstOrDefault(t => t.TaskName.Equals(taskName, StringComparison.InvariantCulture));
            Assert.IsNull(found, "Not expecting the task to have been executed: {0}", taskName);
        }

        /// <summary>
        /// Checks that the expected tasks were executed in the specified order
        /// </summary>
        public void AssertExpectedTargetOrdering(params string[] expected)
        {
            foreach (string target in expected)
            {
                this.AssertTargetExecuted(target);
            }

            string[] actual = this.executedTargets.Select(t => t.TargetName).Where(t => expected.Contains(t, StringComparer.Ordinal)).ToArray();

            Console.WriteLine("Expected target order: {0}", string.Join(", ", expected));
            Console.WriteLine("Actual target order: {0}", string.Join(", ", actual));

            CollectionAssert.AreEqual(expected, actual, "Targets were not executed in the expected order");
        }

        public void AssertNoWarningsOrErrors()
        {
            AssertExpectedErrorCount(0);
            AssertExpectedWarningCount(0);
        }

        public void AssertExpectedErrorCount(int expected)
        {
            Assert.AreEqual(expected, this.errors.Count, "Unexpected number of errors raised");
        }

        public void AssertExpectedWarningCount(int expected)
        {
            Assert.AreEqual(expected, this.warnings.Count, "Unexpected number of warnings raised");
        }

        #endregion
    }
}
