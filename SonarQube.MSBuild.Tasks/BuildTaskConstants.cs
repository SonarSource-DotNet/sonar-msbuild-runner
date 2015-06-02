﻿//-----------------------------------------------------------------------
// <copyright file="BuildTaskConstants.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.MSBuild.Tasks
{
    public static class BuildTaskConstants
    {
        /// <summary>
        /// Item name of the analysis result item type
        /// </summary>
        public const string ResultItemName = "AnalysisResult";

        /// <summary>
        /// Name of the analysis result "id" metadata item
        /// </summary>
        public const string ResultMetadataIdProperty = "Id";

        /// <summary>
        /// Item name of the analysis setting item type
        /// </summary>
        public const string SettingItemName = "SonarQubeSetting";

        /// <summary>
        /// Item name of the global analysis setting item type
        /// </summary>
        public const string GlobalSettingItemName = "SonarQubeGlobalSetting";

        /// <summary>
        /// Name of the analysis setting "value" metadata item
        /// </summary>
        public const string SettingValueMetadataName = "Value";
    }
}
