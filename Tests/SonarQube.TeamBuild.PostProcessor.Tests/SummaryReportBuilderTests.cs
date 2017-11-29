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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PostProcessor;
using SonarScanner.Shim;
using TestUtilities;

namespace SonarQube.TeamBuild.PostProcessorTests
{
    [TestClass]
    public class SummaryReportBuilderTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void SummaryReport_NoProjects()
        {
            // Arrange
            var hostUrl = "http://mySonarQube:9000";
            var result = new ProjectInfoAnalysisResult { RanToCompletion = false };
            var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };

            // Act
            var summaryReportData = SummaryReportBuilder.CreateSummaryData(config, result);

            // Assert
            VerifySummaryReportData(summaryReportData, result, hostUrl, config);
            VerifySummaryProjectCounts(
                summaryReportData,
                expectedExcludedProjects: 0,
                expectedInvalidProjects: 0,
                expectedSkippedProjects: 0,
                expectedProductProjects: 0,
                expectedTestProjects: 0);
        }

        [TestMethod]
        public void SummaryReport_WithBranch()
        {
            // Arrange
            var hostUrl = "http://mySonarQube:9000";
            var result = new ProjectInfoAnalysisResult { RanToCompletion = false };
            var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };
            config.LocalSettings = new AnalysisProperties
            {
                new Property() { Id = SonarProperties.ProjectBranch, Value = "master" }
            };
            AddProjectInfoToResult(result, ProjectInfoValidity.Valid, type: ProjectType.Product, count: 4);

            // Act
            var summaryReportData = SummaryReportBuilder.CreateSummaryData(config, result);

            // Assert
            VerifySummaryReportData(summaryReportData, result, hostUrl, config);
            VerifySummaryProjectCounts(
                summaryReportData,
                expectedExcludedProjects: 0,
                expectedInvalidProjects: 0,
                expectedSkippedProjects: 0,
                expectedProductProjects: 4,
                expectedTestProjects: 0);
        }

        [TestMethod]
        public void SummaryReport_AllTypesOfProjects()
        {
            // Arrange
            var hostUrl = "http://mySonarQube:9000";
            var result = new ProjectInfoAnalysisResult() { RanToCompletion = true };
            var config = new AnalysisConfig() { SonarProjectKey = "", SonarQubeHostUrl = hostUrl };

            AddProjectInfoToResult(result, ProjectInfoValidity.ExcludeFlagSet, type: ProjectType.Product, count: 4);
            AddProjectInfoToResult(result, ProjectInfoValidity.ExcludeFlagSet, type: ProjectType.Test, count: 1);
            AddProjectInfoToResult(result, ProjectInfoValidity.InvalidGuid, type: ProjectType.Product, count: 7);
            AddProjectInfoToResult(result, ProjectInfoValidity.InvalidGuid, type: ProjectType.Test, count: 8);
            AddProjectInfoToResult(result, ProjectInfoValidity.NoFilesToAnalyze, count: 11);
            AddProjectInfoToResult(result, ProjectInfoValidity.Valid, type: ProjectType.Product, count: 13);
            AddProjectInfoToResult(result, ProjectInfoValidity.Valid, type: ProjectType.Test, count: 17);

            // Act
            var summaryReportData = SummaryReportBuilder.CreateSummaryData(config, result);

            // Assert
            VerifySummaryReportData(summaryReportData, result, hostUrl, config);
            VerifySummaryProjectCounts(
                summaryReportData,
                expectedExcludedProjects: 5, // ExcludeFlagSet
                expectedInvalidProjects: 15, // InvalidGuid, DuplicateGuid is not possible anymore
                expectedSkippedProjects: 11, // No files to analyse
                expectedProductProjects: 13,
                expectedTestProjects: 17);
        }

        [TestMethod]
        public void SummaryReport_ReportIsGenerated()
        {
            // Arrange
            var hostUrl = "http://mySonarQube:9000";
            var result = new ProjectInfoAnalysisResult();
            var config = new AnalysisConfig() { SonarProjectKey = "Foo", SonarQubeHostUrl = hostUrl };

            var settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(TestContext.DeploymentDirectory);
            config.SonarOutputDir = TestContext.TestDeploymentDir; // this will be cleaned up by VS when there are too many results
            var expectedReportPath = Path.Combine(TestContext.TestDeploymentDir, SummaryReportBuilder.SummaryMdFilename);

            // Act
            var builder = new SummaryReportBuilder();
            builder.GenerateReports(settings, config, result, new TestLogger());

            // Assert
            Assert.IsTrue(File.Exists(expectedReportPath) && (new FileInfo(expectedReportPath)).Length > 0, "The report file cannot be found or is empty");
        }

        private static void VerifySummaryReportData(
            SummaryReportBuilder.SummaryReportData summaryReportData,
            ProjectInfoAnalysisResult analysisResult,
            string expectedHostUrl,
            AnalysisConfig config)
        {
            string expectedUrl;

            config.GetAnalysisSettings(false).TryGetValue("sonar.branch", out string branch);

            if (string.IsNullOrEmpty(branch))
            {
                expectedUrl = string.Format(
                    SummaryReportBuilder.DashboardUrlFormat,
                    expectedHostUrl,
                    config.SonarProjectKey);
            }
            else
            {
                expectedUrl = string.Format(
                    SummaryReportBuilder.DashboardUrlFormatWithBranch,
                    expectedHostUrl,
                    config.SonarProjectKey,
                    branch);
            }

            Assert.AreEqual(expectedUrl, summaryReportData.DashboardUrl, "Invalid dashboard url");
            Assert.AreEqual(analysisResult.RanToCompletion, summaryReportData.Succeeded, "Invalid outcome");
        }

        private static void VerifySummaryProjectCounts(
            SummaryReportBuilder.SummaryReportData summaryReportData,
            int expectedInvalidProjects,
            int expectedProductProjects,
            int expectedSkippedProjects,
            int expectedTestProjects,
            int expectedExcludedProjects)
        {
            Assert.AreEqual(expectedInvalidProjects, summaryReportData.InvalidProjects);
            Assert.AreEqual(expectedProductProjects, summaryReportData.ProductProjects);
            Assert.AreEqual(expectedSkippedProjects, summaryReportData.SkippedProjects);
            Assert.AreEqual(expectedTestProjects, summaryReportData.TestProjects);
            Assert.AreEqual(expectedExcludedProjects, summaryReportData.ExcludedProjects);
        }

        private static void AddProjectInfoToResult(ProjectInfoAnalysisResult result, ProjectInfoValidity validity, ProjectType type = ProjectType.Product, uint count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                result.Projects.Add(new ProjectData(new ProjectInfo { ProjectType = type }) { Status = validity });
            }
        }
    }
}
