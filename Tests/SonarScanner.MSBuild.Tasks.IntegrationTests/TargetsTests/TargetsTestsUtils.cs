﻿/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Tasks.IntegrationTests;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.IntegrationTests.TargetsTests
{
    public class TargetsTestsUtils
    {
        public TestContext TestContextInstance { get; set; }

        public TargetsTestsUtils(TestContext testContext)
        {
            TestContextInstance = testContext;
        }

        /// <summary>
        /// Creates a valid project with the necessary ruleset and assembly files on disc
        /// to successfully run the "OverrideRoslynCodeAnalysisProperties" target
        /// </summary>
        public string GetProjectTemplate(AnalysisConfig analysisConfig, string projectDirectory)
        {
            if (analysisConfig != null)
            {
                var configFilePath = Path.Combine(projectDirectory, FileConstants.ConfigFileName);
                analysisConfig.Save(configFilePath);
            }

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(TestContextInstance);
            File.Exists(sqTargetFile).Should().BeTrue("Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
            TestContextInstance.AddResultFile(sqTargetFile);

            return GetTemplateContent("SonarScanner.Integration.Tasks.IntegrationTests.Resources.TargetTestsProjectTemplate.xml");
        }

        public string GetImportBeforeTemplate(string importBeforeFilePath)
        {
            // Locate the real "ImportsBefore" target file
            File.Exists(importBeforeFilePath).Should().BeTrue("Test error: the SonarQube imports before target file does not exist. Path: {0}", importBeforeFilePath);
            return GetTemplateContent("SonarScanner.Integration.Tasks.IntegrationTests.Resources.ImportBeforeTargetTestsTemplate.xml");
        }

        public string GetTemplateContent(string resourceName)
        {
            using (var stream = typeof(TargetsTestsUtils).Assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public string CreateProjectFile(string projectDirectory, string projectData)
        {
            var projectFilePath = Path.Combine(projectDirectory, TestContextInstance.TestName + ".proj.txt");
            File.WriteAllText(projectFilePath, projectData);
            TestContextInstance.AddResultFile(projectFilePath);

            return projectFilePath;
        }

        public void CreateCaptureDataTargetsFile(string directory, string afterTargets)
        {
            // Most of the tests above want to check the value of build property
            // or item group after a target has been executed. However, this
            // information is not available through the buildlogger interface.
            // So, we'll add a special target that writes the properties/items
            // we are interested in to the message log.
            // The SimpleXmlLogger has special handling to extract the data
            // from the message and add it to the BuildLog.
            string xml = "";

            using (var stream = typeof(TargetsTestsUtils).Assembly.GetManifestResourceStream("SonarScanner.Integration.Tasks.IntegrationTests.Resources.CaptureDataTargetsFileTemplate.xml"))
            using (var reader = new StreamReader(stream))
            {
                xml = reader.ReadToEnd();
            }

            xml = string.Format(xml, afterTargets);

            // We're using :: as a separator here: replace it with whatever
            // whatever the logger is using as a separator
            xml = xml.Replace("::", SimpleXmlLogger.CapturedDataSeparator);

            var filePath = Path.Combine(directory, "Capture.targets");
            File.WriteAllText(filePath, xml);
            TestContextInstance.AddResultFile(filePath);
        }
    }
}
