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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Entities
{

    /// <summary>
    /// Identifications equal without regard to case have the same hash code: a set, a dictionary or Distinct()
    /// takes "ODbL" and "odbl" as one, as Equals does.
    /// </summary>
    [TestFixture]
    public class CaseInsensitiveIdTests
    {

        #region Ids_equal_without_case_have_one_hash_code()

        [Test]
        public void Ids_equal_without_case_have_one_hash_code()
        {

            using (Assert.EnterMultipleScope())
            {

                Assert.That(DataLicense_Id.      Parse("ODbL"),          Is.EqualTo(DataLicense_Id.      Parse("odbl")));
                Assert.That(DataLicense_Id.      Parse("ODbL").GetHashCode(),
                                                                         Is.EqualTo(DataLicense_Id.      Parse("odbl").GetHashCode()));
                Assert.That(new[] { DataLicense_Id.Parse("ODbL"), DataLicense_Id.Parse("odbl") }.Distinct().Count(),
                                                                         Is.EqualTo(1));

                Assert.That(OpenSourceLicense_Id.Parse("Apache-2.0").GetHashCode(),
                                                                         Is.EqualTo(OpenSourceLicense_Id.Parse("apache-2.0").GetHashCode()));
                Assert.That(new HashSet<OpenSourceLicense_Id> { OpenSourceLicense_Id.Parse("Apache-2.0"), OpenSourceLicense_Id.Parse("APACHE-2.0") }.Count,
                                                                         Is.EqualTo(1));

                Assert.That(AttachedFile_Id.     Parse("Report.PDF").GetHashCode(),
                                                                         Is.EqualTo(AttachedFile_Id.     Parse("report.pdf").GetHashCode()));

                // Different ids stay different.
                Assert.That(DataLicense_Id.      Parse("ODbL"),          Is.Not.EqualTo(DataLicense_Id.  Parse("CC-BY-4.0")));

            }

        }

        #endregion

    }

}
