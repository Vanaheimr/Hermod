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

using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// An SSH public key: the key-type name, the wire blob, and an optional comment. Parses and emits the
    /// one-line <c>authorized_keys</c> / <c>.pub</c> format (<c>&lt;type&gt; &lt;base64-blob&gt; &lt;comment&gt;</c>)
    /// and the multi-line RFC 4716 format, and exposes the fingerprints.
    /// </summary>
    public sealed class SshPublicKey
    {

        #region Properties

        /// <summary>
        /// The key-type / algorithm name (e.g. <c>ssh-ed25519</c>), read from the blob.
        /// </summary>
        public String  Algorithm  { get; }

        /// <summary>
        /// The SSH public-key wire blob.
        /// </summary>
        public Byte[]  Blob       { get; }

        /// <summary>
        /// An optional comment (e.g. <c>user@host</c>).
        /// </summary>
        public String  Comment    { get; }

        /// <summary>
        /// The <c>SHA256:…</c> fingerprint.
        /// </summary>
        public String  Sha256Fingerprint  => SshFingerprint.Sha256(Blob);

        /// <summary>
        /// The legacy <c>MD5:…</c> fingerprint.
        /// </summary>
        public String  Md5Fingerprint     => SshFingerprint.Md5(Blob);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a public key from its wire blob and an optional comment.
        /// </summary>
        public SshPublicKey(Byte[] Blob, String Comment = "")
        {
            this.Blob       = Blob;
            this.Comment    = Comment;
            this.Algorithm  = ReadAlgorithm(Blob);
        }

        #endregion


        #region (static) FromHostKey(HostKey, Comment = "")

        /// <summary>
        /// The public key of a signing key (host or user key).
        /// </summary>
        public static SshPublicKey FromHostKey(ISshHostKey HostKey, String Comment = "")
            => new (HostKey.PublicKeyBlob, Comment);

        #endregion

        #region (static) Parse(Line) / TryParse(Line, out PublicKey)

        /// <summary>
        /// Parse an <c>authorized_keys</c> / <c>.pub</c> line: <c>&lt;type&gt; &lt;base64&gt; [comment]</c>.
        /// Leading key options are not handled here (see the authorized_keys parser).
        /// </summary>
        public static SshPublicKey Parse(String Line)
            => TryParse(Line, out var key)
                   ? key!
                   : throw new SshWireException($"'{Line}' is not a valid SSH public-key line.");

        /// <summary>
        /// Try to parse an <c>authorized_keys</c> / <c>.pub</c> line.
        /// </summary>
        public static Boolean TryParse(String Line, out SshPublicKey? PublicKey)
        {

            PublicKey = null;

            var parts = Line.Trim().Split((Char[]?) null, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return false;

            var type     = parts[0];
            var comment  = parts.Length == 3 ? parts[2] : "";

            Byte[] blob;
            try   { blob = Convert.FromBase64String(parts[1]); }
            catch { return false; }

            // The blob must start with the same type name.
            String blobType;
            try   { blobType = ReadAlgorithm(blob); }
            catch { return false; }

            if (blobType != type)
                return false;

            PublicKey = new SshPublicKey(blob, comment);
            return true;

        }

        #endregion

        #region ToAuthorizedKeyLine()

        /// <summary>
        /// Emit the one-line <c>authorized_keys</c> / <c>.pub</c> representation.
        /// </summary>
        public String ToAuthorizedKeyLine()
        {
            var line = Algorithm + " " + Convert.ToBase64String(Blob);
            return Comment.Length > 0 ? line + " " + Comment : line;
        }

        #endregion

        #region RFC 4716 (SSH2 public key file)

        /// <summary>
        /// Emit the multi-line RFC 4716 <c>---- BEGIN SSH2 PUBLIC KEY ----</c> representation.
        /// </summary>
        public String ToRfc4716()
        {

            var builder = new StringBuilder();
            builder.Append("---- BEGIN SSH2 PUBLIC KEY ----\n");

            if (Comment.Length > 0)
                builder.Append(WrapHeader("Comment: \"" + Comment + "\""));

            var base64 = Convert.ToBase64String(Blob);
            for (var offset = 0; offset < base64.Length; offset += 70)
                builder.Append(base64, offset, Math.Min(70, base64.Length - offset)).Append('\n');

            builder.Append("---- END SSH2 PUBLIC KEY ----\n");
            return builder.ToString();

        }

        /// <summary>
        /// Parse an RFC 4716 <c>---- BEGIN SSH2 PUBLIC KEY ----</c> block.
        /// </summary>
        public static SshPublicKey ParseRfc4716(String Text)
        {

            var lines    = Text.Replace("\r\n", "\n").Split('\n');
            var body     = new StringBuilder();
            var comment  = "";
            var inBody   = false;
            var continued = false;

            foreach (var raw in lines)
            {

                var line = raw;

                if (line.StartsWith("---- BEGIN SSH2 PUBLIC KEY ----", StringComparison.Ordinal)) { inBody = true; continue; }
                if (line.StartsWith("---- END SSH2 PUBLIC KEY ----",   StringComparison.Ordinal)) break;
                if (!inBody)
                    continue;

                // Header lines contain a ':' (before the base64 body) and may continue with a trailing '\'.
                if (continued || line.Contains(':'))
                {
                    if (line.StartsWith("Comment:", StringComparison.OrdinalIgnoreCase))
                        comment = line[8..].Trim().Trim('"');
                    continued = line.EndsWith('\\');
                    continue;
                }

                body.Append(line.Trim());

            }

            var blob = Convert.FromBase64String(body.ToString());
            return new SshPublicKey(blob, comment);

        }

        private static String WrapHeader(String Header)
        {
            // RFC 4716 headers wrap at 72 chars with a trailing backslash; short ones fit on one line.
            if (Header.Length <= 72)
                return Header + "\n";

            var builder = new StringBuilder();
            var offset  = 0;
            while (offset < Header.Length)
            {
                var take = Math.Min(71, Header.Length - offset);
                builder.Append(Header, offset, take);
                offset += take;
                builder.Append(offset < Header.Length ? "\\\n" : "\n");
            }
            return builder.ToString();

        }

        #endregion

        #region (private static) ReadAlgorithm(Blob)

        private static String ReadAlgorithm(ReadOnlySpan<Byte> Blob)
        {
            var reader = new SshPacketReader(Blob);
            return reader.ReadString();
        }

        #endregion

    }

}
