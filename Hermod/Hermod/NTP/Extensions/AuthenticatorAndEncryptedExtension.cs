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

using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    public class AuthenticatorAndEncryptedExtension : NTPExtension
    {

        public Byte[]                     Nonce                  { get; }
        public Byte[]                     Ciphertext             { get; }
        public IEnumerable<NTPExtension>  EncryptedExtensions    { get; }

        public AuthenticatorAndEncryptedExtension(Byte[]                      Nonce,
                                                  Byte[]                      Ciphertext,
                                                  IEnumerable<NTPExtension>?  EncryptedExtensions   = null)

            : base(ExtensionTypes.AuthenticatorAndEncrypted,
                   new Byte[4 + ((Nonce.Length + 3) & ~3) + ((Ciphertext.Length + 3) & ~3)])

        {

            this.Nonce                  = Nonce;
            this.Ciphertext             = Ciphertext;
            this.EncryptedExtensions    = EncryptedExtensions ?? [];

            var nonceLength             = Nonce.Length;
            var ciphertextLength        = Ciphertext.Length;// Math.Max(Ciphertext.Length - 16, 16);

            var paddedNonceLength       = (nonceLength      + 3) & ~3;
            //var paddedciphertextLength  = (ciphertextLength + 3) & ~3;

            Value[0] = (Byte) ((nonceLength      >> 8) & 0xff);
            Value[1] = (Byte)  (nonceLength            & 0xff);

            Value[2] = (Byte) ((ciphertextLength >> 8) & 0xff);
            Value[3] = (Byte)  (ciphertextLength       & 0xff);

            Buffer.BlockCopy(Nonce,      0, Value, 4,                     nonceLength);
            Buffer.BlockCopy(Ciphertext, 0, Value, 4 + paddedNonceLength, ciphertextLength);

        }

        public static Boolean TryParse(Byte[]                                                        ReceivedValue,
                                       IEnumerable<Byte[]>                                           AssociatedData,
                                       ref List<NTPExtension>                                        AuthenticatedExtensions,
                                       Byte[]                                                        Key, // S2C or C2S key!
                                       [NotNullWhen(true)]  out AuthenticatorAndEncryptedExtension?  AuthExtension,
                                       [NotNullWhen(false)] out String?                              ErrorResponse)
        {

            ErrorResponse = null;
            AuthExtension = null;

            if (ReceivedValue is null || ReceivedValue.Length < 4)
            {
                ErrorResponse = "NTS Authenticator and Encrypted extension value is null or too short!";
                return false;
            }

            var nonceLength                  = (UInt16) ((ReceivedValue[0] << 8) | ReceivedValue[1]);
            var ciphertextLength             = (UInt16) ((ReceivedValue[2] << 8) | ReceivedValue[3]);

            var paddedNonceLength            = (nonceLength      + 3) & ~3;
            var paddedCiphertextLength       = (ciphertextLength + 3) & ~3;

            var expectedTotalLength          = 4 + paddedNonceLength + paddedCiphertextLength;
            if (ReceivedValue.Length != expectedTotalLength)
            {
                ErrorResponse = "NTS Authenticator and Encrypted extension value has unexpected length!";
                return false;
            }

            var receivedNonce             = new Byte[nonceLength];
            Buffer.BlockCopy(ReceivedValue, 4, receivedNonce, 0, nonceLength);

            var receivedCiphertext        = new Byte[ciphertextLength];
            if (ciphertextLength > 0)
                Buffer.BlockCopy(ReceivedValue, 4 + paddedNonceLength, receivedCiphertext, 0, ciphertextLength);

            // Recompute the AEAD output using AES-SIV.
            var aesSiv                    = new AES_SIV(Key);
            var computedOutput            = aesSiv.Decrypt(AssociatedData, receivedNonce, receivedCiphertext);
            var extensions                = new List<NTPExtension>();

            var offset = 0;
            while (offset + 4 <= computedOutput.Length)
            {

                var type   = (ExtensionTypes) ((computedOutput[offset]     << 8) | computedOutput[offset + 1]);
                var length = (UInt16)         ((computedOutput[offset + 2] << 8) | computedOutput[offset + 3]);

                if (length < 4)
                {
                    ErrorResponse = $"Illegal length of extension {length} at offset {offset}!";
                    return false;
                }

                if (offset + length > computedOutput.Length)
                    break;

                var data = new Byte[length - 4];
                Array.Copy(computedOutput, offset + 4, data, 0, length - 4);

                switch (type)
                {

                    case ExtensionTypes.UniqueIdentifier:
                        break;

                    case ExtensionTypes.NTSCookie:
                        extensions.Add(
                            new NTSCookieExtension(data, Authenticated: true, Encrypted: true)
                        );
                        break;

                    case ExtensionTypes.NTSCookiePlaceholder:
                        break;

                    case ExtensionTypes.AuthenticatorAndEncrypted:
                        break;

                    case ExtensionTypes.Debug:
                        if (DebugExtension.TryParse(data,
                                                    out var debugExtension,
                                                    out     ErrorResponse,
                                                    Authenticated: true,
                                                    Encrypted:     true))
                        {
                            extensions.Add(debugExtension);
                        }
                        break;

                    default:
                        extensions.Add(
                            new NTPExtension(
                                type,
                                data
                            )
                        );
                        break;

                }

                offset += length;

            }

            AuthExtension = new AuthenticatorAndEncryptedExtension(
                                receivedNonce,
                                receivedCiphertext,
                                extensions
                            );

            return true;

        }


        #region Create(NTSKEResponse, AssociatedData, Plainttext = null, Nonce = null)

        /// <summary>
        /// Create a "NTS Authenticator and Encrypted Extension Fields" extension (type=0x0404)
        /// </summary>
        /// <param name="NTSKEResponse">A Network Time Security Key Establishment (NTS-KE) response containing the C2S key.</param>
        /// <param name="AssociatedData">An array of byte arrays to be authenticated but not encrypted.</param>
        /// <param name="Plaintext">The optional plaintext to be encrypted (e.g. internal extension fields).</param>
        /// <param name="Nonce">The optional nonce to be used for encryption.</param>
        public static AuthenticatorAndEncryptedExtension

            Create(NTSKE_Response  NTSKEResponse,
                   IList<Byte[]>   AssociatedData,
                   Byte[]?         Plaintext   = null,
                   Byte[]?         Nonce       = null)

        {

            var nonce = Nonce ?? new Byte[16];

            if (Nonce is null)
                RandomNumberGenerator.Fill(nonce);

            return new AuthenticatorAndEncryptedExtension(
                       nonce,
                       new AES_SIV(NTSKEResponse.C2SKey).Encrypt(
                                                             [ AssociatedData.Aggregate() ],
                                                             nonce,
                                                             Plaintext ?? []
                                                         )
                   );

        }

        #endregion


    }

}
