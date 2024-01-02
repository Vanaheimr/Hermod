/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;
using NUnit.Framework.Legacy;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTPS
{

    /// <summary>
    /// Tests between Hermod HTTPS clients and Hermod HTTPS servers.
    /// </summary>
    [TestFixture]
    public class HTTPSClientTests : AHTTPSServerTests
    {

        #region Data

        public static readonly IPPort HTTPSPort = IPPort.Parse(4083);

        #endregion

        #region Constructor(s)

        public HTTPSClientTests()
            : base(HTTPSPort)
        { }

        #endregion


        #region Test_001()

        [Test]
        public async Task Test_001()
        {

            var httpsClient    = new HTTPSClient(
                                     URL.Parse($"https://127.0.0.1:{HTTPSPort}"),
                                     RemoteCertificateValidator: (sender, certificate, chain, policyErrors) => {

                                         // Certificate verification
                                         if (certificate is null ||
                                             certificate.Subject != "OU=GraphDefined PKI Services, O=GraphDefined GmbH, CN=AHTTPSServerTests Server Certificate" ||
                                             certificate.Issuer  != "OU=GraphDefined PKI Services, O=GraphDefined GmbH, CN=AHTTPSServerTests Server CA"          ||
                                             certificate.NotBefore.ToUniversalTime() > Timestamp.Now ||
                                             certificate.NotAfter. ToUniversalTime() < Timestamp.Now ||
                                             certificate.GetSerialNumberString().IsNullOrEmpty())
                                         {
                                             return (false, Array.Empty<String>());
                                         }

                                         // Trust chain verification
                                         var certificateChain = new X509Chain();
                                         certificateChain.ChainPolicy.ExtraStore.Add(rootCA);
                                         certificateChain.ChainPolicy.ExtraStore.Add(serverCA);
                                         certificateChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                                         certificateChain.ChainPolicy.RevocationMode    = X509RevocationMode.   NoCheck;

                                         if (certificateChain.Build(certificate)      &&
                                             certificateChain.ChainElements.Count > 0 &&
                                             certificateChain.ChainElements[^1].Certificate.Thumbprint == rootCA.Thumbprint)
                                         {
                                             return (true, Array.Empty<String>());
                                         }

                                         // Collect status errors
                                         var errors = new List<String>();
                                         errors.AddRange(certificateChain.ChainStatus.Select(status => $"{status.Status} => '{status.StatusInformation}'!"));

                                         foreach (var chainElement in certificateChain.ChainElements)
                                         {
                                             foreach (var chainStatus in chainElement.ChainElementStatus)
                                                 errors.Add($"{chainElement.Certificate.SubjectName} => '{chainStatus.StatusInformation}'!");
                                         }

                                         return (false, errors);

                                     }
                                 );
            var httpsResponse  = await httpsClient.GET(HTTPPath.Root).
                                                   ConfigureAwait(false);



            var request       = httpsResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            ClassicAssert.IsFalse(request.Contains("Date:"),                          request);
            ClassicAssert.IsTrue (request.Contains("GET / HTTP/1.1"),                  request);
            ClassicAssert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPSPort}"),   request);



            var response      = httpsResponse.EntirePDU;
            var httpsBody     = httpsResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Hermod Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                12
            // Connection:                    close
            // 
            // Hello World!

            ClassicAssert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),   response);
            ClassicAssert.IsTrue  (response.Contains("Hello World!"),      response);

            ClassicAssert.AreEqual("Hello World!",                         httpsBody);

            ClassicAssert.AreEqual("Hermod Test Server",                   httpsResponse.Server);
            ClassicAssert.AreEqual("Hello World!".Length,                  httpsResponse.ContentLength);

        }

        #endregion


    }

}
