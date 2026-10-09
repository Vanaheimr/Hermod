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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Entities
{

    /// <summary>
    /// Open Source and Open Data licenses read back as they are written.
    ///
    /// ToJSON() writes the identification as "@id", and TryParse() once read
    /// only "id" - so no license it had written could be read again.
    /// </summary>
    [TestFixture]
    public class LicenseJSONTests
    {

        #region OpenSourceLicense_WrittenIsRead(Embedded)

        [TestCase(false)]
        [TestCase(true)]
        public void OpenSourceLicense_WrittenIsRead(Boolean Embedded)
        {

            var written = OpenSourceLicense.AGPL3.ToJSON(Embedded);

            Assert.That(OpenSourceLicense.TryParse(written, out var license, out var errorResponse), Is.True, errorResponse);
            Assert.That(license, Is.EqualTo(OpenSourceLicense.AGPL3));

        }

        #endregion

        #region OpenSourceLicense_WithItsIdAsId_IsStillRead()

        [Test]
        public void OpenSourceLicense_WithItsIdAsId_IsStillRead()
        {

            Assert.That(OpenSourceLicense.TryParse(JObject.Parse("""{ "id": "AGPL-3.0" }"""), out var license, out var errorResponse), Is.True, errorResponse);
            Assert.That(license!.Id.ToString(), Is.EqualTo("AGPL-3.0"));

        }

        #endregion

        #region DataLicense_WrittenIsRead(Embedded)

        [TestCase(false)]
        [TestCase(true)]
        public void DataLicense_WrittenIsRead(Boolean Embedded)
        {

            var written = DataLicense.OpenDatabaseLicense.ToJSON(Embedded);

            Assert.That(DataLicense.TryParse(written, out var license, out var errorResponse), Is.True, errorResponse);
            Assert.That(license, Is.EqualTo(DataLicense.OpenDatabaseLicense));

        }

        #endregion

        #region DataLicense_WithItsIdAsId_IsStillRead()

        [Test]
        public void DataLicense_WithItsIdAsId_IsStillRead()
        {

            Assert.That(DataLicense.TryParse(JObject.Parse("""{ "id": "ODbL" }"""), out var license, out var errorResponse), Is.True, errorResponse);
            Assert.That(license!.Id.ToString(), Is.EqualTo("ODbL"));

        }

        #endregion

    }

}
