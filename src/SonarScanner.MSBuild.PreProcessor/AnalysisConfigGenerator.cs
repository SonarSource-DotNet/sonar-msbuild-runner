﻿/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    public static class AnalysisConfigGenerator
    {
        /// <summary>
        /// Combines the various configuration options into the AnalysisConfig file
        /// used by the build and post-processor. Saves the file and returns the config instance.
        /// </summary>
        /// <param name="localSettings">Processed local settings, including command line arguments supplied the user.</param>
        /// <param name="buildSettings">Build environment settings</param>
        /// <param name="additionalSettings">Additional settings generated by this Scanner. Can be empty.</param>
        /// <param name="serverProperties">Analysis properties downloaded from the SonarQube server.</param>
        /// <param name="analyzersSettings">Specifies the Roslyn analyzers to use. Can be empty.</param>
        /// <param name="sonarQubeVersion">SonarQube/SonarCloud server version.</param>
        public static AnalysisConfig GenerateFile(ProcessedArgs localSettings,
                                                  BuildSettings buildSettings,
                                                  Dictionary<string, string> additionalSettings,
                                                  IDictionary<string, string> serverProperties,
                                                  List<AnalyzerSettings> analyzersSettings,
                                                  string sonarQubeVersion)
        {
            _ = localSettings ?? throw new ArgumentNullException(nameof(localSettings));
            _ = buildSettings ?? throw new ArgumentNullException(nameof(buildSettings));
            _ = additionalSettings ?? throw new ArgumentNullException(nameof(additionalSettings));
            _ = serverProperties ?? throw new ArgumentNullException(nameof(serverProperties));
            _ = analyzersSettings ?? throw new ArgumentNullException(nameof(analyzersSettings));
            var config = new AnalysisConfig
            {
                SonarConfigDir = buildSettings.SonarConfigDirectory,
                SonarOutputDir = buildSettings.SonarOutputDirectory,
                SonarBinDir = buildSettings.SonarBinDirectory,
                SonarScannerWorkingDirectory = buildSettings.SonarScannerWorkingDirectory,
                SourcesDirectory = buildSettings.SourcesDirectory,
                HasBeginStepCommandLineCredentials = localSettings.CmdLineProperties.HasProperty(SonarProperties.SonarUserName),
                SonarQubeHostUrl = localSettings.SonarQubeUrl,
                SonarQubeVersion = sonarQubeVersion,
                SonarProjectKey = localSettings.ProjectKey,
                SonarProjectVersion = localSettings.ProjectVersion,
                SonarProjectName = localSettings.ProjectName,
                ServerSettings = new(),
                LocalSettings = new(),
                AnalyzersSettings = analyzersSettings
            };
            config.SetBuildUri(buildSettings.BuildUri);
            config.SetTfsUri(buildSettings.TfsUri);
            config.SetVsCoverageConverterToolPath(buildSettings.CoverageToolUserSuppliedPath);
            foreach (var item in additionalSettings)
            {
                config.SetConfigValue(item.Key, item.Value);
            }
            foreach (var property in serverProperties.Where(x => !Utilities.IsSecuredServerProperty(x.Key)))
            {
                AddSetting(config.ServerSettings, property.Key, property.Value);
            }
            foreach (var property in localSettings.CmdLineProperties.GetAllProperties())    // Only those from command line
            {
                AddSetting(config.LocalSettings, property.Id, property.Value);
            }
            if (!string.IsNullOrEmpty(localSettings.Organization))
            {
                AddSetting(config.LocalSettings, SonarProperties.Organization, localSettings.Organization);
            }
            if (localSettings.PropertiesFileName != null)
            {
                config.SetSettingsFilePath(localSettings.PropertiesFileName);
            }
            config.Save(buildSettings.AnalysisConfigFilePath);
            return config;
        }

        private static void AddSetting(AnalysisProperties properties, string id, string value)
        {
            var property = new Property { Id = id, Value = value };

            // Ensure it isn't possible to write sensitive data to the config file
            if (!property.ContainsSensitiveData())
            {
                properties.Add(new Property { Id = id, Value = value });
            }
        }
    }
}
