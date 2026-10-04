/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Reflection;
using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The public surface of HTTPStandardHandlers, which until now existed
    /// twice under two names. H-18 in HTTP1ConformanceTests.
    /// </summary>
    /// <remarks>
    /// "Deprecated old HTTP implementation in favour of new one!" (2026-04-20)
    /// moved URLMapping/ to URLMapping_old/ and commented every file out. One
    /// file came back: a copy named HTTPStandardHandlersX, and that copy is
    /// where the modernisation then happened — extension methods on today's
    /// HTTPAPI, HTTPExtAPI and HTTPServer, compiled into the assembly and
    /// called both by this suite and by five other repositories, living in a
    /// directory whose name says it is over. The deprecated twin beside it
    /// kept the plain name and 1271 commented-out lines.
    ///
    /// So the modernisation had happened to the copy. This fixture pins the
    /// result of putting that right: one class, under the plain name, in
    /// URLMapping/ with the rest of the live routing code.
    ///
    /// These are surface tests on purpose, and they reach the class by name
    /// rather than through typeof so that the fixture still compiles against
    /// the assembly as it was — all four were red before the move rather than
    /// merely unbuildable. Moving a file cannot change behaviour, but it can
    /// quietly lose an overload, and that breaks a caller in another
    /// repository at compile time, where this suite would never see it.
    ///
    /// What can be tested as behaviour is tested elsewhere:
    /// HTTPServerHandlerTests.The_Register_Helpers_Are_Reachable for the three
    /// Register*Handler methods, HTTPTestServerTests for
    /// MapResourceAssembliesFolder on HTTPAPI. That is four of the eleven
    /// overloads below; the other seven have no caller anywhere in this suite,
    /// including all three on HTTPExtAPI, which thirteen other fixtures use but
    /// never for serving a file. This fixture pins their signatures and does
    /// not pretend to exercise them.
    ///
    /// The last test is about the file next door rather than about this class:
    /// same cleanup, same failure mode, and too small to be a fixture of its
    /// own.
    /// </remarks>
    [TestFixture]
    public class HTTPStandardHandlersTests
    {

        #region Data

        private const           String    typeName  = "org.GraphDefined.Vanaheimr.Hermod.HTTP.HTTPStandardHandlers";

        private static readonly Assembly  hermod    = typeof(HTTPAPI).Assembly;

        /// <summary>
        /// Every public entry point, as (name, the type it extends, parameter
        /// count). Written out rather than derived, so that an overload lost
        /// in a move and an overload added without a word both go red.
        /// </summary>
        private static readonly (String Name, Type Extends, Int32 Parameters)[] expected = [

            ("RegisterRAWRequestHandler",        typeof(HTTPServer),  5),
            ("RegisterMovedTemporarilyHandler",  typeof(HTTPServer),  5),
            ("RegisterMovedPermanentlyHandler",  typeof(HTTPServer),  5),

            ("MapResourceAssemblyFolder",        typeof(HTTPAPI),     7),
            ("MapResourceAssemblyFolder",        typeof(HTTPExtAPI),  8),
            ("MapResourceAssembliesFolder",      typeof(HTTPAPI),     8),
            ("MapResourceAssembliesFolder",      typeof(HTTPExtAPI),  9),
            ("MapFileSystemFolder",              typeof(HTTPAPI),     8),
            ("MapFileSystemFolder",              typeof(HTTPExtAPI),  9),

            ("RegisterResourcesFile",            typeof(HTTPAPI),     7),
            ("RegisterFileSystemFile",           typeof(HTTPAPI),     7)

        ];

        #endregion

        #region (private) EntryPoints()

        private static MethodInfo[] EntryPoints()
        {

            var standardHandlers = hermod.GetType(typeName);

            Assert.That(standardHandlers, Is.Not.Null, typeName + " is missing entirely");

            return [.. standardHandlers!.
                           GetMethods(BindingFlags.Public | BindingFlags.Static).
                           Where(method => !method.IsSpecialName)];

        }

        #endregion


        #region ThereIsOneStandardHandlersClass()

        /// <summary>
        /// The plain name is the one that exists, and the copy is gone.
        /// </summary>
        [Test]
        public void ThereIsOneStandardHandlersClass()
        {

            var standardHandlers = hermod.GetTypes().
                                       Where (type => type.Name.StartsWith("HTTPStandardHandlers")).
                                       Select(type => type.Name).
                                       Order().
                                       ToArray();

            Assert.That(standardHandlers, Is.EqualTo(new[] { "HTTPStandardHandlers" }));

        }

        #endregion

        #region EveryEntryPointSurvivedTheMove()

        /// <summary>
        /// The whole surface, in both directions: nothing missing, nothing new.
        /// </summary>
        [Test]
        public void EveryEntryPointSurvivedTheMove()
        {

            var actual = EntryPoints().
                             Select(method => (method.Name,
                                               Extends:     method.GetParameters()[0].ParameterType,
                                               Parameters:  method.GetParameters().Length)).
                             ToHashSet();

            Assert.Multiple(() => {

                Assert.That(expected.ToHashSet().Except(actual),
                            Is.Empty,
                            "entry points that have gone missing");

                Assert.That(actual.Except(expected.ToHashSet()),
                            Is.Empty,
                            "entry points this fixture does not know about");

            });

        }

        #endregion

        #region EveryEntryPointIsStillAnExtensionMethod()

        /// <summary>
        /// All eleven are called as httpAPI.MapSomething(...) by code in other
        /// repositories. A move that dropped a "this" would leave every
        /// signature above intact and break every one of those call sites, so
        /// the parameter list is not the whole contract.
        /// </summary>
        [Test]
        public void EveryEntryPointIsStillAnExtensionMethod()
        {

            var entryPoints = EntryPoints();

            Assert.Multiple(() => {

                Assert.That(entryPoints.Length,
                            Is.EqualTo(expected.Length),
                            "and all of them are there to be checked");

                Assert.That(entryPoints.
                                Where (method => !method.IsDefined(typeof(ExtensionAttribute), false)).
                                Select(method => method.Name),
                            Is.Empty);

            });

        }

        #endregion

        #region TheLoggerIsStillSettable()

        /// <summary>
        /// The one piece of state the class carries, and the one thing the
        /// deprecated twin never had: these handlers log through it, and a
        /// move that turned it private would silence them.
        /// </summary>
        [Test]
        public void TheLoggerIsStillSettable()
        {

            var logger = hermod.GetType(typeName)?.GetProperty("Logger", BindingFlags.Public | BindingFlags.Static);

            Assert.Multiple(() => {
                Assert.That(logger,                 Is.Not.Null);
                Assert.That(logger?.CanWrite,       Is.True);
                Assert.That(logger?.GetValue(null), Is.Not.Null, "and it starts out as a logger rather than as null");
            });

        }

        #endregion

        #region TheRequestHandlersClassHasNoXEither()

        /// <summary>
        /// HTTPRequestHandlersX, the other leftover of the same rework: the
        /// record of handlers that MethodNode, PathNode, ParsedRequest and
        /// HTTPAPI all pass around, carrying an X with no twin to be
        /// distinguished from. It is HTTPRequestHandlers now.
        /// </summary>
        /// <remarks>
        /// The name was free as a type and taken as a member - MethodNode has
        /// an IEnumerable&lt;HTTPRequestHandlers&gt; property of exactly that
        /// name - which C# allows, since one is read in type position and the
        /// other in expression position.
        /// </remarks>
        [Test]
        public void TheRequestHandlersClassHasNoXEither()
        {

            var requestHandlers = hermod.GetTypes().
                                      Where (type => type.Name.StartsWith("HTTPRequestHandlers")).
                                      Select(type => type.Name).
                                      Order().
                                      ToArray();

            Assert.That(requestHandlers, Is.EqualTo(new[] { "HTTPRequestHandlers" }));

        }

        #endregion

    }

}
