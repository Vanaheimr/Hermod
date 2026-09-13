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
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A signed-in session: whose it is, when it was created, when it was
    /// last seen and when it expires. For impersonation the session acts as
    /// UserId while SuperUserId is the user who really signed in.
    /// </summary>
    /// <param name="Token">The random token that identifies the session, e.g. within a cookie.</param>
    /// <param name="UserId">The user the session acts as.</param>
    /// <param name="CreatedAt">When the session was created.</param>
    /// <param name="LastSeenAt">When the session was last used.</param>
    /// <param name="ExpiresAt">When the session ends unless it is used before, or ends for good.</param>
    /// <param name="SuperUserId">The user who really signed in, when impersonating UserId.</param>
    public sealed record Session(SecurityToken_Id  Token,
                                 User_Id           UserId,
                                 DateTimeOffset    CreatedAt,
                                 DateTimeOffset    LastSeenAt,
                                 DateTimeOffset    ExpiresAt,
                                 User_Id?          SuperUserId   = null)
    {

        /// <summary>
        /// Whether this session has expired at the given time.
        /// </summary>
        /// <param name="Now">The current time.</param>
        public Boolean IsExpired(DateTimeOffset Now)
            => Now >= ExpiresAt;

    }


    /// <summary>
    /// The signed-in sessions of an HTTP API, identified by a random token
    /// that travels in a cookie. A session ends after an optional idle
    /// timeout, at the latest after its maximum lifetime, when the user
    /// signs out, or when all sessions of a user are revoked, e.g. after a
    /// password change.
    ///
    /// With an attached file the sessions survive a restart: every creation
    /// and removal is appended right away, and the file is compacted to the
    /// live sessions whenever it is attached. The last-seen time of sliding
    /// sessions is not written on every request, so after a restart such a
    /// session is as old as its creation says.
    /// </summary>
    public sealed class SessionStore : IEnumerable<Session>
    {

        #region Data

        /// <summary>
        /// The default maximum lifetime of a session.
        /// </summary>
        public static readonly TimeSpan  DefaultMaximumLifetime   = TimeSpan.FromDays(30);

        /// <summary>
        /// The number of random bytes within a session token.
        /// </summary>
        public const           Byte      TokenSize                = 32;

        private const          String    AddLine                  = "add";
        private const          String    RemoveLine               = "remove";

        private readonly ConcurrentDictionary<SecurityToken_Id, Session>  sessions   = [];
        private readonly Lock                                              fileLock   = new();
        private          String?                                           filePath;

        #endregion

        #region Properties

        /// <summary>
        /// The optional idle timeout: a session ends when it was not used for this long.
        /// </summary>
        public TimeSpan?  IdleTimeout        { get; }

        /// <summary>
        /// The maximum lifetime of a session, counted from its creation.
        /// </summary>
        public TimeSpan   MaximumLifetime    { get; }

        /// <summary>
        /// The file the sessions are persisted in, if any.
        /// </summary>
        public String?    FilePath
            => filePath;

        /// <summary>
        /// The number of live sessions.
        /// </summary>
        public Int32      Count
        {
            get
            {
                var now = DateTimeOffset.UtcNow;
                return sessions.Values.Count(session => !session.IsExpired(now));
            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new session store.
        /// </summary>
        /// <param name="IdleTimeout">An optional idle timeout: a session ends when it was not used for this long.</param>
        /// <param name="MaximumLifetime">The maximum lifetime of a session, counted from its creation; 30 days by default.</param>
        /// <param name="FilePath">An optional file to persist the sessions in.</param>
        public SessionStore(TimeSpan?  IdleTimeout       = null,
                            TimeSpan?  MaximumLifetime   = null,
                            String?    FilePath          = null)
        {

            if (IdleTimeout.HasValue && IdleTimeout.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(IdleTimeout), "The idle timeout must be positive!");

            if (MaximumLifetime.HasValue && MaximumLifetime.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(MaximumLifetime), "The maximum lifetime must be positive!");

            this.IdleTimeout      = IdleTimeout;
            this.MaximumLifetime  = MaximumLifetime ?? DefaultMaximumLifetime;

            if (FilePath is not null)
                AttachFile(FilePath);

        }

        #endregion


        #region Create(UserId, SuperUserId = null)

        /// <summary>
        /// Start a new session for the given user.
        /// </summary>
        /// <param name="UserId">The user the session acts as.</param>
        /// <param name="SuperUserId">The user who really signed in, when impersonating UserId.</param>
        public Session Create(User_Id   UserId,
                              User_Id?  SuperUserId   = null)
        {

            if (UserId.IsNullOrEmpty)
                throw new ArgumentException("The user identification must not be null or empty!", nameof(UserId));

            Sweep();

            var now      = DateTimeOffset.UtcNow;
            var session  = new Session(
                               NewToken(),
                               UserId,
                               now,
                               now,
                               ExpiresAt(now, now),
                               SuperUserId
                           );

            sessions[session.Token] = session;
            Append(Line(session));

            return session;

        }

        #endregion

        #region TryGet(Token, out Session)

        /// <summary>
        /// Look up a live session by its token. With an idle timeout the
        /// session is extended by this use.
        /// </summary>
        /// <param name="Token">A session token.</param>
        /// <param name="Session">The live session.</param>
        public Boolean TryGet(SecurityToken_Id                  Token,
                              [NotNullWhen(true)] out Session?  Session)
        {

            Session = null;

            if (Token.IsNullOrEmpty || !sessions.TryGetValue(Token, out var session))
                return false;

            var now = DateTimeOffset.UtcNow;

            if (session.IsExpired(now))
            {
                sessions.TryRemove(Token, out _);
                return false;
            }

            if (IdleTimeout.HasValue)
            {

                session = session with {
                              LastSeenAt  = now,
                              ExpiresAt   = ExpiresAt(session.CreatedAt, now)
                          };

                sessions[Token] = session;

            }

            Session = session;
            return true;

        }

        #endregion

        #region Remove(Token)

        /// <summary>
        /// End the session with the given token, e.g. at sign-out.
        /// </summary>
        /// <param name="Token">A session token.</param>
        public Boolean Remove(SecurityToken_Id Token)
        {

            if (Token.IsNullOrEmpty || !sessions.TryRemove(Token, out _))
                return false;

            Append(Line(Token));
            return true;

        }

        #endregion

        #region RemoveAllForUser(UserId, ExceptToken = null)

        /// <summary>
        /// End all sessions of the given user, e.g. after a password change,
        /// optionally except the one the change was made from. Sessions in
        /// which the user impersonates somebody else end as well.
        /// </summary>
        /// <param name="UserId">A user identification.</param>
        /// <param name="ExceptToken">An optional session to keep.</param>
        public Int32 RemoveAllForUser(User_Id            UserId,
                                      SecurityToken_Id?  ExceptToken   = null)
        {

            var removed = 0;

            foreach (var session in sessions.Values.Where(session => (session.UserId == UserId || session.SuperUserId == UserId) &&
                                                                     session.Token != ExceptToken).ToList())
            {
                if (Remove(session.Token))
                    removed++;
            }

            return removed;

        }

        #endregion

        #region CountForUser(UserId)

        /// <summary>
        /// The number of live sessions of the given user.
        /// </summary>
        /// <param name="UserId">A user identification.</param>
        public Int32 CountForUser(User_Id UserId)
        {
            var now = DateTimeOffset.UtcNow;
            return sessions.Values.Count(session => session.UserId == UserId && !session.IsExpired(now));
        }

        #endregion

        #region AttachFile(FilePath)

        /// <summary>
        /// Load the sessions from the given file, if it exists, drop the
        /// expired and the removed ones, write the live sessions back and
        /// append all further changes to the file.
        /// </summary>
        /// <param name="FilePath">The file to persist the sessions in.</param>
        public void AttachFile(String FilePath)
        {

            if (FilePath.IsNullOrEmpty())
                throw new ArgumentException("The file path must not be null or empty!", nameof(FilePath));

            lock (fileLock)
            {

                filePath = FilePath;

                if (File.Exists(FilePath))
                {

                    var now     = DateTimeOffset.UtcNow;
                    var number  = 0;

                    foreach (var line in File.ReadLines(FilePath))
                    {

                        number++;

                        if (line.Length == 0 || line[0] == '#')
                            continue;

                        if (!TryParseLine(line, out var operation, out var session, out var token))
                        {
                            DebugX.Log($"Invalid line {number} in the sessions file '{FilePath}'!");
                            continue;
                        }

                        if (operation == AddLine && session is not null && !session.IsExpired(now))
                            sessions[session.Token] = session;

                        else if (operation == RemoveLine)
                            sessions.TryRemove(token, out _);

                    }

                }

                Compact();

            }

        }

        #endregion


        #region (private) ExpiresAt(CreatedAt, LastSeenAt)

        private DateTimeOffset ExpiresAt(DateTimeOffset  CreatedAt,
                                         DateTimeOffset  LastSeenAt)
        {

            var latest = CreatedAt + MaximumLifetime;

            return IdleTimeout.HasValue && LastSeenAt + IdleTimeout.Value < latest
                       ? LastSeenAt + IdleTimeout.Value
                       : latest;

        }

        #endregion

        #region (private) Sweep()

        private void Sweep()
        {

            var now = DateTimeOffset.UtcNow;

            foreach (var session in sessions.Values.Where(session => session.IsExpired(now)).ToList())
                sessions.TryRemove(session.Token, out _);

        }

        #endregion

        #region (private static) NewToken()

        private static SecurityToken_Id NewToken()
            => SecurityToken_Id.Parse(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSize)));

        #endregion

        #region (private) Persistence

        // One line per change: "add;token;userId;createdAt;lastSeenAt;expiresAt;superUserId" or "remove;token".

        private static String Line(Session Session)

            => String.Join(';',
                           AddLine,
                           Session.Token.ToString(),
                           Session.UserId.ToString(),
                           Session.CreatedAt. ToString("o", CultureInfo.InvariantCulture),
                           Session.LastSeenAt.ToString("o", CultureInfo.InvariantCulture),
                           Session.ExpiresAt. ToString("o", CultureInfo.InvariantCulture),
                           Session.SuperUserId?.ToString() ?? "");

        private static String Line(SecurityToken_Id Token)

            => String.Join(';',
                           RemoveLine,
                           Token.ToString());

        private static Boolean TryParseLine(String                Line,
                                            out String            Operation,
                                            out Session?          Session,
                                            out SecurityToken_Id  Token)
        {

            Operation  = "";
            Session    = null;
            Token      = default;

            var parts = Line.Split(';');

            if (parts.Length == 2 &&
                parts[0] == RemoveLine &&
                SecurityToken_Id.TryParse(parts[1], out Token))
            {
                Operation = RemoveLine;
                return true;
            }

            if (parts.Length == 7 &&
                parts[0] == AddLine &&
                SecurityToken_Id.TryParse(parts[1], out Token) &&
                User_Id.TryParse(parts[2], out var userId) &&
                TryParseTime(parts[3], out var createdAt) &&
                TryParseTime(parts[4], out var lastSeenAt) &&
                TryParseTime(parts[5], out var expiresAt))
            {

                User_Id? superUserId = null;

                if (parts[6].Length > 0)
                {

                    if (!User_Id.TryParse(parts[6], out var parsedSuperUserId))
                        return false;

                    superUserId = parsedSuperUserId;

                }

                Operation  = AddLine;
                Session    = new Session(Token, userId, createdAt, lastSeenAt, expiresAt, superUserId);
                return true;

            }

            return false;

        }

        private static Boolean TryParseTime(String              Text,
                                            out DateTimeOffset  Time)

            => DateTimeOffset.TryParse(Text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out Time);

        private void Append(String Line)
        {

            if (filePath is null)
                return;

            lock (fileLock)
            {
                try
                {
                    File.AppendAllText(filePath, Line + Environment.NewLine);
                }
                catch (Exception e)
                {
                    DebugX.Log($"Could not append to the sessions file '{filePath}': {e.Message}");
                }
            }

        }

        private void Compact()
        {

            if (filePath is null)
                return;

            try
            {

                var now = DateTimeOffset.UtcNow;

                File.WriteAllLines(
                    filePath,
                    sessions.Values.
                        Where  (session => !session.IsExpired(now)).
                        OrderBy(session => session.CreatedAt).
                        Select (session => Line(session))
                );

            }
            catch (Exception e)
            {
                DebugX.Log($"Could not write the sessions file '{filePath}': {e.Message}");
            }

        }

        #endregion

        #region IEnumerable<Session> Members

        /// <summary>
        /// A snapshot of the live sessions.
        /// </summary>
        public IEnumerator<Session> GetEnumerator()
        {
            var now = DateTimeOffset.UtcNow;
            return sessions.Values.Where(session => !session.IsExpired(now)).ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        #endregion

    }

}
