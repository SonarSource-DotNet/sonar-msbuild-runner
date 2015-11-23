//-----------------------------------------------------------------------
// <copyright file="SarifTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class SarifTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void Utilities_SarifCompilerVersionCheck_IsUnsupported()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  }
        }";

            // Act
            bool valid = Utilities.IsSarifFromUnsupportedCompiler(testSarif);

            // Assert
            Assert.IsTrue(valid, "Expecting the compiler version check to return true for unsupported version");
        }

        [TestMethod]
        public void Utilities_SarifCompilerVersionCheck_IsSupported()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.1.0"",
    ""fileVersion"": ""1.0.0""
  }
        }";

            // Act
            bool valid = Utilities.IsSarifFromUnsupportedCompiler(testSarif);

            // Assert
            Assert.IsFalse(valid, "Expecting the compiler version check to return false for supported version");
        }

        /// <summary>
        /// Tests whether the compiler version check can handle improper escaping, as the JSON parse will throw an exception.
        /// </summary>
        [TestMethod]
        public void Utilities_SarifCompilerVersionCheck_HasImproperEscaping()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Foo\Bar"",
    ""productVersion"": ""1.1.0"",
    ""fileVersion"": ""1.0.0""
  }
        }";

            // Act
            bool valid = Utilities.IsSarifFromUnsupportedCompiler(testSarif);

            // Assert
            Assert.IsFalse(valid, "Expecting the compiler version check to return false for supported version");
        }

        [TestMethod]
        public void Utilities_SarifCompilerVersionCheck_FailureNoToolInfo()
        {
            // Arrange
            string testSarif = @"{ }";

            // Act
            bool valid = Utilities.IsSarifFromUnsupportedCompiler(testSarif);

            // Assert
            Assert.IsFalse(valid, "Expecting the compiler version check to return false for supported version");
        }

        [TestMethod]
        public void Utilities_SarifCompilerVersionCheck_FailureInvalidToolInfo()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""foo""
  }
        }";

            // Act
            bool valid = Utilities.IsSarifFromUnsupportedCompiler(testSarif);

            // Assert
            Assert.IsFalse(valid, "Expecting the compiler version check to return false for supported version");
        }

        [TestMethod]
        public void Utilities_SarifFixer_ShouldNotChange()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.1.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs"",
}
          ]
        }
      ],
      ""shortMessage"": ""Test shortMessage. It features \""quoted text\""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";

            // Act
            string fixedSarif;
            bool changeApplied = Utilities.FixImproperlyEscapedSarif(testSarif, out fixedSarif);

            // Assert
            Assert.IsFalse(changeApplied);
            Assert.AreEqual(@"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.1.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs"",
}
          ]
        }
      ],
      ""shortMessage"": ""Test shortMessage. It features \""quoted text\""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}", fixedSarif);
        }

        [TestMethod]
        public void Utilities_SarifFixer_ShouldReplaceBackslashes()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\agent\_work\2\s\MyTestProj\Program.cs"",
}
          ]
        }
      ],
    }
  ]
}";

            // Act
            string fixedSarif;
            bool changeApplied = Utilities.FixImproperlyEscapedSarif(testSarif, out fixedSarif);

            // Assert
            Assert.IsTrue(changeApplied);
            Assert.AreEqual(@"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs"",
}
          ]
        }
      ],
    }
  ]
}", fixedSarif);
        }

        [TestMethod]
        public void Utilities_SarifFixer_ShouldReplaceQuotes()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""shortMessage"": ""Test shortMessage. It features ""quoted text""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";

            // Act
            string fixedSarif;
            bool changeApplied = Utilities.FixImproperlyEscapedSarif(testSarif, out fixedSarif);

            // Assert
            Assert.IsTrue(changeApplied);
            Assert.AreEqual(@"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""shortMessage"": ""Test shortMessage. It features \""quoted text\""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}", fixedSarif);
        }

        #endregion


    }
}
