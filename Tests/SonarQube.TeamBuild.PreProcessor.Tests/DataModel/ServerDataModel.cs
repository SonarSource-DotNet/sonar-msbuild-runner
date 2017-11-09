﻿/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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
 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class ServerDataModel
    {
        private readonly IList<QualityProfile> qualityProfiles;
        private readonly IDictionary<string, byte[]> embeddedFilesMap;
    
        public ServerDataModel()
        {
            this.qualityProfiles = new List<QualityProfile>();
            this.ServerProperties = new Dictionary<string, string>();
            this.Languages = new List<string>();
            this.embeddedFilesMap = new Dictionary<string, byte[]>();
        }

        public IEnumerable<QualityProfile> QualityProfiles { get { return this.qualityProfiles; } }

        public IDictionary<string, string> ServerProperties { get; set; }

        public IList<string> Languages { get; set; }

        #region Builder methods

        public QualityProfile AddQualityProfile(string id, string language, string organization)
        {
            QualityProfile profile = this.FindProfile(id);
            Assert.IsNull(profile, "A quality profile already exists. Id: {0}, language: {1}", id, language);

            profile = new QualityProfile(id, language, organization);
            this.qualityProfiles.Add(profile);
            return profile;
        }

        public void AddActiveRuleToProfile(string qProfile, ActiveRule rule)
        {
            QualityProfile profile = this.FindProfile(qProfile);
            profile.ActiveRules.Add(rule);
        }

        public void AddInactiveRuleToProfile(string qProfile, string ruleKey)
        {
            QualityProfile profile = this.FindProfile(qProfile);
            profile.InactiveRules.Add(ruleKey);
        }

        public void AddEmbeddedZipFile(string pluginKey, string embeddedFileName, params string[] contentFileNames)
        {
            this.embeddedFilesMap.Add(GetEmbeddedFileKey(pluginKey, embeddedFileName), CreateDummyZipFile(contentFileNames));
        }

        #endregion

        #region Locator methods

        public QualityProfile FindProfile(string id)
        {
            QualityProfile profile = this.qualityProfiles.SingleOrDefault(qp => string.Equals(qp.Id, id));
            return profile;
        }

        public byte[] FindEmbeddedFile(string pluginKey, string embeddedFileName)
        {
            this.embeddedFilesMap.TryGetValue(GetEmbeddedFileKey(pluginKey, embeddedFileName), out byte[] content);
            return content;
        }

        private static string GetEmbeddedFileKey(string pluginKey, string embeddedFileName)
        {
            return pluginKey + "___" + embeddedFileName;
        }
        #endregion


        #region Private methods

        private byte[] CreateDummyZipFile(params string[] contentFileNames)
        {
            string fileName = "dummy.zip";

            // Create a temporary directory structure
            string tempDir = Path.Combine(Path.GetTempPath(), "sqTestsTemp", System.Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            string zipDir = Path.Combine(tempDir, "zipDir");
            Directory.CreateDirectory(zipDir);
            string zippedFilePath = Path.Combine(tempDir, fileName);

            // Create and read the zip file
            foreach (string contentFileName in contentFileNames)
            {
                TestUtils.CreateTextFile(zipDir, contentFileName, "dummy file content");
            }

            ZipFile.CreateFromDirectory(zipDir, zippedFilePath);
            byte[] zipData = File.ReadAllBytes(zippedFilePath);

            // Cleanup
            Directory.Delete(tempDir, true);

            return zipData;
        }

        #endregion
    }
}
