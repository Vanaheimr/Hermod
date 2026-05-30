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
using System.Security.Cryptography;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NUnit.Framework;

using Org.BouncyCastle.Security;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Argus;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Argus signature verification tests.
    /// </summary>
    [TestFixture]
    public class ArgusSignatureTests
    {

        #region Verifies_Canonical_Response_With_Offset_Timestamp()

        /// <summary>
        /// Newtonsoft parses ISO timestamp strings as DateTime by default.  The Argus
        /// verifier must preserve them as strings, otherwise the re-canonicalized
        /// payload differs from the service payload that was signed.
        /// </summary>
        [Test]
        public void Verifies_Canonical_Response_With_Offset_Timestamp()
        {

            var keys = ServiceCheckKeys.GenerateECKeys();

            var response = JSONObject.Create(
                               new JProperty("timestamp",      "2026-05-30T13:55:16.7730509+00:00"),
                               new JProperty("service",        "Hermod Test Service"),
                               new JProperty("processorCount", 8),
                               new JProperty("gc",             JSONObject.Create(
                                                             new JProperty("heapMB",           117.06529235839844),
                                                             new JProperty("pauseTotalMs",     645.68),
                                                             new JProperty("allocatedTotalMB", 3799.544906616211)
                                                         ))
                           );

            response.Add("publicKey", keys.PublicKeyECHEX);

            var plaintext  = CanonicalJSON.Serialize(response);
            var hash       = SHA256.HashData(plaintext.ToUTF8Bytes());
            var signer     = SignerUtilities.GetSigner("NONEwithECDSA");
            signer.Init(true, keys.PrivateKeyEC);
            signer.BlockUpdate(hash, 0, hash.Length);
            response.Add("signature", signer.GenerateSignature().ToHexString());

            var responseBody = response.ToString(Formatting.None);

            Assert.That(JObject.Parse(responseBody)["timestamp"]!.Type, Is.EqualTo(JTokenType.Date));

            var verifyMethod = typeof(HTTPSMonitor).GetMethod(
                                   "VerifyResponseSignature",
                                   BindingFlags.Static | BindingFlags.NonPublic
                               );

            Assert.That(verifyMethod, Is.Not.Null);

            var verification = (SignatureVerification) verifyMethod!.Invoke(null, [ responseBody ])!;

            Assert.That(verification.Present, Is.True);
            Assert.That(verification.Valid,   Is.True, verification.Error);

        }

        #endregion

    }

}
