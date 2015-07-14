﻿//-----------------------------------------------------------------------
// <copyright file="ProjectDescriptor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace TestUtilities
{
    /// <summary>
    /// Describes the expected contents of a single project
    /// </summary>
    /// <remarks>Used to dynamically create folders and files for tests,
    /// and to check actual results against</remarks>
    public class ProjectDescriptor
    {
        public ProjectDescriptor()
        {
            this.AnalysisResults = new List<AnalysisResult>();
        }

        #region Public properties

        public string ProjectLanguage { get; set; }

        public Guid ProjectGuid { get; set; }

        public IList<string> ManagedSourceFiles { get; set; }

        public IList<string> ContentFiles { get; set; }

        public bool IsTestProject { get; set; }

        public bool IsExcluded { get; set; }

        public List<AnalysisResult> AnalysisResults { get; private set; }


        /// <summary>
        /// The full path to the parent directory
        /// </summary>
        public string ParentDirectoryPath { get; set; }

        /// <summary>
        /// The name of the folder in which the project exists
        /// </summary>
        public string ProjectFolderName { get; set; }

        /// <summary>
        /// The name of the project file
        /// </summary>
        public string ProjectFileName { get; set; }

        public string FullDirectoryPath
        {
            get { return Path.Combine(this.ParentDirectoryPath, this.ProjectFolderName); }
        }

        public string FullFilePath
        {
            get { return Path.Combine(this.FullDirectoryPath, this.ProjectFileName); }
        }

        /// <summary>
        /// The user-friendly name for the project
        /// </summary>
        public string ProjectName
        {

            get
            {
                return Path.GetFileNameWithoutExtension(this.ProjectFileName);
            }
        }

        public IEnumerable<string> AllFiles
        {
            get
            {
                List<string> files = new List<string>();
                if (this.ManagedSourceFiles != null)
                {
                    files.AddRange(this.ManagedSourceFiles);
                }
                if (this.ContentFiles != null)
                {
                    files.AddRange(this.ContentFiles);
                }
                return files;
            }
        }

        #endregion

        #region Public methods

        public ProjectInfo CreateProjectInfo()
        {
            ProjectInfo info = new ProjectInfo()
            {
                FullPath = this.FullFilePath,
                ProjectLanguage = this.ProjectLanguage,
                ProjectGuid = this.ProjectGuid,
                ProjectName = this.ProjectName,
                ProjectType = this.IsTestProject ? ProjectType.Test : ProjectType.Product,
                IsExcluded = this.IsExcluded,
                AnalysisResults = new List<AnalysisResult>(this.AnalysisResults)
            };

            return info;
        }

        #endregion

    }
}
