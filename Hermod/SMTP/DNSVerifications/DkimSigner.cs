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

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// Signs a message with the DKIM key(s) of a domain (RFC 6376): RSA, Ed25519
    /// (RFC 8463), or both - each key adding a DKIM-Signature of its own, so that a
    /// verifier that knows only RSA finds that one, and one that knows Ed25519 the
    /// shorter, stronger one.
    /// </summary>
    public sealed class DkimSigner
    {

        private sealed record Key(DkimConfig Config, RSA? Rsa, Ed25519PrivateKeyParameters? Ed25519);

        private readonly IReadOnlyList<Key>  _keys;
        private readonly ILogger             _logger;

        public DkimSigner(DkimConfig config, ILogger logger)
            : this([ config ], logger)
        { }

        /// <param name="configs">The keys of the domain, one signature each - RSA and Ed25519 PEM alike.</param>
        /// <param name="logger">Where it says what it does.</param>
        public DkimSigner(IReadOnlyList<DkimConfig> configs, ILogger logger)
        {

            if (configs.Count == 0)
                throw new ArgumentException("A DKIM signer needs a key.", nameof(configs));

            _logger = logger;
            _keys   = [ .. configs.Select(config => {

                          if (DkimKeyMaterial.Ed25519Of(config.PrivateKeyPem) is { } ed25519)
                              return new Key(config, null, ed25519);

                          var rsa = RSA.Create();
                          rsa.ImportFromPem(config.PrivateKeyPem);
                          return new Key(config, rsa, null);

                      }) ];

            foreach (var key in _keys)
                logger.Log(LogLevel.Info, $"DKIM signer initialized: domain={key.Config.Domain}, selector={key.Config.Selector}, {(key.Ed25519 is not null ? "ed25519" : "rsa")}");

        }

        /// <summary>
        /// Prepend a DKIM-Signature header to the message (RFC 6376). Uses the shared
        /// canonicalizer and hashes over UTF-8 octets so the signature matches what
        /// receiving verifiers compute over the wire bytes. On any error the message is
        /// returned unchanged (unsigned).
        /// </summary>
        public string SignMessage(string message)
            => SignAll(message, Encoding.UTF8);

        /// <summary>
        /// Prepend a DKIM-Signature header to a message given as octets - signed over
        /// exactly those octets, whatever charset an 8-bit body is in. On any error
        /// the message is returned unchanged (unsigned).
        /// </summary>
        public Byte[] SignOctets(Byte[] Message)
            => MessageOctets.ToOctets(SignAll(MessageOctets.ToText(Message), Encoding.Latin1));

        private string SignAll(string message, Encoding octets)
        {

            foreach (var key in _keys)
                message = Sign(key, message, octets);

            return message;

        }

        /// <param name="octets">How the text becomes the octets on the wire: UTF-8 for text, Latin-1 for octets read as text.</param>
        private string Sign(Key key, string message, Encoding octets)
        {

            var config = key.Config;

            try
            {

                var (headerBlock, body) = DkimCanonicalization.Split(message);
                var fields              = DkimCanonicalization.ParseFields(headerBlock);

                var slash        = config.Canonicalization.Split('/');
                var headerCanon  = slash[0].ToLowerInvariant();
                var bodyCanon    = slash.Length > 1 ? slash[1].ToLowerInvariant() : headerCanon;

                // Body hash over the original wire octets (UTF-8), not ASCII.
                var canonBody    = DkimCanonicalization.CanonicalizeBody(body, bodyCanon);
                var bodyHash     = Convert.ToBase64String(SHA256.HashData(octets.GetBytes(canonBody)));

                // Only advertise/sign headers that are actually present.
                var signedNames  = config.SignedHeaders.Split(':').
                                       Select(h => h.Trim()).
                                       Where (h => h.Length > 0 &&
                                                   fields.Any(f => f.Name.Equals(h, StringComparison.OrdinalIgnoreCase))).
                                       ToList();

                var timestamp    = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var dkimValue    = BuildDkimHeaderValue(config, key.Ed25519 is not null ? "ed25519-sha256" : "rsa-sha256", bodyHash, timestamp, string.Join(":", signedNames));

                // The signature covers the DKIM-Signature header itself with an empty b= value.
                var dkimField    = new DkimHeaderField("DKIM-Signature", " " + dkimValue, "DKIM-Signature: " + dkimValue);
                var signingInput = DkimCanonicalization.BuildHeaderHashInput(fields, signedNames, dkimField, headerCanon);

                // Ed25519 signs the SHA-256 hash of the input (RFC 8463 §3), RSA the input itself.
                Byte[] signature;

                if (key.Ed25519 is { } ed25519)
                {
                    var hash   = SHA256.HashData(octets.GetBytes(signingInput));
                    var signer = new Ed25519Signer();
                    signer.Init(true, ed25519);
                    signer.BlockUpdate(hash, 0, hash.Length);
                    signature  = signer.GenerateSignature();
                }
                else
                    signature  = key.Rsa!.SignData(octets.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                var foldedSig    = FoldBase64(Convert.ToBase64String(signature), 76);

                _logger.Log(LogLevel.Debug, $"DKIM signature generated for domain {config.Domain}");

                return $"DKIM-Signature: {dkimValue}{foldedSig}\r\n" + message;

            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"DKIM signing failed: {ex.Message}");
                return message;
            }
        }

        private static string BuildDkimHeaderValue(DkimConfig config, string algorithm, string bodyHash, long timestamp, string signedHeaders)

            => new StringBuilder().
                   Append("v=1; ").
                   Append($"a={algorithm}; ").
                   Append($"c={config.Canonicalization}; ").
                   Append($"d={config.Domain}; ").
                   Append($"s={config.Selector}; ").
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

}
