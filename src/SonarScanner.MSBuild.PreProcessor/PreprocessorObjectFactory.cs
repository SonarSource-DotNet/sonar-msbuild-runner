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
using System.Linq;
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.PreProcessor.WebServer;

namespace SonarScanner.MSBuild.PreProcessor
{
    /// <summary>
    /// Default implementation of the object factory interface that returns the product implementations of the required classes.
    /// </summary>
    /// <remarks>
    /// Note: the factory is stateful and expects objects to be requested in the order they are used.
    /// </remarks>
    public class PreprocessorObjectFactory : IPreprocessorObjectFactory
    {
        private readonly ILogger logger;

        public PreprocessorObjectFactory(ILogger logger) =>
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<ISonarWebServer> CreateSonarWebServer(ProcessedArgs args, IDownloader downloader = null)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            var userName = args.GetSetting(SonarProperties.SonarUserName, null);
            var password = args.GetSetting(SonarProperties.SonarPassword, null);
            var clientCertPath = args.GetSetting(SonarProperties.ClientCertPath, null);
            var clientCertPassword = args.GetSetting(SonarProperties.ClientCertPassword, null);

            // If the baseUri has relative parts (like "/api"), then the relative part must be terminated with a slash, (like "/api/"),
            // if the relative part of baseUri is to be preserved in the constructed Uri.
            // See: https://learn.microsoft.com/en-us/dotnet/api/system.uri.-ctor?view=net-7.0
            var serverUri = WebUtils.CreateUri(args.SonarQubeUrl);

            downloader ??= new WebClientDownloaderBuilder(args.SonarQubeUrl, logger)
                            .AddAuthorization(userName, password)
                            .AddCertificate(clientCertPath, clientCertPassword)
                            .Build();

            var serverVersion = await QueryServerVersion(downloader);
            if (serverVersion == null)
            {
                return null;
            }

            return SonarProduct.IsSonarCloud(serverUri.Host, serverVersion)
                       ? new SonarCloudWebServer(downloader, serverVersion, logger, args.Organization)
                       : new SonarQubeWebServer(downloader, serverVersion, logger, args.Organization);
        }

        public ITargetsInstaller CreateTargetInstaller() =>
            new TargetsInstaller(logger);

        public IAnalyzerProvider CreateRoslynAnalyzerProvider(ISonarWebServer server)
        {
            server = server ?? throw new ArgumentNullException(nameof(server));
            return new RoslynAnalyzerProvider(new EmbeddedAnalyzerInstaller(server, logger), logger);
        }

        private async Task<Version> QueryServerVersion(IDownloader downloader)
        {
            logger.LogDebug(Resources.MSG_FetchingVersion);
            try
            {
                var contents = await downloader.Download("api/server/version");
                return new Version(contents.Split('-').First());
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
