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
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    public sealed class FileMailStorage(string basePath, ILogger logger) : IMailStorage
    {
        public async Task<string> StoreAsync(
            EMailMessage        message,
            string              envelopeFrom,
            IEnumerable<string> envelopeTo,
            CancellationToken   ct = default)
        {
            Directory.CreateDirectory(basePath);

            var timestamp = Timestamp.Now.ToString("yyyyMMdd_HHmmss_fff");
            var messageId = message.MessageId ?? UUIDv7.Generate().ToString("N")[..16];
            var fileName = $"{timestamp}_{SanitizeFileName(messageId)}.eml";
            var filePath = Path.Combine(basePath, fileName);

            // Build metadata header (CRLF line endings, independent of host OS)
            var metadata = new StringBuilder();
            void AddHeader(string name, string value) => metadata.Append(name).Append(": ").Append(value).Append("\r\n");

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

            var fullMessage = metadata.ToString() + message.RawMessage;
            // Write UTF-8 without BOM so the stored .eml starts with the first header byte.
            await File.WriteAllTextAsync(filePath, fullMessage, new UTF8Encoding(false), ct);

            logger.Log(LogLevel.Info, $"Stored message: {filePath}");
            return filePath;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }
    }

}
