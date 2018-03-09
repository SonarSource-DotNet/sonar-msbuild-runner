﻿/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.PreProcessor.Roslyn;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockAnalyzerInstaller : IAnalyzerInstaller
    {
        #region Test helpers

        public ISet<string> AssemblyPathsToReturn { get; set; }

        public List<Plugin> SuppliedPlugins = new List<Plugin>();

        #endregion Test helpers

        #region Checks

        public void AssertExpectedPluginsRequested(IEnumerable<string> plugins)
        {
            foreach(var plugin in plugins)
            {
                AssertExpectedPluginRequested(plugin);
            }
        }

        public void AssertExpectedPluginRequested(string key)
        {
            Assert.IsFalse(SuppliedPlugins == null || !SuppliedPlugins.Any(), "No plugins have been requested");
            var found = SuppliedPlugins.Any(p => string.Equals(key, p.Key, System.StringComparison.Ordinal));
            Assert.IsTrue(found, "Expected plugin was not requested. Id: {0}", key);
        }

        #endregion Checks

        #region IAnalyzerInstaller methods

        IEnumerable<string> IAnalyzerInstaller.InstallAssemblies(IEnumerable<Plugin> plugins)
        {
            Assert.IsNotNull(plugins, "Supplied list of plugins should not be null");
            foreach(var p in plugins)
            {
                Debug.WriteLine(p.StaticResourceName);
            }
            SuppliedPlugins.AddRange(plugins);

            return AssemblyPathsToReturn;
        }

        #endregion IAnalyzerInstaller methods
    }
}
