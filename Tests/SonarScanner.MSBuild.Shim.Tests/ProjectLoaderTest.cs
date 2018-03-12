﻿/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Shim.Tests
{
    [TestClass]
    public class ProjectLoaderTest
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void ProjectLoader()
        {
            // Arrange
            var testSourcePath = TestUtils.CreateTestSpecificFolder(TestContext);

            // Create sub-directories, some with project info XML files and some without
            TestUtils.EnsureTestSpecificFolder(TestContext, "EmptyDir1");

            var validTestProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = testSourcePath,
                ProjectFolderName = "validTestProjectDir",
                ProjectFileName = "validTestProject.csproj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = true
            };
            validTestProject.AddCompileInputFile("TestFile1.cs", true);
            validTestProject.AddCompileInputFile("TestFile1.cs", true);
            validTestProject.AddContentFile("contentFile1.js", true);
            CreateFilesFromDescriptor(validTestProject, "testCompileListFile", "testVisualStudioCodeCoverageReport");

            TestUtils.EnsureTestSpecificFolder(TestContext, "EmptyDir2");

            var validNonTestProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = testSourcePath,
                ProjectFolderName = "validNonTestProjectDir",
                ProjectFileName = "validNonTestproject.proj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false
            };
            validNonTestProject.AddContentFile("ASourceFile.vb", true);
            validNonTestProject.AddContentFile("AnotherSourceFile.vb", true);
            CreateFilesFromDescriptor(validNonTestProject, "list.txt", "visualstudio-codecoverage.xml");

            var validNonTestNoReportsProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = testSourcePath,
                ProjectFolderName = "validNonTestNoReportsProjectDir",
                ProjectFileName = "validNonTestNoReportsProject.proj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false
            };
            validNonTestNoReportsProject.AddContentFile("SomeFile.cs", true);
            CreateFilesFromDescriptor(validNonTestNoReportsProject, "SomeList.txt", null);

            // Act
            IEnumerable<ProjectInfo> projects = SonarScanner.MSBuild.Shim.ProjectLoader.LoadFrom(testSourcePath);

            // Assert
            Assert.AreEqual(3, projects.Count());

            AssertProjectResultExists(validTestProject.ProjectName, projects);

            AssertProjectResultExists(validNonTestProject.ProjectName, projects);

            AssertProjectResultExists(validNonTestNoReportsProject.ProjectName, projects);
        }

        [TestMethod]
        [Description("Checks that the loader only looks in the top-level folder for project folders")]
        public void ProjectLoader_NonRecursive()
        {
            // 0. Setup
            var rootTestDir = Path.Combine(TestContext.DeploymentDirectory, "ProjectLoader_NonRecursive");
            var childDir = Path.Combine(rootTestDir, "Child1");

            // Create a valid project in the child directory
            var validNonTestProject = new ProjectDescriptor()
            {
                ParentDirectoryPath = childDir,
                ProjectFolderName = "validNonTestProjectDir",
                ProjectFileName = "validNonTestproject.proj",
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false
            };
            validNonTestProject.AddCompileInputFile("ASourceFile.vb", true);
            validNonTestProject.AddCompileInputFile("AnotherSourceFile.vb", true);
            CreateFilesFromDescriptor(validNonTestProject, "CompileList.txt", null);

            // 1. Run against the root dir -> not expecting the project to be found
            IEnumerable<ProjectInfo> projects = SonarScanner.MSBuild.Shim.ProjectLoader.LoadFrom(rootTestDir);
            Assert.AreEqual(0, projects.Count());

            // 2. Run against the child dir -> project should be found
            projects = SonarScanner.MSBuild.Shim.ProjectLoader.LoadFrom(childDir);
            Assert.AreEqual(1, projects.Count());
        }

        #endregion Tests

        #region Helper methods

        /// <summary>
        /// Creates a folder containing a ProjectInfo.xml and compiled file list as
        /// specified in the supplied descriptor
        /// </summary>
        private static void CreateFilesFromDescriptor(ProjectDescriptor descriptor, string compileFiles, string visualStudioCodeCoverageReportFileName)
        {
            if (!Directory.Exists(descriptor.FullDirectoryPath))
            {
                Directory.CreateDirectory(descriptor.FullDirectoryPath);
            }

            var projectInfo = descriptor.CreateProjectInfo();

            // Create the analysis file list if any input files have been specified
            if (descriptor.FilesToAnalyse.Any())
            {
                var fullAnalysisFileListPath = Path.Combine(descriptor.FullDirectoryPath, compileFiles);
                File.WriteAllLines(fullAnalysisFileListPath, descriptor.FilesToAnalyse);

                // Add the compile list as an analysis result
                projectInfo.AnalysisResults.Add(new AnalysisResult() { Id = AnalysisType.FilesToAnalyze.ToString(), Location = fullAnalysisFileListPath });
            }

            // Create the Visual Studio Code Coverage report file
            if (visualStudioCodeCoverageReportFileName != null)
            {
                var fullVisualStudioCodeCoverageName = Path.Combine(descriptor.FullDirectoryPath, visualStudioCodeCoverageReportFileName);
                File.Create(fullVisualStudioCodeCoverageName);

                // Add the Visual Studio Code Coverage report as an analysis result
                var analysisResult = new AnalysisResult() { Id = AnalysisType.VisualStudioCodeCoverage.ToString(), Location = fullVisualStudioCodeCoverageName };
                descriptor.AnalysisResults.Add(analysisResult);
                projectInfo.AnalysisResults.Add(analysisResult);
            }

            // Save a project info file in the target directory
            projectInfo.Save(Path.Combine(descriptor.FullDirectoryPath, FileConstants.ProjectInfoFileName));
        }

        #endregion Helper methods

        #region Assertions

        private static ProjectInfo AssertProjectResultExists(string expectedProjectName, IEnumerable<ProjectInfo> actualProjects)
        {
            var actual = actualProjects.FirstOrDefault(p => expectedProjectName.Equals(p.ProjectName));
            Assert.IsNotNull(actual, "Failed to find project with the expected name: {0}", expectedProjectName);
            return actual;
        }

        #endregion Assertions
    }
}