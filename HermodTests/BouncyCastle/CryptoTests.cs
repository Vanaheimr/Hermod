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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.BouncyCastle
{

    /// <summary>
    /// Crypto tests.
    /// </summary>
    [TestFixture]
    public class CryptoTests
    {

        #region UncompressedToPEM_Test()

        /// <summary>
        /// A test for parsing an uncompressed public key into its PEM format.
        /// </summary>
        [Test]
        public void UncompressedToPEM_Test()
        {

            var uncompressedPublicKey = "04A8D26E1E0365745E2CC9E82CA78FA359FF280A1E9039DFF0CF2F6F51D72E57153276F93AEF1D61F1ACFA44BBD2B3BDC73BBF37B6CEF4E17B90432A8A4F263DE8";

            var pem = CryptoExtensions.UncompressedToPEM(uncompressedPublicKey.FromHEX());

            const String expectedPEM = @"-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEqNJuHgNldF4syegsp4+jWf8oCh6Q
Od/wzy9vUdcuVxUydvk67x1h8az6RLvSs73HO783ts704XuQQyqKTyY96A==
-----END PUBLIC KEY-----";

            Assert.That(pem.Trim(), Is.EqualTo(expectedPEM.Trim()));

        }

        #endregion

    }

}
