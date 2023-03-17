﻿/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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
using System.Net.Http;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class WebClientDownloaderBuilderTest
    {
        private const string BaseAddress = "https://sonarsource.com/";
        private TestLogger logger;

        [TestInitialize]
        public void TestInitialize() =>
            logger = new TestLogger();

        [TestMethod]
        [DataRow(null, null, null)]
        [DataRow(null, "password", null)]
        [DataRow("da39a3ee5e6b4b0d3255bfef95601890afd80709", null, "Basic ZGEzOWEzZWU1ZTZiNGIwZDMyNTViZmVmOTU2MDE4OTBhZmQ4MDcwOTo=")]
        [DataRow("da39a3ee5e6b4b0d3255bfef95601890afd80709", "", "Basic ZGEzOWEzZWU1ZTZiNGIwZDMyNTViZmVmOTU2MDE4OTBhZmQ4MDcwOTo=")]
        [DataRow("admin", "password", "Basic YWRtaW46cGFzc3dvcmQ=")]
        public void Build_WithAuthorization_ShouldHaveAuthorizationHeader(string username, string password, string expected)
        {
            var sut = new WebClientDownloaderBuilder(BaseAddress, logger).AddAuthorization(username, password);

            var result = sut.Build();

            GetHeader(result, "Authorization").Should().Be(expected);
        }

        [TestMethod]
        public void Build_BasicBuild_UserAgentShouldBeSet()
        {
            var scannerVersion = typeof(WebClientDownloaderTest).Assembly.GetName().Version.ToDisplayString();
            var sut = new WebClientDownloaderBuilder(BaseAddress, logger);

            var result = sut.Build();

            GetHeader(result, "User-Agent").Should().Be($"SonarScanner-for-.NET/{scannerVersion}");
            // This asserts wrong "UserAgent" header. Should be removed as part of https://github.com/SonarSource/sonar-scanner-msbuild/issues/1421
            GetHeader(result, "UserAgent").Should().Be($"ScannerMSBuild/{scannerVersion}");
        }

        [TestMethod]
        public void Build_UserNameWithSemiColon_ShouldThrow()
        {
            Action act = () => new WebClientDownloaderBuilder(BaseAddress, logger).AddAuthorization("admin:name", string.Empty);

            act.Should().ThrowExactly<ArgumentException>().WithMessage("username cannot contain the ':' character due to basic authentication limitations");
        }

        [TestMethod]
        [DataRow("héhé")]
        [DataRow("hàhà")]
        [DataRow("hèhè")]
        [DataRow("hùhù")]
        [DataRow("hûhû")]
        [DataRow("hähä")]
        [DataRow("höhö")]
        public void Build_NonAsciiUserName_ShouldThrow(string userName)
        {
            Action act = () => new WebClientDownloaderBuilder(BaseAddress, logger).AddAuthorization(userName, "password");
            act.Should().ThrowExactly<ArgumentException>().WithMessage("username and password should contain only ASCII characters due to basic authentication limitations");
        }

        [TestMethod]
        [DataRow("héhé")]
        [DataRow("hàhà")]
        [DataRow("hèhè")]
        [DataRow("hùhù")]
        [DataRow("hûhû")]
        [DataRow("hähä")]
        [DataRow("höhö")]
        public void Build_NonAsciiPassword_ShouldThrow(string password)
        {
            Action act = () => new WebClientDownloaderBuilder(BaseAddress, logger).AddAuthorization("userName", password);
            act.Should().ThrowExactly<ArgumentException>().WithMessage("username and password should contain only ASCII characters due to basic authentication limitations");
        }

        [TestMethod]
        public void Build_AddCertificate_ShouldNotThrow() =>
            FluentActions.Invoking(() => new WebClientDownloaderBuilder(BaseAddress, logger).AddCertificate("certtestsonar.pem", "dummypw")).Should().NotThrow();

        [TestMethod]
        public void Build_MissingCertificate_ShouldThrow() =>
            FluentActions.Invoking(() => new WebClientDownloaderBuilder(BaseAddress, logger).AddCertificate("missingcert.pem", "dummypw")).Should().Throw<CryptographicException>();

        private static string GetHeader(WebClientDownloader downloader, string header)
        {
            var client = (HttpClient)new PrivateObject(downloader).GetField("client");
            return client.DefaultRequestHeaders.Contains(header)
                ? string.Join(";", client.DefaultRequestHeaders.GetValues(header))
                : null;
        }
    }
}