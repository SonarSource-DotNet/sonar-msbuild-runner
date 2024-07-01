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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.JreCaching;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreCaching;

[TestClass]
public class JreCacheTests
{
    private static IEnumerable<object[]> DirectoryCreateExceptions
    {
        get
        {
            yield return [typeof(IOException)];
            yield return [typeof(UnauthorizedAccessException)];
            yield return [typeof(ArgumentException)];
            yield return [typeof(ArgumentNullException)];
            yield return [typeof(PathTooLongException)];
            yield return [typeof(DirectoryNotFoundException)];
            yield return [typeof(NotSupportedException)];
        }
    }

    [TestMethod]
    public void UserHomeIsCreated()
    {
        var home = @"C:\Users\user\.sonar";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(false);
        var fileWrapper = Substitute.For<IFileWrapper>();

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new JreDescriptor("jre", "sha", "java"));
        result.Should().Be(new JreCacheMiss());
        directoryWrapper.Received().CreateDirectory(home);
    }

    [DataTestMethod]
    [DataRow(typeof(IOException))]
    [DataRow(typeof(UnauthorizedAccessException))]
    [DataRow(typeof(ArgumentException))]
    [DataRow(typeof(ArgumentNullException))]
    [DataRow(typeof(PathTooLongException))]
    [DataRow(typeof(DirectoryNotFoundException))]
    [DataRow(typeof(NotSupportedException))]
    public void UserHomeCreationFails(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(home)).Throw((Exception)Activator.CreateInstance(exceptionType));
        var fileWrapper = Substitute.For<IFileWrapper>();

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new JreDescriptor("jre", "sha", "java"));
        result.Should().Be(new JreCacheFailure(@"The JRE cache directory in 'C:\Users\user\.sonar\cache' could not be created."));
        directoryWrapper.Received().CreateDirectory(home);
    }

    [TestMethod]
    public void CacheHomeIsCreated()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.Exists(cache).Returns(false);
        var fileWrapper = Substitute.For<IFileWrapper>();

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new JreDescriptor("jre", "sha", "java"));
        result.Should().Be(new JreCacheMiss());
        directoryWrapper.DidNotReceive().CreateDirectory(home);
        directoryWrapper.Received().CreateDirectory(cache);
    }

    [DataTestMethod]
    [DynamicData(nameof(DirectoryCreateExceptions))]
    public void CacheHomeCreationFails(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.When(x => x.CreateDirectory(cache)).Throw((Exception)Activator.CreateInstance(exceptionType));
        var fileWrapper = Substitute.For<IFileWrapper>();

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new JreDescriptor("jre", "sha", "java"));
        result.Should().Be(new JreCacheFailure(@"The JRE cache directory in 'C:\Users\user\.sonar\cache' could not be created."));
        directoryWrapper.DidNotReceive().CreateDirectory(home);
        directoryWrapper.Received().CreateDirectory(cache);
    }

    [TestMethod]
    public void ExtractedDirectoryDoesNotExists()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var expectedExtractedPath = Path.Combine(cache, "sha", "filename.tar.gz_extracted");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(expectedExtractedPath).Returns(false);
        var fileWrapper = Substitute.For<IFileWrapper>();

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new JreDescriptor("filename.tar.gz", "sha", "jdk/bin/java"));
        result.Should().Be(new JreCacheMiss());
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void JavaExecutableDoesNotExists()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var expectedExtractedPath = Path.Combine(cache, "sha", "filename.tar.gz_extracted");
        var expectedExtractedJavaExe = Path.Combine(expectedExtractedPath, "jdk/bin/java");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(expectedExtractedPath).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(expectedExtractedJavaExe).Returns(false);

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new JreDescriptor("filename.tar.gz", "sha", "jdk/bin/java"));
        result.Should().Be(new JreCacheFailure(
            @"The java executable in the JRE cache could not be found at the expected location 'C:\Users\user\.sonar\cache\sha\filename.tar.gz_extracted\jdk/bin/java'."));
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void CacheHit()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var expectedExtractedPath = Path.Combine(cache, "sha", "filename.tar.gz_extracted");
        var expectedExtractedJavaExe = Path.Combine(expectedExtractedPath, "jdk/bin/java");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(expectedExtractedPath).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(expectedExtractedJavaExe).Returns(true);

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new JreDescriptor("filename.tar.gz", "sha", "jdk/bin/java"));
        result.Should().Be(new JreCacheHit(expectedExtractedJavaExe));
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }
}
