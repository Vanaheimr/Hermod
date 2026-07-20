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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public static class DkimKeyGenerator
    {
        public static (string PrivateKeyPem, string PublicKeyPem, string DnsRecord) GenerateKeyPair(
            string domain,
            string selector,
            int keySize = 2048)
        {
            using var rsa = RSA.Create(keySize);

            var privateKeyPem = rsa.ExportRSAPrivateKeyPem();
            var publicKeyPem  = rsa.ExportRSAPublicKeyPem();

            // Extract public key for DNS record
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyBase64 = Convert.ToBase64String(publicKeyBytes);

            // DNS TXT record format
            var dnsRecord = $"{selector}._domainkey.{domain}. IN TXT \"v=DKIM1; k=rsa; p={publicKeyBase64}\"";

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
