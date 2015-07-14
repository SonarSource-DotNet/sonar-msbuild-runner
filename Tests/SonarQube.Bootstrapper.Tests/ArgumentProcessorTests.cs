﻿//-----------------------------------------------------------------------
// <copyright file="ArgumentProcessorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.IO;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class ArgumentProcessorTests
    {
        [TestInitialize]
        public void Initialize()
        {
            // The project setup means the default properties file will automatically
            // be copied alongside the product binaries.st of these tests assume
            // the default properties file does not exist so we'll ensure it doesn't.
            // Any tests that do require default properties file should re-create it
            // with known content.
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();
        }

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void ArgProc_UnrecognizedArgumentsAreIgnored()
        {
            TestLogger logger = new TestLogger();

            // 1. Minimal command line settings with extra values
            IBootstrapperSettings settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "foo", "blah", "/xxxx");
            AssertCommonSettings(settings, "foo", true, "\"/d:sonar.host.url=foo\" \"foo\" \"blah\" \"/xxxx\"");
        }

        [TestMethod]
        public void ArgProc_InstallTargets()
        {
            TestLogger logger = new TestLogger();

            //  No argument passed -> install targets
            IBootstrapperSettings settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo");
            AssertCommonSettings(settings, "foo", true, "\"/d:sonar.host.url=foo\"");

            // "true"-> install targets
            settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "/install:true");
            AssertCommonSettings(settings, "foo", true, "\"/d:sonar.host.url=foo\"");

            // Case insensitive "TrUe"-> install targets
            settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "/install:TrUe");
            AssertCommonSettings(settings, "foo", true, "\"/d:sonar.host.url=foo\"");

            // "false"-> don't install targets
            settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "/install:false");
            AssertCommonSettings(settings, "foo", false, "\"/d:sonar.host.url=foo\"");

            // Case insensitive "falSE" 
            settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "/install:falSE");
            AssertCommonSettings(settings, "foo", false, "\"/d:sonar.host.url=foo\"");

            // Invalid value -> parsing should fail
            logger = CheckProcessingFails("/d:sonar.host.url=foo", "/install:1");
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("/install");

            // Invalid value -> parsing should fail
            logger = CheckProcessingFails("/d:sonar.host.url=foo", "/install:");
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("/install");

            // Invalid value -> parsing should fail
            logger = CheckProcessingFails("/d:sonar.host.url=foo", @"/install:"" """);
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("/install");

            // Duplicate value -> parsing should fail
            logger = CheckProcessingFails("/d:sonar.host.url=foo", "/install:true", "/install:false");
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("/install");
        }

        [TestMethod]
        public void ArgProc_StripVerbsAndPrefixes()
        {
            TestLogger logger = new TestLogger();
            
            IBootstrapperSettings settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "/begin:true", "/install:true");
            AssertCommonSettings(settings, "foo", true, "\"/d:sonar.host.url=foo\" \"/begin:true\"");

            settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "begin", "/installXXX:true");
            AssertCommonSettings(settings, "foo", true, "\"/d:sonar.host.url=foo\" \"/installXXX:true\"");
        }

        [TestMethod]
        [Ignore] // SONARMSBRU-101
        public void ArgProc_ArgumentsWithWellKnownVerb()
        {
            TestLogger logger = new TestLogger();

            IBootstrapperSettings settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "begin", "begingX");
        }


        [TestMethod]
        public void ArgProc_UrlIsRequired()
        {
            // 0. Setup
            TestLogger logger;

            // Create a valid settings file that contains a URL
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string propertiesFilePath = Path.Combine(testDir, "mysettings.txt");

            AnalysisProperties properties = new AnalysisProperties();
            properties.Add(new Property() { Id = SonarProperties.HostUrl, Value = "http://filehost" });
            properties.Save(propertiesFilePath);


            // 1. Url is not specified on the command line or in a properties file -> fail
            logger = CheckProcessingFails("/key:k1", "/name:n1", "/version:1.0");

            logger.AssertErrorLogged(SonarQube.Bootstrapper.Resources.ERROR_Args_UrlRequired);
            logger.AssertErrorsLogged(1);


            // 2. Url is specified in the file -> ok
            logger = new TestLogger();
            IBootstrapperSettings settings = CheckProcessingSucceeds(logger, "/key:k1", "/name:n1", "/version:1.0", "/s:" + propertiesFilePath);
            AssertCommonSettings(
                settings,
                "http://filehost",
                true);

            // 3. Url is specified on the command line too -> ok, and overrides the file setting
            logger = new TestLogger();
            settings = CheckProcessingSucceeds(logger, "/key:k1", "/name:n1", "/version:1.0", "/s:" + propertiesFilePath, "/d:sonar.host.url=http://cmdlinehost");
            AssertCommonSettings(
                settings,
                "http://cmdlinehost",
                true);
        }


        [TestMethod]
        public void ArgProc_PropertyOverriding()
        {
            // Command line properties should take precedence

            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "settings");
            string fullPropertiesPath = Path.Combine(testDir, "settings.txt");
            AnalysisProperties properties = new AnalysisProperties();
            properties.Add(new Property() { Id = SonarProperties.HostUrl, Value = "http://settingsFile" });
            properties.Save(fullPropertiesPath);

            TestLogger logger = new TestLogger();

            // 1. Settings file only
            IBootstrapperSettings settings = CheckProcessingSucceeds(logger, "/s: " + fullPropertiesPath);
            AssertCommonSettings(settings, "http://settingsFile", true);

            //// 2. Both file and cmd line
            settings = CheckProcessingSucceeds(logger, "/s: " + fullPropertiesPath, "/d:sonar.host.url=http://cmdline");
            AssertCommonSettings(settings, "http://cmdline", true); // cmd line wins

            //// 3. Cmd line only
            settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=http://cmdline", "/d:other=property", "/d:a=b c");
            AssertCommonSettings(settings, "http://cmdline", true); // cmd line wins
        }

        [TestMethod]
        public void ArgProc_InvalidCmdLineProperties()
        {
            // Incorrectly formed /d:[key]=[value] arguments
            TestLogger logger;

            logger = CheckProcessingFails("/d:sonar.host.url=foo",
                "/d: key1=space before",
                "/d:key2 = space after)");

            logger.AssertSingleErrorExists(" key1");
            logger.AssertSingleErrorExists("key2 ");
        }

        [TestMethod]
        public void ArgProc_BeginVerb()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string validUrl = "/d:sonar.host.url=http://foo";

            // 1. Minimal parameters -> valid
            IBootstrapperSettings settings = CheckProcessingSucceeds(logger, validUrl, "begin");
            AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
            logger.AssertWarningsLogged(0);

            // 2. With additional parameters -> valid
            settings = CheckProcessingSucceeds(logger, validUrl, "begin", "ignored", "k=2");
            AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
            logger.AssertWarningsLogged(0);

            // 3. Multiple occurrences -> error
            logger = CheckProcessingFails(validUrl, "begin", "begin");
            logger.AssertSingleErrorExists(ArgumentProcessor.BeginVerb);

            // 4. Missing -> valid with warning
            logger = new TestLogger();
            CheckProcessingSucceeds(logger, validUrl);
            logger.AssertSingleWarningExists(ArgumentProcessor.BeginVerb);

            // 5. Incorrect case -> treated as unrecognised argument 
            // -> valid with 1 warning (no begin / end specified warning)
            logger = new TestLogger();
            CheckProcessingSucceeds(logger, validUrl, "BEGIN"); // wrong case
            logger.AssertWarningsLogged(1);
            logger.AssertSingleWarningExists(ArgumentProcessor.BeginVerb);
        }

        [TestMethod]
        public void ArgProc_EndVerb()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string validUrl = "/d:sonar.host.url=http://foo";

            // 1. Minimal parameters -> valid
            IBootstrapperSettings settings = CheckProcessingSucceeds(logger, "end");
            AssertExpectedPhase(AnalysisPhase.PostProcessing, settings);

            // 2. With additional parameters -> valid
            logger = new TestLogger();
            settings = CheckProcessingSucceeds(logger, "end", "ignored", "/d:key=value");
            AssertExpectedPhase(AnalysisPhase.PostProcessing, settings);
            logger.AssertWarningsLogged(0);

            // 3. Multiple occurrences -> invalid
            logger = CheckProcessingFails(validUrl, "end", "end");
            logger.AssertSingleErrorExists(ArgumentProcessor.EndVerb);

            // 4. Missing, no other arguments -> valid with warning
            logger = new TestLogger();
            settings = CheckProcessingSucceeds(logger);
            AssertExpectedPhase(AnalysisPhase.PostProcessing, settings);
            logger.AssertWarningsLogged(1);

            // 5. Incorrect case -> unrecognised -> treated as preprocessing -> fails (URL not supplied)
            logger = CheckProcessingFails("END");
            logger.AssertErrorsLogged();
        }

        [TestMethod]
        public void ArgProc_BeginAndEndVerbs()
        {
            // Arrange
            TestLogger logger;
            string validUrl = "/d:sonar.host.url=http://foo";

            // 1. Both present
            logger = CheckProcessingFails(validUrl, "begin", "end");
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("begin", "end");
        }

        #endregion

        #region Checks

        private static IBootstrapperSettings CheckProcessingSucceeds(TestLogger logger, params string[] cmdLineArgs)
        {
            IBootstrapperSettings settings;
            bool success = ArgumentProcessor.TryProcessArgs(cmdLineArgs, logger, out settings);

            Assert.IsTrue(success, "Expecting processing to succeed");
            Assert.IsNotNull(settings, "Settings should not be null if processing succeeds");
            logger.AssertErrorsLogged(0);

            return settings;
        }

        private static TestLogger CheckProcessingFails(params string[] cmdLineArgs)
        {
            TestLogger logger = new TestLogger();
            IBootstrapperSettings settings;
            bool success = ArgumentProcessor.TryProcessArgs(cmdLineArgs, logger, out settings);

            Assert.IsFalse(success, "Expecting processing to fail");
            Assert.IsNull(settings, "Settings should be null if processing fails");
            logger.AssertErrorsLogged();

            return logger;
        }

        private void AssertCommonSettings(IBootstrapperSettings settings, string expectedUrl, bool expectedInstallLoaderTargets, string expectedCmdLineArgs)
        {
            Assert.AreEqual(expectedUrl, settings.SonarQubeUrl, "Unexpected SonarQube URL");
            Assert.AreEqual(expectedInstallLoaderTargets, settings.InstallLoaderTargets, "Unexpected Install Targets setting");
            Assert.AreEqual(expectedCmdLineArgs, settings.ChildCmdLineArgs, "Unexpected child command line arguments");
        }

        private void AssertCommonSettings(IBootstrapperSettings settings, string expectedUrl, bool expectedInstallLoaderTargets)
        {
            Assert.AreEqual(expectedUrl, settings.SonarQubeUrl, "Unexpected SonarQube URL");
            Assert.AreEqual(expectedInstallLoaderTargets, settings.InstallLoaderTargets, "Unexpected Install Targets setting");
        }

        private static void AssertExpectedPhase(AnalysisPhase expected, IBootstrapperSettings settings)
        {
            Assert.AreEqual(expected, settings.Phase, "Unexpected analysis phase");
        }

        #endregion
    }
}
