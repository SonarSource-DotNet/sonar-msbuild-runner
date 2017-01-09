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

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class to describe the analysis settings for a single SonarQube project
    /// </summary>
    /// <remarks>The class is XML-serializable</remarks>
    [XmlRoot(Namespace = XmlNamespace)]
    public class AnalysisConfig
    {
        public const string XmlNamespace = ProjectInfo.XmlNamespace;

        public string SonarConfigDir { get; set; }

        public string SonarOutputDir { get; set; }

        public string SonarBinDir { get; set; }

        /// <summary>
        /// The working directory as perceived by the user, i.e. the current directory for command line builds
        /// </summary>
        /// <remarks>Users expect to specify paths relative to the working directory and not to the location of the sonar-scanner program.
        ///  See https://jira.sonarsource.com/browse/SONARMSBRU-100 for details.</remarks>
        public string SonarScannerWorkingDirectory { get; set; }

        /// <summary>
        /// Parent directory of the source files. 
        /// </summary>
        /// <remarks>SCM plugins like Git or TFVC expect to find .git or $tf subdirectories directly under the sources directory 
        /// in order to and provide annotations. </remarks>
        public string SourcesDirectory { get; set; }

        #region SonarQube project properties

        public string SonarQubeHostUrl { get; set; }

        public string SonarProjectKey { get; set; }

        public string SonarProjectVersion { get; set; }

        public string SonarProjectName { get; set; }

        /// <summary>
        /// List of additional configuration-related settings
        /// e.g. the build system identifier, if appropriate.
        /// </summary>
        /// <remarks>These settings will not be supplied to the sonar-scanner.</remarks>
        public List<ConfigSetting> AdditionalConfig { get; set; }

        /// <summary>
        /// List of analysis settings inherited from the SonarQube server
        /// </summary>
        public AnalysisProperties ServerSettings { get; set; }

        /// <summary>
        /// List of analysis settings supplied locally (either on the
        /// command line or in a file)
        /// </summary>
        public AnalysisProperties LocalSettings { get; set; }

        /// <summary>
        /// Configuration for Roslyn analysers
        /// </summary>
        public List<AnalyzerSettings> AnalyzersSettings { get; set; }

        #endregion

        #region Serialization

        [XmlIgnore]
        public string FileName { get; private set; }

        /// <summary>
        /// Saves the project to the specified file as XML
        /// </summary>
        public void Save(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            Serializer.SaveModel(this, fileName);
            this.FileName = fileName;
        }

        /// <summary>
        /// Loads and returns project info from the specified XML file
        /// </summary>
        public static AnalysisConfig Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            AnalysisConfig model = Serializer.LoadModel<AnalysisConfig>(fileName);
            model.FileName = fileName;
            return model;
        }

        #endregion

    }
}
