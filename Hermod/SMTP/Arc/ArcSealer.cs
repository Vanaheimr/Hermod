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

    /// <summary>
    /// Adds a new ARC set (ARC-Authentication-Results, ARC-Message-Signature, ARC-Seal) to a
    /// message, extending or starting the Authenticated Received Chain (RFC 8617 §5.1). Used by
    /// an intermediary that re-sends a message it received (e.g. forwarding), so downstream
    /// receivers can still see the authentication results this hop observed even after SPF/DKIM
    /// break in transit. Reuses the shared DKIM canonicalizer so it interoperates with other
    /// ARC implementations.
    /// </summary>
    public sealed class ArcSealer
    {

        private readonly ArcConfig _config;
        private readonly RSA       _privateKey;
        private readonly ILogger   _logger;

        public ArcSealer(ArcConfig config, ILogger logger)
        {
            _config     = config;
            _logger     = logger;
            _privateKey = RSA.Create();
            _privateKey.ImportFromPem(config.PrivateKeyPem);
            logger.Log(LogLevel.Info, $"ARC sealer initialized: domain={config.Domain}, selector={config.Selector}");
        }

        #region Seal(message, authServId, authResults, chainStatus)

        /// <summary>
        /// Prepend a new ARC set to <paramref name="message"/>.
        /// </summary>
        /// <param name="authServId">The authserv-id (this host's identity).</param>
        /// <param name="authResults">The auth method results, e.g. "spf=pass smtp.mailfrom=…; dkim=pass header.d=…".</param>
        /// <param name="chainStatus">Validation status of the chain as received (from <c>VerifyArcAsync</c>).</param>
        public String Seal(String message, String authServId, String authResults, ArcResult chainStatus)
        {
            try
            {
                var (headerBlock, body) = DkimCanonicalization.Split(message);
                var fields              = DkimCanonicalization.ParseFields(headerBlock);

                var priorChain = ArcChain.Parse(fields);
                var instance   = (priorChain?.MaxInstance ?? 0) + 1;

                // cv: first hop -> none; otherwise the validation result of the received chain.
                // A chain that arrived broken is sealed cv=fail and stays dead (RFC 8617 §5.1.1).
                var cv = instance == 1
                             ? "none"
                             : chainStatus == ArcResult.Pass ? "pass" : "fail";

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // --- ARC-Authentication-Results (AAR) ---
                var aarValue = $"i={instance}; {authServId}" + (string.IsNullOrWhiteSpace(authResults) ? "" : $"; {authResults}");
                var aarField = Field("ARC-Authentication-Results", aarValue);

                // --- ARC-Message-Signature (AMS) --- a DKIM-style signature over the message.
                var canonBody = DkimCanonicalization.CanonicalizeBody(body, "relaxed");
                var bodyHash  = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(canonBody)));

                var signedNames = _config.SignedHeaders.Split(':')
                                      .Select(h => h.Trim())
                                      .Where(h => h.Length > 0 && fields.Any(f => f.Name.Equals(h, StringComparison.OrdinalIgnoreCase)))
                                      .ToList();

                var amsValue = $"i={instance}; a=rsa-sha256; c=relaxed/relaxed; d={_config.Domain}; s={_config.Selector}; " +
                               $"t={timestamp}; h={string.Join(":", signedNames)}; bh={bodyHash}; b=";
                var amsField = Field("ARC-Message-Signature", amsValue);

                var amsInput = DkimCanonicalization.BuildHeaderHashInput(fields, signedNames, amsField, "relaxed");
                var amsSig   = Sign(amsInput);
                var foldedSig = Fold(amsSig);
                var amsFull  = $"ARC-Message-Signature: {amsValue}{foldedSig}";

                // Re-materialize the AMS field WITH its signature for the seal input. It must
                // use the FOLDED signature exactly as it appears in the message, because relaxed
                // canonicalization turns each fold into a space — so an unfolded copy here would
                // hash differently from what the verifier re-parses.
                var amsSignedField = Field("ARC-Message-Signature", amsValue + foldedSig);

                // --- ARC-Seal (AS) --- signs the whole chain incl. this instance.
                var asValue = $"i={instance}; a=rsa-sha256; d={_config.Domain}; s={_config.Selector}; t={timestamp}; cv={cv}; b=";
                var asField = Field("ARC-Seal", asValue);

                var sets = new List<ArcSet>(priorChain?.Sets ?? []) { new (instance, asField, amsSignedField, aarField) };
                var asInput = ArcChain.BuildSealSigningInput(sets, instance);
                var asSig   = Sign(asInput);
                var asFull  = $"ARC-Seal: {asValue}{Fold(asSig)}";

                var aarFull = $"ARC-Authentication-Results: {aarValue}";

                // Prepend the new set (order among the three is irrelevant to verification).
                return $"{asFull}\r\n{amsFull}\r\n{aarFull}\r\n{message}";
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"ARC sealing failed: {ex.Message}");
                return message;
            }
        }

        #endregion

        #region (private) helpers

        private static DkimHeaderField Field(String name, String value)
            => new (name, " " + value, $"{name}: {value}");

        private String Sign(String input)
            => Convert.ToBase64String(_privateKey.SignData(Encoding.UTF8.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        private static String Fold(String base64)
        {
            if (base64.Length <= 76)
                return base64;
            var sb = new StringBuilder();
            for (var i = 0; i < base64.Length; i += 76)
            {
                if (i > 0) sb.Append("\r\n\t");
                sb.Append(base64.AsSpan(i, Math.Min(76, base64.Length - i)));
            }
            return sb.ToString();
        }

        #endregion

    }

}
