﻿/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class AnalysisConfigGeneratorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void AnalysisConfGen_Simple()
        {
            // Arrange
            string analysisDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            TestLogger logger = new TestLogger();

            ListPropertiesProvider propertyProvider = new ListPropertiesProvider();
            propertyProvider.AddProperty(SonarProperties.HostUrl, "http://foo");
            ProcessedArgs args = new ProcessedArgs("valid.key", "valid.name", "1.0", null, false, EmptyPropertyProvider.Instance, propertyProvider, EmptyPropertyProvider.Instance);

            TeamBuildSettings tbSettings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);

            Dictionary<string, string> serverSettings = new Dictionary<string, string>
            {
                { "server.key.1", "server.value.1" }
            };

            AnalyzerSettings analyzerSettings = new AnalyzerSettings
            {
                RuleSetFilePath = "c:\\xxx.ruleset",
                AdditionalFilePaths = new List<string>()
            };
            analyzerSettings.AdditionalFilePaths.Add("f:\\additionalPath1.txt");
            analyzerSettings.AnalyzerAssemblyPaths = new List<string>
            {
                "f:\\temp\\analyzer1.dll"
            };

            List<AnalyzerSettings> analyzersSettings = new List<AnalyzerSettings>
            {
                analyzerSettings
            };
            Directory.CreateDirectory(tbSettings.SonarConfigDirectory); // config directory needs to exist

            // Act
            AnalysisConfig actualConfig = AnalysisConfigGenerator.GenerateFile(args, tbSettings, serverSettings, analyzersSettings, logger);

            // Assert
            AssertConfigFileExists(actualConfig);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            Assert.AreEqual("valid.key", actualConfig.SonarProjectKey);
            Assert.AreEqual("valid.name", actualConfig.SonarProjectName);
            Assert.AreEqual("1.0", actualConfig.SonarProjectVersion);

            Assert.AreEqual("http://foo", actualConfig.SonarQubeHostUrl);

            Assert.AreEqual(tbSettings.SonarBinDirectory, actualConfig.SonarBinDir);
            Assert.AreEqual(tbSettings.SonarConfigDirectory, actualConfig.SonarConfigDir);
            Assert.AreEqual(tbSettings.SonarOutputDirectory, actualConfig.SonarOutputDir);
            Assert.AreEqual(tbSettings.SonarScannerWorkingDirectory, actualConfig.SonarScannerWorkingDirectory);
            Assert.AreEqual(tbSettings.BuildUri, actualConfig.GetBuildUri());
            Assert.AreEqual(tbSettings.TfsUri, actualConfig.GetTfsUri());

            Assert.IsNotNull(actualConfig.ServerSettings);
            Property serverProperty = actualConfig.ServerSettings.SingleOrDefault(s => string.Equals(s.Id, "server.key.1", System.StringComparison.Ordinal));
            Assert.IsNotNull(serverProperty);
            Assert.AreEqual("server.value.1", serverProperty.Value);
            Assert.AreSame(analyzerSettings, actualConfig.AnalyzersSettings[0]);
        }

        [TestMethod]
        public void AnalysisConfGen_FileProperties()
        {
            // File properties should not be copied to the file.
            // Instead, a pointer to the file should be created.

            // Arrange
            string analysisDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            TestLogger logger = new TestLogger();

            // The set of file properties to supply
            AnalysisProperties fileProperties = new AnalysisProperties
            {
                new Property() { Id = SonarProperties.HostUrl, Value = "http://myserver" },
                new Property() { Id = "file.only", Value = "file value" }
            };
            string settingsFilePath = Path.Combine(analysisDir, "settings.txt");
            fileProperties.Save(settingsFilePath);

            FilePropertyProvider fileProvider = FilePropertyProvider.Load(settingsFilePath);

            ProcessedArgs args = new ProcessedArgs("key", "name", "version", "organization", false, EmptyPropertyProvider.Instance, fileProvider, EmptyPropertyProvider.Instance);

            TeamBuildSettings settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            // Act
            AnalysisConfig actualConfig = AnalysisConfigGenerator.GenerateFile(args, settings, new Dictionary<string, string>(), new List<AnalyzerSettings>(), logger);

            // Assert
            AssertConfigFileExists(actualConfig);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            string actualSettingsFilePath = actualConfig.GetSettingsFilePath();
            Assert.AreEqual(settingsFilePath, actualSettingsFilePath, "Unexpected settings file path");

            // Check the file setting value do not appear in the config file
            AssertFileDoesNotContainText(actualConfig.FileName, "file.only");

            Assert.AreEqual(settings.SourcesDirectory, actualConfig.SourcesDirectory);
            Assert.AreEqual(settings.SonarScannerWorkingDirectory, actualConfig.SonarScannerWorkingDirectory);
            AssertExpectedLocalSetting(SonarProperties.Organization, "organization", actualConfig);
        }

        [TestMethod]
        [WorkItem(127)] // Do not store the db and server credentials in the config files: http://jira.sonarsource.com/browse/SONARMSBRU-127
        public void AnalysisConfGen_AnalysisConfigDoesNotContainSensitiveData()
        {
            // Arrange
            string analysisDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            TestLogger logger = new TestLogger();

            ListPropertiesProvider cmdLineArgs = new ListPropertiesProvider();
            // Public args - should be written to the config file
            cmdLineArgs.AddProperty("sonar.host.url", "http://host");
            cmdLineArgs.AddProperty("public.key", "public value");
            cmdLineArgs.AddProperty("sonar.user.license.secured", "user input license");
            cmdLineArgs.AddProperty("server.key.secured.xxx", "not really secure");
            cmdLineArgs.AddProperty("sonar.value", "value.secured");

            // Sensitive values - should not be written to the config file
            cmdLineArgs.AddProperty(SonarProperties.DbPassword, "secret db password");

            // Create a settings file with public and sensitive data
            AnalysisProperties fileSettings = new AnalysisProperties
            {
                new Property() { Id = "file.public.key", Value = "file public value" },
                new Property() { Id = SonarProperties.DbUserName, Value = "secret db user" },
                new Property() { Id = SonarProperties.DbPassword, Value = "secret db password" }
            };
            string fileSettingsPath = Path.Combine(analysisDir, "fileSettings.txt");
            fileSettings.Save(fileSettingsPath);
            FilePropertyProvider fileProvider = FilePropertyProvider.Load(fileSettingsPath);

            ProcessedArgs args = new ProcessedArgs("key", "name", "1.0", null, false, cmdLineArgs, fileProvider, EmptyPropertyProvider.Instance);

            IDictionary<string, string> serverProperties = new Dictionary<string, string>
            {
                // Public server settings
                { "server.key.1", "server value 1" },
                // Sensitive server settings
                { SonarProperties.SonarUserName, "secret user" },
                { SonarProperties.SonarPassword, "secret pwd" },
                { "sonar.vbnet.license.secured", "secret license" },
                { "sonar.cpp.License.Secured", "secret license 2" }
            };

            TeamBuildSettings settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(analysisDir);
            Directory.CreateDirectory(settings.SonarConfigDirectory); // config directory needs to exist

            // Act
            AnalysisConfig config = AnalysisConfigGenerator.GenerateFile(args, settings, serverProperties, new List<AnalyzerSettings>(), logger);

            // Assert
            AssertConfigFileExists(config);
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);

            // Check the config

            // "Public" arguments should be in the file
            Assert.AreEqual("key", config.SonarProjectKey, "Unexpected project key");
            Assert.AreEqual("name", config.SonarProjectName, "Unexpected project name");
            Assert.AreEqual("1.0", config.SonarProjectVersion, "Unexpected project version");

            AssertExpectedLocalSetting(SonarProperties.HostUrl, "http://host", config);
            AssertExpectedLocalSetting("sonar.user.license.secured", "user input license", config); // we only filter out *.secured server settings
            AssertExpectedLocalSetting("sonar.value", "value.secured", config);
            AssertExpectedLocalSetting("server.key.secured.xxx", "not really secure", config);
            AssertExpectedServerSetting("server.key.1", "server value 1", config);

            AssertFileDoesNotContainText(config.FileName, "file.public.key"); // file settings values should not be in the config
            AssertFileDoesNotContainText(config.FileName, "secret"); // sensitive data should not be in config
        }

        #endregion Tests

        #region Checks

        private void AssertConfigFileExists(AnalysisConfig config)
        {
            Assert.IsNotNull(config, "Supplied config should not be null");

            Assert.IsFalse(string.IsNullOrWhiteSpace(config.FileName), "Config file name should be set");
            Assert.IsTrue(File.Exists(config.FileName), "Expecting the analysis config file to exist. Path: {0}", config.FileName);

            this.TestContext.AddResultFile(config.FileName);
        }

        private static void AssertSettingDoesNotExist(string key, AnalysisConfig actualConfig)
        {
            bool found = actualConfig.GetAnalysisSettings(true).TryGetProperty(key, out Property setting);
            Assert.IsFalse(found, "The setting should not exist. Key: {0}", key);
        }

        private static void AssertExpectedServerSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            bool found = Property.TryGetProperty(key, actualConfig.ServerSettings, out Property property);

            Assert.IsTrue(found, "Expected server property was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, property.Value, "Unexpected server value. Key: {0}", key);
        }

        private static void AssertExpectedLocalSetting(string key, string expectedValue, AnalysisConfig acutalConfig)
        {
            bool found = Property.TryGetProperty(key, acutalConfig.LocalSettings, out Property property);

            Assert.IsTrue(found, "Expected local property was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, property.Value, "Unexpected local value. Key: {0}", key);
        }

        private static void AssertFileDoesNotContainText(string filePath, string text)
        {
            Assert.IsTrue(File.Exists(filePath), "File should exist: {0}", filePath);

            string content = File.ReadAllText(filePath);
            Assert.IsTrue(content.IndexOf(text, System.StringComparison.InvariantCultureIgnoreCase) < 0, "Not expecting text to be found in the file. Text: '{0}', file: {1}",
                text, filePath);
        }

        #endregion Checks
    }
}