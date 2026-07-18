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

using System.Text.Json;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.Argus;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.JSON.Canonical
{

    /// <summary>
    /// Argus JSON serialization tests.
    /// </summary>
    [TestFixture]
    public class ArgusJsonSerializerOptionsTests
    {

        private record TimestampProbe(DateTimeOffset Timestamp);


        #region Serializes_DateTimeOffset_As_Argus_ISO8601_UTC()

        /// <summary>
        /// Argus JSON output should use the same UTC ISO 8601 timestamp shape as
        /// the rest of Hermod: yyyy-MM-ddTHH:mm:ss.fffZ.
        /// </summary>
        [Test]
        public void Serializes_DateTimeOffset_As_Argus_ISO8601_UTC()
        {

            var json = JsonSerializer.Serialize(
                           new TimestampProbe(
                               new DateTimeOffset(
                                   2026, 05, 30,
                                   15, 55, 16, 123,
                                   TimeSpan.FromHours(2)
                               )
                           ),
                           JSONSerializerOptions.Default
                       );

            Assert.That(json, Is.EqualTo("""{"timestamp":"2026-05-30T13:55:16.123Z"}"""));

        }

        #endregion

        #region Deserializes_ISO8601_DateTimeOffset_As_UTC()

        /// <summary>
        /// Log replay must keep reading timestamps written by Argus.
        /// </summary>
        [Test]
        public void Deserializes_ISO8601_DateTimeOffset_As_UTC()
        {

            var probe = JsonSerializer.Deserialize<TimestampProbe>(
                            """{"timestamp":"2026-05-30T13:55:16.123Z"}""",
                            JSONSerializerOptions.Default
                        );

            Assert.That(probe, Is.Not.Null);
            Assert.That(probe!.Timestamp, Is.EqualTo(new DateTimeOffset(2026, 05, 30, 13, 55, 16, 123, TimeSpan.Zero)));

        }

        #endregion

    }

}
