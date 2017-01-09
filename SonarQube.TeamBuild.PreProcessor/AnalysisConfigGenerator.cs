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
using SonarQube.TeamBuild.Integration;
using System;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor
{
    public static class AnalysisConfigGenerator
    {
        /// <summary>
        /// Combines the various configuration options into the AnalysisConfig file
        /// used by the build and post-processor. Saves the file and returns the config instance.
        /// </summary>
        /// <param name="args">Processed command line arguments supplied the user</param>
        /// <param name="buildSettings">Build environment settings</param>
        /// <param name="serverProperties">Analysis properties downloaded from the SonarQube server</param>
        /// <param name="analyzerSettings">Specifies the Roslyn analyzers to use</param>
        public static AnalysisConfig GenerateFile(ProcessedArgs args,
            TeamBuildSettings buildSettings,
            IDictionary<string, string> serverProperties,
            List<AnalyzerSettings> analyzersSettings,
            ILogger logger)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }
            if (buildSettings == null)
            {
                throw new ArgumentNullException(nameof(buildSettings));
            }
            if (serverProperties == null)
            {
                throw new ArgumentNullException(nameof(serverProperties));
            }
            if (analyzersSettings == null)
            {
                throw new ArgumentNullException(nameof(analyzersSettings));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            AnalysisConfig config = new AnalysisConfig();
            config.SonarProjectKey = args.ProjectKey;
            config.SonarProjectName = args.ProjectName;
            config.SonarProjectVersion = args.ProjectVersion;
            config.SonarQubeHostUrl = args.GetSetting(SonarProperties.HostUrl);

            config.SetBuildUri(buildSettings.BuildUri);
            config.SetTfsUri(buildSettings.TfsUri);

            config.SonarConfigDir = buildSettings.SonarConfigDirectory;
            config.SonarOutputDir = buildSettings.SonarOutputDirectory;
            config.SonarBinDir = buildSettings.SonarBinDirectory;
            config.SonarScannerWorkingDirectory = buildSettings.SonarScannerWorkingDirectory;
            config.SourcesDirectory = buildSettings.SourcesDirectory;

            // Add the server properties to the config
            config.ServerSettings = new AnalysisProperties();

            foreach (var property in serverProperties)
            {
                if (!Utilities.IsSecuredServerProperty(property.Key))
                {
                    AddSetting(config.ServerSettings, property.Key, property.Value);
                }
            }

            config.LocalSettings = new AnalysisProperties();
            foreach (var property in args.LocalProperties.GetAllProperties())
            {
                AddSetting(config.LocalSettings, property.Id, property.Value);
            }

            // Set the pointer to the properties file
            if (args.PropertiesFileName != null)
            {
                config.SetSettingsFilePath(args.PropertiesFileName);
            }

            config.AnalyzersSettings = analyzersSettings;

            config.Save(buildSettings.AnalysisConfigFilePath);

            return config;
        }

        private static void AddSetting(AnalysisProperties properties, string id, string value)
        {
            Property property = new Property() { Id = id, Value = value };

            // Ensure it isn't possible to write sensitive data to the config file
            if (!property.ContainsSensitiveData())
            {
                properties.Add(new Property() { Id = id, Value = value });
            }
        }
    }
}