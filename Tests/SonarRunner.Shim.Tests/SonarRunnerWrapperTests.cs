﻿//-----------------------------------------------------------------------
// <copyright file="SonarRunnerWrapperTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using TestUtilities;

namespace SonarRunner.Shim.Tests
{
    [TestClass]
    public class SonarRunnerWrapperTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void SonarRunnerHome_NoWarning()
        {
            // Arrange
            TestLogger testLogger = new TestLogger();
            string exePath = TestUtils.WriteBatchFileForTest(TestContext, "exit 0");
            string path = Path.Combine(TestUtils.CreateTestSpecificFolder(TestContext), "analysis.properties");
            File.CreateText(path);

            // Act
            SonarRunnerWrapper.ExecuteJavaRunner(testLogger, exePath, exePath);

            // Assert
            testLogger.AssertWarningNotLogged(SonarRunner.Shim.Resources.WARN_SonarRunnerHomeIsSet);
        }

        [TestMethod]
        public void SonarRunnerHome_Warning()
        {
            try
            {
                // Arrange
                TestLogger testLogger = new TestLogger();
                string exePath = TestUtils.WriteBatchFileForTest(TestContext, "exit 0");
                string path = Path.Combine(TestUtils.CreateTestSpecificFolder(TestContext), "analysis.properties");
                File.CreateText(path);
                Environment.SetEnvironmentVariable(SonarRunnerWrapper.SonarRunnerHomeVariableName, "some_path");

                // Act
                SonarRunnerWrapper.ExecuteJavaRunner(testLogger, exePath, exePath);

                // Assert
                testLogger.AssertSingleWarningExists(SonarRunner.Shim.Resources.WARN_SonarRunnerHomeIsSet);
            }
            finally
            {
                Environment.SetEnvironmentVariable(SonarRunnerWrapper.SonarRunnerHomeVariableName, String.Empty);
            }
        }

        #endregion Tests
    }
}