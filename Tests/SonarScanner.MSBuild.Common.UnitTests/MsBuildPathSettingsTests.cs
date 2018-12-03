﻿/*
 * SonarScanner for MSBuild
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

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Common.UnitTests
{
    [TestClass]
    public class MsBuildPathSettingsTests
    {
        [TestMethod]
        public void GetImportBeforePaths_NonWindows_AppData_Is_NullOrEmpty()
        {
            Action action;

            action = new Action(() => new MsBuildPathSettings((folder, o) => string.Empty, IsWindows(false), FileExists(true)).GetImportBeforePaths());
            action.Should().ThrowExactly<IOException>().WithMessage("Cannot find local application data directory.");

            action = new Action(() => new MsBuildPathSettings((folder, o) => null, IsWindows(false), FileExists(true)).GetImportBeforePaths());
            action.Should().ThrowExactly<IOException>().WithMessage("Cannot find local application data directory.");
        }

        [TestMethod]
        public void GetImportBeforePaths_NonWindows_User_Account()
        {
            var paths = new[]
            {
                (Environment.SpecialFolder.LocalApplicationData, "c:\\app data"),
                (Environment.SpecialFolder.UserProfile, "c:\\user profile"),
            };

            var settings = new MsBuildPathSettings(GetFolderPath(paths), IsWindows(false), FileExists(true));

            var result = settings.GetImportBeforePaths();

            result.Should().HaveCount(7);
            result.Should().Contain(
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\use profile", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore")
                );
        }

        [TestMethod]
        public void GetImportBeforePaths_NonWindows_User_Account_UserProfile_Missing()
        {
            var paths = new[]
            {
                (Environment.SpecialFolder.LocalApplicationData, "c:\\app data"),
                (Environment.SpecialFolder.UserProfile, string.Empty),
            };

            var settings = new MsBuildPathSettings(GetFolderPath(paths), IsWindows(false), FileExists(true));

            var action = new Action(() => settings.GetImportBeforePaths());

            action.Should().ThrowExactly<IOException>().WithMessage("Cannot find user profile directory.");
        }

        [TestMethod]
        public void GetImportBeforePaths_Windows_AppData_Is_NullOrEmpty()
        {
            Action action;

            action = new Action(() => new MsBuildPathSettings((folder, o) => string.Empty, IsWindows(true), FileExists(true)).GetImportBeforePaths());
            action.Should().ThrowExactly<IOException>().WithMessage("Cannot find local application data directory.");

            action = new Action(() => new MsBuildPathSettings((folder, o) => null, IsWindows(true), FileExists(true)).GetImportBeforePaths());
            action.Should().ThrowExactly<IOException>().WithMessage("Cannot find local application data directory.");
        }

        [TestMethod]
        public void GetImportBeforePaths_Windows_User_Account()
        {
            var paths = new[]
            {
                (Environment.SpecialFolder.LocalApplicationData, "c:\\app data"),
                (Environment.SpecialFolder.System, "c:\\windows\\system32"),
                (Environment.SpecialFolder.SystemX86, "c:\\windows\\systemWOW64"),
            };

            var settings = new MsBuildPathSettings(GetFolderPath(paths), IsWindows(true), FileExists(true));

            var result = settings.GetImportBeforePaths();

            result.Should().HaveCount(6);
            result.Should().Contain(
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"));
        }

        [TestMethod]
        public void GetImportBeforePaths_Windows_System_Account()
        {
            var paths = new[]
            {
                (Environment.SpecialFolder.LocalApplicationData, "c:\\windows\\system32\\app data"),
                (Environment.SpecialFolder.System, "c:\\windows\\system32"),
                (Environment.SpecialFolder.SystemX86, "c:\\windows\\systemWOW64"),
            };

            var settings = new MsBuildPathSettings(GetFolderPath(paths), IsWindows(true), FileExists(true));

            var result = settings.GetImportBeforePaths();

            result.Should().HaveCount(18);
            result.Should().Contain(
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),

                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),

                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore")
                );
        }

        [TestMethod]
        public void GetImportBeforePaths_Windows_System_Account_WOW64_Missing()
        {
            var paths = new[]
            {
                (Environment.SpecialFolder.LocalApplicationData, "c:\\windows\\system32\\app data"),
                (Environment.SpecialFolder.System, "c:\\windows\\system32"),
                (Environment.SpecialFolder.SystemX86, "c:\\windows\\systemWOW64"),
            };

            var settings = new MsBuildPathSettings(GetFolderPath(paths), IsWindows(true), path => !path.Contains("WOW64")); // paths with wow64 do not exist, others exist

            var result = settings.GetImportBeforePaths();

            result.Should().HaveCount(12);
            result.Should().Contain(
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),

                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysnative\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore")
                );
        }

        [TestMethod]
        public void GetImportBeforePaths_Windows_System_Account_Sysnative_Missing()
        {
            var paths = new[]
            {
                (Environment.SpecialFolder.LocalApplicationData, "c:\\windows\\system32\\app data"),
                (Environment.SpecialFolder.System, "c:\\windows\\system32"),
                (Environment.SpecialFolder.SystemX86, "c:\\windows\\systemWOW64"),
            };

            var settings = new MsBuildPathSettings(GetFolderPath(paths), IsWindows(true), path => !path.Contains("Sysnative")); // paths with Sysnative do not exist, others exist

            var result = settings.GetImportBeforePaths();

            result.Should().HaveCount(12);
            result.Should().Contain(
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),

                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore")
                );
        }

        [TestMethod]
        public void GetGlobalTargetsPaths_WhenProgramFilesIsEmptyOrNull_Returns_Empty()
        {
            // Arrange
            var testSubject1 = new MsBuildPathSettings((x, y) => x == Environment.SpecialFolder.ProgramFiles ? null : "foo", () => true, (path) => true);
            var testSubject2 = new MsBuildPathSettings((x, y) => x == Environment.SpecialFolder.ProgramFiles ? "" : "foo", () => true, (path) => true);

            // Act
            testSubject1.GetGlobalTargetsPaths().Should().BeEmpty();
            testSubject2.GetGlobalTargetsPaths().Should().BeEmpty();
        }

        [TestMethod]
        public void GetGlobalTargetsPaths_WhenProgramFilesNotEmpty_ReturnsExpectedPaths()
        {
            var paths = new[]
            {
                (Environment.SpecialFolder.ProgramFiles, "C:\\Program")
            };

            // Arrange
            var settings = new MsBuildPathSettings(GetFolderPath(paths), IsWindows(true), FileExists(true));

            // Act
            var result = settings.GetGlobalTargetsPaths().ToList();

            // Assert
            result.Should().HaveCount(1);
            result.Should().ContainInOrder(
                "C:\\Program\\MSBuild\\14.0\\Microsoft.Common.Targets\\ImportBefore");
        }

        private static Func<bool> IsWindows(bool result) => () => result;
        private static Func<string, bool> FileExists(bool result) => path => result;
        private static Func<Environment.SpecialFolder, Environment.SpecialFolderOption, string> GetFolderPath(params (Environment.SpecialFolder, string)[] paths) =>
            (folder, option) => paths.First(p => p.Item1 == folder).Item2;
    }
}
