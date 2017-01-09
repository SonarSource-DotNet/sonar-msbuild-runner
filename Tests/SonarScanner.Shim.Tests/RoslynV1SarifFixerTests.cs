/*
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
using SonarScanner.Shim;
using System;
using System.IO;
using TestUtilities;

namespace SonarQube.Shim.Tests
{
    [TestClass]
    public class RoslynV1SarifFixerTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        /// <summary>
        /// There should be no change to input if it is already valid, as attempting to fix valid SARIF may cause over-escaping.
        /// This should be the case even if the output came from VS 2015 RTM.
        /// </summary>
        [TestMethod]
        public void SarifFixer_ShouldNotChange_Valid()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
      ""shortMessage"": ""Test shortMessage. It features \""quoted text\""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage, logger);

            // Assert
            // already valid -> no change to file, same file path returned
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.AreEqual(testSarifPath, returnedSarifPath);
        }

        [TestMethod]
        public void SarifFixer_ShouldNotChange_Unfixable()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    { 

}}}}}}}}}}}}}}}}}}}}}}}}}

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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage, logger);

            // Assert
            // unfixable -> no change to file, null return
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNull(returnedSarifPath);
        }

        /// <summary>
        /// The current solution cannot fix values spanning multiple fields. As such it should not attempt to.
        /// 
        /// Example invalid:
        /// "fullMessage": "message 
        /// \test\ ["_"]",
        /// </summary>
        [TestMethod]
        public void SarifFixer_ShouldNotChange_MultipleLineValues()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""shortMessage"": ""Test shortMessage. 
It features ""quoted text""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage, logger);

            // Assert
            // unfixable -> no change to file, null return
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNull(returnedSarifPath);
        }

        [TestMethod]
        public void SarifFixer_ShouldChange_EscapeBackslashes()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage, logger);

            // Assert
            // fixable -> no change to file, file path in return value, file contents as expected
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNotNull(returnedSarifPath);

            string returnedSarifString = File.ReadAllText(returnedSarifPath);
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
}", returnedSarifString);
        }

        [TestMethod]
        public void SarifFixer_ShouldChange_EscapeQuotes()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage, logger);

            // Assert
            // fixable -> no change to file, file path in return value, file contents as expected
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNotNull(returnedSarifPath);

            string returnedSarifString = File.ReadAllText(returnedSarifPath);
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
}", returnedSarifString);
        }

        [TestMethod]
        public void SarifFixer_ShouldChange_EscapeCharsInAllAffectedFields()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
      ""shortMessage"": ""Test shortMessage. It features ""quoted text"" and has \slashes."",
      ""fullMessage"": ""Test fullMessage. It features ""quoted text"" and has \slashes."",
      ""properties"": {
        ""severity"": ""Info"",
        ""title"": ""Test title. It features ""quoted text"" and has \slashes."",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage, logger);

            // Assert
            // fixable -> no change to file, file path in return value, file contents as expected
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNotNull(returnedSarifPath);

            string returnedSarifString = File.ReadAllText(returnedSarifPath);
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
      ""shortMessage"": ""Test shortMessage. It features \""quoted text\"" and has \\slashes."",
      ""fullMessage"": ""Test fullMessage. It features \""quoted text\"" and has \\slashes."",
      ""properties"": {
        ""severity"": ""Info"",
        ""title"": ""Test title. It features \""quoted text\"" and has \\slashes."",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}", returnedSarifString);
        }

        [TestMethod]
        public void SarifFixer_VBNet()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual Basic Compiler"",
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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.VBNetLanguage, logger);

            // Assert
            // fixable -> no change to file, file path in return value, file contents as expected
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNotNull(returnedSarifPath);

            string returnedSarifString = File.ReadAllText(returnedSarifPath);
            Assert.AreEqual(@"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual Basic Compiler"",
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
}", returnedSarifString);

        }

        /// <summary>
        /// To avoid FPs, the tool name declared in the file is compared with the language. If it doesn't match, do nothing.
        /// </summary>
        [TestMethod]
        public void SarifFixer_ShouldNotFixInvalid()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.VBNetLanguage, logger);
            Assert.IsNull(returnedSarifPath);
        }

        #endregion

        #region Private Methods

        private void AssertFileUnchanged(string filePath, DateTime originalWriteTime)
        {
            Assert.AreEqual(originalWriteTime, new FileInfo(filePath).LastWriteTime);
        }

        #endregion
    }
}
