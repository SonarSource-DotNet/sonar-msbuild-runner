﻿/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SonarScanner.MSBuild.Shim.IgnoredIssues
{
    public static class SarifIssueRemover04
    {
        private const string RunLogs = "runLogs";
        private const string Results = "results";
        private const string RuleId = "ruleId";

        public static bool Filter(JObject issuesJson, ISet<string> sonarRuleIds)
        {
            var tokensToRemove = new List<JToken>();
            if (issuesJson.ContainsKey(RunLogs)
                && issuesJson.GetValue(RunLogs)?.First[Results] != null)
            {
                tokensToRemove.AddRange(
                    issuesJson.GetValue(RunLogs).First[Results].Where(x => x[RuleId].Value<string>() != null
                                                                           && !sonarRuleIds.Contains(x[RuleId].Value<string>())));
                tokensToRemove.ForEach(x => x.Remove());
            }
            return tokensToRemove.Any();
        }
    }
}
