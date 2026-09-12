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

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    /// <summary>
    /// Who we are to the browser: the relying party id (a domain), its name
    /// and the origins a ceremony may come from.
    /// </summary>
    /// <param name="RpId">The relying party id, e.g. "localhost" or "demo.example.org".</param>
    /// <param name="RpName">The name shown by the authenticator.</param>
    /// <param name="Origins">The allowed origins, e.g. "http://localhost:8080".</param>
    /// <param name="RequireUserVerification">Whether the authenticator must verify the user (PIN, biometrics) every time.</param>
    public sealed record WebAuthnSettings(String                 RpId,
                                          String                 RpName,
                                          IReadOnlyList<String>  Origins,
                                          Boolean                RequireUserVerification = false);


    /// <summary>
    /// The result of a verified authentication.
    /// </summary>
    public sealed record AssertionResult(UInt32   SignCount,
                                         Boolean  UserVerified,
                                         Boolean  BackedUp);


    /// <summary>
    /// The two WebAuthn ceremonies as the server sees them: building the
    /// options for navigator.credentials and verifying what comes back.
    /// Implemented with .NET cryptography and the Styx CBOR reader; the
    /// JSON shapes are those of WebAuthn Level 3 (PublicKeyCredential.toJSON()).
    /// </summary>
    public static class WebAuthn
    {

        #region Data

        public const Int32  ES256   = -7;
        public const Int32  RS256   = -257;

        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

        // authenticator data flags
        private const Byte FlagUserPresent       = 0x01;
        private const Byte FlagUserVerified      = 0x04;
        private const Byte FlagBackupEligible    = 0x08;
        private const Byte FlagBackedUp          = 0x10;
        private const Byte FlagAttestedData      = 0x40;
        private const Byte FlagExtensionData     = 0x80;

        #endregion


        #region CreationOptions(Settings, Ceremony, UserId, UserName, DisplayName = null, ExistingPasskeys = null)

        /// <summary>
        /// PublicKeyCredentialCreationOptions for registering a passkey. A
        /// discoverable credential is required, so that signing in works
        /// without a username; user verification is preferred, not required.
        /// </summary>
        public static PublicKeyCredentialCreationOptions CreationOptions(WebAuthnSettings       Settings,
                                                                         Ceremony               Ceremony,
                                                                         User_Id                UserId,
                                                                         String                 UserName,
                                                                         String?                DisplayName        = null,
                                                                         IEnumerable<Passkey>?  ExistingPasskeys   = null)

            => new (
                   new PublicKeyCredentialRpEntity(Settings.RpId, Settings.RpName),
                   // The user handle is the internal id, never the email: it comes
                   // back with every assertion and identifies the account.
                   new PublicKeyCredentialUserEntity(Encoding.UTF8.GetBytes(UserId.ToString()), UserName, DisplayName ?? UserName),
                   Ceremony.Challenge,
                   [
                       new PublicKeyCredentialParameters(PublicKeyCredentialType.PublicKey, COSEAlgorithmIdentifiers.ES256),
                       new PublicKeyCredentialParameters(PublicKeyCredentialType.PublicKey, COSEAlgorithmIdentifiers.RS256)
                   ],
                   Timeout,
                   (ExistingPasskeys ?? []).Select(Descriptor),
                   new AuthenticatorSelectionCriteria(
                       ResidentKey:         ResidentKeyRequirement.Required,
                       RequireResidentKey:  true,
                       UserVerification:    UserVerification(Settings)
                   ),
                   Attestation:  AttestationConveyancePreference.None,
                   // credProps tells us afterwards whether the credential really is discoverable.
                   Extensions:   new AuthenticationExtensions(AuthenticationExtension.CredProps)
               );

        #endregion

        #region RequestOptions(Settings, Ceremony, AllowedPasskeys = null)

        /// <summary>
        /// PublicKeyCredentialRequestOptions for signing in. Without allowed
        /// passkeys the authenticator offers its discoverable credentials for
        /// this relying party; with them (username-first) it is told which
        /// credentials to use.
        /// </summary>
        public static PublicKeyCredentialRequestOptions RequestOptions(WebAuthnSettings       Settings,
                                                                       Ceremony               Ceremony,
                                                                       IEnumerable<Passkey>?  AllowedPasskeys   = null)

            => new (
                   Ceremony.Challenge,
                   Settings.RpId,
                   UserVerification(Settings),
                   Timeout,
                   (AllowedPasskeys ?? []).Select(Descriptor)
               );

        private static UserVerificationRequirement UserVerification(WebAuthnSettings Settings)

            => Settings.RequireUserVerification
                   ? UserVerificationRequirement.Required
                   : UserVerificationRequirement.Preferred;

        private static PublicKeyCredentialDescriptor Descriptor(Passkey Passkey)

            => new (
                   Base64Url.DecodeFromChars(Passkey.Id),
                   PublicKeyCredentialType.PublicKey,
                   Passkey.Transports.Select(AuthenticatorTransport.Parse)
               );

        #endregion


        #region TryVerifyRegistration(Settings, Ceremony, Credential, Name, out Passkey, out Error)

        /// <summary>
        /// Verify the credential the browser created and turn it into a passkey.
        /// </summary>
        /// <param name="Settings">Relying party settings.</param>
        /// <param name="Ceremony">The registration ceremony the challenge belongs to.</param>
        /// <param name="Credential">The PublicKeyCredential as JSON (toJSON() shape).</param>
        /// <param name="Name">The name the user chose for the passkey.</param>
        /// <param name="Passkey">The verified passkey.</param>
        /// <param name="Error">Why the registration was refused.</param>
        public static Boolean TryVerifyRegistration(WebAuthnSettings                   Settings,
                                                    Ceremony                           Ceremony,
                                                    JObject                            Credential,
                                                    String                             Name,
                                                    [NotNullWhen(true)]  out Passkey?  Passkey,
                                                    [NotNullWhen(false)] out String?   Error)
        {

            Passkey  = null;

            try
            {

                if (Credential["response"] is not JObject response)
                {
                    Error = "The credential has no response!";
                    return false;
                }

                var id                 = Credential.Value<String>("id")  ?? "";
                var rawId              = DecodeBase64Url(Credential.Value<String>("rawId"), "rawId");
                var clientDataJSON     = DecodeBase64Url(response.Value<String>("clientDataJSON"),    "clientDataJSON");
                var attestationObject  = DecodeBase64Url(response.Value<String>("attestationObject"), "attestationObject");

                if (id.Length == 0 || id != Base64Url.EncodeToString(rawId))
                {
                    Error = "The credential id does not match rawId!";
                    return false;
                }

                if (!VerifyClientData(clientDataJSON, "webauthn.create", Ceremony, Settings, out Error))
                    return false;

                // The attestation object is a CBOR map { fmt, attStmt, authData }.
                // We asked for "none" and do not evaluate attestation statements;
                // the authenticator data is what matters.
                var authData = CBORValue.Parse(attestationObject).GetValue("authData").AsBytes();

                if (!TryParseAuthenticatorData(authData, Settings, out var parsed, out Error))
                    return false;

                if (parsed.CredentialId is null || parsed.PublicKey is null)
                {
                    Error = "The authenticator data carries no credential!";
                    return false;
                }

                if (!parsed.CredentialId.AsSpan().SequenceEqual(rawId))
                {
                    Error = "The credential id in the authenticator data does not match!";
                    return false;
                }

                // The public key must be one we can use, and it must parse now
                // rather than at the first sign-in.
                var algorithm = ParseCOSEKey(parsed.PublicKey, out var ecdsa, out var rsa);
                ecdsa?.Dispose();
                rsa?.Dispose();

                var transports    = response["transports"] is JArray transportsJSON
                                        ? transportsJSON.Values<String>().OfType<String>().ToList()
                                        : [];

                var discoverable  = Credential["clientExtensionResults"]?["credProps"]?["rk"]?.Type == JTokenType.Boolean
                                        ? Credential["clientExtensionResults"]!["credProps"]!.Value<Boolean>("rk")
                                        : (Boolean?) null;

                Passkey = new Passkey(
                              Id:              id,
                              PublicKey:       parsed.PublicKey,
                              Algorithm:       algorithm,
                              SignCount:       parsed.SignCount,
                              Transports:      transports,
                              Discoverable:    discoverable,
                              BackupEligible:  (parsed.Flags & FlagBackupEligible) != 0,
                              BackedUp:        (parsed.Flags & FlagBackedUp)       != 0,
                              AAGUID:          parsed.AAGUID ?? Guid.Empty,
                              Name:            Name,
                              CreatedAt:       DateTimeOffset.UtcNow,
                              LastUsedAt:      null
                          );

                Error = null;
                return true;

            }
            catch (Exception e)
            {
                Error = $"The registration could not be verified: {e.Message}";
                return false;
            }

        }

        #endregion

        #region TryVerifyAuthentication(Settings, Ceremony, Credential, Passkey, ExpectedUserId, out Result, out Error)

        /// <summary>
        /// Verify an assertion against the stored passkey.
        /// </summary>
        /// <param name="Settings">Relying party settings.</param>
        /// <param name="Ceremony">The authentication ceremony the challenge belongs to.</param>
        /// <param name="Credential">The PublicKeyCredential as JSON (toJSON() shape).</param>
        /// <param name="Passkey">The stored passkey with the matching credential id.</param>
        /// <param name="ExpectedUserId">The id of the account owning the passkey.</param>
        /// <param name="Result">The new signature counter and flags.</param>
        /// <param name="Error">Why the assertion was refused.</param>
        public static Boolean TryVerifyAuthentication(WebAuthnSettings                           Settings,
                                                      Ceremony                                   Ceremony,
                                                      JObject                                    Credential,
                                                      Passkey                                    Passkey,
                                                      User_Id                                    ExpectedUserId,
                                                      [NotNullWhen(true)]  out AssertionResult?  Result,
                                                      [NotNullWhen(false)] out String?           Error)
        {

            Result = null;

            try
            {

                if (Credential["response"] is not JObject response)
                {
                    Error = "The credential has no response!";
                    return false;
                }

                var clientDataJSON     = DecodeBase64Url(response.Value<String>("clientDataJSON"),    "clientDataJSON");
                var authenticatorData  = DecodeBase64Url(response.Value<String>("authenticatorData"), "authenticatorData");
                var signature          = DecodeBase64Url(response.Value<String>("signature"),         "signature");
                var userHandle         = response.Value<String>("userHandle");

                // A discoverable credential tells us who it belongs to; it must
                // agree with the account we found by credential id.
                if (!String.IsNullOrEmpty(userHandle) &&
                    userHandle != Base64Url.EncodeToString(Encoding.UTF8.GetBytes(ExpectedUserId.ToString())))
                {
                    Error = "The user handle does not belong to this passkey!";
                    return false;
                }

                if (!VerifyClientData(clientDataJSON, "webauthn.get", Ceremony, Settings, out Error))
                    return false;

                if (!TryParseAuthenticatorData(authenticatorData, Settings, out var parsed, out Error))
                    return false;

                // The signature covers the authenticator data and the hash of the client data.
                var signedData = new Byte[authenticatorData.Length + 32];
                authenticatorData.CopyTo(signedData, 0);
                SHA256.HashData(clientDataJSON).CopyTo(signedData, authenticatorData.Length);

                if (!VerifySignature(Passkey, signedData, signature))
                {
                    Error = "The signature is invalid!";
                    return false;
                }

                // A counter that did not move on an authenticator that uses one
                // points at a cloned credential.
                if (parsed.SignCount != 0 || Passkey.SignCount != 0)
                {
                    if (parsed.SignCount <= Passkey.SignCount)
                    {
                        Error = "The signature counter did not increase; the passkey may have been cloned!";
                        return false;
                    }
                }

                Result = new AssertionResult(
                             SignCount:     parsed.SignCount,
                             UserVerified:  (parsed.Flags & FlagUserVerified) != 0,
                             BackedUp:      (parsed.Flags & FlagBackedUp)     != 0
                         );

                Error = null;
                return true;

            }
            catch (Exception e)
            {
                Error = $"The assertion could not be verified: {e.Message}";
                return false;
            }

        }

        #endregion


        #region (private) VerifyClientData(ClientDataJSON, ExpectedType, Ceremony, Settings, out Error)

        private static Boolean VerifyClientData(Byte[]                            ClientDataJSON,
                                                String                            ExpectedType,
                                                Ceremony                          Ceremony,
                                                WebAuthnSettings                  Settings,
                                                [NotNullWhen(false)] out String?  Error)
        {

            var clientData  = JObject.Parse(Encoding.UTF8.GetString(ClientDataJSON));
            var type        = clientData.Value<String>("type");
            var challenge   = clientData.Value<String>("challenge");
            var origin      = clientData.Value<String>("origin") ?? "";

            if (type != ExpectedType)
            {
                Error = $"Unexpected client data type '{type}'!";
                return false;
            }

            if (challenge != Ceremony.ChallengeBase64Url)
            {
                Error = "The challenge does not match!";
                return false;
            }

            if (!Settings.Origins.Any(allowed => allowed.Equals(origin, StringComparison.OrdinalIgnoreCase)))
            {
                Error = $"The origin '{origin}' is not allowed!";
                return false;
            }

            if (clientData.Value<Boolean?>("crossOrigin") == true)
            {
                Error = "Cross-origin ceremonies are not allowed!";
                return false;
            }

            Error = null;
            return true;

        }

        #endregion

        #region (private) TryParseAuthenticatorData(Data, Settings, out Parsed, out Error)

        private sealed record AuthenticatorData(Byte     Flags,
                                                UInt32   SignCount,
                                                Guid?    AAGUID,
                                                Byte[]?  CredentialId,
                                                Byte[]?  PublicKey);

        private static Boolean TryParseAuthenticatorData(Byte[]                                       Data,
                                                         WebAuthnSettings                             Settings,
                                                         [NotNullWhen(true)]  out AuthenticatorData?  Parsed,
                                                         [NotNullWhen(false)] out String?             Error)
        {

            Parsed = null;

            if (Data.Length < 37)
            {
                Error = "The authenticator data is too short!";
                return false;
            }

            var span = Data.AsSpan();

            if (!span[..32].SequenceEqual(SHA256.HashData(Encoding.UTF8.GetBytes(Settings.RpId))))
            {
                Error = $"The authenticator data is for another relying party than '{Settings.RpId}'!";
                return false;
            }

            var flags      = span[32];
            var signCount  = BinaryPrimitives.ReadUInt32BigEndian(span[33..37]);

            if ((flags & FlagUserPresent) == 0)
            {
                Error = "The user was not present!";
                return false;
            }

            if (Settings.RequireUserVerification && (flags & FlagUserVerified) == 0)
            {
                Error = "The user was not verified!";
                return false;
            }

            Guid?    aaguid        = null;
            Byte[]?  credentialId  = null;
            Byte[]?  publicKey     = null;
            var      position      = 37;

            if ((flags & FlagAttestedData) != 0)
            {

                if (Data.Length < position + 18)
                {
                    Error = "The attested credential data is truncated!";
                    return false;
                }

                aaguid         = new Guid(span.Slice(position, 16), bigEndian: true);
                position      += 16;

                var idLength   = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(position, 2));
                position      += 2;

                if (idLength == 0 || idLength > 1023 || Data.Length < position + idLength)
                {
                    Error = "The credential id length is invalid!";
                    return false;
                }

                credentialId   = span.Slice(position, idLength).ToArray();
                position      += idLength;

                // The COSE key is one CBOR map without a length prefix; extensions
                // may follow it, so a reader has to find where it ends.
                var reader     = new CBORReader(span[position..]);
                publicKey      = reader.ReadEncodedValue().ToArray();
                position      += reader.Position;

            }

            if ((flags & FlagExtensionData) != 0)
                new CBORReader(span[position..]).SkipValue();   // checked for well-formedness only

            Parsed = new AuthenticatorData(flags, signCount, aaguid, credentialId, publicKey);
            Error  = null;
            return true;

        }

        #endregion

        #region (private) COSE keys and signatures

        /// <summary>
        /// Parse a COSE_Key (RFC 9053): EC2 P-256 for ES256, RSA for RS256.
        /// Returns the algorithm and exactly one usable key object.
        /// </summary>
        private static Int32 ParseCOSEKey(Byte[]      COSEKey,
                                          out ECDsa?  ECDsa,
                                          out RSA?    RSA)
        {

            ECDsa  = null;
            RSA    = null;

            // COSE_Key (RFC 9053) labels: 1 = kty, 3 = alg; the negative labels
            // depend on the key type.
            var map = CBORValue.Parse(COSEKey);

            if (!map.ParseMandatoryInt64(1, "key type",  out var keyType,   out var error) ||
                !map.ParseMandatoryInt64(3, "algorithm", out var algorithm, out     error))
            {
                throw new FormatException($"The COSE key is invalid: {error}");
            }

            switch (keyType, algorithm)
            {

                case (2, ES256): {

                    // crv (-1) must be P-256 (1); x (-2) and y (-3) are the coordinates.
                    if (!map.ParseMandatoryInt64(-1, "curve",        out var curve, out error) ||
                        !map.ParseMandatoryBytes(-2, "x coordinate", out var x,     out error) ||
                        !map.ParseMandatoryBytes(-3, "y coordinate", out var y,     out error))
                    {
                        throw new FormatException($"The COSE key is invalid: {error}");
                    }

                    if (curve != 1 || x.Length != 32 || y.Length != 32)
                        throw new FormatException("ES256 keys must be on P-256 with 32-byte coordinates!");

                    ECDsa = System.Security.Cryptography.ECDsa.Create(new ECParameters {
                                Curve  = ECCurve.NamedCurves.nistP256,
                                Q      = new ECPoint { X = x, Y = y }
                            });

                    return ES256;

                }

                case (3, RS256): {

                    // n (-1) is the modulus, e (-2) the public exponent.
                    if (!map.ParseMandatoryBytes(-1, "modulus",  out var modulus,  out error) ||
                        !map.ParseMandatoryBytes(-2, "exponent", out var exponent, out error))
                    {
                        throw new FormatException($"The COSE key is invalid: {error}");
                    }

                    if (modulus.Length < 256 || exponent.Length == 0)
                        throw new FormatException("RS256 keys need a modulus of at least 2048 bits and an exponent!");

                    RSA = System.Security.Cryptography.RSA.Create();
                    RSA.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });

                    return RS256;

                }

                default:
                    throw new FormatException($"Unsupported COSE key type {keyType} with algorithm {algorithm}!");

            }

        }

        private static Boolean VerifySignature(Passkey  Passkey,
                                               Byte[]   SignedData,
                                               Byte[]   Signature)
        {

            var algorithm = ParseCOSEKey(Passkey.PublicKey, out var ecdsa, out var rsa);

            using (ecdsa)
            using (rsa)
            {

                return algorithm switch {
                           ES256  => ecdsa!.VerifyData(SignedData, Signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence),
                           RS256  => rsa!.  VerifyData(SignedData, Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
                           _      => false
                       };

            }

        }

        #endregion

        #region (private) DecodeBase64Url(Text, What)

        private static Byte[] DecodeBase64Url(String?  Text,
                                              String   What)
        {

            if (String.IsNullOrEmpty(Text))
                throw new FormatException($"'{What}' is missing!");

            try
            {
                return Base64Url.DecodeFromChars(Text);
            }
            catch (FormatException)
            {
                throw new FormatException($"'{What}' is not base64url!");
            }

        }

        #endregion

    }

}
