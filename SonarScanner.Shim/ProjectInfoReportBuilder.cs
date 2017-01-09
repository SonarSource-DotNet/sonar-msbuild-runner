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

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarScanner.Shim
{
    /// <summary>
    /// Outputs a report summarising the project info files that were found.
    /// This is not used by SonarQube: it is only for debugging purposes.
    /// </summary>
    internal class ProjectInfoReportBuilder
    {
        private const string ReportFileName = "ProjectInfo.log";

        private readonly AnalysisConfig config;
        private readonly ProjectInfoAnalysisResult analysisResult;
        private readonly ILogger logger;

        private readonly StringBuilder sb;

        #region Public methods

        public static void WriteSummaryReport(AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            ProjectInfoReportBuilder builder = new ProjectInfoReportBuilder(config, result, logger);
            builder.Generate();
        }

        #endregion

        #region Private methods

        private ProjectInfoReportBuilder(AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            this.config = config;
            this.analysisResult = result;
            this.logger = logger;
            this.sb = new StringBuilder();
        }

        private void Generate()
        {
            IEnumerable<ProjectInfo> validProjects = this.analysisResult.GetProjectsByStatus(ProjectInfoValidity.Valid);

            WriteTitle(Resources.REPORT_ProductProjectsTitle);
            WriteFileList(validProjects.Where(p => p.ProjectType == ProjectType.Product));
            WriteGroupSpacer();

            WriteTitle(Resources.REPORT_TestProjectsTitle);
            WriteFileList(validProjects.Where(p => p.ProjectType == ProjectType.Test));
            WriteGroupSpacer();

            WriteTitle(Resources.REPORT_InvalidProjectsTitle);
            WriteFilesByStatus(ProjectInfoValidity.DuplicateGuid);
            WriteFilesByStatus(ProjectInfoValidity.InvalidGuid);
            WriteGroupSpacer();

            WriteTitle(Resources.REPORT_SkippedProjectsTitle);
            WriteFilesByStatus(ProjectInfoValidity.NoFilesToAnalyze);
            WriteGroupSpacer();

            WriteTitle(Resources.REPORT_ExcludedProjectsTitle);
            WriteFilesByStatus(ProjectInfoValidity.ExcludeFlagSet);
            WriteGroupSpacer();

            string reportFileName = Path.Combine(config.SonarOutputDir, ReportFileName);
            logger.LogDebug(Resources.MSG_WritingSummary, reportFileName);
            File.WriteAllText(reportFileName, sb.ToString());
        }

        private void WriteTitle(string title)
        {
            this.sb.AppendLine(title);
            this.sb.AppendLine("---------------------------------------");
        }

        private void WriteGroupSpacer()
        {
            this.sb.AppendLine();
            this.sb.AppendLine();
        }

        private void WriteFilesByStatus(params ProjectInfoValidity[] statuses)
        {
            IEnumerable<ProjectInfo> projects = Enumerable.Empty<ProjectInfo>();

            foreach (ProjectInfoValidity status in statuses)
            {
                projects = projects.Concat(this.analysisResult.GetProjectsByStatus(status));
            }

            if (!projects.Any())
            {
                this.sb.AppendLine(Resources.REPORT_NoProjectsOfType);
            }
            else
            {
                WriteFileList(projects);
            }
        }

        private void WriteFileList(IEnumerable<ProjectInfo> projects)
        {
            foreach(ProjectInfo project in projects)
            {
                this.sb.AppendLine(project.FullPath);
            }
        }

        #endregion
    }
}
