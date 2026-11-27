/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests
{

    /// <summary>
    /// Every [TestCase] must be one a FullyQualifiedName filter can select.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NUnit names a test case after its arguments, and writes a character
    /// that XML 1.0 does not allow and that has no short escape of its own
    /// (\0, \a, \b, \t, \n, \v, \f, \r) as "\uXXXX": U+0001 to U+0006,
    /// U+000E to U+001F, U+FFFE, U+FFFF and a surrogate on its own. A case
    /// named that way runs in a run without a filter, and in one whose filter
    /// matches every test, but not under every FullyQualifiedName filter -
    /// measured 2026-11-27 with NUnit 5.0.0 and NUnit3TestAdapter 6.3.1:
    /// "FullyQualifiedName~Tests.HTTP." ran 1957 of the 1960 tests
    /// "--list-tests" names, and "FullyQualifiedName~SaslPrepTests" 29 of 30,
    /// while a filter of eight terms that the SaslPrep case also matched ran
    /// it. Which filters lose such a case and which do not was not worked out.
    /// The three were regression tests for control characters in header
    /// values and chunk extensions, and HTTP1ConformanceTests' CI and the
    /// HTTP/1.x regression selection are filters of that kind: neither had
    /// run them since they were written.
    /// </para>
    /// <para>
    /// The cure is a TestName, or an argument that is not the character
    /// itself. This test finds what a [TestCase] passes. A
    /// [TestCaseSource] it cannot see; compare "--list-tests" with a filtered
    /// run for those.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class FilterSafeTestNamesTests
    {

        /// <summary>
        /// Whether NUnit writes this character as "\uXXXX" in a test name.
        /// </summary>
        private static Boolean IsEscapedAsUnicode(String Text, Int32 Index)
        {

            var c = (Int32) Text[Index];

            if (c is >= 0x01 and <= 0x06 or >= 0x0E and <= 0x1F or 0xFFFE or 0xFFFF)
                return true;

            if (Char.IsHighSurrogate(Text[Index]))
                return Index + 1 >= Text.Length || !Char.IsLowSurrogate(Text[Index + 1]);

            if (Char.IsLowSurrogate(Text[Index]))
                return Index == 0 || !Char.IsHighSurrogate(Text[Index - 1]);

            return false;

        }

        private static Boolean HasCharacterEscapedAsUnicode(Object? Argument)

            => Argument switch {
                   String text      => Enumerable.Range(0, text.Length).Any(i => IsEscapedAsUnicode(text, i)),
                   Char   character => IsEscapedAsUnicode(character.ToString(), 0),
                   Object[] array   => array.Any(HasCharacterEscapedAsUnicode),
                   _                => false
               };


        [Test]
        public void EveryTestCase_CanBeSelectedByAFilter()
        {

            var unsafeCases = new List<String>();

            foreach (var type in typeof(FilterSafeTestNamesTests).Assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    foreach (var testCase in method.GetCustomAttributes<TestCaseAttribute>())
                    {

                        if (testCase.TestName is not null)
                            continue;

                        if (testCase.Arguments.Any(HasCharacterEscapedAsUnicode))
                            unsafeCases.Add($"{type.FullName}.{method.Name}");

                    }
                }
            }

            Assert.That(unsafeCases,
                        Is.Empty,
                        "These test cases pass a character NUnit writes as \\uXXXX in the test's name, and a FullyQualifiedName filter may not select them. Give each a TestName.");

        }

    }

}
