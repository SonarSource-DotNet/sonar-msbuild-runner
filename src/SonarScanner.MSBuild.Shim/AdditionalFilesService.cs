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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim;

public class AdditionalFilesService(IDirectoryWrapper directoryWrapper) : IAdditionalFilesService
{
    private const char Comma = ',';

    private static readonly List<string> SupportedLanguages =
    [
        "sonar.tsql.file.suffixes",
        "sonar.plsql.file.suffixes",
        "sonar.yaml.file.suffixes",
        "sonar.xml.file.suffixes",
        "sonar.json.file.suffixes",
        "sonar.css.file.suffixes",
        "sonar.html.file.suffixes",
        "sonar.javascript.file.suffixes",
        "sonar.typescript.file.suffixes"
    ];

    private static readonly List<string> SupportedTestLanguages =
    [
        "sonar.javascript.file.suffixes",
        "sonar.typescript.file.suffixes"
    ];

    private static readonly List<string> SupportedTestInfixes =
    [
        "test",
        "spec"
    ];

    public AdditionalFiles AdditionalFiles(AnalysisConfig analysisConfig, DirectoryInfo projectBaseDir)
    {
        var extensions = GetExtensions(analysisConfig.ServerSettings);
        if (extensions.Length == 0)
        {
            return new([], []);
        }
        var allFiles = GetAllFiles(extensions, projectBaseDir);
        // Respect user defined sonar.tests and do not re-populate it.
        // This might lead to some files considered as both source and test, in which case the user should exclude them via sonar.exclusions.
        return HasUserSpecifiedSonarTests(analysisConfig)
            ? new(allFiles, [])
            : PartitionAdditionalFiles(allFiles, analysisConfig);
    }

    private List<FileInfo> GetAllFiles(IEnumerable<string> extensions, DirectoryInfo projectBaseDir) =>
        directoryWrapper
            .EnumerateFiles(projectBaseDir.FullName, "*", SearchOption.AllDirectories)
            .Select(x => new FileInfo(x))
            .Where(x => extensions.Any(e => x.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase) && !x.Name.Equals(e, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    private static bool HasUserSpecifiedSonarTests(AnalysisConfig analysisConfig) =>
        analysisConfig.LocalSettings.Exists(x => x.Id == SonarProperties.Tests);

    private static AdditionalFiles PartitionAdditionalFiles(List<FileInfo> allFiles, AnalysisConfig analysisConfig)
    {
        var testExtensions = GetTestExtensions(analysisConfig.ServerSettings);
        if (testExtensions.Length == 0)
        {
            return new(allFiles, []);
        }
        var sources = new List<FileInfo>();
        var tests = new List<FileInfo>();
        foreach (var file in allFiles)
        {
            if (Array.Exists(testExtensions, x => file.Name.EndsWith(x) && !file.Name.Equals(x)))
            {
                tests.Add(file);
            }
            else
            {
                sources.Add(file);
            }
        }
        return new(sources, tests);
    }

    private static string[] GetTestExtensions(AnalysisProperties properties) =>
        properties is null
            ? []
            : properties
                .Where(x => SupportedTestLanguages.Contains(x.Id))
                .SelectMany(x => x.Value.Split([Comma], StringSplitOptions.RemoveEmptyEntries))
                .SelectMany(x => SupportedTestInfixes.Select(infix => $".{infix}{EnsureDot(x)}"))
                .Distinct()
                .ToArray();

    private static string[] GetExtensions(AnalysisProperties properties) =>
        properties is null
            ? []
            : SupportedLanguages
                .Select(x => properties.Find(property => property.Id == x))
                .Where(x => x is {Value: { } })
                .SelectMany(x => x.Value.Split([Comma], StringSplitOptions.RemoveEmptyEntries).Select(EnsureDot))
                .ToArray();

    private static string EnsureDot(string x)
    {
        x = x.Trim();
        return x.StartsWith(".") ? x : $".{x}";
    }
}

public sealed class AdditionalFiles(ICollection<FileInfo> sources, ICollection<FileInfo> tests)
{
    public ICollection<FileInfo> Sources { get; } = sources;
    public ICollection<FileInfo> Tests { get; } = tests;
}
