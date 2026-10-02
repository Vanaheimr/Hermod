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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// ParseOptional of an IEnumerable keeps the contract of every
    /// ParseOptional of Illias: true while the property is there - with an
    /// error response while a value of it is not valid - and false while it
    /// is not there or null, without one.
    /// </summary>
    /// <remarks>
    /// A value not valid returned false, and a JSON null was an array not
    /// valid: a parser asking as the others are asked passed over the one, and
    /// refused the other.
    /// </remarks>
    [TestFixture]
    public class ParseOptionalValuesTests
    {

        [Test]
        public void ParseOptionalValues_KeepsTheContract()
        {

            var notThere = JObject.Parse("""{ "other": 1 }""").            ParseOptional("value", "value", Int32.TryParse, out IEnumerable<Int32> _,      out var errorNotThere);
            var isNull   = JObject.Parse("""{ "value": null }""").         ParseOptional("value", "value", Int32.TryParse, out IEnumerable<Int32> _,      out var errorNull);
            var notValid = JObject.Parse("""{ "value": [ 1, "one" ] }"""). ParseOptional("value", "value", Int32.TryParse, out IEnumerable<Int32> _,      out var errorNotValid);
            var valid    = JObject.Parse("""{ "value": [ 1, 2 ] }""").     ParseOptional("value", "value", Int32.TryParse, out IEnumerable<Int32> values, out var errorValid);

            Assert.Multiple(() => {

                Assert.That(notThere,      Is.False,        "A value not there is said to be there.");
                Assert.That(errorNotThere, Is.Null,         "A value not there is said to be an error.");

                Assert.That(isNull,        Is.False,        "A value null is said to be there.");
                Assert.That(errorNull,     Is.Null,         "A value null is said to be an error.");

                Assert.That(notValid,      Is.True,         "A value not valid is said not to be there.");
                Assert.That(errorNotValid, Is.Not.Null,     "A value not valid is no error.");

                Assert.That(valid,         Is.True,         "A valid value is said not to be there.");
                Assert.That(values,        Is.EqualTo(new[] { 1, 2 }));
                Assert.That(errorValid,    Is.Null,         "A valid value is said to be an error.");

            });

        }

    }

}
