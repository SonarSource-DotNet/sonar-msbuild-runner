﻿//-----------------------------------------------------------------------
// <copyright file="AnalysisPropertyFileProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Handles locating an analysis properties file and returning the appropriate properties
    /// </summary>
    public class AnalysisPropertyFileProvider : IAnalysisPropertyProvider
    {
        private const string DescriptorId = "properties.file.argument";

        public static readonly ArgumentDescriptor Descriptor = new ArgumentDescriptor(DescriptorId, new string[] { "/s:" }, false, Resources.CmdLine_ArgDescription_PropertiesFilePath, false);

        public const string DefaultFileName = "SonarQube.Analysis.xml";

        private readonly AnalysisProperties propertiesFile;

        #region Public methods

        /// <summary>
        /// Attempts to construct and return a file-based properties provider
        /// </summary>
        /// <param name="defaultPropertiesFileDirectory">Directory in which to look for the default properties file (optional)</param>
        /// <param name="commandLineArguments">List of command line arguments (optional)</param>
        /// <returns>False if errors occurred when constructing the provider, otherwise true</returns>
        /// <remarks>If a properties file could not be located then an empty provider will be returned</remarks>
        public static bool TryCreateProvider(IEnumerable<ArgumentInstance> commandLineArguments, string defaultPropertiesFileDirectory, ILogger logger, out AnalysisPropertyFileProvider provider)
        {
            if (commandLineArguments == null)
            {
                throw new ArgumentNullException("commandLineArguments");
            }
            if (string.IsNullOrWhiteSpace(defaultPropertiesFileDirectory))
            {
                throw new ArgumentNullException("defaultDirectory");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            // If the path to a properties file was specified on the command line, use that.
            // Otherwise, look for a default properties file in the default directory.

            string propertiesFilePath;
            ArgumentInstance.TryGetArgumentValue(DescriptorId, commandLineArguments, out propertiesFilePath);

            AnalysisProperties locatedPropertiesFile;
            if (TryGetPropertiesFile(propertiesFilePath, defaultPropertiesFileDirectory, logger, out locatedPropertiesFile) && locatedPropertiesFile != null)
            {
                provider = new AnalysisPropertyFileProvider(locatedPropertiesFile);
                return true;
            }

            provider = null;
            return false;
        }

        public AnalysisProperties PropertiesFile {  get { return this.propertiesFile; } }

        #endregion

        #region IAnalysisPropertyProvider methods

        public IEnumerable<Property> GetAllProperties()
        {
            return this.propertiesFile ?? Enumerable.Empty<Property>();
        }

        public bool TryGetProperty(string key, out Property property)
        {
            return Property.TryGetProperty(key, this.propertiesFile, out property);
        }

        #endregion

        #region Private methods

        private AnalysisPropertyFileProvider(AnalysisProperties properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }
            this.propertiesFile = properties;
        }

        /// <summary>
        /// Attempt to find a properties file - either the one specified by the user, or the default properties file.
        /// Returns false if a path is specified to a file that does not exist, otherwise returns true.
        /// </summary>
        private static bool TryGetPropertiesFile(string propertiesFilePath, string defaultPropertiesFileDirectory, ILogger logger, out AnalysisProperties properties)
        {
            properties = null;
            bool isValid = true;

            string resolvedPath = propertiesFilePath ?? TryGetDefaultPropertiesFilePath(defaultPropertiesFileDirectory, logger);

            if (resolvedPath != null)
            {
                if (File.Exists(resolvedPath))
                {
                    try
                    {
                        logger.LogMessage(Resources.DIAG_Properties_LoadingPropertiesFromFile, resolvedPath);
                        properties = AnalysisProperties.Load(resolvedPath);
                    }
                    catch (InvalidOperationException)
                    {
                        logger.LogError(Resources.ERROR_Properties_InvalidPropertiesFile, resolvedPath);
                        isValid = false;
                    }
                }
                else
                {
                    logger.LogError(Resources.ERROR_Properties_GlobalPropertiesFileDoesNotExist, resolvedPath);
                }
            }
            return isValid;
        }

        private static string TryGetDefaultPropertiesFilePath(string defaultDirectory, ILogger logger)
        {
            string fullPath = Path.Combine(defaultDirectory, DefaultFileName);
            if (File.Exists(fullPath))
            {
                logger.LogMessage(Resources.DIAG_Properties_DefaultPropertiesFileFound, fullPath);
                return fullPath;
            }
            else
            {
                logger.LogMessage(Resources.DIAG_Properties_DefaultPropertiesFileNotFound, fullPath);

                return null;
            }
        }

        #endregion
    }
}
