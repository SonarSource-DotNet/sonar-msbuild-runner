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

namespace SonarScanner.MSBuild.Common;

public class EnvironmentBasedPlatformHelper : IPlatformHelper
{
    public static IPlatformHelper Instance { get; } = new EnvironmentBasedPlatformHelper();

    private EnvironmentBasedPlatformHelper()
    {
    }

    public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => Environment.GetFolderPath(folder, option);
    public bool DirectoryExists(string path) => System.IO.Directory.Exists(path);
    public bool IsWindows() => Environment.OSVersion.Platform == PlatformID.Win32NT;
    public bool IsMacOSX() => Environment.OSVersion.Platform == PlatformID.MacOSX;
}
