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
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.IO;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class RulesetGeneratorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void RulesetGet_Simple()
        {
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string rulesetFilePath = Path.Combine(testDir, "r1.txt");

            List<ActiveRule> activeRules = new List<ActiveRule>();
            activeRules.Add(new ActiveRule("repo1", "repo1.aaa.r1.internal"));
            activeRules.Add(new ActiveRule("repo1", "repo1.aaa.r2.internal"));
            activeRules.Add(new ActiveRule("repo1", "repo1.aaa.r3.internal"));
            activeRules.Add(new ActiveRule("repo2", "repo2.aaa.r4.internal"));

            RulesetGenerator generator = new RulesetGenerator();
            generator.Generate("repo1", activeRules, rulesetFilePath);

            PreProcessAsserts.AssertRuleSetContainsRules(rulesetFilePath,
                "repo1.aaa.r1.internal", "repo1.aaa.r2.internal", "repo1.aaa.r3.internal");
        }

        [TestMethod]
        public void RulesetGet_NoRules()
        {
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string rulesetFilePath = Path.Combine(testDir, "r1.txt");

            List<ActiveRule> activeRules = new List<ActiveRule>();
            RulesetGenerator generator = new RulesetGenerator();
            generator.Generate("repo1", activeRules, rulesetFilePath);

            AssertFileDoesNotExist(rulesetFilePath);
        }

        [TestMethod]
        public void RulesetGet_ValidateArgs()
        {
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string rulesetFilePath = Path.Combine(testDir, "r1.txt");

            List<ActiveRule> activeRules = new List<ActiveRule>();

            RulesetGenerator generator = new RulesetGenerator();
            AssertException.Expects<ArgumentNullException>(() => generator.Generate("repo1", null, rulesetFilePath));
            AssertException.Expects<ArgumentNullException>(() => generator.Generate(null, activeRules, rulesetFilePath));
            AssertException.Expects<ArgumentNullException>(() => generator.Generate("repo1", activeRules, null));
        }

        #endregion

        #region Checks

        private static void AssertFileDoesNotExist(string filePath)
        {
            Assert.IsFalse(File.Exists(filePath), "Not expecting file to exist: {0}", filePath);
        }

        #endregion
    }
}
