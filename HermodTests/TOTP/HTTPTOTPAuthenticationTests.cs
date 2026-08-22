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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TOTP
{

    /// <summary>
    /// API-level behaviour of the "Authorization: TOTP" scheme carrier. The
    /// wire format itself - building, parsing, leniency and malformed-header
    /// rejection - is vector-driven in TOTPVectorTests, against the canonical
    /// HTTP authentication vectors of the conformance suite
    /// (TOTP/TestVectors/totp-http-auth-vectors.json).
    /// </summary>
    [TestFixture]
    public class HTTPTOTPAuthenticationTests
    {

        #region EmptyUsername_IsRejected

        [Test]
        public void EmptyUsername_IsRejected()
        {

            Assert.Multiple(() => {

                Assert.That(HTTPTOTPAuthentication.TryCreate("   ", "CN63y502maVh", out _),  Is.False);

                Assert.Throws<ArgumentException>(
                    () => HTTPTOTPAuthentication.Create("", "CN63y502maVh")
                );

            });

        }

        #endregion

    }

}
