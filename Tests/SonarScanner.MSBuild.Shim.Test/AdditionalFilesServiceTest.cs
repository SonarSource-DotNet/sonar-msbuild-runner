﻿/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class AdditionalFilesServiceTest
{
    private static readonly DirectoryInfo ProjectBaseDir = new("C:\\dev");

    private readonly IDirectoryWrapper wrapper;
    private readonly AdditionalFilesService sut;

    public AdditionalFilesServiceTest()
    {
        wrapper = Substitute.For<IDirectoryWrapper>();
        wrapper
            .EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories)
            .Returns([]);
        sut = new(wrapper);
    }

    [TestMethod]
    public void AdditionalFiles_EmptyServerSettings_NoExtensionsFound()
    {
        var files = sut.AdditionalFiles(new() { MultiFileAnalysis = true, ServerSettings = [] }, ProjectBaseDir);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
        wrapper.DidNotReceive().EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [TestMethod]
    public void AdditionalFiles_NullServerSettings_NoExtensionsFound()
    {
        var files = sut.AdditionalFiles(new() { MultiFileAnalysis = true, ServerSettings = null }, ProjectBaseDir);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
        wrapper.DidNotReceive().EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [TestMethod]
    public void AdditionalFiles_MultiFileAnalysisDisabled()
    {
        wrapper
            .EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(["valid.js"]);
        var config = new AnalysisConfig
        {
            MultiFileAnalysis = false,
            LocalSettings = [],
            ServerSettings = [new("sonar.javascript.file.suffixes", ".js")]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow(".sonarqube")]
    [DataRow(".SONARQUBE")]
    [DataRow(".SonaRQubE")]
    [DataRow(".sonar")]
    [DataRow(".SONAR")]
    public void AdditionalFiles_ExtensionsFound_SonarQubeIgnored(string template)
    {
        var valid = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, "valid"));
        var invalid = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, template));
        wrapper
            .EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories)
            .Returns([valid, invalid]);
        wrapper
            .EnumerateFiles(valid.FullName, "*", SearchOption.TopDirectoryOnly)
            .Returns([
                // sources
                "valid.js",
                $"{template}.js",
                $"not{template}{Path.DirectorySeparatorChar}not{template}.js",
                $"{template}not{Path.DirectorySeparatorChar}{template}not.js",
                // tests
                $"{template}.test.js",
                $"not{template}{Path.DirectorySeparatorChar}not{template}.spec.js",
                ]);
        wrapper
            .EnumerateFiles(invalid.FullName, "*", SearchOption.TopDirectoryOnly)
            .Returns([
                $"invalid.js",
                $"invalid.test.js",
                $"invalid.spec.js",
                ]);
        var analysisConfig = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [],
            ServerSettings =
            [
                new("sonar.javascript.file.suffixes", ".js"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(
            "valid.js",
            $"{template}.js",
            $"not{template}.js",
            $"{template}not.js");
        files.Tests.Select(x => x.Name).Should().BeEquivalentTo(
            $"{template}.test.js",
            $"not{template}.spec.js");
    }

    [DataTestMethod]
    [DataRow(".js,.jsx")]
    [DataRow(".js, .jsx")]
    [DataRow(" .js, .jsx")]
    [DataRow(" .js,.jsx")]
    [DataRow(".js,.jsx ")]
    [DataRow(".js,.jsx ")]
    [DataRow("js,jsx")]
    [DataRow(" js , jsx ")]
    public void AdditionalFiles_ExtensionsFound_AllExtensionPermutations(string propertyValue)
    {
        wrapper
            .EnumerateFiles(ProjectBaseDir.FullName, "*", SearchOption.TopDirectoryOnly)
            .Returns(["valid.js", "valid.JSX", "invalid.ajs", "invalidjs", @"C:\.js", @"C:\.jsx"]);
        var config = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [],
            ServerSettings = [new("sonar.javascript.file.suffixes", propertyValue)]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(["valid.js", "valid.JSX"]);
        files.Tests.Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow("sonar.tsql.file.suffixes")]
    [DataRow("sonar.plsql.file.suffixes")]
    [DataRow("sonar.yaml.file.suffixes")]
    [DataRow("sonar.xml.file.suffixes")]
    [DataRow("sonar.json.file.suffixes")]
    [DataRow("sonar.css.file.suffixes")]
    [DataRow("sonar.html.file.suffixes")]
    [DataRow("sonar.javascript.file.suffixes")]
    [DataRow("sonar.typescript.file.suffixes")]
    public void AdditionalFiles_ExtensionsFound_SingleProperty(string propertyName)
    {
        wrapper
            .EnumerateFiles(ProjectBaseDir.FullName, "*", SearchOption.TopDirectoryOnly)
            .Returns(["valid.sql", "valid.js", "invalid.cs"]);
        var config = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [],
            ServerSettings = [new(propertyName, ".sql,.js")]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(["valid.sql", "valid.js"]);
        files.Tests.Should().BeEmpty();
    }

    [TestMethod]
    public void AdditionalFiles_ExtensionsFound_MultipleProperties()
    {
        wrapper
            .EnumerateFiles(ProjectBaseDir.FullName, "*", SearchOption.TopDirectoryOnly)
            .Returns(["valid.cs.html", "valid.sql", "invalid.js", "invalid.html", "invalid.vb.html"]);
        var analysisConfig = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [],
            ServerSettings =
            [
                new("sonar.html.file.suffixes", ".cs.html"),
                new("sonar.tsql.file.suffixes", ".sql"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(["valid.cs.html", "valid.sql"]);
        files.Tests.Should().BeEmpty();
    }

    [TestMethod]
    public void AdditionalFiles_ExtensionsFound_MultipleProperties_TestFilesExist_NoSonarTests()
    {
        wrapper
            .EnumerateFiles(ProjectBaseDir.FullName, "*", SearchOption.TopDirectoryOnly)
            .Returns([
                // source files
                $"{Path.DirectorySeparatorChar}.js",      // should be ignored
                $"{Path.DirectorySeparatorChar}.jsx",     // should be ignored
                "file1.js",
                "file2.jsx",
                "file3.ts",
                "file4.tsx",
                // js test files
                "file5.spec.js",
                "file6.test.js",
                "file7.spec.jsx",
                "file8.test.jsx",
                // ts test files
                "file9.spec.ts",
                "file10.test.TS",
                "file11.spec.tsx",
                "file12.test.TSx",
                // random invalid file
                "invalid.html"
                ]);

        var analysisConfig = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [],
            ServerSettings =
            [
                new("sonar.javascript.file.suffixes", "js,jsx"),
                new("sonar.typescript.file.suffixes", ".ts,.tsx"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(["file1.js", "file2.jsx", "file3.ts", "file4.tsx"]);
        files.Tests.Select(x => x.Name).Should().BeEquivalentTo([
            "file5.spec.js",
            "file6.test.js",
            "file7.spec.jsx",
            "file8.test.jsx",
            "file9.spec.ts",
            "file10.test.TS",
            "file11.spec.tsx",
            "file12.test.TSx"
        ]);
    }

    [TestMethod]
    public void AdditionalFiles_ExtensionsFound_MultipleProperties_TestFilesExist_WithSonarTests()
    {
        wrapper
            .EnumerateFiles(ProjectBaseDir.FullName, "*", SearchOption.TopDirectoryOnly)
            .Returns([
                // source files
                "file1.js",
                "file2.jsx",
                "file3.ts",
                "file4.tsx",
                // js test files
                "file5.spec.js",
                "file6.test.js",
                "file7.spec.jsx",
                "file8.test.jsx",
                // ts test files
                "file9.spec.ts",
                "file10.test.ts",
                "file11.spec.tsx",
                "file12.test.tsx",
                // random invalid file
                "invalid.html"
                ]);

        var analysisConfig = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [new("sonar.tests", "whatever")],
            ServerSettings =
            [
                new("sonar.javascript.file.suffixes", ".js,.jsx"),
                new("sonar.typescript.file.suffixes", ".ts,.tsx"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo([
            "file1.js",
            "file2.jsx",
            "file3.ts",
            "file4.tsx",
            "file5.spec.js",
            "file6.test.js",
            "file7.spec.jsx",
            "file8.test.jsx",
            "file9.spec.ts",
            "file10.test.ts",
            "file11.spec.tsx",
            "file12.test.tsx"
        ]);
        files.Tests.Should().BeEmpty();
    }
}
