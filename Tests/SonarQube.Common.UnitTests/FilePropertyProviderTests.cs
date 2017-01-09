﻿/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class FilePropertyProviderTests
    {
        private static readonly ArgumentDescriptor DummyDescriptor = new ArgumentDescriptor("dummy", new string[] { "dummy predifx" }, false, "dummy desc", true);

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_InvalidArguments()
        {
            // 0. Setup
            IAnalysisPropertyProvider provider;

            // 1. Null command line arguments
            AssertException.Expects<ArgumentNullException>(() => FilePropertyProvider.TryCreateProvider(null, string.Empty, new TestLogger(), out provider));

            // 2. Null directory
            AssertException.Expects<ArgumentNullException>(() => FilePropertyProvider.TryCreateProvider(Enumerable.Empty<ArgumentInstance>(), null, new TestLogger(), out provider));

            // 3. Null logger
            AssertException.Expects<ArgumentNullException>(() => FilePropertyProvider.TryCreateProvider(Enumerable.Empty<ArgumentInstance>(), string.Empty, null, out provider));
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_NoFileArguments()
        {
            // Arrange
            IAnalysisPropertyProvider provider;
            TestLogger logger = new TestLogger();
            string defaultPropertiesDir = this.TestContext.TestDeploymentDir;

            // Act
            provider = CheckProcessingSucceeds(Enumerable.Empty<ArgumentInstance>(), defaultPropertiesDir, logger);

            // Assert
            Assert.IsNotNull(provider, "Expecting a provider to have been created");
            Assert.AreEqual(0, provider.GetAllProperties().Count(), "Not expecting the provider to return any properties");
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_UseDefaultPropertiesFile()
        {
            // Arrange
            string defaultPropertiesDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string validPropertiesFile = CreateValidPropertiesFile(defaultPropertiesDir, FilePropertyProvider.DefaultFileName, "key1", "value1");
            TestLogger logger = new TestLogger();

            IList<ArgumentInstance> args = new List<ArgumentInstance>();

            // Act
            IAnalysisPropertyProvider provider = CheckProcessingSucceeds(args, defaultPropertiesDir, logger);

            // Assert
            AssertExpectedPropertiesFile(validPropertiesFile, provider);
            provider.AssertExpectedPropertyValue("key1", "value1");
            AssertIsDefaultPropertiesFile(provider);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_UseSpecifiedPropertiesFile()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string validPropertiesFile = CreateValidPropertiesFile(testDir, "myPropertiesFile.xml", "xxx", "value with spaces");

            string defaultPropertiesDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "Default");
            CreateFile(defaultPropertiesDir, FilePropertyProvider.DefaultFileName, "invalid file - will error if this file is loaded");

            IList<ArgumentInstance> args = new List<ArgumentInstance>();

            args.Add(new ArgumentInstance(FilePropertyProvider.Descriptor, validPropertiesFile));

            TestLogger logger = new TestLogger();

            // Act
            IAnalysisPropertyProvider provider = CheckProcessingSucceeds(args, defaultPropertiesDir, logger);

            // Assert
            AssertExpectedPropertiesFile(validPropertiesFile, provider);
            provider.AssertExpectedPropertyValue("xxx", "value with spaces");
            AssertIsNotDefaultPropertiesFile(provider);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_MissingPropertiesFile()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string defaultPropertiesDir = this.TestContext.DeploymentDirectory;

            IList<ArgumentInstance> args = new List<ArgumentInstance>();
            args.Add(new ArgumentInstance(FilePropertyProvider.Descriptor, "missingFile.txt"));

            // Act
            CheckProcessingFails(args, defaultPropertiesDir, logger);

            // Assert
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("missingFile.txt");
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_InvalidDefaultPropertiesFile()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string defaultPropertiesDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string invalidFile = CreateFile(defaultPropertiesDir, FilePropertyProvider.DefaultFileName, "not a valid XML properties file");

            IList<ArgumentInstance> args = new List<ArgumentInstance>();

            // Act
            CheckProcessingFails(args, defaultPropertiesDir, logger);

            // Assert
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists(invalidFile);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_InvalidSpecifiedPropertiesFile()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string defaultPropertiesDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string invalidFile = CreateFile(defaultPropertiesDir, "invalidPropertiesFile.txt", "not a valid XML properties file");

            IList<ArgumentInstance> args = new List<ArgumentInstance>();
            args.Add(new ArgumentInstance(FilePropertyProvider.Descriptor, invalidFile));

            // Act
            CheckProcessingFails(args, defaultPropertiesDir, logger);

            // Assert
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists(invalidFile);
        }

        #endregion

        #region Private methods

        private static string CreateFile(string path, string fileName, string content)
        {
            string fullPath = Path.Combine(path, fileName);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        /// <summary>
        /// Creates a valid properties file with a single property
        /// </summary>
        private static string CreateValidPropertiesFile(string path, string fileName, string property, string value)
        {
            string fullPath = Path.Combine(path, fileName);

            AnalysisProperties properties = new AnalysisProperties();

            properties.Add(new Property() { Id = property, Value = value });

            properties.Save(fullPath);
            return fullPath;
        }

        private static void AddProperty(IList<Property> properties, string key, string value)
        {
            properties.Add(new Property() { Id = key, Value = value });
        }

        #endregion

        #region Checks

        private static IAnalysisPropertyProvider CheckProcessingSucceeds(IEnumerable<ArgumentInstance> cmdLineArgs, string defaultPropertiesDirectory, TestLogger logger)
        {
            IAnalysisPropertyProvider provider;
            bool isValid = FilePropertyProvider.TryCreateProvider(cmdLineArgs, defaultPropertiesDirectory, logger, out provider);
            
            Assert.IsTrue(isValid, "Expecting the provider to be initialized successfully");
            Assert.IsNotNull(provider, "Not expecting a null provider if the function returned true");
            logger.AssertErrorsLogged(0);

            return provider;
        }

        private static void CheckProcessingFails(IEnumerable<ArgumentInstance> cmdLineArgs, string defaultPropertiesDirectory, TestLogger logger)
        {
            IAnalysisPropertyProvider provider;
            bool isValid = FilePropertyProvider.TryCreateProvider(cmdLineArgs, defaultPropertiesDirectory, logger, out provider);

            Assert.IsFalse(isValid, "Not expecting the provider to be initialized successfully");
            Assert.IsNull(provider, "Not expecting a provider instance if the function returned true");
            logger.AssertErrorsLogged();
        }

        private static void AssertExpectedPropertiesFile(string expectedFilePath, IAnalysisPropertyProvider actualProvider)
        {
            FilePropertyProvider fileProvider = AssertIsFilePropertyProvider(actualProvider);

            Assert.IsNotNull(fileProvider.PropertiesFile, "Properties file object should not be null");
            Assert.AreEqual(expectedFilePath, fileProvider.PropertiesFile.FilePath, "Properties were not loaded from the expected location");
        }
        
        private static void AssertIsDefaultPropertiesFile(IAnalysisPropertyProvider actualProvider)
        {
            FilePropertyProvider fileProvider = AssertIsFilePropertyProvider(actualProvider);
            Assert.IsTrue(fileProvider.IsDefaultSettingsFile, "Expecting the provider to be marked as using the default properties file");
        }

        private static void AssertIsNotDefaultPropertiesFile(IAnalysisPropertyProvider actualProvider)
        {
            FilePropertyProvider fileProvider = AssertIsFilePropertyProvider(actualProvider);
            Assert.IsFalse(fileProvider.IsDefaultSettingsFile, "Not expecting the provider to be marked as using the default properties file");
        }

        private static FilePropertyProvider AssertIsFilePropertyProvider(IAnalysisPropertyProvider actualProvider)
        {
            Assert.IsNotNull(actualProvider, "Supplied provider should not be null");
            Assert.IsInstanceOfType(actualProvider, typeof(FilePropertyProvider), "Expecting a file provider");

            return (FilePropertyProvider)actualProvider;
        }

        #endregion
    }
}