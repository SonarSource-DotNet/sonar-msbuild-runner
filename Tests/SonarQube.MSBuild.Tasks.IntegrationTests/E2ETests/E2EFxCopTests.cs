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

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.IO;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.IntegrationTests.E2E
{
    [TestClass]
    public class E2EFxCopTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [Description("SONARMSBRU-193 RunCodeAnalysisOnce should not be set on C++ projects due to unwanted side-effects")]
        public void E2E_FxCop_NotExecutedOnCppProjects()
        {
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder;
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.SonarQubeConfigPath = rootInputFolder;

            preImportProperties["Language"] = "C++";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);
            BuildAssertions.AssertPropertyDoesNotExist(result.ProjectStateAfterBuild, "RunCodeAnalysisOnce");

            AssertFxCopNotExecuted(logger);
        }

        [TestMethod]
        [Description("When FxCop rules are enabled, but FxCop is not installed (only MSBuild is), analysis should fail")]
        public void E2E_FxCop_FailIfEnabledButNotInstalled()
        {
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder;
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.SonarQubeConfigPath = rootInputFolder;

            // FxCop's targets is imported when Visual Studio is installed, see Microsoft.Common.CurrentVersion.targets
            preImportProperties["CodeAnalysisTargets"] = Path.Combine(rootInputFolder, "non-existing.targets");

            CreateValidFxCopRuleset(rootInputFolder, string.Format(TargetProperties.SonarQubeRulesetFormat, "cs"));

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetFailed(result, TargetConstants.DefaultBuildTarget);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.DetectFxCopRulesetTarget,
                TargetConstants.FailIfFxCopNotInstalledTarget);

            AssertFxCopNotExecuted(logger);
        }

        [TestMethod]
        [Description("If the output folder is not set our custom targets should not be executed")]
        public void E2E_FxCop_OutputFolderNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            // Don't set the output folder
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();

            // Clear all of the variables that could be used to look up the output path
            // (necessary so the test works correctly when run under TeamBuild on a machine
            // that has the integration targets installed)
            preImportProperties.SonarQubeOutputPath = "";
            preImportProperties.TeamBuild2105BuildDirectory = "";
            preImportProperties.TeamBuildLegacyBuildDirectory = "";

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // 1. No code analysis properties
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertTargetNotExecuted(TargetConstants.OverrideFxCopSettingsTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SetFxCopResultsTarget);

            AssertFxCopNotExecuted(logger);

            ProjectInfoAssertions.AssertNoProjectInfoFilesExists(rootOutputFolder);
        }

        [TestMethod]
        [Description("If the temp folder is not set our custom targets should not be executed")]
        public void E2E_FxCop_TempFolderIsNotSet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // 1. No code analysis properties
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertTargetNotExecuted(TargetConstants.OverrideFxCopSettingsTarget);
            logger.AssertTargetNotExecuted(TargetConstants.SetFxCopResultsTarget);

            AssertFxCopNotExecuted(logger);

            ProjectInfoAssertions.AssertNoProjectInfoFilesExists(rootOutputFolder);
        }

        [TestMethod]
        [Description("FxCop analysis should not be run if the output folder is set but a custom ruleset isn't specified")]
        public void E2E_FxCop_OutputFolderSet_SonarRulesetNotSpecified()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            // Set the output folder but not the config folder
            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder; // FIXME
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.SonarQubeConfigPath = rootInputFolder;
            preImportProperties.RunCodeAnalysis = "TRUE";
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertTargetExecuted(TargetConstants.OverrideFxCopSettingsTarget); // output folder is set so this should be executed
            logger.AssertTargetNotExecuted(TargetConstants.SetFxCopResultsTarget);

            AssertFxCopNotExecuted(logger);
            Assert.IsFalse(File.Exists(fxCopLogFile), "FxCop log file should not have been produced");

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultDoesNotExists(projectInfo, AnalysisType.FxCop.ToString());
        }

        [TestMethod]
        [Description("FxCop analysis should not be run if the output folder is set but the custom ruleset couldn't be found")]
        public void E2E_FxCop_OutputFolderSet_SonarRulesetNotFound()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            // Set the output folder and config path
            // Don't create a ruleset file on disc
            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder; // FIXME
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.RunCodeAnalysis = "true"; // our targets should override this value
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            preImportProperties.SonarQubeConfigPath = rootInputFolder;
            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertTargetExecuted(TargetConstants.OverrideFxCopSettingsTarget);  // output folder is set so this should be executed
            logger.AssertTargetNotExecuted(TargetConstants.SetFxCopResultsTarget);

            // We expect the core FxCop *target* to have been started, but it should then be skipped
            // executing the FxCop *task* because the condition on the target is false
            // -> the FxCop output file should not be produced
            AssertFxCopNotExecuted(logger);

            Assert.IsFalse(File.Exists(fxCopLogFile), "FxCop log file should not have been produced");

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultDoesNotExists(projectInfo, AnalysisType.FxCop.ToString());
        }

        [TestMethod]
        [Description("FxCop analysis should be run if the output folder is set and the ruleset can be found")]
        public void E2E_FxCop_AllConditionsMet()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder; // FIXME
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.RunCodeAnalysis = "false";
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            preImportProperties.CodeAnalysisRuleset = "specifiedInProject.ruleset";

            // we expect these values to be overridden
            preImportProperties.TreatWarningsAsErrors = "true";
            preImportProperties.WarningsAsErrors = "CS0111;CS0222";
            preImportProperties.WarningLevel = "3";

            preImportProperties[TargetProperties.SonarQubeConfigPath] = rootInputFolder;
            CreateValidFxCopRuleset(rootInputFolder, string.Format(TargetProperties.SonarQubeRulesetFormat, "cs"));

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            string itemPath = TestUtils.CreateTextFile(rootInputFolder, "my.cs", "class myclass{}");
            projectRoot.AddItem(TargetProperties.ItemType_Compile, itemPath);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.DetectFxCopRulesetTarget,
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.OverrideFxCopSettingsTarget,
                TargetConstants.FxCopTarget,
                TargetConstants.SetFxCopResultsTarget,
                TargetConstants.DefaultBuildTarget);

            AssertAllFxCopTargetsExecuted(logger);
            Assert.IsTrue(File.Exists(fxCopLogFile), "FxCop log file should have been produced");
            BuildAssertions.AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultExists(projectInfo, AnalysisType.FxCop.ToString(), fxCopLogFile);
        }

        [TestMethod]
        [TestCategory("VB")]
        [Description("FxCop analysis should be run on VB projects if the output folder is set and the ruleset can be found")]
        public void E2E_FxCop_AllConditionsMet_VB()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder; // FIXME
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.RunCodeAnalysis = "false";
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            preImportProperties.CodeAnalysisRuleset = "specifiedInProject.ruleset";

            preImportProperties.TreatWarningsAsErrors = "true"; // we expect these values to be overridden
            preImportProperties.WarningsAsErrors = "VB0111;VB0222";
            preImportProperties.WarningLevel = "0";

            preImportProperties[TargetProperties.SonarQubeConfigPath] = rootInputFolder;
            CreateValidFxCopRuleset(rootInputFolder, string.Format(TargetProperties.SonarQubeRulesetFormat, "vbnet"));

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties, isVBProject: true);

            string itemPath = TestUtils.CreateTextFile(rootInputFolder, "my.vb",
@"Public Class Class1

  Public Sub DoStuff()
  End Sub

End Class");

            projectRoot.AddItem(TargetProperties.ItemType_Compile, itemPath);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.DetectFxCopRulesetTarget,
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.OverrideFxCopSettingsTarget,
                TargetConstants.FxCopTarget,
                TargetConstants.SetFxCopResultsTarget,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            AssertAllFxCopTargetsExecuted(logger);
            Assert.IsTrue(File.Exists(fxCopLogFile), "FxCop log file should have been produced");
            BuildAssertions.AssertWarningsAreNotTreatedAsErrorsNorIgnored(result);

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultExists(projectInfo, AnalysisType.FxCop.ToString(), fxCopLogFile);
        }

        [TestMethod]
        [Description("The FxCop analysis result should not exist if there are not files to analyse")]
        public void E2E_FxCop_NoFilesToAnalyse()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder; // FIXME
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.RunCodeAnalysis = "false";
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            preImportProperties.CodeAnalysisRuleset = "specifiedInProject.ruleset";

            preImportProperties[TargetProperties.SonarQubeConfigPath] = rootInputFolder;
            CreateValidFxCopRuleset(rootInputFolder, string.Format(TargetProperties.SonarQubeRulesetFormat, "cs"));

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Add some files to the project
            string itemPath = TestUtils.CreateTextFile(rootInputFolder, "content1.txt", "aaaa");
            projectRoot.AddItem("Content", itemPath);

            itemPath = TestUtils.CreateTextFile(rootInputFolder, "code1.cs", "class myclassXXX{} // wrong item type");
            projectRoot.AddItem("CompileXXX", itemPath);

            projectRoot.Save();

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.OverrideFxCopSettingsTarget,

                TargetConstants.FxCopTarget,
                TargetConstants.SetFxCopResultsTarget, // should set the FxCop results after the platform "run Fx Cop" target

                TargetConstants.DefaultBuildTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            logger.AssertTargetExecuted(TargetConstants.SetFxCopResultsTarget);

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultDoesNotExists(projectInfo, AnalysisType.FxCop.ToString());
        }

        [TestMethod]
        [Description("FxCop analysis should not be run if the project is excluded")]
        public void E2E_FxCop_ExcludedProject()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder; // FIXME
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            preImportProperties.CodeAnalysisRuleset = "specifiedInProject.ruleset";

            preImportProperties.SonarQubeExclude = "true";

            preImportProperties[TargetProperties.SonarQubeConfigPath] = rootInputFolder;
            CreateValidFxCopRuleset(rootInputFolder, string.Format(TargetProperties.SonarQubeRulesetFormat, "cs"));

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Add a file to the project
            string itemPath = TestUtils.CreateTextFile(rootInputFolder, "code1.cs", "class myclassXXX{}");
            projectRoot.AddItem(TargetProperties.ItemType_Compile, itemPath);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.OverrideFxCopSettingsTarget,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.WriteProjectDataTarget);

            logger.AssertTargetExecuted(TargetConstants.FxCopTarget);
            logger.AssertTaskNotExecuted(TargetConstants.FxCopTask);
            logger.AssertTargetNotExecuted(TargetConstants.SetFxCopResultsTarget);

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultDoesNotExists(projectInfo, AnalysisType.FxCop.ToString());
        }

        [TestMethod] // SONARMSBRU-20: Do not run FxCop (nor other rule engines) on test projects
        [Description("FxCop analysis should not be run if the project is a test project")]
        public void E2E_FxCop_TestProject_Explicit()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder; // FIXME
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            preImportProperties.CodeAnalysisRuleset = "specifiedInProject.ruleset";

            preImportProperties.SonarQubeExclude = "false";
            preImportProperties.SonarTestProject = "true";

            preImportProperties[TargetProperties.SonarQubeConfigPath] = rootInputFolder;
            CreateValidFxCopRuleset(rootInputFolder, string.Format(TargetProperties.SonarQubeRulesetFormat, "cs"));

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Add a file to the project
            string itemPath = TestUtils.CreateTextFile(rootInputFolder, "code1.cs", "class myclassXXX{}");
            projectRoot.AddItem(TargetProperties.ItemType_Compile, itemPath);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget, // should always be run so we can detect Fakes
                TargetConstants.OverrideFxCopSettingsTarget,
                TargetConstants.FxCopTarget,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            logger.AssertTargetExecuted(TargetConstants.FxCopTarget);
            logger.AssertTaskNotExecuted(TargetConstants.FxCopTask);
            logger.AssertTargetNotExecuted(TargetConstants.SetFxCopResultsTarget);

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultDoesNotExists(projectInfo, AnalysisType.FxCop.ToString());
        }

        [TestMethod] // SONARMSBRU-20: Do not run FxCop (nor other rule engines) on test projects
        [Description("FxCop analysis should not be run if the project is a test project")]
        public void E2E_FxCop_TestProject_TestProjectTypeGuid()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder; // FIXME
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            preImportProperties.CodeAnalysisRuleset = "specifiedInProject.ruleset";

            preImportProperties.ProjectTypeGuids = "X;" + TargetConstants.MsTestProjectTypeGuid.ToUpperInvariant() + ";Y";

            preImportProperties[TargetProperties.SonarQubeConfigPath] = rootInputFolder;
            CreateValidFxCopRuleset(rootInputFolder, string.Format(TargetProperties.SonarQubeRulesetFormat, "cs"));

            ProjectRootElement projectRoot = BuildUtilities.CreateValidProjectRoot(this.TestContext, rootInputFolder, preImportProperties);

            // Add a file to the project
            string itemPath = TestUtils.CreateTextFile(rootInputFolder, "code1.cs", "class myclassXXX{}");
            projectRoot.AddItem(TargetProperties.ItemType_Compile, itemPath);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.OverrideFxCopSettingsTarget,
                TargetConstants.FxCopTarget,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            // We expect the FxCop target to be executed...
            logger.AssertTargetExecuted(TargetConstants.FxCopTarget);
            // ... but we don't expect the FxCop *task* to have executed
            logger.AssertTaskNotExecuted(TargetConstants.FxCopTask);

            logger.AssertTargetNotExecuted(TargetConstants.SetFxCopResultsTarget);

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultDoesNotExists(projectInfo, AnalysisType.FxCop.ToString());
        }

        [TestMethod] // SONARMSBRU-20: Do not run FxCop (nor other rule engines) on test projects
        [Description("FxCop analysis should not be run if the project is a test project")]
        public void E2E_FxCop_TestProject_TestProjectByName()
        {
            // Arrange
            string rootInputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Inputs");
            string rootOutputFolder = TestUtils.CreateTestSpecificFolder(this.TestContext, "Outputs");

            string fxCopLogFile = Path.Combine(rootInputFolder, "FxCopResults.xml");
            WellKnownProjectProperties preImportProperties = new WellKnownProjectProperties();
            preImportProperties.SonarQubeTempPath = rootOutputFolder; // FIXME
            preImportProperties.SonarQubeOutputPath = rootOutputFolder;
            preImportProperties.CodeAnalysisLogFile = fxCopLogFile;
            preImportProperties.CodeAnalysisRuleset = "specifiedInProject.ruleset";

            preImportProperties.SonarQubeConfigPath = rootInputFolder;

            preImportProperties[TargetProperties.SonarQubeConfigPath] = rootInputFolder;
            CreateValidFxCopRuleset(rootInputFolder, string.Format(TargetProperties.SonarQubeRulesetFormat, "cs"));

            // Create a config file in the config folder containing a reg ex to identify tests projects
            AnalysisConfig config = new AnalysisConfig();
            config.LocalSettings = new AnalysisProperties();
            config.LocalSettings.Add(new Property() { Id = IsTestFileByName.TestRegExSettingId, Value = ".testp." });

            string configFullPath = Path.Combine(rootInputFolder, FileConstants.ConfigFileName);
            config.Save(configFullPath);

            // Create a project with a file name that will match the reg ex
            ProjectDescriptor descriptor = BuildUtilities.CreateValidProjectDescriptor(rootInputFolder, "MyTestProject.proj");
            ProjectRootElement projectRoot = BuildUtilities.CreateInitializedProjectRoot(this.TestContext, descriptor, preImportProperties);


            // Add a file to the project
            string itemPath = TestUtils.CreateTextFile(rootInputFolder, "code1.cs", "class myclassXXX{}");
            projectRoot.AddItem(TargetProperties.ItemType_Compile, itemPath);
            projectRoot.Save();

            BuildLogger logger = new BuildLogger();

            // Act
            BuildResult result = BuildUtilities.BuildTargets(projectRoot, logger);

            // Assert
            BuildAssertions.AssertTargetSucceeded(result, TargetConstants.DefaultBuildTarget);

            logger.AssertExpectedTargetOrdering(
                TargetConstants.CategoriseProjectTarget,
                TargetConstants.OverrideFxCopSettingsTarget,
                TargetConstants.FxCopTarget,
                TargetConstants.DefaultBuildTarget,
                TargetConstants.CalculateFilesToAnalyzeTarget,
                TargetConstants.WriteProjectDataTarget);

            // We expect the FxCop target to be executed...
            logger.AssertTargetExecuted(TargetConstants.FxCopTarget);
            // ... but we don't expect the FxCop *task* to have executed
            logger.AssertTaskNotExecuted(TargetConstants.FxCopTask);

            logger.AssertTargetNotExecuted(TargetConstants.SetFxCopResultsTarget);

            ProjectInfo projectInfo = ProjectInfoAssertions.AssertProjectInfoExists(rootOutputFolder, projectRoot.FullPath);
            ProjectInfoAssertions.AssertAnalysisResultDoesNotExists(projectInfo, AnalysisType.FxCop.ToString());
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Creates a valid FxCop ruleset in the specified location.
        /// The contents of the ruleset are not important for the tests; the only
        /// requirement is that it should allow the FxCop targets to execute correctly.
        /// </summary>
        private void CreateValidFxCopRuleset(string rootInputFolder, string fileName)
        {
            string fullPath = Path.Combine(rootInputFolder, fileName);

            string content = @"<?xml version='1.0' encoding='utf-8'?>
<RuleSet Name='Minimal ruleset' Description='Valid minimalruleset' ToolsVersion='12.0'>
  <Rules AnalyzerId='Microsoft.Analyzers.ManagedCodeAnalysis' RuleNamespace='Microsoft.Rules.Managed'>
    <Rule Id='CA1008' Action='Warning' />
  </Rules>
</RuleSet>";

            File.WriteAllText(fullPath, content);
            this.TestContext.AddResultFile(fullPath);
        }

        #endregion

        #region Assertions methods

        private void AssertAllFxCopTargetsExecuted(BuildLogger logger)
        {
            logger.AssertExpectedTargetOrdering(
                TargetConstants.OverrideFxCopSettingsTarget,
                TargetConstants.FxCopTarget,
                TargetConstants.SetFxCopResultsTarget);

            // If the SonarQube FxCop targets are executed then we expect the FxCop
            // target and task to be executed too
            AssertFxCopExecuted(logger);
        }

        private void AssertFxCopExecuted(BuildLogger logger)
        {
            logger.AssertTargetExecuted(TargetConstants.FxCopTarget);
            logger.AssertTaskExecuted(TargetConstants.FxCopTask);
        }

        private void AssertFxCopNotExecuted(BuildLogger logger)
        {
            // FxCop has a "RunCodeAnalysis" target and a "CodeAnalysis" task: the target executes the task.
            // We are interested in whether the task is executed or not as that is what will actually produce
            // the output file (it's possible that the target will be executed, but that it will decide
            // to skip the task because the required conditions are not met).
            logger.AssertTaskNotExecuted(TargetConstants.FxCopTask);
        }

        #endregion
    }
}
