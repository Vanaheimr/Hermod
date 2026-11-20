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

using System.Security.Cryptography;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public static class DkimKeyGenerator
    {
        /// <summary>
        /// A new RSA key for DKIM: its private and public key as PEM, and the zone file line of
        /// its key record (RFC 6376 §3.6.1).
        /// </summary>
        /// <param name="keySize">The size of the key in bits: at least 1024, 2048 by default (RFC 8301 §3.2).</param>
        /// <exception cref="ArgumentOutOfRangeException">A key size under 1024 bits.</exception>
        public static (string PrivateKeyPem, string PublicKeyPem, string DnsRecord) GenerateKeyPair(
            string domain,
            string selector,
            int keySize = 2048)
        {

            // RFC 8301 §3.2: "Signers MUST use RSA keys of at least 1024 bits for all keys.
            // Signers SHOULD use RSA keys of at least 2048 bits."
            ArgumentOutOfRangeException.ThrowIfLessThan(keySize, DkimKeyMaterial.MinimumRsaKeySize);

            using var rsa = RSA.Create(keySize);

            var privateKeyPem = rsa.ExportRSAPrivateKeyPem();
            var publicKeyPem  = rsa.ExportRSAPublicKeyPem();

            // Extract public key for DNS record
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyBase64 = Convert.ToBase64String(publicKeyBytes);

            // The zone file line: the record in character-strings of at most 255 octets each (RFC
            // 1035 §3.3) - the p= of a 2048-bit key alone has 392 - which "MUST be concatenated
            // together before use with no intervening whitespace" (RFC 6376 §3.6.2.2).
            var dnsRecord = $"{selector}._domainkey.{domain}. IN TXT {TXT.PresentationOf($"v=DKIM1; k=rsa; p={publicKeyBase64}")}";

            return (privateKeyPem, publicKeyPem, dnsRecord);
        }

        public static void SaveKeyPair(string basePath, string domain, string selector)
        {
            var (privateKey, publicKey, dnsRecord) = GenerateKeyPair(domain, selector);

            var privateKeyPath = Path.Combine(basePath, $"dkim_{selector}.private.pem");
            var publicKeyPath  = Path.Combine(basePath, $"dkim_{selector}.public.pem");
            var dnsRecordPath  = Path.Combine(basePath, $"dkim_{selector}.dns.txt");

            Directory.CreateDirectory(basePath);

            File.WriteAllText(privateKeyPath, privateKey);
            File.WriteAllText(publicKeyPath, publicKey);
            File.WriteAllText(dnsRecordPath, dnsRecord);

        }

    }

}
