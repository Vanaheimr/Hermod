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

    #region DKIM Configuration

    public sealed record DkimConfig
    {
        public required string  Domain          { get; init; }
        public required string  Selector        { get; init; }
        public required string  PrivateKeyPem   { get; init; }
        public          string  Canonicalization{ get; init; } = "relaxed/relaxed";
        public          string  SignedHeaders   { get; init; } = "from:to:subject:date:message-id:mime-version:content-type";
        public          int     BodyLengthLimit { get; init; } = 0;  // 0 = no limit
    }

    #endregion

    #region DKIM Signer

    public sealed class DkimSigner
    {

        private readonly DkimConfig _config;
        private readonly RSA        _privateKey;
        private readonly ILogger    _logger;

        public DkimSigner(DkimConfig config, ILogger logger)
        {
            _config     = config;
            _logger     = logger;
            _privateKey = RSA.Create();
            _privateKey.ImportFromPem(config.PrivateKeyPem);

            logger.Log(LogLevel.Info, $"DKIM signer initialized: domain={config.Domain}, selector={config.Selector}");
        }

        /// <summary>
        /// Prepend a DKIM-Signature header to the message (RFC 6376). Uses the shared
        /// canonicalizer and hashes over UTF-8 octets so the signature matches what
        /// receiving verifiers compute over the wire bytes. On any error the message is
        /// returned unchanged (unsigned).
        /// </summary>
        public string SignMessage(string message)
        {
            try
            {

                var (headerBlock, body) = DkimCanonicalization.Split(message);
                var fields              = DkimCanonicalization.ParseFields(headerBlock);

                var slash        = _config.Canonicalization.Split('/');
                var headerCanon  = slash[0].ToLowerInvariant();
                var bodyCanon    = slash.Length > 1 ? slash[1].ToLowerInvariant() : headerCanon;

                // Body hash over the original wire octets (UTF-8), not ASCII.
                var canonBody    = DkimCanonicalization.CanonicalizeBody(body, bodyCanon);
                var bodyHash     = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(canonBody)));

                // Only advertise/sign headers that are actually present.
                var signedNames  = _config.SignedHeaders.Split(':').
                                       Select(h => h.Trim()).
                                       Where (h => h.Length > 0 &&
                                                   fields.Any(f => f.Name.Equals(h, StringComparison.OrdinalIgnoreCase))).
                                       ToList();

                var timestamp    = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var dkimValue    = BuildDkimHeaderValue(bodyHash, timestamp, string.Join(":", signedNames));

                // The signature covers the DKIM-Signature header itself with an empty b= value.
                var dkimField    = new DkimHeaderField("DKIM-Signature", " " + dkimValue, "DKIM-Signature: " + dkimValue);
                var signingInput = DkimCanonicalization.BuildHeaderHashInput(fields, signedNames, dkimField, headerCanon);

                var signature    = _privateKey.SignData(
                                        Encoding.UTF8.GetBytes(signingInput),
                                        HashAlgorithmName.SHA256,
                                        RSASignaturePadding.Pkcs1
                                    );

                var foldedSig    = FoldBase64(Convert.ToBase64String(signature), 76);

                _logger.Log(LogLevel.Debug, $"DKIM signature generated for domain {_config.Domain}");

                return $"DKIM-Signature: {dkimValue}{foldedSig}\r\n" + message;

            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"DKIM signing failed: {ex.Message}");
                return message;
            }
        }

        private string BuildDkimHeaderValue(string bodyHash, long timestamp, string signedHeaders)

            => new StringBuilder().
                   Append("v=1; ").
                   Append("a=rsa-sha256; ").
                   Append($"c={_config.Canonicalization}; ").
                   Append($"d={_config.Domain}; ").
                   Append($"s={_config.Selector}; ").
                   Append($"t={timestamp}; ").
                   Append($"h={signedHeaders}; ").
                   Append($"bh={bodyHash}; ").
                   Append("b=").
                   ToString();

        private static string FoldBase64(string base64, int lineLength)
        {

            if (base64.Length <= lineLength)
                return base64;

            var sb = new StringBuilder();
            for (var i = 0; i < base64.Length; i += lineLength)
            {
                if (i > 0)
                    sb.Append("\r\n\t");
                sb.Append(base64.AsSpan(i, Math.Min(lineLength, base64.Length - i)));
            }

            return sb.ToString();

        }

    }

    #endregion

    #region DKIM Key Generator

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

    #endregion

}
