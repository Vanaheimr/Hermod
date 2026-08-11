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

using System.Buffers;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A minimal certificate authority: issues OpenSSH user and host certificates (the <c>ssh-keygen -s</c>
    /// equivalent), signing a subject public key with a CA key over all certificate fields.
    /// </summary>
    public sealed class OpenSshCertificateBuilder
    {

        #region Properties (fluent)

        /// <summary>The certificate serial number.</summary>
        public UInt64                        Serial           { get; set; }

        /// <summary>Whether to issue a user or host certificate.</summary>
        public SshCertType                   Type             { get; set; } = SshCertType.User;

        /// <summary>The key identifier (free-form, shown by <c>ssh-keygen -L</c>).</summary>
        public String                        KeyId            { get; set; } = "";

        /// <summary>The valid principals (user names or host names); empty = valid for all.</summary>
        public IReadOnlyList<String>         Principals       { get; set; } = [];

        /// <summary>The start of the validity window (default: now).</summary>
        public DateTimeOffset                ValidAfter       { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>The end of the validity window (default: no expiry).</summary>
        public DateTimeOffset                ValidBefore      { get; set; } = DateTimeOffset.MaxValue;

        /// <summary>The critical options (name → data).</summary>
        public IList<KeyValuePair<String, Byte[]>>  CriticalOptions  { get; } = [];

        /// <summary>The extensions (name → data); default: the usual permissive user-cert extensions.</summary>
        public IList<KeyValuePair<String, Byte[]>>  Extensions       { get; } = [];

        #endregion


        #region (static) DefaultUserExtensions

        /// <summary>The extensions OpenSSH grants a user certificate by default (all permit-* flags).</summary>
        public static IReadOnlyList<KeyValuePair<String, Byte[]>> DefaultUserExtensions =>
        [
            new ("permit-X11-forwarding",     []),
            new ("permit-agent-forwarding",   []),
            new ("permit-port-forwarding",    []),
            new ("permit-pty",                []),
            new ("permit-user-rc",            [])
        ];

        #endregion

        #region Sign(SubjectPublicKey, CaKey)

        /// <summary>
        /// Issue a certificate for <paramref name="SubjectPublicKey"/>, signed by <paramref name="CaKey"/>.
        /// </summary>
        public SshCertificate Sign(Byte[] SubjectPublicKey, ISshHostKey CaKey)
        {

            // Split the subject public key into its algorithm name and its raw type-specific field bytes.
            var subjectReader  = new SshPacketReader(SubjectPublicKey);
            var subjectAlg     = subjectReader.ReadString();
            var subjectFields  = SubjectPublicKey[subjectReader.Position..];

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            writer.WriteString(subjectAlg + SshCertificate.CertSuffix);
            writer.WriteBinaryString(RandomNumberGenerator.GetBytes(32));   // nonce

            // The subject key fields are already SSH-encoded — append them verbatim (after the nonce).
            var fields = abw.GetSpan(subjectFields.Length);
            subjectFields.CopyTo(fields);
            abw.Advance(subjectFields.Length);

            writer.WriteUInt64(Serial);
            writer.WriteUInt32((UInt32) Type);
            writer.WriteString(KeyId);
            writer.WriteBinaryString(EncodeStringSequence(Principals));
            writer.WriteUInt64(ToUnix(ValidAfter,  0UL));
            writer.WriteUInt64(ToUnix(ValidBefore, UInt64.MaxValue));
            writer.WriteBinaryString(EncodeTupleSequence(CriticalOptions));
            writer.WriteBinaryString(EncodeTupleSequence(Extensions.Count > 0 || Type == SshCertType.Host ? Extensions : DefaultUserExtensions));
            writer.WriteBinaryString([]);                                    // reserved
            writer.WriteBinaryString(CaKey.PublicKeyBlob);

            // Sign everything so far with the CA key, then append the signature.
            var signedBytes = abw.WrittenSpan.ToArray();
            var signature   = CaKey.Sign(CaKey.AlgorithmNames[0], signedBytes);
            writer.WriteBinaryString(signature);

            return SshCertificate.Parse(abw.WrittenSpan.ToArray());

        }

        #endregion


        #region (private) encoders

        private static Byte[] EncodeStringSequence(IReadOnlyList<String> Values)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            foreach (var value in Values)
                writer.WriteString(value);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] EncodeTupleSequence(IEnumerable<KeyValuePair<String, Byte[]>> Tuples)
        {

            // Critical options and extensions must be sorted lexically by name (OpenSSH requirement).
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            foreach (var tuple in Tuples.OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                writer.WriteString(tuple.Key);
                writer.WriteBinaryString(tuple.Value);
            }

            return abw.WrittenSpan.ToArray();

        }

        private static UInt64 ToUnix(DateTimeOffset Value, UInt64 Sentinel)
            => Value.UtcDateTime.Year >= 9999
                   ? Sentinel                                          // "forever" (0xFFFF… / 0)
                   : (UInt64) Math.Max(0, Value.ToUnixTimeSeconds());

        #endregion

    }

}
