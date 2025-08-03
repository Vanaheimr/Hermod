﻿/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    // https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-tls/
    // one.one.one.one

    // https://dnscrypt.info

    // https://developers.cloudflare.com/1.1.1.1/infrastructure/extended-dns-error-codes/

    /// <summary>
    /// Some Cloudflare DNS TLS tests.
    /// </summary>
    [TestFixture]
    public class CloudflareTLS_Tests : ADNSTests
    {

        [OneTimeSetUp]
        public void InitTests()
        {

            RemoteTLSServerCertificateValidationHandler<DNSTLSClient> validateServerCertificate = (sender,
                                                                                                   certificate,
                                                                                                   certificateChain,
                                                                                                   tlsClient,
                                                                                                   policyErrors) => {

                var sans = certificate?.DecodeSubjectAlternativeNames() ?? [];

                // Accept all certificates!
                return (true, []);

            };

            client = DNSTLSClient.Cloudflare_Random(RemoteCertificateValidationHandler: validateServerCertificate);
            //    URL.Parse("tls://one.one.one.one"),
            //    RemoteCertificateValidationHandler: validateServerCertificate
            //);

        }

    }

}
