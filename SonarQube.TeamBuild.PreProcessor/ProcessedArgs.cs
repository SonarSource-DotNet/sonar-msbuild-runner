﻿//-----------------------------------------------------------------------
// <copyright file="ProcessedArgs.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Data class to hold validated command line arguments required by the pre-processor
    /// </summary>
    public class ProcessedArgs
    {
        private readonly ListPropertiesProvider projectSettingsProvider;
        private readonly IAnalysisPropertyProvider cmdLineProperties;
        private readonly IAnalysisPropertyProvider globalFileProperties;

        private readonly IAnalysisPropertyProvider aggProperties;

        public ProcessedArgs(string key, string name, string version, bool installLoaderTargets, IAnalysisPropertyProvider cmdLineProperties, IAnalysisPropertyProvider globalFileProperties)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException("name");
            }
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException("version");
            }
            if (cmdLineProperties == null)
            {
                throw new ArgumentNullException("cmdLineProperties");
            }
            if (globalFileProperties == null)
            {
                throw new ArgumentNullException("globalFileProperties");
            }

            this.cmdLineProperties = cmdLineProperties;
            this.globalFileProperties = globalFileProperties;
            this.InstallLoaderTargets = installLoaderTargets;

            this.projectSettingsProvider = new ListPropertiesProvider();
            this.projectSettingsProvider.AddProperty(SonarProperties.ProjectKey, key);
            this.projectSettingsProvider.AddProperty(SonarProperties.ProjectName, name);
            this.projectSettingsProvider.AddProperty(SonarProperties.ProjectVersion, version);

            this.aggProperties = new AggregatePropertiesProvider(projectSettingsProvider, cmdLineProperties, globalFileProperties);
        }

        public string ProjectKey { get { return this.GetSetting(SonarProperties.ProjectKey); } }

        public string ProjectName { get { return this.GetSetting(SonarProperties.ProjectName); } }

        public string ProjectVersion { get { return this.GetSetting(SonarProperties.ProjectVersion); } }

        /// <summary>
        /// If true the preprocessor should copy the loader targets to a user location where MSBuild will pick them up
        /// </summary>
        public bool InstallLoaderTargets { get; private set; }

        public IAnalysisPropertyProvider ProjectSettingsProvider { get { return this.projectSettingsProvider; } }

        public IAnalysisPropertyProvider CmdLineProperties {  get { return this.cmdLineProperties; } }

        public IAnalysisPropertyProvider GlobalFileProperties { get { return this.globalFileProperties; } }

        public IAnalysisPropertyProvider AggregateProperties { get { return this.aggProperties; } }

        /// <summary>
        /// Returns the value for the specified setting.
        /// Throws if the setting does not exist.
        /// </summary>
        public string GetSetting(string key)
        {
            string value;
            if (!this.aggProperties.TryGetValue(key, out value))
            {
                string message = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.ERROR_MissingSetting, key);
                throw new InvalidOperationException(message);
            }
            return value;
        }

        /// <summary>
        /// Returns the value for the specified setting, or the supplied
        /// default if the setting does not exist
        /// </summary>
        public string GetSetting(string key, string defaultValue)
        {
            string value;
            if (!this.aggProperties.TryGetValue(key, out value))
            {
                value = defaultValue;
            }
            return value;
        }

        public bool TryGetSetting(string key, out string value)
        {
            return this.aggProperties.TryGetValue(key, out value);
        }

        public IEnumerable<Property> GetAllProperties()
        {
            return this.aggProperties.GetAllProperties();
        }
    }
}
