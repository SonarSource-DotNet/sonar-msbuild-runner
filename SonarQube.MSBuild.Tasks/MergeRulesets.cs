﻿//-----------------------------------------------------------------------
// <copyright file="MergeRuleSets.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SonarQube.MSBuild.Tasks
{
    /// <summary>
    /// MSBuild task to merge rulesets by adding "Include" references to a
    /// specified ruleset
    /// </summary>
    public class MergeRuleSets : Task
    {
        private static readonly XName IncludeElementName = "Include";
        private static readonly XName PathAttributeName = "Path";
        private static readonly XName ActionAttributeName = "Action";
        private const string DefaultActionValue = "Default";

        #region Input properties

        /// <summary>
        /// The path to the main ruleset file i.e. the one that will include the others
        /// </summary>
        [Required]
        public string PrimaryRulesetFilePath { get; set; }

        /// <summary>
        /// Full path to the directory containing the project file
        /// </summary>
        /// <remarks>Required to resolve relative ruleset paths</remarks>
        [Required]
        public string ProjectDirectoryPath { get; set; }

        /// <summary>
        /// The full file paths of any rulesets to include. Can be empty, in which case the task is a no-op.
        /// </summary>
        public string[] IncludedRulesetFilePaths { get; set; }

        #endregion Input properties

        #region Overrides

        public override bool Execute()
        {
            Debug.Assert(this.PrimaryRulesetFilePath != null, "[Required] property PrimaryRulesetFilePath should not be null when the task is called from MSBuild");
            Debug.Assert(this.ProjectDirectoryPath != null, "[Required] property ProjectDirectoryPath should not be null when the task is called from MSBuild");

            this.Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_MergingRulesets);

            if (!File.Exists(this.PrimaryRulesetFilePath))
            {
                throw new FileNotFoundException(Resources.MergeRulesets_MissingPrimaryRuleset, this.PrimaryRulesetFilePath);
            }
            if (this.IncludedRulesetFilePaths == null || this.IncludedRulesetFilePaths.Length == 0)
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.MergeRuleset_NoRulesetsSpecified);
                return true; // nothing to do if there are no rulesets
            }

            XDocument ruleset = XDocument.Load(PrimaryRulesetFilePath);
            foreach (string includePath in this.IncludedRulesetFilePaths)
            {
                string resolvedIncludePath = this.TryResolveIncludedRuleset(includePath);
                if (resolvedIncludePath != null)
                {
                    EnsureIncludeExists(ruleset, resolvedIncludePath);
                }
            }
            this.Log.LogMessage(MessageImportance.Low, Resources.MergeRuleset_SavingUpdatedRuleset, this.PrimaryRulesetFilePath);
            ruleset.Save(this.PrimaryRulesetFilePath);

            return !this.Log.HasLoggedErrors;
        }

        #endregion Overrides

        #region Private methods

        private string TryResolveIncludedRuleset(string includePath)
        {
            string resolvedPath;

            if (Path.IsPathRooted(includePath))
            {
                resolvedPath = includePath; // assume rooted paths are correctly set
            }
            else
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_ResolvingRuleset, includePath);
                resolvedPath = Path.GetFullPath(Path.Combine(this.ProjectDirectoryPath, includePath));
                if (File.Exists(resolvedPath))
                {
                    this.Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_ResolvedRuleset, resolvedPath);
                }
                else
                {
                    this.Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_FailedToResolveRuleset, includePath);
                    resolvedPath = null;
                }
            }

            return resolvedPath;
        }

        private void EnsureIncludeExists(XDocument ruleset, string includePath)
        {
            if (IncludeExists(ruleset, includePath))
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_RulesetAlreadyIncluded, includePath);
            }
            else
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.MergeRuleset_IncludingRuleset, includePath);
                XElement newInclude = new XElement(IncludeElementName);
                newInclude.SetAttributeValue(PathAttributeName, includePath);
                newInclude.SetAttributeValue(ActionAttributeName, DefaultActionValue);

                ruleset.Root.AddFirst(newInclude);
            }
        }

        private static bool IncludeExists(XDocument ruleset, string includePath)
        {
            return ruleset.Descendants(IncludeElementName).Any(e =>
                {
                    XAttribute attr = e.Attribute(PathAttributeName);
                    return attr != null && string.Equals(includePath, attr.Value, StringComparison.OrdinalIgnoreCase);
                });
        }

        #endregion Private methods
    }
}