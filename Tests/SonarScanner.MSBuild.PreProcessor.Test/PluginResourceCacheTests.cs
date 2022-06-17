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
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.PreProcessor.Roslyn;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class PluginResourceCacheTests
    {
        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void Constructor_NullOrWhiteSpaceCacheDirectory_ThrowsArgumentNullException(string basedir) =>
            ((Action)(() => new PluginResourceCache(basedir))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("basedir");

        [TestMethod]
        public void Constructor_NullOrWhiteSpaceCacheDirectory_ThrowsArgumentNullException() =>
            ((Action)(() => new PluginResourceCache("nonExistent"))).Should().Throw<DirectoryNotFoundException>().WithMessage("no such directory: nonExistent");
    }
}
