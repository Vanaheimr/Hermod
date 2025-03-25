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

using System.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using ECPoint = Org.BouncyCastle.Math.EC.ECPoint;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Passkeys;
using Org.BouncyCastle.Asn1.X9;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    public class WebAuthnService(String  Name,
                                 String  Hostname)
    {

        #region Properties

        public String  Name        { get; } = Name;

        public String  Hostname    { get; } = Hostname;


        #endregion


        #region Challenges

        private readonly ConcurrentDictionary<User_Id, Byte[]> challenges = [];

        public void StoreChallengeForLogin(User_Id UserId, Byte[] Challenge)
        {

            if (!challenges.TryAdd(UserId, Challenge))
                challenges[UserId] = Challenge;

            DebugX.Log($"Stored passkey challenge '{Challenge.ToBase64URL()}' for user '{UserId}'!");

        }

        public Byte[] GetStoredChallenge(User_Id UserId)
            => challenges[UserId];

        #endregion

        #region StoredCredentials

        private readonly ConcurrentDictionary<User_Id, List<StoredCredential>> storedCredentials = [];

        public void SaveCredentialForUser(User_Id           UserId,
                                          StoredCredential  Credential)
        {

            if (!storedCredentials.TryGetValue(UserId, out var credentials))
            {
                credentials = [];
                storedCredentials.TryAdd(UserId, credentials);
            }

            credentials.Add(Credential);

        }

        public IEnumerable<StoredCredential> GetStoredCredentials(User_Id UserId)
        {

            if (storedCredentials.TryGetValue(UserId, out var credentials))
                return credentials;

            return [];

        }

        public User_Id? GetUserForCredentialId(String CId)
        {

            foreach (var kvp in storedCredentials)
            {
                foreach (var credential in kvp.Value)
                {
                    if (credential.CredentialId == CId)
                        return kvp.Key;
                }
            }

            return null;

        }

        public StoredCredential? GetCredential(String CId)
        {

            foreach (var kvp in storedCredentials)
            {
                foreach (var credential in kvp.Value)
                {
                    if (credential.CredentialId == CId)
                        return credential;
                }
            }

            return null;

        }

        #endregion


        #region GenerateRegistrationOptions (User)

        public PublicKeyCredentialCreationOptions GenerateRegistrationOptions(IUser User)
        {

            var challenge  = new Byte[32];
            RandomNumberGenerator.Fill(challenge);

            StoreChallengeForLogin(User.Id, challenge);

            var options    = new PublicKeyCredentialCreationOptions(
                                 Challenge:            challenge,
                                 RelyingParty:         new PublicKeyCredentialRpEntity(
                                                           Id:          Hostname,
                                                           Name:        Name
                                                       ),
                                 User:                 new PublicKeyCredentialUserEntity(
                                                           Id:           Encoding.UTF8.GetBytes(User.Id.ToString()),
                                                           Name:         User.Id.  ToString(),
                                                           DisplayName:  User.Name.FirstText()
                                                       ),
                                 PubKeyCredParams:     [
                                                           new (
                                                               PublicKeyCredentialType. PublicKey,
                                                               COSEAlgorithmIdentifiers.ES256
                                                           ),
                                                           new (
                                                               PublicKeyCredentialType. PublicKey,
                                                               COSEAlgorithmIdentifiers.ES384
                                                           ),
                                                           new (
                                                               PublicKeyCredentialType. PublicKey,
                                                               COSEAlgorithmIdentifiers.ES512
                                                           )
                                                       ],
                                 Timeout:              TimeSpan.FromSeconds(60),
                                 Attestation:          AttestationConveyancePreference.Direct,
                                 ExcludeCredentials:   GetRegisteredCredentialsForUser(User) // Avoid duplicates!
                             );

            return options;

        }


        /// <summary>
        /// Dummy-Implementierung: Gibt eine leere Liste zurück.
        /// Hier solltest du in einer echten Anwendung aus deiner Datenbank alle bereits registrierten Credentials des Benutzers abrufen.
        /// </summary>
        private IEnumerable<PublicKeyCredentialDescriptor> GetRegisteredCredentialsForUser(IUser currentUser)
        {
            // Beispiel: Wenn du bereits gespeicherte Credentials hast, kannst du diese hier konvertieren:
            // return _credentialRepository.GetCredentialsByUserId(currentUser.Id)
            //         .Select(c => new PublicKeyCredentialDescriptor
            //         {
            //             Type = "public-key",
            //             Id = Convert.FromBase64String(c.CredentialId),
            //             Transports = c.Transports // sofern vorhanden
            //         }).ToList();
            return [];
        }

        #endregion

        #region VerifyAndRegisterCredential (UserId, RegistrationData)

        public VerificationResult VerifyAndRegisterCredential(User_Id                     UserId,
                                                              CredentialRegistrationData  RegistrationData)
        {

            // {
            //     "id":    "stYg9baewMM-ZV_m0-rBefNrIt6rSN2KSuPtsOPhNUI",
            //     "rawId": "stYg9baewMM+ZV/m0+rBefNrIt6rSN2KSuPtsOPhNUI=",
            //     "type":  "public-key",
            //     "response": {
            //         "clientDataJSON":    "eyJ0eXBlIjoid2ViYXV0aG4uY3JlYXRlIiwiY2hhbGxlbmdlIjoiSnhjaWVGUTJueUxWZm8zWi1JTjlIRnhlVWtOSldiUmZpekxQckpIbk9DNCIsIm9yaWdpbiI6Imh0dHA6Ly9sb2NhbGhvc3Q6OTAwMCIsImNyb3NzT3JpZ2luIjpmYWxzZX0=",
            //         "attestationObject": "o2NmbXRkbm9uZWdhdHRTdG10oGhhdXRoRGF0YVikSZYN5YgOjGh0NBcPZHZgW4/krrmihjLHmVzzuoMdl2NFAAAAAAiYcFjK3EuBtuEw3lDcvpYAILLWIPW2nsDDPmVf5tPqwXnzayLeq0jdikrj7bDj4TVCpQECAyYgASFYIEbKnY9jluznYxkvOivvEq/1z5g9XPHN0bZVSRG8DK1bIlggyftdKhZgi8AqPGPVbnh+6nN8AcjfkI2xhQtJ40ez510="
            //     }
            // }

            #region ClientData JSON

            ClientData clientData;

            try
            {

                clientData = ClientData.Parse(JObject.Parse(Encoding.UTF8.GetString(RegistrationData.Response.ClientDataJSON.FromBASE64())));

                // {
                //   "type":        "webauthn.create",
                //   "challenge":   "MGKsCCrae_9LI-6ZBVWA1DeEcVSb321M-G3UPXfd3GM",
                //   "origin":      "http://localhost:9000",
                //   "crossOrigin":  false
                // }

            }
            catch (Exception e)
            {
                return VerificationResult.Failed("Deserialisation of data.Response.ClientDataJSON failed: " + e.Message);
            }

            DebugX.Log("VerifyAndRegisterCredential => " + Environment.NewLine + clientData.ToJSON().ToString());


            if (clientData.Type      != "webauthn.create")
                return VerificationResult.Failed($"Invalid clientData.Type '{clientData.Type}'!");

            if (clientData.Challenge != GetStoredChallenge(UserId).ToBase64URL())
                return VerificationResult.Failed($"Invalid clientData.Challenge '{clientData.Challenge}' != '{GetStoredChallenge(UserId).ToBase64URL()}'!");

            if (clientData.Origin    == "")
                return VerificationResult.Failed($"Invalid clientData.Origin '{clientData.Origin}'!");

            #endregion

            #region AttestationObject CBOR

            byte[] attestationObjectBytes;
            try
            {
                attestationObjectBytes = Convert.FromBase64String(RegistrationData.Response.AttestationObject);
            }
            catch (Exception e)
            {
                return VerificationResult.Failed("Could not decode attestation object: " + e.Message);
            }

            var attestationObjectJSON = CborToJsonConverter.CBOR2JSON(attestationObjectBytes) ?? [];

            // {
            //   "fmt":       "none",
            //   "attStmt":    {},
            //   "authData":  "SZYN5YgOjGh0NBcPZHZgW4/krrmihjLHmVzzuoMdl2NFAAAAAAiYcFjK3EuBtuEw3lDcvpYAIFb82xznSWVH0S++gAmTjwgbHZhWC6Uw98/aRFfBO+/GpQECAyYgASFYIAI2PyygAcJcwQY3D35ITfRzs/lqjZBP2Yyfwf89PgMBIlgg96NtcKB4Ebqzab4Qb9XGEc50aJqxCanLvVfOMz0/U18="
            // }

            DebugX.Log("AttestationObject => " + Environment.NewLine + attestationObjectJSON.ToString());

            var authData = attestationObjectJSON["authData"]?.Value<Byte[]>() ?? [];

            if (authData.Length == 0)
                return VerificationResult.Failed("No authData found within the AttestationObject!");

            #endregion

            #region Verify authenticator data (z. B. RP-ID-Hash, Flags etc.)

            // Validate/check RP-ID-Hash and flags (User Presence, User Verification)
            //if (authData.)
            //{
            //    return VerificationResult.Failed(ErrorMessage: "Authenticator-Daten Überprüfung fehlgeschlagen");
            //}

            #endregion

            #region Extract the public key from authData

            byte[]? publicKeyBytes = null;

            try
            {

                // Extrahiert den CBOR-kodierten Public Key aus den Authenticator-Daten.
                // Das Format:
                // [32 Bytes RP-ID Hash] [1 Byte Flags] [4 Bytes SignCount] [AttestedCredentialData]
                // Im AttestedCredentialData:
                //   16 Bytes AAGUID, 2 Bytes Credential ID Length, Credential ID, CBOR-kodierter Public Key

                // Überspringe: 32 (RP-ID Hash) + 1 (Flags) + 4 (SignCount)
                int offset = 37;
                // Überspringe AAGUID (16 Bytes)
                offset += 16;
                // Lese Credential ID Length (2 Bytes, Big-Endian)
                ushort credIdLen = (ushort) ((authData[offset] << 8) | authData[offset + 1]);
                offset += 2;
                // Überspringe Credential ID
                offset += credIdLen;
                // Der verbleibende Teil ist der CBOR-kodierte Public Key
                var remaining = authData.Length - offset;
                publicKeyBytes = new Byte[remaining];
                Array.Copy(authData, offset, publicKeyBytes, 0, remaining);

                if (publicKeyBytes is null || publicKeyBytes.Length == 0)
                    return VerificationResult.Failed("Extracting the public key failed!");

            }
            catch (Exception e)
            {
                return VerificationResult.Failed("Extracting the public key failed: " + e.Message);
            }

            #endregion

            #region Verify the attestation statement

            //if (!VerifyAttestationStatement(attestationObjectBytes))
            //{
            //    return VerificationResult.Failed(ErrorMessage: "Attestationsstatement Überprüfung fehlgeschlagen");
            //}

            #endregion

            #region Read the sign counter from authData

            var signCount = 0U;

            if (authData.Length >= 37)
            {
                // SignCount beginnt bei Index 33 (Byte 34-37)
                signCount = (UInt32) ((authData[33] << 24) | (authData[34] << 16) | (authData[35] << 8) | authData[36]);
            }

            #endregion


            SaveCredentialForUser(
                UserId,
                new StoredCredential(
                    CredentialId:  RegistrationData.Id,
                    PublicKey:     publicKeyBytes,
                    SignCount:     signCount
                )
            );

            return VerificationResult.Success();

        }

        #endregion



        #region (private static) VerifyAssertionSignature  (PublicKeyBytes, AuthenticatorData, ClientDataJSON, Signature, EllipticCurveName = "secp256r1")

        /// <summary>
        /// Verify the signature of the assertion.
        /// </summary>
        /// <param name="PublicKeyBytes">The public key as CBOR encoded COSE-Key.</param>
        /// <param name="AuthenticatorData">The authenticator data (in binary format) from the client.</param>
        /// <param name="ClientDataJSON">The client data JSON from the client as byte array.</param>
        /// <param name="Signature">The signature from the authenticator (DER encoded).</param>
        /// <param name="EllipticCurveName">An optional elliptic curve name (default: secp256r1)</param>
        public static Boolean VerifyAssertionSignature(Byte[]  PublicKeyBytes,
                                                       Byte[]  AuthenticatorData,
                                                       Byte[]  ClientDataJSON,
                                                       Byte[]  Signature,
                                                       String  EllipticCurveName = "secp256r1")
        {

            var clientDataHash  = SHA256.HashData(ClientDataJSON);

            var signedData      = new Byte[AuthenticatorData.Length + clientDataHash.Length];
            Array.Copy(AuthenticatorData, 0, signedData, 0,                        AuthenticatorData.Length);
            Array.Copy(clientDataHash,    0, signedData, AuthenticatorData.Length, clientDataHash.   Length);

            var ellipticCurve   = SecNamedCurves.GetByName(EllipticCurveName);

            var domainParams    = new ECDomainParameters(
                                      ellipticCurve.Curve,
                                      ellipticCurve.G,
                                      ellipticCurve.N,
                                      ellipticCurve.H
                                  );

            var pubKeyParams    = new ECPublicKeyParameters(
                                      ExtractECPointFromCOSEKey(
                                          PublicKeyBytes,
                                          ellipticCurve
                                      ),
                                      domainParams
                                  );

            var signer = SignerUtilities.GetSigner("SHA-256withECDSA");
            signer.Init(false, pubKeyParams);
            signer.BlockUpdate(signedData, 0, signedData.Length);

            return signer.VerifySignature(Signature);

        }

        #endregion

        #region (private static) ExtractECPointFromCOSEKey (COSEKeyBytes, EllipticCurve)

        /// <summary>
        /// Extracts the EC point (X and Y coordinate) from a CBOR-encoded COSE_Key.
        /// </summary>
        /// <param name="COSEKeyBytes">The public key as CBOR encoded COSE-Key.</param>
        /// <param name="EllipticCurve">The elliptic curve to use.</param>
        private static ECPoint ExtractECPointFromCOSEKey(Byte[]          COSEKeyBytes,
                                                         X9ECParameters  EllipticCurve)
        {

            var reader    = new CborReader(COSEKeyBytes);
            var mapLength = reader.ReadStartMap();

            byte[]? x = null;
            byte[]? y = null;

            for (var i = 0; i < mapLength; i++)
            {

                var key = reader.ReadInt32();

                switch (key)
                {

                    case -2: // x-coordinate
                        x = reader.ReadByteString();
                        break;

                    case -3: // y-coordinate
                        y = reader.ReadByteString();
                        break;

                    default:
                        reader.SkipValue();
                        break;

                }
            }

            reader.ReadEndMap();

            if (x is null)
                throw new Exception("The x-coordinate of the public key is missing!");

            if (y is null)
                throw new Exception("The y-coordinate of the public key is missing!");

            return EllipticCurve.
                       Curve.CreatePoint(
                           new BigInteger(1, x),
                           new BigInteger(1, y)
                       );
        }

        #endregion


        public Boolean ValidateHostname(String URL)
        {

            var protocolEndIndex = URL.IndexOf("://");
            if (protocolEndIndex >= 0)
                URL = URL.Substring(protocolEndIndex + 3);

            var lastColonIndex = URL.LastIndexOf(':');
            if (lastColonIndex >= 0)
                URL = URL.Substring(0, lastColonIndex);

            return URL == Hostname;

        }


    }

}
