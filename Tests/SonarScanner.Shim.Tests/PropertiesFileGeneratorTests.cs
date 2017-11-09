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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using TestUtilities;

namespace SonarScanner.Shim.Tests
{
    [TestClass]
    public class PropertiesFileGeneratorTests
    {
        private const string TestSonarqubeOutputDir = @"e:\.sonarqube\out";
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void FileGen_NoProjectInfoFiles()
        {
            // Properties file should not be generated if there are no project info files.

            // Arrange - two sub-directories, neither containing a ProjectInfo.xml
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string subDir1 = TestUtils.EnsureTestSpecificFolder(this.TestContext, "dir1");
            string subDir2 = TestUtils.EnsureTestSpecificFolder(this.TestContext, "dir2");

            CreateEmptyFile(subDir1, "file1.txt");
            CreateEmptyFile(subDir2, "file2.txt");

            TestLogger logger = new TestLogger();
            AnalysisConfig config = new AnalysisConfig() { SonarOutputDir = testDir };

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            AssertFailedToCreatePropertiesFiles(result, logger);
            AssertExpectedProjectCount(0, result);
        }

        [TestMethod]
        public void FileGen_DuplicateProjectIds()
        {
            // ProjectInfo files with duplicate ids should be ignored

            // Arrange - three files, all with the same Guid, one of which is excluded
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid duplicateGuid = Guid.NewGuid();
            CreateProjectInfoInSubDir(testDir, "duplicate1", duplicateGuid, ProjectType.Product, false, "c:\\abc\\duplicateProject1.proj", "UTF-8"); // not excluded
            CreateProjectInfoInSubDir(testDir, "duplicate2", duplicateGuid, ProjectType.Test, false, "S:\\duplicateProject2.proj", "UTF-8"); // not excluded
            CreateProjectInfoInSubDir(testDir, "excluded", duplicateGuid, ProjectType.Product, true, "c:\\abc\\excluded.proj", "UTF-8"); // excluded

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Act
            ProjectInfoAnalysisResult result = null;
            using (new AssertIgnoreScope()) // expecting the properties writer to assert
            {
                result = PropertiesFileGenerator.GenerateFile(config, logger);
            }

            // Assert
            AssertExpectedStatus("duplicate1", ProjectInfoValidity.DuplicateGuid, result);
            AssertExpectedStatus("duplicate2", ProjectInfoValidity.DuplicateGuid, result);
            AssertExpectedStatus("excluded", ProjectInfoValidity.ExcludeFlagSet, result); // Expecting excluded rather than duplicate
            AssertExpectedProjectCount(3, result);

            // No valid project info files -> file not generated
            AssertFailedToCreatePropertiesFiles(result, logger);
            logger.AssertWarningsLogged(2); // should be a warning for each project with a duplicate id

            logger.AssertSingleWarningExists(duplicateGuid.ToString(), "c:\\abc\\duplicateProject1.proj");
            logger.AssertSingleWarningExists(duplicateGuid.ToString(), "S:\\duplicateProject2.proj");
        }

        [TestMethod]
        public void FileGen_ExcludedProjectsAreNotDuplicates()
        {
            // Excluded ProjectInfo files should be ignored when calculating duplicates

            // Arrange - two sub-directories, neither containing a ProjectInfo.xml
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            Guid duplicateGuid = Guid.NewGuid();
            CreateProjectInfoInSubDir(testDir, "excl1", duplicateGuid, ProjectType.Product, true, "c:\\abc\\excluded1.proj", "UTF-8"); // excluded
            CreateProjectInfoInSubDir(testDir, "excl2", duplicateGuid, ProjectType.Test, true, "c:\\abc\\excluded2.proj", "UTF-8"); // excluded
            CreateProjectInfoInSubDir(testDir, "notExcl", duplicateGuid, ProjectType.Product, false, "c:\\abc\\included.proj", "UTF-8"); // not excluded

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            AssertExpectedStatus("excl1", ProjectInfoValidity.ExcludeFlagSet, result);
            AssertExpectedStatus("excl2", ProjectInfoValidity.ExcludeFlagSet, result);
            AssertExpectedStatus("notExcl", ProjectInfoValidity.NoFilesToAnalyze, result); // not "duplicate" since the duplicate guids are excluded
            AssertExpectedProjectCount(3, result);

            // One valid project info file -> file
            AssertFailedToCreatePropertiesFiles(result, logger);
            logger.AssertWarningsLogged(0); // expects no warning
        }

