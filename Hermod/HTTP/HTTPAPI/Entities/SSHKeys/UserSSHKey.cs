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

using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An SSH public key of a user: the line of an <c>authorized_keys</c> file
    /// it was handed in as - its options, from= and expiry-time= among them,
    /// hold as they would there - what it was called, and when and by whom it
    /// was let in.
    /// </summary>
    /// <remarks>
    /// The line is what is kept, and what the key is read from again whenever
    /// it is read back: there is one way to understand it, Hermod's
    /// <see cref="AuthorizedKeysFile"/>, and a line it cannot hold to is never
    /// let in at all.
    /// </remarks>
    public sealed class UserSSHKey
    {

        #region Properties

        /// <summary>
        /// The SHA-256 fingerprint of the public key, "SHA256:..." as OpenSSH
        /// writes it: what the key is known by.
        /// </summary>
        public String          Fingerprint    { get; }

        /// <summary>
        /// The <c>authorized_keys</c> line, with its options, as it was handed in.
        /// </summary>
        public String          Line           { get; }

        /// <summary>
        /// The line, read: the key, the options and the restrictions they put on a session.
        /// </summary>
        public AuthorizedKey   Key            { get; }

        /// <summary>
        /// What the key was called when it was let in, if anything.
        /// </summary>
        public String?         Label          { get; }

        /// <summary>
        /// When the key was let in.
        /// </summary>
        public DateTimeOffset  Created        { get; }

        /// <summary>
        /// Who let it in, if anybody said: a user, or what stood in for one, as
        /// the database file names the one behind a change.
        /// </summary>
        public String?         CreatedBy      { get; }

        #endregion

        #region Constructor(s)

        private UserSSHKey(String          Line,
                           AuthorizedKey   Key,
                           String?         Label,
                           DateTimeOffset  Created,
                           String?         CreatedBy)
        {

            this.Fingerprint  = SshFingerprint.Sha256(Key.PublicKey.Blob);
            this.Line         = Line;
            this.Key          = Key;
            this.Label        = Label;
            this.Created      = Created;
            this.CreatedBy    = CreatedBy;

        }

        #endregion


        #region (static) TryCreate(Line, Label, Created, CreatedBy, out SSHKey, out Refused)

        /// <summary>
        /// A key of the given <c>authorized_keys</c> line - or why the line is
        /// refused: a key that cannot be read, a private key, or an option that
        /// cannot be held to.
        /// </summary>
        /// <param name="Line">One line, as an authorized_keys file holds it.</param>
        /// <param name="Label">What the key is called, if anything.</param>
        /// <param name="Created">When it is let in.</param>
        /// <param name="CreatedBy">Who lets it in, if anybody says.</param>
        /// <param name="SSHKey">The key.</param>
        /// <param name="Refused">Why the line is refused.</param>
        public static Boolean TryCreate(String                                  Line,
                                        String?                                 Label,
                                        DateTimeOffset                          Created,
                                        String?                                 CreatedBy,
                                        [NotNullWhen(true)]  out UserSSHKey?    SSHKey,
                                        [NotNullWhen(false)] out String?        Refused)
        {

            SSHKey = null;

            var line = Line.Trim();

            // Never a private key: that is what a key's owner keeps, and a node
            // is the last place to hand it. Asked first, as one is several lines.
            if (line.Contains("PRIVATE KEY", StringComparison.Ordinal) ||
                line.StartsWith("PuTTY-User-Key-File", StringComparison.Ordinal))
            {
                Refused = "That is a private key. The public one is wanted: the .pub file beside it, or what PuTTYgen shows as the public key.";
                return false;
            }

            if (line.Contains('\n') || line.Contains('\r'))
            {
                Refused = "One key is one line.";
                return false;
            }

            if (!AuthorizedKeysFile.TryParseLine(line, out var key, out Refused))
                return false;

            SSHKey = new UserSSHKey(
                         line,
                         key,
                         Label?.Trim() is { Length: > 0 } label ? label : null,
                         Created,
                         CreatedBy
                     );

            return true;

        }

        #endregion

        #region (static) TryParse(JSON, out SSHKey, out ErrorResponse)

        /// <summary>
        /// A key as <see cref="ToJSON"/> wrote it, read again from its line: a
        /// line that cannot be held to any more is not let in, and a
        /// fingerprint that is not the line's is not believed.
        /// </summary>
        public static Boolean TryParse(JObject                                 JSON,
                                       [NotNullWhen(true)]  out UserSSHKey?    SSHKey,
                                       [NotNullWhen(false)] out String?        ErrorResponse)
        {

            SSHKey = null;

            if (JSON["line"]?.Value<String>() is not String line)
            {
                ErrorResponse = "Missing 'line'!";
                return false;
            }

            if (JSON["created"]?.Value<DateTime?>() is not DateTime created)
            {
                ErrorResponse = "Missing or invalid 'created'!";
                return false;
            }

            if (!TryCreate(line,
                           JSON["label"]?.    Value<String>(),
                           new DateTimeOffset(DateTime.SpecifyKind(created, DateTimeKind.Utc)),
                           JSON["createdBy"]?.Value<String>(),
                           out SSHKey,
                           out var refused))
            {
                ErrorResponse = refused;
                return false;
            }

            if (JSON["fingerprint"]?.Value<String>() is String fingerprint &&
                fingerprint != SSHKey.Fingerprint)
            {
                ErrorResponse = $"The fingerprint '{fingerprint}' is not the line's, '{SSHKey.Fingerprint}'!";
                SSHKey        = null;
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// The key as the database file and the routes have it.
        /// </summary>
        public JObject ToJSON()

            => JSONObject.Create(

                   new JProperty("fingerprint",    Fingerprint),
                   new JProperty("line",           Line),

                   Label is not null
                       ? new JProperty("label",    Label)
                       : null,

                   new JProperty("created",        Created.UtcDateTime),

                   CreatedBy is not null
                       ? new JProperty("createdBy", CreatedBy)
                       : null

               );

        #endregion


        #region (static) FingerprintInURL(Fingerprint) / FingerprintFromURL(Text)

        /// <summary>
        /// A fingerprint as it goes into a URL: without "SHA256:", and in the
        /// URL-safe Base64 alphabet, where '/' and '+' would be read as
        /// something else.
        /// </summary>
        public static String FingerprintInURL(String Fingerprint)

            => (Fingerprint.StartsWith("SHA256:", StringComparison.Ordinal)
                    ? Fingerprint["SHA256:".Length..]
                    : Fingerprint).
               Replace('+', '-').
               Replace('/', '_');

        /// <summary>
        /// A fingerprint from a URL: as FingerprintInURL wrote it, or with its
        /// "SHA256:" and the plain Base64 alphabet, as OpenSSH writes it.
        /// </summary>
        public static String FingerprintFromURL(String Text)
        {

            var text = Uri.UnescapeDataString(Text.Trim());

            if (text.StartsWith("SHA256:", StringComparison.Ordinal))
                text = text["SHA256:".Length..];

            return "SHA256:" + text.Replace('-', '+').Replace('_', '/');

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// The fingerprint, and the label where there is one.
        /// </summary>
        public override String ToString()

            => Label is not null
                   ? $"{Fingerprint} ({Label})"
                   : Fingerprint;

        #endregion

    }

}
