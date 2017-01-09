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

using SonarQube.Common;
using SonarQube.TeamBuild.PreProcessor.Interfaces;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Factory that creates the various objects required by the pre-processor
    /// </summary>
    public interface IPreprocessorObjectFactory
    {
        /// <summary>
        /// Creates and returns the component that interacts with the SonarQube server
        /// </summary>
        /// <param name="args">Validated arguments</param>
        /// <remarks>It is the responsibility of the caller to dispose of the server, if necessary</remarks>
        ISonarQubeServer CreateSonarQubeServer(ProcessedArgs args, ILogger logger);

        /// <summary>
        /// Creates and returns the component to install the MSBuild targets
        /// </summary>
        ITargetsInstaller CreateTargetInstaller();

        /// <summary>
        /// Creates and returns the component that provisions the Roslyn analyzers
        /// </summary>
        IAnalyzerProvider CreateRoslynAnalyzerProvider(ILogger logger);

        IRulesetGenerator CreateRulesetGenerator();
    }
}
