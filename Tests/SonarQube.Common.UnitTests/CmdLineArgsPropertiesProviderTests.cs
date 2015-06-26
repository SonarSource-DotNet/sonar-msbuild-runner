﻿//-----------------------------------------------------------------------
// <copyright file="CmdLineArgsPropertiesProviderTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class CmdLineArgsPropertiesProviderTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_InvalidArguments()
        {
            IAnalysisPropertyProvider provider;

            AssertException.Expects<ArgumentNullException>(() => CmdLineArgPropertyProvider.TryCreateProvider(null, new TestLogger(), out provider));
            AssertException.Expects<ArgumentNullException>(() => CmdLineArgPropertyProvider.TryCreateProvider(Enumerable.Empty<ArgumentInstance>(), null, out provider));
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_NoArguments()
        {
            IAnalysisPropertyProvider provider = CheckProcessingSucceeds(Enumerable.Empty<ArgumentInstance>(), new TestLogger());

            Assert.AreEqual(0, provider.GetAllProperties().Count(), "Not expecting any properties");
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_DynamicProperties()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            IList<ArgumentInstance> args = new List<ArgumentInstance>();

            ArgumentDescriptor dummyDescriptor = new ArgumentDescriptor("dummy", new string[] { "dummy prefix" }, false, "dummy desc", true);
            ArgumentDescriptor dummyDescriptor2 = new ArgumentDescriptor("dummy2", new string[] { "dummy prefix 2" }, false, "dummy desc 2", true);

            args.Add(new ArgumentInstance(dummyDescriptor, "should be ignored"));
            args.Add(new ArgumentInstance(dummyDescriptor2, "should be ignored"));

            AddDynamicArguments(args, "key1=value1", "key2=value two with spaces");

            // Act
            IAnalysisPropertyProvider provider = CheckProcessingSucceeds(args, logger);

            // Assert
            AssertPropertyHasValue("key1", "value1", provider);
            AssertPropertyHasValue("key2", "value two with spaces", provider);

            AssertExpectedSettingCount(2, provider);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_DynamicProperties_Invalid()
        {
            // Arrange
            // Act 
            TestLogger logger = CheckProcessingFails(
                    "invalid1 =aaa",
                    "notkeyvalue",
                    " spacebeforekey=bb",
                    "missingvalue=",
                    "validkey=validvalue");

            // Assert
            logger.AssertSingleErrorExists("invalid1 =aaa");
            logger.AssertSingleErrorExists("notkeyvalue");
            logger.AssertSingleErrorExists(" spacebeforekey=bb");
            logger.AssertSingleErrorExists("missingvalue=");

            logger.AssertErrorsLogged(4);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_DynamicProperties_Duplicates()
        {
            // Arrange
            // Act 
            TestLogger logger = CheckProcessingFails(
                    "dup1=value1", "dup1=value2",
                    "dup2=value3", "dup2=value4",
                    "unique=value5");

            // Assert
            logger.AssertSingleErrorExists("dup1=value2", "value1");
            logger.AssertSingleErrorExists("dup2=value4", "value3");
            logger.AssertErrorsLogged(2);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_Disallowed_DynamicProperties()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Named arguments cannot be overridden
            logger = CheckProcessingFails(
                "sonar.projectKey=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectKey, "/k");


            logger = CheckProcessingFails(
                "sonar.projectName=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectName, "/n");


            logger = CheckProcessingFails(
                "sonar.projectVersion=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectVersion, "/v");


            // 2. Other values that can't be set
            logger = CheckProcessingFails(
                "sonar.projectBaseDir=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectBaseDir);


            logger = CheckProcessingFails(
                "sonar.working.directory=value1");
            logger.AssertSingleErrorExists(SonarProperties.WorkingDirectory);

        }

        #endregion

        #region Private methods

        private static void AddDynamicArguments(IList<ArgumentInstance> args, params string[] argValues)
        {
            foreach(string argValue in argValues)
            {
                args.Add(new ArgumentInstance(CmdLineArgPropertyProvider.Descriptor, argValue));
            }
        }

        #endregion

        #region Checks

        private static void AssertPropertyHasValue(string key, string expectedValue, IAnalysisPropertyProvider actualProvider)
        {
            Property actualProperty;
            bool success = actualProvider.TryGetProperty(key, out actualProperty);
            Assert.IsTrue(success, "Failed to retrieve the expected setting. Key: {0}", key);
            Assert.AreEqual(expectedValue, actualProperty.Value, "Setting does not have the expected value. Key: {0}", key);
        }

        private static void AssertExpectedSettingCount(int expected, IAnalysisPropertyProvider actualProvider)
        {
            IEnumerable<Property> properties = actualProvider.GetAllProperties();
            Assert.IsNotNull(properties, "Returned properties should not be null");
            Assert.AreEqual(expected, properties.Count(), "Unexpected number of properties");
        }

        private static TestLogger CheckProcessingFails(params string[] argValues)
        {
            IList<ArgumentInstance> args = new List<ArgumentInstance>();
            AddDynamicArguments(args, argValues);

            return CheckProcessingFails(args);
        }

        private static TestLogger CheckProcessingFails(IEnumerable<ArgumentInstance> args)
        {
            TestLogger logger = new TestLogger();

            IAnalysisPropertyProvider provider;
            bool success = CmdLineArgPropertyProvider.TryCreateProvider(args, logger, out provider);
            Assert.IsFalse(success, "Not expecting the provider to be created");
            Assert.IsNull(provider, "Expecting the provider to be null is processing fails");
            logger.AssertErrorsLogged();

            return logger;
        }

        private static IAnalysisPropertyProvider CheckProcessingSucceeds(IEnumerable<ArgumentInstance> args, TestLogger logger)
        {
            IAnalysisPropertyProvider provider;
            bool success = CmdLineArgPropertyProvider.TryCreateProvider(args, logger, out provider);

            Assert.IsTrue(success, "Expected processing to succeed");
            Assert.IsNotNull(provider, "Not expecting a null provider when processing succeeds");
            logger.AssertErrorsLogged(0);

            return provider;
        }

        #endregion
    }
}
