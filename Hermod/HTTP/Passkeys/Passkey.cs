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

using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    /// <summary>
    /// A passkey (WebAuthn credential) registered for an account. Only the
    /// public key is stored; the private key never leaves the authenticator.
    /// </summary>
    /// <param name="Id">The credential id, base64url.</param>
    /// <param name="PublicKey">The COSE-encoded public key.</param>
    /// <param name="Algorithm">The COSE algorithm identifier: -7 (ES256) or -257 (RS256).</param>
    /// <param name="SignCount">The signature counter reported by the authenticator.</param>
    /// <param name="Transports">How the authenticator can be reached ("internal", "hybrid", "usb", ...).</param>
    /// <param name="Discoverable">Whether the credential is discoverable (a "real" passkey), when the client told us.</param>
    /// <param name="BackupEligible">Whether the credential may be synced (BE flag).</param>
    /// <param name="BackedUp">Whether the credential is currently synced (BS flag).</param>
    /// <param name="AAGUID">The authenticator model identifier.</param>
    /// <param name="Name">A name chosen by the user, e.g. "Windows Hello on the laptop".</param>
    public sealed record Passkey(String                 Id,
                                 Byte[]                 PublicKey,
                                 Int32                  Algorithm,
                                 UInt32                 SignCount,
                                 IReadOnlyList<String>  Transports,
                                 Boolean?               Discoverable,
                                 Boolean                BackupEligible,
                                 Boolean                BackedUp,
                                 Guid                   AAGUID,
                                 String                 Name,
                                 DateTimeOffset         CreatedAt,
                                 DateTimeOffset?        LastUsedAt)
    {

        public const Int32 MaxNameLength = 64;


        #region AlgorithmName

        /// <summary>
        /// The COSE algorithm as a human readable name.
        /// </summary>
        public String AlgorithmName

            => Algorithm switch {
                   WebAuthn.ES256  => "ES256",
                   WebAuthn.RS256  => "RS256",
                   _               => Algorithm.ToString()
               };

        #endregion


        #region ToJSON()

        /// <summary>
        /// The public view, as returned by the API: everything but the key.
        /// </summary>
        public JObject ToJSON()

            => new (
                   new JProperty("id",              Id),
                   new JProperty("algorithm",       AlgorithmName),
                   new JProperty("signCount",       SignCount),
                   new JProperty("transports",      new JArray(Transports)),
                   new JProperty("discoverable",    Discoverable),
                   new JProperty("backupEligible",  BackupEligible),
                   new JProperty("backedUp",        BackedUp),
                   new JProperty("aaguid",          AAGUID.ToString()),
                   new JProperty("name",            Name),
                   new JProperty("createdAt",       CreatedAt.ToString("o")),
                   new JProperty("lastUsedAt",      LastUsedAt?.ToString("o"))
               );

        #endregion

        #region ToStorageJSON()

        /// <summary>
        /// The credential including the public key, for the users file.
        /// </summary>
        public JObject ToStorageJSON()
        {

            var json = ToJSON();

            json["algorithm"] = Algorithm;
            json.Add(new JProperty("publicKey", Base64Url.EncodeToString(PublicKey)));

            return json;

        }

        #endregion

        #region (static) TryParse(JSON, out Passkey, out Error)

        public static Boolean TryParse(JObject                            JSON,
                                       [NotNullWhen(true)]  out Passkey?  Passkey,
                                       [NotNullWhen(false)] out String?   Error)
        {

            Passkey  = null;
            Error    = null;

            try
            {

                var id         = JSON.Value<String>("id");
                var publicKey  = JSON.Value<String>("publicKey");
                var algorithm  = JSON.Value<Int32?>("algorithm");

                if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(publicKey) || algorithm is null)
                {
                    Error = "A passkey needs an id, a public key and an algorithm!";
                    return false;
                }

                Passkey = new Passkey(
                              Id:              id,
                              PublicKey:       Base64Url.DecodeFromChars(publicKey),
                              Algorithm:       algorithm.Value,
                              SignCount:       JSON.Value<UInt32?>("signCount") ?? 0,
                              Transports:      JSON["transports"] is JArray transports
                                                   ? [.. transports.Values<String>().OfType<String>()]
                                                   : [],
                              Discoverable:    JSON.Value<Boolean?>("discoverable"),
                              BackupEligible:  JSON.Value<Boolean?>("backupEligible") ?? false,
                              BackedUp:        JSON.Value<Boolean?>("backedUp")       ?? false,
                              AAGUID:          Guid.TryParse(JSON.Value<String>("aaguid"), out var aaguid) ? aaguid : Guid.Empty,
                              Name:            JSON.Value<String>("name") ?? "Passkey",
                              CreatedAt:       DateTimeOffset.TryParse(JSON.Value<String>("createdAt"),  null, System.Globalization.DateTimeStyles.RoundtripKind, out var createdAt)  ? createdAt  : DateTimeOffset.UtcNow,
                              LastUsedAt:      DateTimeOffset.TryParse(JSON.Value<String>("lastUsedAt"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastUsedAt) ? lastUsedAt : null
                          );

                return true;

            }
            catch (Exception e)
            {
                Error = $"Invalid passkey: {e.Message}";
                return false;
            }

        }

        #endregion

    }

}
