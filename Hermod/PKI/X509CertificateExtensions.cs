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

using Org.BouncyCastle.X509;
using Org.BouncyCastle.OpenSsl;

using dotNet = System.Security.Cryptography.X509Certificates;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.PKI
{

    /// <summary>
    /// Extension methods for X.509 Certificates.
    /// </summary>
    public static class X509CertificateExtensions
    {

        /// <summary>
        /// Exports the given X.509 Certificate encoded in PEM format.
        /// </summary>
        /// <param name="Certificate">A X.509 Certificate.</param>
        public static String ToPEM(this X509Certificate Certificate)
        {

            using var stringWriter = new StringWriter();
            using var pemWriter    = new PemWriter(stringWriter);

            pemWriter.WriteObject(Certificate);

            return stringWriter.ToString();

        }


        public static Boolean IsCertificateAuthority(this dotNet.X509Certificate2 Certificate)
        {

            if (Certificate.Extensions["2.5.29.19"] is not dotNet.X509BasicConstraintsExtension basicConstraintsExtension)
                return false;

            return basicConstraintsExtension.CertificateAuthority;

        }


    }

}