        [TestMethod]
        public void FileGen_ValidFiles()
        {
            // Only non-excluded projects with files to analyze should be marked as valid

            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            CreateProjectInfoInSubDir(testDir, "withoutFiles", Guid.NewGuid(), ProjectType.Product, false, "c:\\abc\\withoutfile.proj", "UTF-8"); // not excluded
            CreateProjectWithFiles("withFiles1", testDir);
            CreateProjectWithFiles("withFiles2", testDir);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            AssertExpectedStatus("withoutFiles", ProjectInfoValidity.NoFilesToAnalyze, result);
            AssertExpectedStatus("withFiles1", ProjectInfoValidity.Valid, result);
            AssertExpectedStatus("withFiles2", ProjectInfoValidity.Valid, result);
            AssertExpectedProjectCount(3, result);

            // One valid project info file -> file created
            AssertPropertiesFilesCreated(result, logger);
        }

        [TestMethod]
        public void FileGen_ValidFiles_SourceEncoding_Provided()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            CreateProjectWithFiles("withFiles1", testDir);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            config.LocalSettings = new AnalysisProperties
            {
                new Property { Id = SonarProperties.SourceEncoding, Value = "test-encoding-here" }
            };

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            var settingsFileContent = File.ReadAllText(result.FullPropertiesFilePath);
            Assert.IsTrue(settingsFileContent.Contains("sonar.sourceEncoding=test-encoding-here"), "Command line parameter 'sonar.sourceEncoding' is ignored.");
        }

        [TestMethod]
        public void FileGen_ValidFiles_WithAlreadyValidSarif()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // SARIF file path
            string testSarifPath = Path.Combine(testDir, "testSarif.json");

