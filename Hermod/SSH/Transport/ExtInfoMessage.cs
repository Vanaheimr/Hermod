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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The well-known SSH extension names carried by <see cref="ExtInfoMessage"/> (RFC 8308).
    /// </summary>
    public static class SshExtensionNames
    {

        /// <summary>
        /// "server-sig-algs" (RFC 8308 §3.1): the server advertises the public-key signature algorithms
        /// it will accept for user authentication, so a client can pick e.g. rsa-sha2-256/512 over the
        /// deprecated SHA-1 "ssh-rsa". The value is a comma-separated name-list.
        /// </summary>
        public const String ServerSigAlgs = "server-sig-algs";


        /// <summary>
        /// The signature algorithms our server accepts for client public-key authentication — the
        /// default "server-sig-algs" value (everything <see cref="SshSignature"/> can verify).
        /// </summary>
        public static readonly String[] DefaultServerSignatureAlgorithms =
        [
            SshAlgorithmNames.HostKey.Ed25519,
            SshAlgorithmNames.HostKey.EcdsaNistP256,
            SshAlgorithmNames.HostKey.EcdsaNistP384,
            SshAlgorithmNames.HostKey.EcdsaNistP521,
            SshAlgorithmNames.HostKey.RsaSha2_512,
            SshAlgorithmNames.HostKey.RsaSha2_256
        ];

    }


    /// <summary>
    /// The SSH_MSG_EXT_INFO message (RFC 8308 §2.3): a set of extension name/value pairs a peer sends
    /// right after its SSH_MSG_NEWKEYS, once both sides have signalled support via the ext-info-c /
    /// ext-info-s markers during KEXINIT.
    /// </summary>
    public sealed class ExtInfoMessage
    {

        #region Properties

        /// <summary>The extensions, in order (name → value; the value is extension-specific).</summary>
        public IReadOnlyList<KeyValuePair<String, String>>  Extensions    { get; }

        /// <summary>The value of the given extension, or null if absent.</summary>
        public String? this[String Name]
        {
            get
            {
                foreach (var extension in Extensions)
                    if (String.Equals(extension.Key, Name, StringComparison.Ordinal))
                        return extension.Value;
                return null;
            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>Create an EXT_INFO message from a set of extensions.</summary>
        public ExtInfoMessage(IReadOnlyList<KeyValuePair<String, String>> Extensions)
        {
            this.Extensions = Extensions;
        }

        /// <summary>Create an EXT_INFO message from a set of extensions.</summary>
        public ExtInfoMessage(params (String Name, String Value)[] Extensions)
        {
            this.Extensions = [.. Extensions.Select(e => new KeyValuePair<String, String>(e.Name, e.Value))];
        }

        #endregion


        #region (static) ForServerSigAlgs(SignatureAlgorithms)

        /// <summary>
        /// Build an EXT_INFO carrying only "server-sig-algs" with the given signature-algorithm name-list.
        /// </summary>
        public static ExtInfoMessage ForServerSigAlgs(IEnumerable<String> SignatureAlgorithms)
            => new ((SshExtensionNames.ServerSigAlgs, String.Join(',', SignatureAlgorithms)));

        #endregion

        #region Encode()

        /// <summary>
        /// Encode this message into its SSH payload (starting with the SSH_MSG_EXT_INFO byte).
        /// </summary>
        public Byte[] Encode()
        {

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            writer.WriteByte((Byte) SshMessageNumber.ExtInfo);
            writer.WriteUInt32((UInt32) Extensions.Count);

            foreach (var extension in Extensions)
            {
                writer.WriteString(extension.Key);
                writer.WriteString(extension.Value);
            }

            return abw.WrittenSpan.ToArray();

        }

        #endregion

        #region (static) Decode(Payload)

        /// <summary>
        /// Decode an EXT_INFO message from its SSH payload.
        /// </summary>
        /// <param name="Payload">The payload, starting with the SSH_MSG_EXT_INFO byte.</param>
        public static ExtInfoMessage Decode(ReadOnlySpan<Byte> Payload)
        {

            var reader = new SshPacketReader(Payload);

            var messageNumber = reader.ReadByte();
            if (messageNumber != (Byte) SshMessageNumber.ExtInfo)
                throw new SshWireException($"Expected SSH_MSG_EXT_INFO (7), but found message number {messageNumber}!");

            var count = reader.ReadUInt32();

            // A defensive bound: EXT_INFO carries a handful of extensions, never thousands.
            if (count > 256)
                throw new SshWireException($"SSH_MSG_EXT_INFO advertises an implausible extension count ({count})!");

            var extensions = new List<KeyValuePair<String, String>>((Int32) count);

            for (var i = 0U; i < count; i++)
            {
                var name   = reader.ReadString();
                var value  = reader.ReadString();
                extensions.Add(new KeyValuePair<String, String>(name, value));
            }

            return new ExtInfoMessage(extensions);

        }

        #endregion

    }

}
