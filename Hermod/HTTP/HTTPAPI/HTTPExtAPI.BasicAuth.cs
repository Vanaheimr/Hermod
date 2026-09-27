/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// HTTP Basic Auth that was verified a moment ago, believed again without
    /// being verified again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Basic Auth sends the password with every request, and every request
    /// used to cost a full verification - 600 000 rounds of PBKDF2 - and, since
    /// that was rationed like the sign-in route, one of its ten attempts a
    /// minute. So a client that knew the password was treated like one
    /// guessing it: its eleventh request in a minute was answered 401, and it
    /// had spent the ration the sign-in page of the same account and the same
    /// address draws on, too.
    /// </para>
    /// <para>
    /// Now credentials that were verified are remembered for a while, by a
    /// keyed hash of the username and the password and never by the password,
    /// and a request that brings the same again is its user without a hash
    /// and without the ration. Only what was verified is remembered, so a
    /// guess never is: every credential not remembered is rationed and
    /// verified exactly as before. What is remembered is let go of when the
    /// account's password is no longer the one it was verified against, when
    /// the account is gone, or when it no longer accepts the EULA - and a
    /// disabled account is turned away at TryGetHTTPUser's door, as always.
    /// </para>
    /// </remarks>
    public partial class HTTPExtAPI
    {

        #region Data

        /// <summary>
        /// How long credentials verified through HTTP Basic Auth are believed
        /// again without being verified again.
        /// </summary>
        public static readonly  TimeSpan  BasicAuthRememberedFor      = TimeSpan.FromMinutes(10);

        /// <summary>
        /// The most credentials remembered at once. Only verified ones are,
        /// so this is a bound on accounts rather than on anybody's guesses.
        /// </summary>
        public const            Int32     MaxRememberedBasicAuths     = 10_000;

        /// <summary>
        /// What the remembered credentials are known by is keyed with this:
        /// random, for the life of this API, and never written anywhere.
        /// </summary>
        private readonly Byte[] basicAuthKey = RandomNumberGenerator.GetBytes(32);

        private readonly ConcurrentDictionary<String, RememberedBasicAuth> rememberedBasicAuths = new (StringComparer.Ordinal);

        /// <summary>
        /// Credentials that were verified: whose they were, the password they
        /// were verified against, and until when they are believed.
        /// </summary>
        private sealed record RememberedBasicAuth(User_Id         UserId,
                                                  SecurePassword  VerifiedAgainst,
                                                  DateTimeOffset  Until);

        #endregion


        #region (private) BasicAuthKeyOf(Credentials)

        /// <summary>
        /// What the given credentials are remembered by: a keyed hash of the
        /// username and the password, with the length of the username in front,
        /// so that no other split of the same characters is the same key.
        /// </summary>
        private String BasicAuthKeyOf(HTTPBasicAuthentication Credentials)

            => Convert.ToBase64String(
                   HMACSHA256.HashData(
                       basicAuthKey,
                       Encoding.UTF8.GetBytes($"{Credentials.Username.Length}:{Credentials.Username}{Credentials.Password}")
                   )
               );

        #endregion

        #region (private) TryRememberedBasicAuth(Key, out User)

        /// <summary>
        /// The account the credentials of this key were verified for, where
        /// nothing that made them good has changed since.
        /// </summary>
        private Boolean TryRememberedBasicAuth(String                           Key,
                                               [NotNullWhen(true)] out IUser?  User)
        {

            User = null;

            if (!rememberedBasicAuths.TryGetValue(Key, out var remembered))
                return false;

            var now = Timestamp.Now;

            if (remembered.Until > now                                                         &&
                users.         TryGetValue(remembered.UserId, out var user)                    &&
                loginPasswords.TryGetValue(remembered.UserId, out var loginPassword)           &&
                loginPassword.Password.Equals(remembered.VerifiedAgainst)                      &&
                user.AcceptedEULA.HasValue                                                     &&
                user.AcceptedEULA.Value < now)
            {
                User = user;
                return true;
            }

            // Whatever of it no longer holds, it no longer holds next time
            // either: a changed password does not change back.
            rememberedBasicAuths.TryRemove(new KeyValuePair<String, RememberedBasicAuth>(Key, remembered));

            return false;

        }

        #endregion

        #region (private) RememberBasicAuth(Key, User, VerifiedAgainst)

        /// <summary>
        /// Remember credentials that were just verified.
        /// </summary>
        /// <param name="Key">What they are remembered by.</param>
        /// <param name="User">Whose they are.</param>
        /// <param name="VerifiedAgainst">The password they were verified against - the one read before verifying, not the account's password read again now, which a change in between would have replaced.</param>
        private void RememberBasicAuth(String          Key,
                                       IUser           User,
                                       SecurePassword  VerifiedAgainst)
        {

            var now = Timestamp.Now;

            if (rememberedBasicAuths.Count >= MaxRememberedBasicAuths)
            {

                foreach (var pair in rememberedBasicAuths)
                    if (pair.Value.Until <= now)
                        rememberedBasicAuths.TryRemove(pair);

                // Still full of what is still good: this one is verified
                // again next time rather than something else forgotten early.
                if (rememberedBasicAuths.Count >= MaxRememberedBasicAuths)
                    return;

            }

            rememberedBasicAuths[Key] = new RememberedBasicAuth(User.Id, VerifiedAgainst, now + BasicAuthRememberedFor);

        }

        #endregion

    }

}
