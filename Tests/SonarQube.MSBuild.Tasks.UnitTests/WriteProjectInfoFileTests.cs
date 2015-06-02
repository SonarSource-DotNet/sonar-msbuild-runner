﻿//-----------------------------------------------------------------------
// <copyright file="WriteProjectInfoFileTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class WriteProjectInfoFileTests
    {
        private const string ExpectedProjectInfoFileName = "ProjectInfo.xml";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [Description("Tests that the project info file is created when the task is executed")]
        public void WriteProjectInfoFile_FileCreated()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.FullProjectPath = "c:\\fullPath\\project.proj";
            task.IsTest = true;
            task.IsExcluded = false;
            task.OutputFolder = testFolder;
            task.ProjectGuid = projectGuid.ToString("B");
            task.ProjectName = "MyProject";
            // No analysis results are supplied

            // Act
            ProjectInfo reloadedProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);

            // Addition assertions
            ProjectInfoAssertions.AssertExpectedValues(
                "c:\\fullPath\\project.proj",
                ProjectType.Test,
                projectGuid,
                "MyProject",
                false, // IsExcluded
                reloadedProjectInfo);
        }

        [TestMethod]
        [Description("Tests that analysis results are correctly handled")]
        public void WriteProjectInfoFile_AnalysisResults()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.BuildEngine = new DummyBuildEngine();
            task.FullProjectPath = "x:\\a.csproj";
            task.IsTest = false;
            task.OutputFolder = testFolder;
            task.ProjectGuid = projectGuid.ToString("B");
            task.ProjectName = "MyProject";

            List<ITaskItem> resultInputs = new List<ITaskItem>();

            // Add invalid task items
            // Note: the TaskItem class won't allow the item spec or metadata values to be null,
            // so we aren't testing those
            resultInputs.Add(CreateMetadataItem("itemSpec1", "abc", "def")); // Id field is missing
            resultInputs.Add(CreateAnalysisResultTaskItem("\r", "should be ignored - whitespace")); // whitespace id
            resultInputs.Add(CreateAnalysisResultTaskItem("should be ignored - whitespace", " ")); // whitespace location

            // Add valid task items
            resultInputs.Add(CreateAnalysisResultTaskItem("id1", "location1"));
            resultInputs.Add(CreateAnalysisResultTaskItem("id2", "location2", "md1", "md1 value", "md2", "md2 value")); // valid but with extra metadata

            task.AnalysisResults = resultInputs.ToArray();

            // Act
            ProjectInfo createdProjectInfo;
            using (new AssertIgnoreScope()) // We've deliberately created task items with unexpected item names that will cause assertions
            {
                createdProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);
            }

            // Assert
            AssertAnalysisResultExists(createdProjectInfo, "id1", "location1");
            AssertAnalysisResultExists(createdProjectInfo, "id2", "location2");
            AssertExpectedAnalysisResultCount(2, createdProjectInfo);
        }

        [TestMethod]
        [Description("Tests that analysis settings are correctly handled")]
        public void WriteProjectInfoFile_AnalysisSettings()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            WriteProjectInfoFile task = new WriteProjectInfoFile();

            DummyBuildEngine buildEngine = new DummyBuildEngine();
            task.BuildEngine = buildEngine;
            task.FullProjectPath = "x:\\analysisSettings.csproj";
            task.IsTest = false;
            task.OutputFolder = testFolder;
            task.ProjectGuid = projectGuid.ToString("B");
            task.ProjectName = "MyProject";

            // Example of a valid setting:
            // <SonarQubeSetting Include="sonar.resharper.projectname">
            //    <Value>C:\zzz\reportlocation.xxx</Value>
            // </SonarQubeSetting>                

            List<ITaskItem> settingsInputs = new List<ITaskItem>();

            // Add invalid task items
            // Note: the TaskItem class won't allow the item spec or metadata values to be null,
            // so we aren't testing those
            settingsInputs.Add(CreateMetadataItem("invalid.missing.value.metadata", "NotValueMetadata", "missing value 1")); // value field is missing
            settingsInputs.Add(CreateAnalysisSettingTaskItem(" ", "should be ignored - key is whitespace only")); // whitespace key
            settingsInputs.Add(CreateAnalysisSettingTaskItem("invalid spaces in key", "spaces.in.key")); // spaces in key
            settingsInputs.Add(CreateAnalysisSettingTaskItem(" invalid.key.has.leading.whitespace", "leading whitespace in key"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem("invalid.key.has.trailing.whitespace ", "trailing whitespace in key"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem(".invalid.non.alpha.first.character", "non alpha first character"));

            // Add valid task items
            settingsInputs.Add(CreateAnalysisSettingTaskItem("valid.setting.1", @"c:\dir1\dir2\file.txt"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem("valid.value.is.whitespace.only", " "));
            settingsInputs.Add(CreateMetadataItem("valid.metadata.name.is.case.insensitive", BuildTaskConstants.SettingValueMetadataName.ToUpperInvariant(), "uppercase metadata name")); // metadata name is in the wrong case
            settingsInputs.Add(CreateAnalysisSettingTaskItem("valid.value.has.whitespace", "valid setting with whitespace"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem("X", "single character key"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem("Y...", "single character followed by periods"));
            settingsInputs.Add(CreateAnalysisSettingTaskItem("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.sonar.projectName", "guid followed by key"));

            task.AnalysisSettings = settingsInputs.ToArray();

            // Act
            ProjectInfo createdProjectInfo;
            createdProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);

            // Assert
            buildEngine.AssertSingleWarningExists("invalid.missing.value.metadata");
            // Can't easily check for the message complaining against the empty key
            buildEngine.AssertSingleWarningExists("invalid spaces in key");
            buildEngine.AssertSingleWarningExists(" invalid.key.has.leading.whitespace");
            buildEngine.AssertSingleWarningExists("invalid.key.has.trailing.whitespace ");
            buildEngine.AssertSingleWarningExists(".invalid.non.alpha.first.character");

            AssertAnalysisSettingExists(createdProjectInfo, "valid.setting.1", @"c:\dir1\dir2\file.txt");
            AssertAnalysisSettingExists(createdProjectInfo, "valid.value.is.whitespace.only", " ");
            AssertAnalysisSettingExists(createdProjectInfo, "valid.value.has.whitespace", "valid setting with whitespace");
            AssertAnalysisSettingExists(createdProjectInfo, "valid.metadata.name.is.case.insensitive", "uppercase metadata name");
            AssertAnalysisSettingExists(createdProjectInfo, "X", "single character key");
            AssertAnalysisSettingExists(createdProjectInfo, "Y...", "single character followed by periods");
            AssertAnalysisSettingExists(createdProjectInfo, "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.sonar.projectName", "guid followed by key");

            AssertExpectedAnalysisSettingsCount(7, createdProjectInfo);
            AssertExpectedGlobalAnalysisSettingsCount(0, createdProjectInfo);
        }

        [TestMethod]
        [Description("Tests that global analysis settings are correctly handled")]
        public void WriteProjectInfoFile_GlobalAnalysisSettings()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid projectGuid = Guid.NewGuid();

            WriteProjectInfoFile task = new WriteProjectInfoFile();

            DummyBuildEngine buildEngine = new DummyBuildEngine();
            task.BuildEngine = buildEngine;
            task.FullProjectPath = "x:\\analysisSettings.csproj";
            task.IsTest = false;
            task.OutputFolder = testFolder;
            task.ProjectGuid = projectGuid.ToString("B");
            task.ProjectName = "MyProject";

            // Example of a valid global setting:
            // <SonarQubeGlobalSetting Include="sonar.vso.team">
            //    <Value>My Team Name</Value>
            // </SonarQubeGlobalSetting>                

            List<ITaskItem> globalSettingsInputs = new List<ITaskItem>();

            // Add invalid task items
            // Note: the TaskItem class won't allow the item spec or metadata values to be null,
            // so we aren't testing those
            globalSettingsInputs.Add(CreateMetadataItem("invalid.missing.value.metadata", "NotValueMetadata", "missing value 1")); // value field is missing
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem(" ", "should be ignored - key is whitespace only")); // whitespace key
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem("invalid spaces in key", "spaces.in.key")); // spaces in key
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem(" invalid.key.has.leading.whitespace", "leading whitespace in key"));
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem("invalid.key.has.trailing.whitespace ", "trailing whitespace in key"));
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem(".invalid.non.alpha.first.character", "non alpha first character"));

            // Add valid task items
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem("valid.setting.1", @"c:\dir1\dir2\file.txt"));
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem("valid.value.is.whitespace.only", " "));
            globalSettingsInputs.Add(CreateMetadataItem("valid.metadata.name.is.case.insensitive", BuildTaskConstants.SettingValueMetadataName.ToUpperInvariant(), "uppercase metadata name")); // metadata name is in the wrong case
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem("valid.value.has.whitespace", "valid setting with whitespace"));
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem("X", "single character key"));
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem("Y...", "single character followed by periods"));
            globalSettingsInputs.Add(CreateAnalysisSettingTaskItem("7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.sonar.projectName", "guid followed by key"));

            task.GlobalAnalysisSettings = globalSettingsInputs.ToArray();

            // Act
            ProjectInfo createdProjectInfo;
            createdProjectInfo = ExecuteAndCheckSucceeds(task, testFolder);

            // Assert
            buildEngine.AssertSingleWarningExists("invalid.missing.value.metadata");
            // Can't easily check for the message complaining against the empty key
            buildEngine.AssertSingleWarningExists("invalid spaces in key");
            buildEngine.AssertSingleWarningExists(" invalid.key.has.leading.whitespace");
            buildEngine.AssertSingleWarningExists("invalid.key.has.trailing.whitespace ");
            buildEngine.AssertSingleWarningExists(".invalid.non.alpha.first.character");

            AssertGlobalAnalysisSettingExists(createdProjectInfo, "valid.setting.1", @"c:\dir1\dir2\file.txt");
            AssertGlobalAnalysisSettingExists(createdProjectInfo, "valid.value.is.whitespace.only", " ");
            AssertGlobalAnalysisSettingExists(createdProjectInfo, "valid.value.has.whitespace", "valid setting with whitespace");
            AssertGlobalAnalysisSettingExists(createdProjectInfo, "valid.metadata.name.is.case.insensitive", "uppercase metadata name");
            AssertGlobalAnalysisSettingExists(createdProjectInfo, "X", "single character key");
            AssertGlobalAnalysisSettingExists(createdProjectInfo, "Y...", "single character followed by periods");
            AssertGlobalAnalysisSettingExists(createdProjectInfo, "7B3B7244-5031-4D74-9BBD-3316E6B5E7D5.sonar.projectName", "guid followed by key");

            AssertExpectedGlobalAnalysisSettingsCount(7, createdProjectInfo);
            AssertExpectedAnalysisSettingsCount(0, createdProjectInfo);
        }

        [TestMethod]
        [Description("Tests that the project info file is not created if a project guid is not supplied")]
        [WorkItem(50)] // Regression test for Bug 50:MSBuild projects with missing ProjectGuids cause the build to fail
        public void WriteProjectInfoFile_MissingProjectGuid()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            WriteProjectInfoFile task = new WriteProjectInfoFile();
            task.FullProjectPath = "c:\\fullPath\\project.proj";
            task.IsTest = true;
            task.OutputFolder = testFolder;
            task.ProjectName = "ProjectWithoutProjectGuid";
            // No analysis results are supplied

            // Act
            DummyBuildEngine engine = new DummyBuildEngine();
            task.BuildEngine = engine;
            bool success = task.Execute();

            Assert.IsTrue(success, "Not expecting the task to fail as this would fail the build");
            engine.AssertNoErrors();
            Assert.AreEqual(1, engine.Warnings.Count, "Expecting a build warning as the ProjectGuid is missing");

            BuildWarningEventArgs firstWarning = engine.Warnings[0];
            Assert.IsNotNull(firstWarning.Message, "Warning message should not be null");
        }

        #endregion

        #region Helper methods

        private static ITaskItem CreateAnalysisResultTaskItem(string id, string location, params string[] idAndValuePairs)
        {
            ITaskItem item = CreateMetadataItem(location, idAndValuePairs);
            item.SetMetadata(BuildTaskConstants.ResultMetadataIdProperty, id);
            return item;
        }

        private static ITaskItem CreateAnalysisSettingTaskItem(string key, string value)
        {
            ITaskItem item = new TaskItem(key);
            item.SetMetadata(BuildTaskConstants.SettingValueMetadataName, value);
            return item;
        }

        private static ITaskItem CreateMetadataItem(string itemSpec, params string[] idAndValuePairs)
        {
            ITaskItem item = new TaskItem(itemSpec);
            int remainder;
            int dummy = Math.DivRem(idAndValuePairs.Length, 2, out remainder);
            Assert.AreEqual(0, remainder, "Test setup error: the supplied list should contain id-location pairs");

            for (int index = 0; index < idAndValuePairs.Length; index += 2)
            {
                item.SetMetadata(idAndValuePairs[index], idAndValuePairs[index + 1]);
            }
            return item;
        }

        #endregion

        #region Checks

        private ProjectInfo ExecuteAndCheckSucceeds(Task task, string testFolder)
        {
            string expectedOutputFile = Path.Combine(testFolder, ExpectedProjectInfoFileName);
            Assert.IsFalse(File.Exists(expectedOutputFile), "Test error: output file should not exist before the task is executed");

            bool result = task.Execute();

            Assert.IsTrue(result, "Expecting the task execution to succeed");
            Assert.IsTrue(File.Exists(expectedOutputFile), "Expected output file was not created by the task. Expected: {0}", expectedOutputFile);
            this.TestContext.AddResultFile(expectedOutputFile);

            ProjectInfo reloadedProjectInfo = ProjectInfo.Load(expectedOutputFile);
            Assert.IsNotNull(reloadedProjectInfo, "Not expecting the reloaded project info file to be null");
            return reloadedProjectInfo;
        }

        private static void AssertAnalysisResultExists(ProjectInfo actual, string expectedId, string expectedLocation)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.AnalysisResults, "AnalysisResults should not be null");

            AnalysisResult result = actual.AnalysisResults.FirstOrDefault(ar => expectedId.Equals(ar.Id, StringComparison.InvariantCulture));
            Assert.IsNotNull(result, "AnalysisResult with the expected id does not exist. Id: {0}", expectedId);

            Assert.AreEqual(expectedLocation, result.Location, "Analysis result does not have the expected location");
        }

        private static void AssertExpectedAnalysisResultCount(int count, ProjectInfo actual)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.AnalysisResults, "AnalysisResults should not be null");

            Assert.AreEqual(count, actual.AnalysisResults.Count, "Unexpected number of AnalysisResult items");
        }

        private static void AssertAnalysisSettingExists(ProjectInfo actual, string expectedId, string expectedValue)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.AnalysisSettings, "AnalysisSettings should not be null");

            AnalysisSetting setting = actual.AnalysisSettings.FirstOrDefault(ar => expectedId.Equals(ar.Id, StringComparison.InvariantCulture));
            Assert.IsNotNull(setting, "AnalysisSetting with the expected id does not exist. Id: {0}", expectedId);

            Assert.AreEqual(expectedValue, setting.Value, "Setting does not have the expected value");
        }

        private static void AssertGlobalAnalysisSettingExists(ProjectInfo actual, string expectedId, string expectedValue)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.GlobalAnalysisSettings, "GlobalAnalysisSettings should not be null");

            AnalysisSetting setting = actual.GlobalAnalysisSettings.FirstOrDefault(ar => expectedId.Equals(ar.Id, StringComparison.InvariantCulture));
            Assert.IsNotNull(setting, "GlobalAnalysisSetting with the expected id does not exist. Id: {0}", expectedId);

            Assert.AreEqual(expectedValue, setting.Value, "Setting does not have the expected value");
        }

        private static void AssertExpectedAnalysisSettingsCount(int count, ProjectInfo actual)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.AnalysisSettings, "AnalysisSettings should not be null");

            Assert.AreEqual(count, actual.AnalysisSettings.Count, "Unexpected number of AnalysisSettings items");
        }

        private static void AssertExpectedGlobalAnalysisSettingsCount(int count, ProjectInfo actual)
        {
            Assert.IsNotNull(actual, "Supplied project info should not be null");
            Assert.IsNotNull(actual.GlobalAnalysisSettings, "GlobalAnalysisSettings should not be null");

            Assert.AreEqual(count, actual.GlobalAnalysisSettings.Count, "Unexpected number of GlobalAnalysisSettings items");
        }

        #endregion
    }
}
