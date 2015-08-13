﻿//-----------------------------------------------------------------------
// <copyright file="ConsoleLoggerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class ConsoleLoggerTests
    {
        #region Tests
       
        [TestMethod]
        [Description("Regression test: checks the logger does not fail on null message")]
        public void CLogger_NoExceptionOnNullMessage()
        {
            // 1. Logger without timestamps
            ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogInfo(null);
            logger.LogInfo(null, null);
            logger.LogInfo(null, "abc");

            logger.LogWarning(null);
            logger.LogWarning(null, null);
            logger.LogWarning(null, "abc");

            logger.LogError(null);
            logger.LogError(null, null);
            logger.LogError(null, "abc");

            // 2. Logger without timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogInfo(null);
            logger.LogInfo(null, null);
            logger.LogInfo(null, "abc");

            logger.LogWarning(null);
            logger.LogWarning(null, null);
            logger.LogWarning(null, "abc");

            logger.LogError(null);
            logger.LogError(null, null);
            logger.LogError(null, "abc");

        }

        [TestMethod]
        [Description("Regression test: checks the logger does not fail on null arguments")]
        public void CLogger_NoExceptionOnNullArgs()
        {
            // 1. Logger without timestamps
            ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogInfo(null, null);
            logger.LogInfo("123", null);

            logger.LogWarning(null, null);
            logger.LogWarning("123", null);

            logger.LogError(null, null);
            logger.LogError("123", null);

            // 2. Logger without timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogInfo(null, null);
            logger.LogInfo("123", null);

            logger.LogWarning(null, null);
            logger.LogWarning("123", null);

            logger.LogError(null, null);
            logger.LogError("123", null);
        }

        [TestMethod]
        public void CLogger_ExpectedMessages_Message()
        {
            using (OutputCaptureScope output = new OutputCaptureScope())
            {
                // 1. Logger without timestamps
                ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

                logger.LogInfo("message1");
                output.AssertExpectedLastMessage("message1");

                logger.LogInfo("message2", null);
                output.AssertExpectedLastMessage("message2");

                logger.LogInfo("message3 {0}", "xxx");
                output.AssertExpectedLastMessage("message3 xxx");

                // 2. Logger with timestamps
                logger = new ConsoleLogger(includeTimestamp: true);

                logger.LogInfo("message4");
                output.AssertLastMessageEndsWith("message4");

                logger.LogInfo("message5{0}{1}", null, null);
                output.AssertLastMessageEndsWith("message5");

                logger.LogInfo("message6 {0}{1}", "xxx", "yyy", "zzz");
                output.AssertLastMessageEndsWith("message6 xxxyyy");
            }
        }

        [TestMethod]
        public void CLogger_ExpectedMessages_Warning()
        {
            // NOTE: we expect all warnings to be prefixed with a localised
            // "WARNING" prefix, so we're using "AssertLastMessageEndsWith"
            // even for warnings that do not have timestamps.

            using (OutputCaptureScope output = new OutputCaptureScope())
            {
                // 1. Logger without timestamps
                ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

                logger.LogWarning("warn1");
                output.AssertLastMessageEndsWith("warn1");

                logger.LogWarning("warn2", null);
                output.AssertLastMessageEndsWith("warn2");

                logger.LogWarning("warn3 {0}", "xxx");
                output.AssertLastMessageEndsWith("warn3 xxx");

                // 2. Logger with timestamps
                logger = new ConsoleLogger(includeTimestamp: true);

                logger.LogWarning("warn4");
                output.AssertLastMessageEndsWith("warn4");

                logger.LogWarning("warn5{0}{1}", null, null);
                output.AssertLastMessageEndsWith("warn5");

                logger.LogWarning("warn6 {0}{1}", "xxx", "yyy", "zzz");
                output.AssertLastMessageEndsWith("warn6 xxxyyy");
            }
        }

        [TestMethod]
        public void CLogger_ExpectedMessages_Error()
        {
            using (OutputCaptureScope output = new OutputCaptureScope())
            {
                // 1. Logger without timestamps
                ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

                logger.LogError("simple error1");
                output.AssertExpectedLastError("simple error1");

                logger.LogError("simple error2", null);
                output.AssertExpectedLastError("simple error2");

                logger.LogError("simple error3 {0}", "xxx");
                output.AssertExpectedLastError("simple error3 xxx");

                // 2. Logger with timestamps
                logger = new ConsoleLogger(includeTimestamp: true);

                logger.LogError("simple error4");
                output.AssertLastErrorEndsWith("simple error4");

                logger.LogError("simple error5{0}{1}", null, null);
                output.AssertLastErrorEndsWith("simple error5");

                logger.LogError("simple error6 {0}{1}", "xxx", "yyy", "zzz");
                output.AssertLastErrorEndsWith("simple error6 xxxyyy");
            }
        }

        [TestMethod]
        [Description("Checks that formatted strings and special formatting characters are handled correctly")]
        public void CLogger_FormattedStrings()
        {
            using (OutputCaptureScope output = new OutputCaptureScope())
            {

                // 1. Logger without timestamps
                ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

                logger.LogInfo("{ }");
                output.AssertExpectedLastMessage("{ }");

                logger.LogInfo("}{");
                output.AssertExpectedLastMessage("}{");

                logger.LogInfo("a{1}2", null);
                output.AssertExpectedLastMessage("a{1}2");

                logger.LogInfo("{0}", "123");
                output.AssertExpectedLastMessage("123");

                logger.LogInfo("a{0}{{{1}}}", "11", "22");
                output.AssertExpectedLastMessage("a11{22}");

                // 2. Logger with timestamps
                logger = new ConsoleLogger(includeTimestamp: true);

                logger.LogInfo("{ }");
                output.AssertLastMessageEndsWith("{ }");

                logger.LogInfo("}{");
                output.AssertLastMessageEndsWith("}{");

                logger.LogInfo("a{1}2", null);
                output.AssertLastMessageEndsWith("a{1}2");

                logger.LogInfo("{0}", "123");
                output.AssertLastMessageEndsWith("123");

                logger.LogInfo("a{0}{{{1}}}", "11", "22");
                output.AssertLastMessageEndsWith("a11{22}");
            }
        }

        [TestMethod]
        public void CLogger_Verbosity()
        {
            ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);
            Assert.AreEqual(logger.Verbosity, LoggerVerbosity.Debug, "Default verbosity should be Debug");

            using (OutputCaptureScope output = new OutputCaptureScope())
            {
                logger.Verbosity = LoggerVerbosity.Info;
                logger.LogInfo("info1");
                output.AssertExpectedLastMessage("info1");
                logger.LogInfo("info2");
                output.AssertExpectedLastMessage("info2");
                logger.LogDebug("debug1");
                output.AssertExpectedLastMessage("info2"); // the debug message should not have been logged
          
                logger.Verbosity = LoggerVerbosity.Debug;
                logger.LogDebug("debug");
                output.AssertExpectedLastMessage("debug");
                logger.LogInfo("info3");
                output.AssertExpectedLastMessage("info3");

                logger.Verbosity = LoggerVerbosity.Info;
                logger.LogInfo("info4");
                output.AssertExpectedLastMessage("info4");
                logger.LogDebug("debug2");
                output.AssertExpectedLastMessage("info4");
            }
        }


        #endregion
    }
}
