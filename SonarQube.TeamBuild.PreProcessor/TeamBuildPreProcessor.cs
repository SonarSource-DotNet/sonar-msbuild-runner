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
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace SonarQube.TeamBuild.PreProcessor
{
    public class TeamBuildPreProcessor : ITeamBuildPreProcessor
    {
        public const string CSharpLanguage = "cs";
        public const string CSharpPluginKey = "csharp";

        public const string VBNetLanguage = "vbnet";
        public const string VBNetPluginKey = "vbnet";

        public const string FxCopRulesetName = "SonarQubeFxCop-{0}.ruleset";

        private readonly static List<PluginDefinition> plugins;
        private readonly static PluginDefinition csharp = new PluginDefinition(CSharpLanguage, CSharpPluginKey);
        private readonly static PluginDefinition vbnet = new PluginDefinition(VBNetLanguage, VBNetPluginKey);

        private readonly IPreprocessorObjectFactory factory;
        private readonly ILogger logger;

        #region Constructor(s)

        public TeamBuildPreProcessor(IPreprocessorObjectFactory factory, ILogger logger)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            this.factory = factory;
            this.logger = logger;
        }

        static TeamBuildPreProcessor()
        {
            plugins = new List<PluginDefinition>();
            plugins.Add(csharp);
            plugins.Add(vbnet);
        }

        #endregion Constructor(s)

        #region Inner class
        class PluginDefinition
        {
            public string Language { get; private set; }
            public string PluginKey { get; private set; }

            public PluginDefinition(string language, string pluginKey)
            {
                this.Language = language;
                this.PluginKey = pluginKey;
            }
        }
        #endregion

        #region Public methods

        public bool Execute(string[] args)
        {
            ProcessedArgs processedArgs = ArgumentProcessor.TryProcessArgs(args, this.logger);

            if (processedArgs == null)
            {
                this.logger.LogError(Resources.ERROR_InvalidCommandLineArgs);
                return false;
            }
            else
            {
                return DoExecute(processedArgs);
            }
        }

        private bool DoExecute(ProcessedArgs args)
        {
            Debug.Assert(args != null, "Not expecting the process arguments to be null");

            this.logger.Verbosity = VerbosityCalculator.ComputeVerbosity(args.AggregateProperties, this.logger);

            InstallLoaderTargets(args);

            TeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(this.logger);

            // We're checking the args and environment variables so we can report all config errors to the user at once
            if (teamBuildSettings == null)
            {
                this.logger.LogError(Resources.ERROR_CannotPerformProcessing);
                return false;
            }

            // Create the directories
            this.logger.LogDebug(Resources.MSG_CreatingFolders);
            if (!Utilities.TryEnsureEmptyDirectories(this.logger,
                teamBuildSettings.SonarConfigDirectory,
                teamBuildSettings.SonarOutputDirectory))
            {
                return false;
            }

            ISonarQubeServer server = this.factory.CreateSonarQubeServer(args, this.logger);

            IDictionary<string, string> serverSettings;
            List<AnalyzerSettings> analyzersSettings;
            if (!FetchArgumentsAndRulesets(server, args, teamBuildSettings, out serverSettings, out analyzersSettings))
            {
                return false;
            }
            Debug.Assert(analyzersSettings != null, "Not expecting the analyzers settings to be null");

            AnalysisConfigGenerator.GenerateFile(args, teamBuildSettings, serverSettings, analyzersSettings, this.logger);

            return true;
        }

        #endregion Public methods

        #region Private methods

        private void InstallLoaderTargets(ProcessedArgs args)
        {
            if (args.InstallLoaderTargets)
            {
                ITargetsInstaller installer = this.factory.CreateTargetInstaller();
                Debug.Assert(installer != null, "Factory should not return null");
                installer.InstallLoaderTargets(this.logger, Directory.GetCurrentDirectory());
            }
            else
            {
                this.logger.LogDebug(Resources.MSG_NotCopyingTargets);
            }
        }

        private bool FetchArgumentsAndRulesets(ISonarQubeServer server, ProcessedArgs args, TeamBuildSettings settings, out IDictionary<string, string> serverSettings, out List<AnalyzerSettings> analyzersSettings)
        {
            serverSettings = null;
            analyzersSettings = new List<AnalyzerSettings>();

            try
            {
                this.logger.LogInfo(Resources.MSG_FetchingAnalysisConfiguration);

                // Respect sonar.branch setting if set
                string projectBranch = null;
                args.TryGetSetting(SonarProperties.ProjectBranch, out projectBranch);

                // Fetch the SonarQube project properties
                serverSettings = server.GetProperties(args.ProjectKey, projectBranch);

                // Fetch installed plugins
                IEnumerable<string> installedPlugins = server.GetInstalledPlugins();

                foreach (PluginDefinition plugin in plugins)
                {
                    if (!installedPlugins.Contains(plugin.PluginKey))
                    {
                        continue;
                    }

                    // Fetch project quality profile
                    string qualityProfile;
                    if (!server.TryGetQualityProfile(args.ProjectKey, projectBranch, plugin.Language, out qualityProfile))
                    {
                        this.logger.LogDebug(Resources.RAP_NoQualityProfile, plugin.Language, args.ProjectKey);
                        continue;
                    }

                    // Fetch rules
                    IList<ActiveRule> activeRules = server.GetActiveRules(qualityProfile);

                    if (!activeRules.Any())
                    {
                        this.logger.LogDebug(Resources.RAP_NoActiveRules, plugin.Language);
                        continue;
                    }

                    IList<string> inactiveRules = server.GetInactiveRules(qualityProfile, plugin.Language);

                    this.logger.LogInfo(Resources.MSG_GeneratingRulesets);
                    string fxCopPath = Path.Combine(settings.SonarConfigDirectory, string.Format(FxCopRulesetName, plugin.Language));
                    if (plugin.Language.Equals(VBNetLanguage))
                    {
                        GenerateFxCopRuleset("fxcop-vbnet", activeRules, fxCopPath);
                    }
                    else
                    {
                        GenerateFxCopRuleset("fxcop", activeRules, fxCopPath);
                    }

                    // Generate Roslyn analyzers settings and rulesets
                    IAnalyzerProvider analyzerProvider = this.factory.CreateRoslynAnalyzerProvider(this.logger);
                    Debug.Assert(analyzerProvider != null, "Factory should not return null");

                    // Will be null if the processing of settings and active rules resulted in an empty ruleset
                    AnalyzerSettings analyzer = analyzerProvider.SetupAnalyzer(settings, serverSettings, activeRules, inactiveRules,
                        plugin.Language);

                    if (analyzer != null)
                    {
                        analyzersSettings.Add(analyzer);
                    }
                }

            }
            catch (WebException ex)
            {
                string hostUrl = args.GetSetting(SonarProperties.HostUrl);
                if (Utilities.HandleHostUrlWebException(ex, hostUrl, this.logger))
                {
                    return false;
                }

                throw;
            }
            finally
            {
                Utilities.SafeDispose(server);
            }

            return true;
        }

        private void GenerateFxCopRuleset(string repository, IList<ActiveRule> activeRules, string path)
        {
            this.logger.LogDebug(Resources.MSG_GeneratingRuleset, path);
            this.factory.CreateRulesetGenerator().Generate(repository, activeRules, path);
        }

        #endregion Private methods
    }
}