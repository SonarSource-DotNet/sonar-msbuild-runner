﻿/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using System.Security.Principal;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class MutexWrapperTests
    {
        [TestMethod]
        public void MultipleDispose_DoesntThrow()
        {
            var m = new MutexWrapper("test_00");
            m.Dispose();
            m.Dispose();
            m.Dispose();
        }

        private static void WaitForStep(List<int>steps, int step)
        {
            while (!steps.Contains(step))
            {
                Thread.Sleep(10);
            }
        }

        [TestMethod]
        public void TestSynchronization_WithMutexWrapper()
        {
            // Arrange
            const string mutexName = "sonarsource.scannerformsbuild.test1";
            var oneMinute = TimeSpan.FromMinutes(1);
            var steps = new List<int>(10);

            var t1 = new Thread(() =>
            {
                steps.Add(101);
                using (var m = new MutexWrapper(mutexName, oneMinute))
                {
                    steps.Add(102);
                }
                steps.Add(103);
            });

            var t2 = new Thread(() =>
                {
                    try
                    {
                        new MutexWrapper(mutexName, oneMinute);
                        steps.Add(201);
                        Thread.Sleep(oneMinute);
                        steps.Add(202);
                    }
                    catch (ThreadAbortException)
                    {
                        Thread.Sleep(500);
                        steps.Add(203);
                    }
                });

            var t3 = new Thread(() =>
                {
                    steps.Add(301);
                    using (var m = new MutexWrapper(mutexName, oneMinute))
                    {
                        Thread.Sleep(500);
                        steps.Add(302);
                    }
                    steps.Add(303);
                });

            // Act & Assert
            t1.Start();
            WaitForStep(steps, 103);
            CollectionAssert.AreEqual(new[] { 101, 102, 103 }, steps);

            t2.Start();
            WaitForStep(steps, 201);
            CollectionAssert.AreEqual(new[] { 101, 102, 103, 201 }, steps);

            t3.Start();
            WaitForStep(steps, 301);
            CollectionAssert.AreEqual(new[] { 101, 102, 103, 201, 301 }, steps);

            t2.Abort();
            WaitForStep(steps, 203);
            CollectionAssert.AreEqual(new[] { 101, 102, 103, 201, 301, 203 }, steps);

            WaitForStep(steps, 303);
            CollectionAssert.AreEqual(new[] { 101, 102, 103, 201, 301, 203, 302, 303 }, steps);
        }
    }
}
