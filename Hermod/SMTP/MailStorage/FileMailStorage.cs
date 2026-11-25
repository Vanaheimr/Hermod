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

using org.GraphDefined.Vanaheimr.Illias;
using System.Security.Cryptography;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    public sealed class FileMailStorage(string basePath, ILogger logger) : IMailStorage
    {

        /// <summary>
        /// The most octets of a Message-ID that go into a file name as they are. A longer one is
        /// cut, and a hash of the whole of it keeps the name its own: NTFS and ext4 take no name
        /// of more than 255 characters or octets, and a Message-ID has no length of its own but
        /// the line's (RFC 5322 §3.6.4, §2.1.1).
        /// </summary>
        internal const Int32 MaxMessageIdOctetsInFileName = 100;

        public async Task<string> StoreAsync(
            EMailMessage        message,
            string              envelopeFrom,
            IEnumerable<string> envelopeTo,
            CancellationToken   ct = default)
        {
            Directory.CreateDirectory(basePath);

            var timestamp = Timestamp.Now.ToString("yyyyMMdd_HHmmss_fff");
            var messageId = FileNameOf(message.MessageId ?? UUIDv7.Generate().ToString("N")[..16]);
            var fileName = $"{timestamp}_{messageId}.eml";
            var filePath = Path.Combine(basePath, fileName);

            // Build metadata header (CRLF line endings, independent of host OS). A value is what
            // the server was told - by the client, by DNS, by the message - and goes in as a field
            // body (RFC 5322 §2.2): no line break of its own, folded where it is long.
            var metadata = new StringBuilder();
            void AddHeader(string name, string value) => metadata.Append(HeaderField.Of(name, value));

            AddHeader("X-Envelope-From", envelopeFrom);
            AddHeader("X-Envelope-To",   string.Join(", ", envelopeTo));
            AddHeader("X-Received-At",   $"{message.ReceivedAt:O}");

            if (message.Verification is not null)
            {
                var v = message.Verification;
                AddHeader("X-SPF-Result",   $"{v.Spf}");
                AddHeader("X-DKIM-Result",  $"{v.Dkim}");
                AddHeader("X-DMARC-Result", $"{v.Dmarc}");
                if (v.SpfRecord is not null)
                    AddHeader("X-SPF-Record", v.SpfRecord);
                if (v.DkimDetails is not null)
                    AddHeader("X-DKIM-Details", v.DkimDetails);
                if (v.DmarcPolicy is not null)
                    AddHeader("X-DMARC-Policy", v.DmarcPolicy);
                if (v.MxRecords.Length > 0)
                    AddHeader("X-MX-Records", string.Join(", ", v.MxRecords));
            }

            // The metadata as UTF-8 without BOM, followed by the message exactly as it arrived: an
            // 8-bit body that is not UTF-8 must not come out of the store as U+FFFD. It goes behind
            // the Return-Path, which the delivery server puts "at the beginning of the mail data"
            // (RFC 5321 §4.4) - or first, for a message without one.
            var metadataBytes = Encoding.UTF8.GetBytes(metadata.ToString());
            var messageBytes  = message.GetBytes();
            var at            = EndOfReturnPath(messageBytes);
            var fullMessage   = new Byte[metadataBytes.Length + messageBytes.Length];
            messageBytes.AsSpan(0, at).CopyTo(fullMessage);
            metadataBytes.CopyTo(fullMessage, at);
            messageBytes.AsSpan(at).CopyTo(fullMessage.AsSpan(at + metadataBytes.Length));

            // One message can be stored more than once in the same millisecond - a copy for each
            // ORCPT, with its own Original-Recipient field (RFC 8098 §2.3) - and a copy must not
            // overwrite the one before: a file that is there already gets a number.
            for (var number = 1; ; number++)
            {
                try
                {
                    await using var file = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await file.WriteAsync(fullMessage, ct);
                    break;
                }
                catch (IOException) when (File.Exists(filePath) && number < 1000)
                {
                    filePath = Path.Combine(basePath, $"{timestamp}_{messageId}_{number}.eml");
                }
            }

            logger.Log(LogLevel.Info, $"Stored message: {filePath}");
            return filePath;
        }


        #region (internal static) EndOfReturnPath(Message)

        /// <summary>
        /// Where the Return-Path field the message begins with ends, its CR LF included - or 0
        /// when it does not begin with one.
        /// </summary>
        internal static Int32 EndOfReturnPath(ReadOnlySpan<Byte> Message)
        {

            var name = "Return-Path:"u8;

            if (Message.Length < name.Length || !Ascii.EqualsIgnoreCase(Message[..name.Length], name))
                return 0;

            var start = 0;

            while (true)
            {

                var eol = Message[start..].IndexOf("\r\n"u8);
                if (eol < 0)
                    return 0;

                start += eol + 2;

                // A folded field goes on in a line that begins with white space.
                if (start >= Message.Length || Message[start] is not ((Byte) ' ' or (Byte) '\t'))
                    return start;

            }

        }

        #endregion

        #region (internal static) FileNameOf(MessageId)

        /// <summary>
        /// The part of a file name that names the message: its Message-ID, without what no file
        /// name may have - and, when it is longer than <see cref="MaxMessageIdOctetsInFileName"/>
        /// octets, its beginning and a hash of the whole of it.
        /// </summary>
        internal static String FileNameOf(String MessageId)
        {

            var name = SanitizeFileName(MessageId);

            if (Encoding.UTF8.GetByteCount(name) <= MaxMessageIdOctetsInFileName)
                return name;

            var hash   = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(MessageId)))[..16];
            var prefix = new StringBuilder();
            var octets = 0;

            foreach (var rune in name.EnumerateRunes())
            {
                octets += rune.Utf8SequenceLength;
                if (octets > MaxMessageIdOctetsInFileName - hash.Length - 1)
                    break;
                prefix.Append(rune.ToString());
            }

            return $"{prefix}_{hash}";

        }

        #endregion

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

    }

}