            // Create SARIF report path property and add it to the project info
            AnalysisProperties projectSettings = new AnalysisProperties
            {
                new Property() { Id = PropertiesFileGenerator.ReportFileCsharpPropertyKey, Value = testSarifPath }
            };
            Guid projectGuid = Guid.NewGuid();
            CreateProjectWithFiles("withFiles1", testDir, projectGuid, true, projectSettings);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Mock SARIF fixer simulates already valid sarif
            MockRoslynV1SarifFixer mockSarifFixer = new MockRoslynV1SarifFixer(testSarifPath);
            string mockReturnPath = mockSarifFixer.ReturnVal;

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger, mockSarifFixer);

            // Assert
            Assert.AreEqual(1, mockSarifFixer.CallCount);

            // Already valid SARIF -> no change in file -> unchanged property
            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(projectGuid.ToString().ToUpper() + "." + PropertiesFileGenerator.ReportFileCsharpPropertyKey, mockReturnPath);
        }

        [TestMethod]
        public void FileGen_ValidFiles_WithFixableSarif()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // SARIF file path
            string testSarifPath = Path.Combine(testDir, "testSarif.json");

            // Create SARIF report path property and add it to the project info
            AnalysisProperties projectSettings = new AnalysisProperties
            {
                new Property() { Id = PropertiesFileGenerator.ReportFileCsharpPropertyKey, Value = testSarifPath }
            };
            Guid projectGuid = Guid.NewGuid();
            CreateProjectWithFiles("withFiles1", testDir, projectGuid, true, projectSettings);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Mock SARIF fixer simulates fixable SARIF with fixed name
            string returnPathDir = Path.GetDirectoryName(testSarifPath);
            string returnPathFileName = Path.GetFileNameWithoutExtension(testSarifPath) +
                RoslynV1SarifFixer.FixedFileSuffix + Path.GetExtension(testSarifPath);

            MockRoslynV1SarifFixer mockSarifFixer = new MockRoslynV1SarifFixer(returnPathFileName);
            string escapedMockReturnPath = mockSarifFixer.ReturnVal.Replace(@"\", @"\\");

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger, mockSarifFixer);

            // Assert
            Assert.AreEqual(1, mockSarifFixer.CallCount);
            Assert.AreEqual(RoslynV1SarifFixer.CSharpLanguage, mockSarifFixer.LastLanguage);


            // Fixable SARIF -> new file saved -> changed property
            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(projectGuid.ToString().ToUpper() + "." + PropertiesFileGenerator.ReportFileCsharpPropertyKey, escapedMockReturnPath);
        }

        [TestMethod]
        public void FileGen_ValidFiles_WithFixableSarif_VBNet()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // SARIF file path
            string testSarifPath = Path.Combine(testDir, "testSarif.json");

            // Create SARIF report path property and add it to the project info
            AnalysisProperties projectSettings = new AnalysisProperties
            {
                new Property() { Id = PropertiesFileGenerator.ReportFileVbnetPropertyKey, Value = testSarifPath }
            };
            Guid projectGuid = Guid.NewGuid();
            CreateProjectWithFiles("withFiles1", testDir, projectGuid, true, projectSettings);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Mock SARIF fixer simulates fixable SARIF with fixed name
            string returnPathDir = Path.GetDirectoryName(testSarifPath);
            string returnPathFileName = Path.GetFileNameWithoutExtension(testSarifPath) +
                RoslynV1SarifFixer.FixedFileSuffix + Path.GetExtension(testSarifPath);

            MockRoslynV1SarifFixer mockSarifFixer = new MockRoslynV1SarifFixer(returnPathFileName);
            string escapedMockReturnPath = mockSarifFixer.ReturnVal.Replace(@"\", @"\\");

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger, mockSarifFixer);

            // Assert
            Assert.AreEqual(1, mockSarifFixer.CallCount);
            Assert.AreEqual(RoslynV1SarifFixer.VBNetLanguage, mockSarifFixer.LastLanguage);

            // Fixable SARIF -> new file saved -> changed property
            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(projectGuid.ToString().ToUpper() + "." + PropertiesFileGenerator.ReportFileVbnetPropertyKey, escapedMockReturnPath);
        }

        [TestMethod]
        public void FileGen_ValidFiles_WithUnfixableSarif()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // SARIF file path
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            string escapedSarifPath = testSarifPath.Replace(@"\", @"\\");

            // Create SARIF report path property and add it to the project info
            AnalysisProperties projectSettings = new AnalysisProperties
            {
                new Property() { Id = PropertiesFileGenerator.ReportFileCsharpPropertyKey, Value = testSarifPath }
            };
            Guid projectGuid = Guid.NewGuid();
            CreateProjectWithFiles("withFiles1", testDir, projectGuid, true, projectSettings);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Mock SARIF fixer simulated unfixable/absent file
            MockRoslynV1SarifFixer mockSarifFixer = new MockRoslynV1SarifFixer(null);

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger, mockSarifFixer);

            // Assert
            Assert.AreEqual(1, mockSarifFixer.CallCount);

            // One valid project info file -> file created
            AssertPropertiesFilesCreated(result, logger);

            // Unfixable SARIF -> cannot fix -> report file property removed
            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingDoesNotExist(projectGuid.ToString().ToUpper() + "." + PropertiesFileGenerator.ReportFileCsharpPropertyKey);
        }

        [TestMethod]
        public void FileGen_FilesOutsideProjectPath()
        {
            // Files outside the project root should be ignored

            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string projectDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "project");
            string projectPath = Path.Combine(projectDir, "project.proj");
            string projectInfo = CreateProjectInfoInSubDir(testDir, "projectName", Guid.NewGuid(), ProjectType.Product, false, "UTF-8", projectPath); // not excluded

            // Create a content file, but not under the project directory
            string contentFileList = CreateFile(projectDir, "contentList.txt", Path.Combine(testDir, "contentFile1.txt"));
            AddAnalysisResult(projectInfo, AnalysisType.FilesToAnalyze, contentFileList);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            AssertExpectedStatus("projectName", ProjectInfoValidity.NoFilesToAnalyze, result);
            AssertExpectedProjectCount(1, result);

            // No files -> project file not created
            AssertFailedToCreatePropertiesFiles(result, logger);
        }

        [TestMethod]
        public void FileGen_SharedFiles()
        {
            // Shared files should be attached to the root project

            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string project1Dir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "project1");
            string project1Path = Path.Combine(project1Dir, "project1.proj");
            string project1Info = CreateProjectInfoInSubDir(testDir, "projectName1", Guid.NewGuid(), ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
            var sharedFile = Path.Combine(testDir, "contentFile.txt");
            CreateEmptyFile(testDir, "contentFile.txt");

            // Reference shared file, but not under the project directory
            string contentFileList1 = CreateFile(project1Dir, "contentList.txt", sharedFile);
            AddAnalysisResult(project1Info, AnalysisType.FilesToAnalyze, contentFileList1);

            string project2Dir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "project2");
            string project2Path = Path.Combine(project2Dir, "project2.proj");
            string project2Info = CreateProjectInfoInSubDir(testDir, "projectName2", Guid.NewGuid(), ProjectType.Product, false, project2Path, "UTF-8"); // not excluded

            // Reference shared file, but not under the project directory
            string contentFileList2 = CreateFile(project2Dir, "contentList.txt", sharedFile);
            AddAnalysisResult(project2Info, AnalysisType.FilesToAnalyze, contentFileList2);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists("sonar.projectBaseDir", testDir);
            provider.AssertSettingExists("sonar.sources", sharedFile);
        }

        // SONARMSBRU-335
        [TestMethod]
        public void FileGen_SharedFiles_CaseInsensitive()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            // Create 2 uuids and order them so that test is reproducible
            Guid[] uuids = new Guid[] { Guid.NewGuid(), Guid.NewGuid() };
            Array.Sort(uuids);

            string project1Dir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "project1");
            string project1Path = Path.Combine(project1Dir, "project1.proj");
            string project1Info = CreateProjectInfoInSubDir(testDir, "projectName1", uuids[0], ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
            var sharedFile = Path.Combine(testDir, "contentFile.txt");
            var sharedFileDifferentCase = Path.Combine(testDir, "ContentFile.TXT");
            CreateEmptyFile(testDir, "contentFile.txt");

            // Reference shared file, but not under the project directory
            string contentFileList1 = CreateFile(project1Dir, "contentList.txt", sharedFile);
            AddAnalysisResult(project1Info, AnalysisType.FilesToAnalyze, contentFileList1);

            string project2Dir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "project2");
            string project2Path = Path.Combine(project2Dir, "project2.proj");
            string project2Info = CreateProjectInfoInSubDir(testDir, "projectName2", uuids[1], ProjectType.Product, false, project2Path, "UTF-8"); // not excluded

            // Reference shared file, but not under the project directory
            string contentFileList2 = CreateFile(project2Dir, "contentList.txt", sharedFileDifferentCase);
            AddAnalysisResult(project2Info, AnalysisType.FilesToAnalyze, contentFileList2);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists("sonar.projectBaseDir", testDir);
            // First one wins
            provider.AssertSettingExists("sonar.sources", sharedFile);
        }

        // SONARMSBRU-336
        [TestMethod]
        public void FileGen_SharedFiles_BelongToAnotherProject()
        {
            // Shared files that belong to another project should NOT be attached to the root project

            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string project1Dir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "project1");
            string project1Path = Path.Combine(project1Dir, "project1.proj");
            Guid project1Guid = Guid.NewGuid();
            string project1Info = CreateProjectInfoInSubDir(testDir, "projectName1", project1Guid, ProjectType.Product, false, project1Path, "UTF-8"); // not excluded
            var fileInProject1 = Path.Combine(project1Dir, "contentFile.txt");
            CreateEmptyFile(project1Dir, "contentFile.txt");

            // Reference shared file, but not under the project directory
            string contentFileList1 = CreateFile(project1Dir, "contentList.txt", fileInProject1);
            AddAnalysisResult(project1Info, AnalysisType.FilesToAnalyze, contentFileList1);

            string project2Dir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "project2");
            string project2Path = Path.Combine(project2Dir, "project2.proj");
            string project2Info = CreateProjectInfoInSubDir(testDir, "projectName2", Guid.NewGuid(), ProjectType.Product, false, project2Path, "UTF-8"); // not excluded

            // Reference shared file, but not under the project directory
            string contentFileList2 = CreateFile(project2Dir, "contentList.txt", fileInProject1);
            AddAnalysisResult(project2Info, AnalysisType.FilesToAnalyze, contentFileList2);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = CreateValidConfig(testDir);

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists("sonar.projectBaseDir", testDir);
            provider.AssertSettingDoesNotExist("sonar.sources");
            provider.AssertSettingExists(project1Guid.ToString().ToUpper() + ".sonar.sources", fileInProject1);
        }

        [TestMethod] //https://jira.codehaus.org/browse/SONARMSBRU-13: Analysis fails if a content file referenced in the MSBuild project does not exist
        public void FileGen_MissingFilesAreSkipped()
        {
            // Create project info with a managed file list and a content file list.
            // Each list refers to a file that does not exist on disk.
            // The missing files should not appear in the generated properties file.

            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string projectBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "Project1");
            string projectFullPath = CreateEmptyFile(projectBaseDir, "project1.proj");


            string existingManagedFile = CreateEmptyFile(projectBaseDir, "File1.cs");
            string existingContentFile = CreateEmptyFile(projectBaseDir, "Content1.txt");

            string missingManagedFile = Path.Combine(projectBaseDir, "MissingFile1.cs");
            string missingContentFile = Path.Combine(projectBaseDir, "MissingContent1.txt");

            ProjectInfo projectInfo = new ProjectInfo()
            {
                FullPath = projectFullPath,
                AnalysisResults = new List<AnalysisResult>(),
                IsExcluded = false,
                ProjectGuid = Guid.NewGuid(),
                ProjectName = "project1.proj",
                ProjectType = ProjectType.Product,
                Encoding = "UTF-8"
            };

            string analysisFileList = CreateFileList(projectBaseDir, "filesToAnalyze.txt", existingManagedFile, missingManagedFile, existingContentFile, missingContentFile);
            projectInfo.AddAnalyzerResult(AnalysisType.FilesToAnalyze, analysisFileList);

            string projectInfoDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "ProjectInfo1Dir");
            string projectInfoFilePath = Path.Combine(projectInfoDir, FileConstants.ProjectInfoFileName);
            projectInfo.Save(projectInfoFilePath);

            TestLogger logger = new TestLogger();
            AnalysisConfig config = new AnalysisConfig()
            {
                SonarProjectKey = "my_project_key",
                SonarProjectName = "my_project_name",
                SonarProjectVersion = "1.0",
                SonarOutputDir = testDir
            };

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            string actual = File.ReadAllText(result.FullPropertiesFilePath);

            // Assert
            AssertFileIsReferenced(existingContentFile, actual);
            AssertFileIsReferenced(existingManagedFile, actual);

            AssertFileIsNotReferenced(missingContentFile, actual);
            AssertFileIsNotReferenced(missingManagedFile, actual);

            logger.AssertSingleWarningExists(missingManagedFile);
            logger.AssertSingleWarningExists(missingContentFile);
        }

        [TestMethod]
        [Description("Checks that the generated properties file contains additional properties")]
        public void FileGen_AdditionalProperties()
        {
            // 0. Arrange
            string analysisRootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            CreateProjectWithFiles("project1", analysisRootDir);
            AnalysisConfig config = CreateValidConfig(analysisRootDir);

            // Add additional properties
            config.LocalSettings = new AnalysisProperties
            {
                new Property() { Id = "key1", Value = "value1" },
                new Property() { Id = "key.2", Value = "value two" },
                new Property() { Id = "key.3", Value = " " },

                // Sensitive data should not be written
                new Property() { Id = SonarProperties.DbPassword, Value = "secret db pwd" },
                new Property() { Id = SonarProperties.SonarPassword, Value = "secret pwd" }
            };

            // Server properties should not be added
            config.ServerSettings = new AnalysisProperties
            {
                new Property() { Id = "server.key", Value = "should not be added" }
            };

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            AssertExpectedProjectCount(1, result);

            // One valid project info file -> file created
            AssertPropertiesFilesCreated(result, logger);

            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists("key1", "value1");
            provider.AssertSettingExists("key.2", "value two");
            provider.AssertSettingExists("key.3", "");

            provider.AssertSettingDoesNotExist("server.key");

            provider.AssertSettingDoesNotExist(SonarProperties.DbPassword);
            provider.AssertSettingDoesNotExist(SonarProperties.SonarPassword);
        }

        [TestMethod] // Old VS Bootstrapper should be forceably disabled: https://jira.sonarsource.com/browse/SONARMSBRU-122
        public void FileGen_VSBootstrapperIsDisabled()
        {
            // 0. Arrange
            TestLogger logger = new TestLogger();

            // Act
            ProjectInfoAnalysisResult result = ExecuteAndCheckSucceeds("disableBootstrapper", logger);

            // Assert
            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(PropertiesFileGenerator.VSBootstrapperPropertyKey, "false");
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void FileGen_VSBootstrapperIsDisabled_OverrideUserSettings_DifferentValue()
        {
            // 0. Arrange
            TestLogger logger = new TestLogger();

            // Try to explicitly enable the setting
            Property bootstrapperProperty = new Property() { Id = PropertiesFileGenerator.VSBootstrapperPropertyKey, Value = "true" };

            // Act
            ProjectInfoAnalysisResult result = ExecuteAndCheckSucceeds("disableBootstrapperDiff", logger, bootstrapperProperty);

            // Assert
            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(PropertiesFileGenerator.VSBootstrapperPropertyKey, "false");
            logger.AssertSingleWarningExists(PropertiesFileGenerator.VSBootstrapperPropertyKey);
        }

        [TestMethod]
        public void FileGen_VSBootstrapperIsDisabled_OverrideUserSettings_SameValue()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            Property bootstrapperProperty = new Property() { Id = PropertiesFileGenerator.VSBootstrapperPropertyKey, Value = "false" };

            // Act
            ProjectInfoAnalysisResult result = ExecuteAndCheckSucceeds("disableBootstrapperSame", logger, bootstrapperProperty);

            // Assert
            SQPropertiesFileReader provider = new SQPropertiesFileReader(result.FullPropertiesFilePath);
            provider.AssertSettingExists(PropertiesFileGenerator.VSBootstrapperPropertyKey, "false");
            logger.AssertSingleDebugMessageExists(PropertiesFileGenerator.VSBootstrapperPropertyKey);
            logger.AssertWarningsLogged(0); // not expecting a warning if the user has supplied the value we want
        }

        [TestMethod]
        public void FileGen_ComputeProjectBaseDir()
        {
            VerifyProjectBaseDir(
                expectedValue: @"d:\work\mysources", // if there is a user value, use it
                teamBuildValue: @"d:\work",
                userValue: @"d:\work\mysources",
                projectPaths: new[] { @"d:\work\proj1.csproj" });

            VerifyProjectBaseDir(
              expectedValue: @"d:\work",  // if no user value, use the team build value
              teamBuildValue: @"d:\work",
              userValue: null,
              projectPaths: new[] { @"e:\work\proj1.csproj" });

            VerifyProjectBaseDir(
               expectedValue: @"e:\work",  // if no team build value, use the common project paths root
               teamBuildValue: null,
               userValue: "",
               projectPaths: new[] { @"e:\work\proj1.csproj" });

            VerifyProjectBaseDir(
              expectedValue: @"e:\work",  // if no team build value, use the common project paths root
              teamBuildValue: null,
              userValue: "",
              projectPaths: new[] { @"e:\work\proj1.csproj", @"e:\work\proj2.csproj" });

            VerifyProjectBaseDir(
              expectedValue: @"e:\work",  // if no team build value, use the common project paths root
              teamBuildValue: null,
              userValue: "",
              projectPaths: new[] { @"e:\work\A\proj1.csproj", @"e:\work\B\C\proj2.csproj" });


            VerifyProjectBaseDir(
              expectedValue: @"e:\work",  // if no team build value, use the common project paths root
              teamBuildValue: null,
              userValue: "",
              projectPaths: new[] { @"e:\work\A\proj1.csproj", @"e:\work\B\proj2.csproj", @"e:\work\C\proj2.csproj" });

            VerifyProjectBaseDir(
              expectedValue: @"e:\work\A",  // if no team build value, use the common project paths root
              teamBuildValue: null,
              userValue: "",
              projectPaths: new[] { @"e:\work\A\X\proj1.csproj", @"e:\work\A\proj2.csproj", @"e:\work\A\proj2.csproj" });

            VerifyProjectBaseDir(
              expectedValue: TestSonarqubeOutputDir,  // if no common root exists, use the .sonarqube/out dir
              teamBuildValue: null,
              userValue: "",
              projectPaths: new[] { @"f:\work\A\proj1.csproj", @"e:\work\B\proj2.csproj" });
        }

        #endregion

        #region Assertions

        /// <summary>
        /// Creates a single new project valid project with dummy files and analysis config file with the specified local settings.
        /// Checks that a property file is created.
        /// </summary>
        private ProjectInfoAnalysisResult ExecuteAndCheckSucceeds(string projectName, TestLogger logger, params Property[] localSettings)
        {
            string analysisRootDir = TestUtils.CreateTestSpecificFolder(this.TestContext, projectName);

            CreateProjectWithFiles(projectName, analysisRootDir);
            AnalysisConfig config = CreateValidConfig(analysisRootDir);

            config.LocalSettings = new AnalysisProperties();
            foreach (Property property in localSettings)
            {
                config.LocalSettings.Add(property);
            }

            // Act
            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);

            // Assert
            AssertExpectedProjectCount(1, result);
            AssertPropertiesFilesCreated(result, logger);

            return result;
        }

        private static void AssertFailedToCreatePropertiesFiles(ProjectInfoAnalysisResult result, TestLogger logger)
        {
            Assert.IsNull(result.FullPropertiesFilePath, "Not expecting the sonar-scanner properties file to have been set");
            Assert.AreEqual(false, result.RanToCompletion, "Expecting the property file generation to have failed");

            AssertNoValidProjects(result);

            logger.AssertErrorsLogged();
        }

        private void AssertPropertiesFilesCreated(ProjectInfoAnalysisResult result, TestLogger logger)
        {
            Assert.IsNotNull(result.FullPropertiesFilePath, "Expecting the sonar-scanner properties file to have been set");

            AssertValidProjectsExist(result);
            this.TestContext.AddResultFile(result.FullPropertiesFilePath);

            logger.AssertErrorsLogged(0);
        }

        private static void AssertExpectedStatus(string expectedProjectName, ProjectInfoValidity expectedStatus, ProjectInfoAnalysisResult actual)
        {
            IEnumerable<ProjectInfo> matches = actual.GetProjectsByStatus(expectedStatus).Where(p => p.ProjectName.Equals(expectedProjectName));
            Assert.IsFalse(matches.Count() > 2, "ProjectName is reported more than once: {0}", expectedProjectName);
            Assert.AreEqual(1, matches.Count(), "ProjectInfo was not classified as expected. Project name: {0}, expected status: {1}", expectedProjectName, expectedStatus);
        }

        private static void AssertNoValidProjects(ProjectInfoAnalysisResult actual)
        {
            IEnumerable<ProjectInfo> matches = actual.GetProjectsByStatus(ProjectInfoValidity.Valid);
            Assert.AreEqual(0, matches.Count(), "Not expecting to find any valid ProjectInfo files");
        }

        private static void AssertValidProjectsExist(ProjectInfoAnalysisResult actual)
        {
            IEnumerable<ProjectInfo> matches = actual.GetProjectsByStatus(ProjectInfoValidity.Valid);
            Assert.AreNotEqual(0, matches.Count(), "Expecting at least one valid ProjectInfo file to exist");
        }

        private static void AssertExpectedProjectCount(int expected, ProjectInfoAnalysisResult actual)
        {
            Assert.AreEqual(expected, actual.Projects.Count, "Unexpected number of projects in the result");
        }

        private static void AssertFileIsReferenced(string fullFilePath, string content)
        {
            string formattedPath = PropertiesWriter.Escape(fullFilePath);
            Assert.IsTrue(content.Contains(formattedPath), "Files should be referenced: {0}", formattedPath);
        }

        private static void AssertFileIsNotReferenced(string fullFilePath, string content)
        {
            string formattedPath = PropertiesWriter.Escape(fullFilePath);
            Assert.IsFalse(content.Contains(formattedPath), "File should not be referenced: {0}", formattedPath);
        }

        #endregion

        #region Private methods

        private void VerifyProjectBaseDir(string expectedValue, string teamBuildValue, string userValue, string[] projectPaths)
        {
            AnalysisConfig config = new AnalysisConfig();
            PropertiesWriter writer = new PropertiesWriter(config);
            config.SonarOutputDir = TestSonarqubeOutputDir;

            config.SourcesDirectory = teamBuildValue;
            config.SetConfigValue(SonarProperties.ProjectBaseDir, userValue);

            TestLogger logger = new TestLogger();

            // Act
            string result = PropertiesFileGenerator.ComputeRootProjectBaseDir(config, projectPaths.Select(p => new ProjectInfo { FullPath = p, ProjectLanguage = ProjectLanguages.CSharp }));

            result.Should().Be(expectedValue);
        }

        /// <summary>
        /// Creates a project info under the specified analysis root directory
        /// together with the supporting project and content files, along with additional properties (if specified)
        /// </summary>
        private void CreateProjectWithFiles(string projectName, string analysisRootPath, bool createContentFiles = true, AnalysisProperties additionalProperties = null)
        {
            CreateProjectWithFiles(projectName, analysisRootPath, Guid.NewGuid(), createContentFiles, additionalProperties);
        }

        /// <summary>
        /// Creates a project info under the specified analysis root directory
        /// together with the supporting project and content files, along with GUID and additional properties (if specified)
        /// </summary>
        private void CreateProjectWithFiles(string projectName, string analysisRootPath, Guid projectGuid, bool createContentFiles = true, AnalysisProperties additionalProperties = null)
        {
            // Create a project with content files in a new subdirectory
            string projectDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, Path.Combine("projects", projectName));
            string projectFilePath = Path.Combine(projectDir, Path.ChangeExtension(projectName, "proj"));

            // Create a project info file in the correct location under the analysis root
            string contentProjectInfo = CreateProjectInfoInSubDir(analysisRootPath, projectName, projectGuid, ProjectType.Product, false, projectFilePath, "UTF-8", additionalProperties); // not excluded

            // Create content / managed files if required
            if (createContentFiles)
            {
                string contentFile = CreateEmptyFile(projectDir, "contentFile1.txt");
                string contentFileList = CreateFile(projectDir, "contentList.txt", contentFile);
                AddAnalysisResult(contentProjectInfo, AnalysisType.FilesToAnalyze, contentFileList);
            }
        }

        private static AnalysisConfig CreateValidConfig(string outputDir)
        {
            string dummyProjectKey = Guid.NewGuid().ToString();

            AnalysisConfig config = new AnalysisConfig()
            {
                SonarOutputDir = outputDir,

                SonarProjectKey = dummyProjectKey,
                SonarProjectName = dummyProjectKey,
                SonarConfigDir = Path.Combine(outputDir, "config"),
                SonarProjectVersion = "1.0"
            };

            return config;
        }

        private static string CreateEmptyFile(string parentDir, string fileName)
        {
            return CreateFile(parentDir, fileName, string.Empty);
        }

        private static string CreateFile(string parentDir, string fileName, string content)
        {
            string fullPath = Path.Combine(parentDir, fileName);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        /// <summary>
        /// Creates a new project info file in a new subdirectory with the given additional properties.
        /// </summary>
        private static string CreateProjectInfoInSubDir(string parentDir,
            string projectName, Guid projectGuid, ProjectType projectType, bool isExcluded, string fullProjectPath, string encoding,
            AnalysisProperties additionalProperties = null)
        {
            string newDir = Path.Combine(parentDir, projectName);
            Directory.CreateDirectory(newDir); // ensure the directory exists

            ProjectInfo project = new ProjectInfo()
            {
                FullPath = fullProjectPath,
                ProjectName = projectName,
                ProjectGuid = projectGuid,
                ProjectType = projectType,
                IsExcluded = isExcluded,
                Encoding = encoding
            };

            if (additionalProperties != null)
            {
                project.AnalysisSettings = additionalProperties;
            }

            string filePath = Path.Combine(newDir, FileConstants.ProjectInfoFileName);
            project.Save(filePath);
            return filePath;
        }

        private static void AddAnalysisResult(string projectInfoFile, AnalysisType resultType, string location)
        {
            ProjectInfo projectInfo = ProjectInfo.Load(projectInfoFile);
            projectInfo.AddAnalyzerResult(resultType, location);
            projectInfo.Save(projectInfoFile);
        }

        private static string CreateFileList(string parentDir, string fileName, params string[] files)
        {
            string fullPath = Path.Combine(parentDir, fileName);
            File.WriteAllLines(fullPath, files);
            return fullPath;
        }

        #endregion
    }
}