﻿/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;
using SonarScanner.MSBuild.PreProcessor.WebServer;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class SonarWebServerTest
{
    private const string ProjectKey = "project-key";

    private readonly TestDownloader downloader;
    private readonly TestLogger logger;
    private readonly Version version;

    private SonarWebServerStub sut;

    public SonarWebServerTest()
    {
        downloader = new TestDownloader();
        version = new Version("9.9");
        logger = new TestLogger();
    }

    [TestInitialize]
    public void Init() =>
        sut = new SonarWebServerStub(downloader, version, logger, null);

    [TestCleanup]
    public void Cleanup() =>
        sut?.Dispose();

    [TestMethod]
    public void Ctor_Null_Throws()
    {
        ((Func<SonarWebServerStub>)(() => new SonarWebServerStub(null, version, logger, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("downloader");
        ((Func<SonarWebServerStub>)(() => new SonarWebServerStub(Substitute.For<IDownloader>(), null, logger, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("serverVersion");
        ((Func<SonarWebServerStub>)(() => new SonarWebServerStub(Substitute.For<IDownloader>(), version, null, null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public async Task DownloadQualityProfile_LogHttpError()
    {
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists($"api/qualityprofiles/search?project={ProjectKey}", Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(true, "trash")));
        sut = new SonarWebServerStub(downloaderMock, version, logger, null);

        Func<Task> action = async () => await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        await action.Should().ThrowAsync<Exception>();
    }

    [TestMethod]
    public async Task DownloadQualityProfile_InvalidOrganizationKey_After_Version63()
    {
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists($"api/qualityprofiles/search?project={ProjectKey}&organization=ThisIsInvalidValue", false)
            .Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        // SonarCloud returns 404, WebClientDownloader returns null
        downloaderMock.Download("api/qualityprofiles/search?defaults=true&organization=ThisIsInvalidValue", false)
            .Returns(Task.FromResult<string>(null));
        sut = new SonarWebServerStub(downloaderMock, new Version("6.4"), logger, "ThisIsInvalidValue");

        Func<Task> act = async () => await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        await act.Should().ThrowAsync<AnalysisException>().WithMessage("Cannot download quality profile. Check scanner arguments and the reported URL for more information.");
        logger.AssertErrorLogged("Cannot download quality profile. Check scanner arguments and the reported URL for more information.");
    }

    [TestMethod]
    [DataRow("foo bar")]
    public async Task DownloadQualityProfile_MainProjectProfile_QualityProfileFound(string projectKey)
    {
        const string profileKey = "profile1k";
        const string language = "cs";
        var qualityProfileUrl = $"api/qualityprofiles/search?project={WebUtility.UrlEncode(projectKey)}";
        var profileResponse = $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}";
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists(qualityProfileUrl, Arg.Any<bool>()).Returns(Task.FromResult(Tuple.Create(true, profileResponse)));
        sut = new SonarWebServerStub(downloaderMock, new Version("9.9"), logger, null);

        var result = await sut.DownloadQualityProfile(projectKey, null, language);

        result.Should().Be(profileKey);
    }

    [TestMethod]
    [DataRow("foo bar", "aBranch")]
    public async Task DownloadQualityProfile_BranchSpecificProfile_QualityProfileFound(string projectKey, string branchName)
    {
        const string profileKey = "profile1k";
        const string language = "cs";
        var qualityProfileUrl = $"api/qualityprofiles/search?project={WebUtility.UrlEncode($"{projectKey}:{branchName}")}";
        var downloadResult = Tuple.Create(true, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}");
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists(qualityProfileUrl, Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        sut = new SonarWebServerStub(downloaderMock, new Version("9.9"), logger, null);

        var result = await sut.DownloadQualityProfile(projectKey, branchName, language);

        result.Should().Be(profileKey);
    }

    [TestMethod]
    [DataRow("foo bar", "my org")]
    public async Task DownloadQualityProfile_OrganizationProfile_QualityProfileFound(string projectKey, string organization)
    {
        const string profileKey = "orgProfile";
        const string language = "cs";
        var qualityProfileUrl = $"api/qualityprofiles/search?project={WebUtility.UrlEncode($"{projectKey}")}&organization={WebUtility.UrlEncode($"{organization}")}";
        var downloadResult = Tuple.Create(true, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}");
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists(qualityProfileUrl, Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        sut = new SonarWebServerStub(downloaderMock, version, logger, organization);

        var result = await sut.DownloadQualityProfile(projectKey, null, language);

        result.Should().Be(profileKey);
    }

    [TestMethod]
    [DataRow("foo bar")]
    public async Task DownloadQualityProfile_FallBackDefaultProfile_QualityProfileFound(string projectKey)
    {
        const string profileKey = "defaultProfile";
        const string language = "cs";
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists($"api/qualityprofiles/search?project={WebUtility.UrlEncode(projectKey)}", Arg.Any<bool>())
            .Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        downloaderMock.Download("api/qualityprofiles/search?defaults=true", Arg.Any<bool>())
            .Returns(Task.FromResult($"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}"));
        sut = new SonarWebServerStub(downloaderMock, new Version("9.9"), logger, null);

        var result = await sut.DownloadQualityProfile(projectKey, null, language);

        result.Should().Be(profileKey);
    }

    [TestMethod]
    [DataRow("foo bar", "java")]
    public async Task DownloadQualityProfile_NoProfileForLanguage_QualityProfileNotFound(string projectKey, string missingLanguage)
    {
        const string profileKey = "defaultProfile";
        const string language = "cs";
        var qualityProfileUrl = $"api/qualityprofiles/search?project={WebUtility.UrlEncode(projectKey)}";
        var downloadResult = Tuple.Create(true, $"{{ profiles: [{{\"key\":\"{profileKey}\",\"name\":\"profile1\",\"language\":\"{language}\"}}]}}");
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists(qualityProfileUrl, Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));

        sut = new SonarWebServerStub(downloaderMock, new Version("9.9"), logger, null);

        var result = await sut.DownloadQualityProfile(projectKey, null, missingLanguage);

        result.Should().BeNull();
    }

    [TestMethod]
    [DataRow("foo bar")]
    public async Task DownloadQualityProfile_NoProfileForProject_QualityProfileNotFound(string projectKey)
    {
        const string language = "cs";
        var downloadResult = Tuple.Create(true, "{ profiles: []}");
        var qualityProfileUrl = $"api/qualityprofiles/search?project={WebUtility.UrlEncode(projectKey)}";
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists(qualityProfileUrl, Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        sut = new SonarWebServerStub(downloaderMock, new Version("9.9"), logger, null);

        var result = await sut.DownloadQualityProfile(projectKey, null, language);

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task DownloadQualityProfile_MissingProfiles_ReturnsFalseAndEmptyContent()
    {
        var downloadResult = Tuple.Create(true, @"{""unexpected"": ""valid json""}");
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists($"api/qualityprofiles/search?project={ProjectKey}", Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        sut = new SonarWebServerStub(downloaderMock, new Version("9.9"), logger, null);

        var qualityProfile = await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        qualityProfile.Should().BeNull();
    }

    [TestMethod]
    public async Task DownloadQualityProfile_MissingKey_ReturnsFalseAndEmptyContent()
    {
        var downloadResult = Tuple.Create(true, @"{ profiles: [ { ""language"":""cs"" } ] }");
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists($"api/qualityprofiles/search?project={ProjectKey}", Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        sut = new SonarWebServerStub(downloaderMock, new Version("9.9"), logger, null);

        var qualityProfile = await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        qualityProfile.Should().BeNull();
    }

    [TestMethod]
    public async Task DownloadQualityProfile_MissingLanguage_ReturnsFalseAndEmptyContent()
    {
        var downloadResult = Tuple.Create(true, @"{ profiles: [ { ""key"":""p1"" } ] }");
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists($"api/qualityprofiles/search?project={ProjectKey}", Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        sut = new SonarWebServerStub(downloaderMock, new Version("9.9"), logger, null);

        var qualityProfile = await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        qualityProfile.Should().BeNull();
    }

    // This scenario is unlikely to happen but still needs to be covered
    // The behavior needs to be update according to the comment in the method.
    // The exception raised is not the correct one.
    [TestMethod]
    public async Task DownloadQualityProfile_MultipleProfileWithSameLanguage_ShouldThrow()
    {
        var downloadResult = Tuple.Create(true, @"{ profiles: [ { ""key"":""p2"", ""language"":""cs"" }, { ""key"":""p1"", ""language"":""cs"" } ] }");
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists($"api/qualityprofiles/search?project={ProjectKey}", Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        sut = new SonarWebServerStub(downloaderMock, new Version("9.9"), logger, null);

        Func<Task> act = async () => await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        await act.Should().ThrowAsync<AnalysisException>();
    }

    [DataTestMethod]
    public async Task DownloadQualityProfile_SpecificProfileRequestUrl_QualityProfileFound()
    {
        var downloadResult = Tuple.Create(true, @"{ profiles: [ { ""key"":""p1"", ""name"":""p1"", ""language"":""cs"", ""isDefault"": false } ] }");
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        sut = new SonarWebServerStub(downloaderMock, version, logger, null);

        var qualityProfile = await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        qualityProfile.Should().Be("p1");
    }

    [DataTestMethod]
    public async Task DownloadQualityProfile_DefaultProfileRequestUrl_QualityProfileFound()
    {
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadIfExists(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(Tuple.Create(false, (string)null)));
        downloaderMock.Download(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(Task.FromResult(@"{ profiles: [ { ""key"":""p1"", ""name"":""p1"", ""language"":""cs"", ""isDefault"": false } ] }"));
        sut = new SonarWebServerStub(downloaderMock, version, logger, null);

        var qualityProfile = await sut.DownloadQualityProfile(ProjectKey, null, "cs");

        qualityProfile.Should().Be("p1");
    }

    [TestMethod]
    public void DownloadRules_UseParamAsKey()
    {
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.Download(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(Task.FromResult(@"{ total: 1, p: 1, ps: 1,
            rules: [{
                key: ""vbnet:S2368"",
                repo: ""vbnet"",
                name: ""Public methods should not have multidimensional array parameters"",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [ ],
                type: ""CODE_SMELL""
            }],

            actives: {
                ""vbnet:S2368"": [
                {
                    qProfile: ""qp"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [
                    {
                      key: ""CheckId"",
                      value: ""OverwrittenId"",
                      type: ""FLOAT""
                    }
                    ]
                }
                ]
            }
            }"));
        sut = new SonarWebServerStub(downloaderMock, version, logger, null);

        var actual = sut.DownloadRules("qp").Result;
        actual.Should().ContainSingle();

        actual[0].RepoKey.Should().Be("vbnet");
        actual[0].RuleKey.Should().Be("OverwrittenId");
        actual[0].InternalKeyOrKey.Should().Be("OverwrittenId");
        actual[0].TemplateKey.Should().BeNull();
        actual[0].Parameters.Should().HaveCount(1);
    }

    [TestMethod]
    public void DownloadRules_ShouldNotGoBeyond_10k_Results()
    {
        for (var page = 1; page <= 21; page++)
        {
            downloader.Pages[$"api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p={page}"] = $@"
                    {{
                    total: 10500,
                    p: {page},
                    ps: 500,
                    rules: [{{
                        key: ""vbnet:S2368"",
                        repo: ""vbnet"",
                        name: ""Public methods should not have multidimensional array parameters"",
                        severity: ""MAJOR"",
                        lang: ""vbnet"",
                        params: [ ],
                        type: ""CODE_SMELL""
                    }},
                    {{
                        key: ""common-vbnet:InsufficientCommentDensity"",
                        repo: ""common-vbnet"",
                        internalKey: ""InsufficientCommentDensity.internal"",
                        templateKey: ""dummy.template.key"",
                        name: ""Source files should have a sufficient density of comment lines"",
                        severity: ""MAJOR"",
                        lang: ""vbnet"",
                        params: [
                            {{
                                key: ""minimumCommentDensity"",
                                defaultValue: ""25"",
                                type: ""FLOAT""
                            }}
                        ],
                        type: ""CODE_SMELL""
                    }}],
                    actives: {{
                        ""vbnet:S2368"": [
                            {{
                                qProfile:""vbnet - sonar - way - 34825"",
                                inherit: ""NONE"",
                                severity:""MAJOR"",
                                params: []
                            }}
                        ],
                    ""common-vbnet:InsufficientCommentDensity"": [
                        {{
                            qProfile: ""vbnet - sonar - way - 34825"",
                            inherit:""NONE"",
                            severity:""MAJOR"",
                            params: [
                            {{
                                key:""minimumCommentDensity"",
                                value:""50""
                            }}
                            ]
                        }}
                    ]
                    }}
                }}";
        }

        var rules = sut.DownloadRules("qp").Result;

        rules.Should().HaveCount(40);
    }

    [TestMethod]
    public void DownloadRules()
    {
        downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1"] =
            @" { total: 3, p: 1, ps: 2,
            rules: [{
                key: ""vbnet:S2368"",
                repo: ""vbnet"",
                name: ""Public methods should not have multidimensional array parameters"",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [ ],
                type: ""CODE_SMELL""
            },
            {
                key: ""common-vbnet:InsufficientCommentDensity"",
                repo: ""common-vbnet"",
                internalKey: ""InsufficientCommentDensity.internal"",
                templateKey: ""dummy.template.key"",
                name: ""Source files should have a sufficient density of comment lines"",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [
                {
                    key: ""minimumCommentDensity"",
                    defaultValue: ""25"",
                    type: ""FLOAT""
                }
                ],
                type: ""CODE_SMELL""
            },
            {
                key: ""vbnet:S1234"",
                repo: ""vbnet"",
                name: ""This rule is not active"",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [ ],
                type: ""CODE_SMELL""
            },],

            actives: {
                ""vbnet:S2368"": [
                {
                    qProfile: ""vbnet - sonar - way - 34825"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [ ]
                }
                ],
                ""common-vbnet:InsufficientCommentDensity"": [
                {
                    qProfile: ""vbnet - sonar - way - 34825"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [
                    {
                        key: ""minimumCommentDensity"",
                        value: ""50""
                    }
                    ]
                }
                ]
            }
            }";

        downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=2"] =
            @" { total: 3, p: 2, ps: 2,
            rules: [{
                key: ""vbnet:S2346"",
                repo: ""vbnet"",
                name: ""Flags enumerations zero-value members should be named \""None\"""",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [ ],
                type: ""CODE_SMELL""
            }],

            actives: {
                ""vbnet:S2346"": [
                {
                    qProfile: ""vbnet - sonar - way - 34825"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [ ]
                }
                ]
            }
            }";

        var actual = sut.DownloadRules("qp").Result;
        actual.Should().HaveCount(4);

        actual[0].RepoKey.Should().Be("vbnet");
        actual[0].RuleKey.Should().Be("S2368");
        actual[0].InternalKeyOrKey.Should().Be("S2368");
        actual[0].TemplateKey.Should().BeNull();
        actual[0].Parameters.Should().HaveCount(0);
        actual[0].IsActive.Should().BeTrue();

        actual[1].RepoKey.Should().Be("common-vbnet");
        actual[1].RuleKey.Should().Be("InsufficientCommentDensity");
        actual[1].InternalKeyOrKey.Should().Be("InsufficientCommentDensity.internal");
        actual[1].TemplateKey.Should().Be("dummy.template.key");
        actual[1].Parameters.Should().HaveCount(1);
        actual[1].Parameters.First().Should().Be(new KeyValuePair<string, string>("minimumCommentDensity", "50"));
        actual[1].IsActive.Should().BeTrue();

        actual[2].RepoKey.Should().Be("vbnet");
        actual[2].RuleKey.Should().Be("S1234");
        actual[2].InternalKeyOrKey.Should().Be("S1234");
        actual[2].TemplateKey.Should().BeNull();
        actual[2].Parameters.Should().BeNull();
        actual[2].IsActive.Should().BeFalse();

        actual[3].RepoKey.Should().Be("vbnet");
        actual[3].RuleKey.Should().Be("S2346");
        actual[3].InternalKeyOrKey.Should().Be("S2346");
        actual[3].TemplateKey.Should().BeNull();
        actual[3].Parameters.Should().HaveCount(0);
        actual[3].IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void DownloadRules_Active_WhenActivesContainsRuleWithMultipleBodies_UseFirst()
    {
        // Arrange
        downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1"] =
            @"{ total: 1, p: 1, ps: 1,
            rules: [{
                key: ""key1"",
                repo: ""vbnet"",
                name: ""Public methods should not have multidimensional array parameters"",
                severity: ""MAJOR"",
                lang: ""vbnet"",
                params: [ ],
                type: ""CODE_SMELL""
            }],

            actives: {
                ""key1"": [
                {
                    qProfile: ""qp"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [
                    {
                      key: ""CheckId"",
                      value: ""OverwrittenId-First"",
                      type: ""FLOAT""
                    }
                    ]
                },
                {
                    qProfile: ""qp"",
                    inherit: ""NONE"",
                    severity: ""MAJOR"",
                    params: [
                    {
                      key: ""CheckId"",
                      value: ""OverwrittenId-Second"",
                      type: ""FLOAT""
                    }
                    ]
                }
                ]
            }
            }";

        var actual = sut.DownloadRules("qp").Result;

        // Assert
        actual.Should().HaveCount(1);
        actual.Single().IsActive.Should().BeTrue();
        actual.Single().RuleKey.Should().Be("OverwrittenId-First");
    }

    [TestMethod]
    public void DownloadRules_NoActives()
    {
        downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1"] = @"
            {
            total: 3,
            p: 1,
            ps: 500,
            rules: [
                {
                    ""key"": ""csharpsquid:S2757"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""BUG""
                },
                {
                    ""key"": ""csharpsquid:S1117"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""CODE_SMELL""
                }
            ]}";

        var rules = sut.DownloadRules("qp").Result;

        rules.Should().HaveCount(2);

        rules[0].RepoKey.Should().Be("csharpsquid");
        rules[0].RuleKey.Should().Be("S2757");
        rules[0].InternalKeyOrKey.Should().Be("S2757");
        rules[0].Parameters.Should().BeNull();
        rules[0].IsActive.Should().BeFalse();

        rules[1].RepoKey.Should().Be("csharpsquid");
        rules[1].RuleKey.Should().Be("S1117");
        rules[1].InternalKeyOrKey.Should().Be("S1117");
        rules[1].Parameters.Should().BeNull();
        rules[1].IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void DownloadRules_EmptyActives()
    {
        downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=qp&p=1"] = @"
            {
            total: 3,
            p: 1,
            ps: 500,
            rules: [
                {
                    ""key"": ""csharpsquid:S2757"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""BUG""
                },
                {
                    ""key"": ""csharpsquid:S1117"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""CODE_SMELL""
                }
            ],

            actives: {}
            }";

        var rules = sut.DownloadRules("qp").Result;

        rules.Should().HaveCount(2);

        rules[0].RepoKey.Should().Be("csharpsquid");
        rules[0].RuleKey.Should().Be("S2757");
        rules[0].InternalKeyOrKey.Should().Be("S2757");
        rules[0].Parameters.Should().BeNull();
        rules[0].IsActive.Should().BeFalse();

        rules[1].RepoKey.Should().Be("csharpsquid");
        rules[1].RuleKey.Should().Be("S1117");
        rules[1].InternalKeyOrKey.Should().Be("S1117");
        rules[1].Parameters.Should().BeNull();
        rules[1].IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void DownloadRules_EscapeUrl()
    {
        downloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=my%23qp&p=1"] = @"
            {
            total: 3,
            p: 1,
            ps: 500,
            rules: [
                {
                    ""key"": ""csharpsquid:S2757"",
                    ""repo"": ""csharpsquid"",
                    ""type"": ""BUG""
                },
            ]}";

        var rules = sut.DownloadRules("my#qp").Result;

        rules.Should().ContainSingle();

        rules[0].RepoKey.Should().Be("csharpsquid");
        rules[0].RuleKey.Should().Be("S2757");
        rules[0].InternalKeyOrKey.Should().Be("S2757");
        rules[0].IsActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task DownloadRules_RequestUrl()
    {
        var testDownloader = new TestDownloader();
        testDownloader.Pages["api/rules/search?f=repo,name,severity,lang,internalKey,templateKey,params,actives&ps=500&qprofile=profile&p=1"] = "{ total: 1, p: 1, ps: 1, rules: [] }";
        sut = new SonarWebServerStub(testDownloader, version, logger, null);

        var rules = await sut.DownloadRules("profile");

        rules.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetInstalledPlugins()
    {
        var downloadResult = "{ languages: [{ key: \"cs\", name: \"C#\" }, { key: \"flex\", name: \"Flex\" } ]}";
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.Download("api/languages/list", Arg.Any<bool>()).Returns(Task.FromResult(downloadResult));
        sut = new SonarWebServerStub(downloaderMock, version, logger, null);
        var expected = new List<string> { "cs", "flex" };

        var actual = (await sut.DownloadAllLanguages()).ToList();

        expected.SequenceEqual(actual).Should().BeTrue();
    }

    [TestMethod]
    public async Task TryDownloadEmbeddedFile_NullPluginKey_Throws()
    {
        Func<Task> act = async () => await sut.TryDownloadEmbeddedFile(null, "filename", "targetDir");

        (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("pluginKey");
    }

    [TestMethod]
    public async Task TryDownloadEmbeddedFile_NullEmbeddedFileName_Throws()
    {
        Func<Task> act = async () => await sut.TryDownloadEmbeddedFile("key", null, "targetDir");

        (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("embeddedFileName");
    }

    [TestMethod]
    public async Task TryDownloadEmbeddedFile_NullTargetDirectory_Throws()
    {
        Func<Task> act = async () => await sut.TryDownloadEmbeddedFile("pluginKey", "filename", null);

        (await act.Should().ThrowAsync<ArgumentNullException>()).And.ParamName.Should().Be("targetDirectory");
    }

    [TestMethod]
    public async Task TryDownloadEmbeddedFile_RequestedFileExist_ReturnsTrue()
    {
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadFileIfExists(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(true));
        sut = new SonarWebServerStub(downloaderMock, version, logger, null);

        var success = await sut.TryDownloadEmbeddedFile("csharp", "dummy.txt", Path.GetRandomFileName());

        success.Should().BeTrue("Expected success");
    }

    [TestMethod]
    public async Task TryDownloadEmbeddedFile_RequestedFileDoesNotExist_ReturnsFalse()
    {
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.TryDownloadFileIfExists(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(false));

        sut = new SonarWebServerStub(downloaderMock, version, logger, null);

        var success = await sut.TryDownloadEmbeddedFile("csharp", "dummy.txt", Path.GetRandomFileName());

        success.Should().BeFalse("Expected failure");
    }

    [TestMethod]
    public void GetServerVersion_ReturnsVersion()
    {
        const string expected = "4.2";
        sut = new SonarWebServerStub(downloader, new Version(expected), logger, null);

        sut.ServerVersion.ToString().Should().Be(expected);
    }

    [TestMethod]
    public async Task DownloadAllLanguages_RequestUrl()
    {
        var downloaderMock = Substitute.For<IDownloader>();
        downloaderMock.Download("api/languages/list", Arg.Any<bool>()).Returns(Task.FromResult("{ languages: [ ] }"));
        sut = new SonarWebServerStub(downloaderMock, version, logger, null);

        var languages = await sut.DownloadAllLanguages();

        languages.Should().BeEmpty();
    }

    private class SonarWebServerStub : SonarWebServer
    {
        public SonarWebServerStub(IDownloader downloader, Version serverVersion, ILogger logger, string organization)
            : base(downloader, serverVersion, logger, organization)
        {
        }

        public override Task<IList<SensorCacheEntry>> DownloadCache(ProcessedArgs localSettings) => throw new NotImplementedException();
        public override bool IsServerVersionSupported() => throw new NotImplementedException();
        public override Task<bool> IsServerLicenseValid() => throw new NotImplementedException();
    }
}
