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
    /// Pure ARC (RFC 8617) chain validation, independent of DNS: the public key for a
    /// <c>(domain, selector)</c> is obtained through the injected resolver (which returns the
    /// base64 <c>p=</c> SubjectPublicKeyInfo, or null). <see cref="DNSVerifier"/> supplies a
    /// resolver backed by DNS; tests supply an in-memory one.
    /// </summary>
    public sealed class ArcValidator(
        Func<String, String, CancellationToken, Task<String?>>  publicKeyResolver,
        ILogger                                                 logger)
    {

        #region ValidateAsync(rawMessage)

        public async Task<ArcResult> ValidateAsync(String rawMessage, CancellationToken ct = default)
        {
            try
            {
                var (headerBlock, body) = DkimCanonicalization.Split(rawMessage);
                var fields              = DkimCanonicalization.ParseFields(headerBlock);

                var chain = ArcChain.Parse(fields);
                if (chain is null)
                    return ArcResult.None;
                if (!chain.WellFormed)
                    return ArcResult.Fail;

                // cv chain consistency: i=1 -> none, i>1 -> pass (RFC 8617 §5.2).
                foreach (var set in chain.Sets)
                {
                    var cv       = ArcChain.ParseTags(set.Seal.RawValue).GetValueOrDefault("cv", "").ToLowerInvariant();
                    var expected = set.Instance == 1 ? "none" : "pass";
                    if (cv != expected)
                    {
                        logger.Log(LogLevel.Debug, $"ARC: instance {set.Instance} cv={cv} (expected {expected}) => fail");
                        return ArcResult.Fail;
                    }
                }

                // Only the newest ARC-Message-Signature is validated against the message.
                if (!await VerifyMessageSignatureAsync(chain.Sets[^1].MessageSignature, fields, body, ct))
                {
                    logger.Log(LogLevel.Debug, "ARC: newest ARC-Message-Signature failed => fail");
                    return ArcResult.Fail;
                }

                // Every ARC-Seal must verify.
                foreach (var set in chain.Sets)
                {
                    if (!await VerifySealAsync(set, chain, ct))
                    {
                        logger.Log(LogLevel.Debug, $"ARC: seal at instance {set.Instance} failed => fail");
                        return ArcResult.Fail;
                    }
                }

                return ArcResult.Pass;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, $"ARC validation error: {ex.Message}");
                return ArcResult.Fail;
            }
        }

        #endregion

        #region (private) ARC-Message-Signature

        private async Task<bool> VerifyMessageSignatureAsync(
            DkimHeaderField sigField, List<DkimHeaderField> fields, string body, CancellationToken ct)
        {
            var p = ArcChain.ParseTags(sigField.RawValue);

            if (!p.TryGetValue("d", out var domain) || !p.TryGetValue("s", out var selector) ||
                !p.TryGetValue("b", out var signature) || !p.TryGetValue("bh", out var bodyHash))
                return false;

            var publicKey = await publicKeyResolver(domain, selector, ct);
            if (publicKey is null)
                return false;

            var algorithm = p.GetValueOrDefault("a", "rsa-sha256");
            var canon     = p.GetValueOrDefault("c", "relaxed/relaxed").Split('/');
            var bodyCanon = canon.Length > 1 ? canon[1].ToLowerInvariant() : canon[0].ToLowerInvariant();
            var hdrCanon  = canon[0].ToLowerInvariant();

            var sha256    = algorithm.Contains("sha256");
            var canonBody = DkimCanonicalization.CanonicalizeBody(body, bodyCanon);
            var computed  = Convert.ToBase64String(sha256
                                ? SHA256.HashData(Encoding.UTF8.GetBytes(canonBody))
                                : SHA1.HashData(Encoding.UTF8.GetBytes(canonBody)));
            if (computed != bodyHash)
                return false;

            var signingInput = DkimCanonicalization.BuildHeaderHashInput(
                                   fields, p.GetValueOrDefault("h", "").Split(':'), sigField, hdrCanon);

            return RsaVerify(publicKey, signingInput, signature, sha256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1);
        }

        #endregion

        #region (private) ARC-Seal

        private async Task<bool> VerifySealAsync(ArcSet set, ArcChain chain, CancellationToken ct)
        {
            var tags = ArcChain.ParseTags(set.Seal.RawValue);

            if (!tags.TryGetValue("d", out var domain) || !tags.TryGetValue("s", out var selector) ||
                !tags.TryGetValue("b", out var signature))
                return false;

            var publicKey = await publicKeyResolver(domain, selector, ct);
            if (publicKey is null)
                return false;

            var sha256 = tags.GetValueOrDefault("a", "rsa-sha256").Contains("sha256");
            return RsaVerify(publicKey, chain.BuildSealSigningInput(set.Instance), signature,
                             sha256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1);
        }

        #endregion

        #region (private) RsaVerify

        private static bool RsaVerify(string publicKeyBase64, string signingInput, string signatureBase64, HashAlgorithmName hash)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
                var sig = Convert.FromBase64String(signatureBase64.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", ""));
                return rsa.VerifyData(Encoding.UTF8.GetBytes(signingInput), sig, hash, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }

        #endregion

    }

}
