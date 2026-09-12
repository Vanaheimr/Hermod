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
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    public enum CeremonyType
    {
        Registration,
        Authentication
    }


    /// <summary>
    /// One WebAuthn ceremony in progress: the challenge the server handed out
    /// and is waiting to see signed. Stored per ceremony, not per user, so
    /// that an authentication can start without knowing who is signing in.
    /// </summary>
    /// <param name="Id">The ceremony id the client sends back with the credential.</param>
    /// <param name="Type">Registration or authentication.</param>
    /// <param name="Challenge">32 random bytes.</param>
    /// <param name="UserId">The signed-in user for a registration, otherwise null.</param>
    /// <param name="ExpiresAt">When the challenge stops being valid.</param>
    public sealed record Ceremony(String          Id,
                                  CeremonyType    Type,
                                  Byte[]          Challenge,
                                  User_Id?        UserId,
                                  DateTimeOffset  ExpiresAt)
    {

        /// <summary>
        /// The challenge as the client will see it in clientDataJSON.
        /// </summary>
        public String ChallengeBase64Url
            => Base64Url.EncodeToString(Challenge);

    }


    /// <summary>
    /// The WebAuthn ceremonies in progress, in memory. A challenge is valid
    /// for a short time and can be used once.
    /// </summary>
    public sealed class CeremonyStore
    {

        #region Data

        private readonly ConcurrentDictionary<String, Ceremony>  ceremonies  = new(StringComparer.Ordinal);
        private readonly TimeSpan                                 lifetime;

        #endregion

        #region Constructor(s)

        /// <param name="Lifetime">How long a challenge stays valid; two minutes by default.</param>
        public CeremonyStore(TimeSpan? Lifetime = null)
        {
            lifetime = Lifetime ?? TimeSpan.FromMinutes(2);
        }

        #endregion


        #region Create(Type, UserId = null)

        /// <summary>
        /// Start a new ceremony with a fresh challenge.
        /// </summary>
        public Ceremony Create(CeremonyType  Type,
                               User_Id?      UserId   = null)
        {

            Sweep();

            var ceremony = new Ceremony(
                               Id:         Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)),
                               Type:       Type,
                               Challenge:  RandomNumberGenerator.GetBytes(32),
                               UserId:     UserId,
                               ExpiresAt:  DateTimeOffset.UtcNow + lifetime
                           );

            ceremonies[ceremony.Id] = ceremony;

            return ceremony;

        }

        #endregion

        #region TryTake(Id, Type, out Ceremony)

        /// <summary>
        /// Fetch and remove a ceremony: a challenge can be answered once.
        /// </summary>
        public Boolean TryTake(String                             Id,
                               CeremonyType                       Type,
                               [NotNullWhen(true)] out Ceremony?  Ceremony)
        {

            Ceremony = null;

            if (String.IsNullOrEmpty(Id) || !ceremonies.TryRemove(Id, out var ceremony))
                return false;

            if (ceremony.Type != Type || DateTimeOffset.UtcNow >= ceremony.ExpiresAt)
                return false;

            Ceremony = ceremony;
            return true;

        }

        #endregion

        #region (private) Sweep()

        private void Sweep()
        {

            var now = DateTimeOffset.UtcNow;

            foreach (var ceremony in ceremonies.Values.Where(ceremony => now >= ceremony.ExpiresAt))
                ceremonies.TryRemove(ceremony.Id, out _);

        }

        #endregion

    }

}
