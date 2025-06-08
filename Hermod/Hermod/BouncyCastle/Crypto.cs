/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Org.BouncyCastle.X509;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Cryptographic extension methods.
    /// </summary>
    public static class CryptoExtensions
    {

        /// <summary>
        /// Convert an uncompressed public key into its PEM format.
        /// </summary>
        public static String UncompressedToPEM(Byte[] keyBytes, String curveName = "secp256r1")
        {

            if (keyBytes.Length != 65 || keyBytes[0] != 0x04)
                throw new ArgumentException("EC public key must be 65 bytes and start with 0x04 (uncompressed)");

            var x9        = SecNamedCurves.GetByName(curveName) ?? throw new ArgumentException($"Unknown curve: {curveName}");
            var curveOid  = SecNamedCurves.GetOid(curveName);

            var ecPoint   = x9.Curve.DecodePoint(keyBytes);
            if (!ecPoint.IsValid())
                throw new ArgumentException("Invalid EC point");

            var publicKey = new ECPublicKeyParameters("EC", ecPoint, curveOid);
            var info      = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey);

            using var sw        = new StringWriter();
            using var pemWriter = new PemWriter(sw);
            pemWriter.WriteObject(info);
            pemWriter.Writer.Flush();

            return sw.ToString();



            //if (keyBytes.Length != 65 || keyBytes[0] != 0x04)
            //    throw new ArgumentException("EC public key must be 65 bytes and start with 0x04 (uncompressed)");

            //// Shorten to exactly 65 bytes (security measure!)
            //var pointBytes = new Byte[65];
            //Array.Copy(keyBytes, pointBytes, 65);

            //var x9 = SecNamedCurves.GetByName(curveName);
            //var curveOid = SecNamedCurves.GetOid(curveName);
            //if (curveOid is null)
            //    throw new ArgumentException($"Unknown curve: {curveName}");

            //var ecPoint    = x9.Curve.DecodePoint(pointBytes);

            //var publicKey  = new ECPublicKeyParameters("EC", ecPoint, curveOid);
            //var info       = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey);

            //using var sw   = new StringWriter();
            //var pemWriter  = new PemWriter(sw);
            //pemWriter.WriteObject(info);
            //pemWriter.Writer.Flush();

            //return sw.ToString();

        }

    }

}
