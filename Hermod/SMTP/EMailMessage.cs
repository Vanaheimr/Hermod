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
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

public sealed class EMailMessage
{

    /// <summary>
    /// The message as text: its octets decoded as UTF-8. Octets that are not UTF-8 - an 8-bit
    /// body in ISO-8859-1, say - come out as U+FFFD here; <see cref="RawBytes"/> keeps them.
    /// </summary>
    public String                              RawMessage      { get; init; } = "";

    /// <summary>
    /// The octets of the message exactly as they arrived over SMTP (after dot-unstuffing, with
    /// the trace headers of this hop in front), or null for a message that was made from text.
    /// What a mail store should write: a decoded and re-encoded copy is a different message for
    /// every byte that was not UTF-8, and for every signature over it.
    /// </summary>
    public Byte[]?                             RawBytes        { get; init; }

    public List<KeyValuePair<String, String>>  Headers         { get; }       = [];
    public String                              Body            { get; set; }  = "";
    public String?                             From            { get; set; }
    public List<String>                        To              { get; } = [];
    public String?                             Subject         { get; set; }
    public DateTimeOffset                      ReceivedAt      { get; init; } = Timestamp.Now;
    public String?                             MessageId       { get; set; }
    public DnsVerificationResult?              Verification    { get; set; }


    /// <summary>
    /// The octets of this message: as they arrived, when it came over SMTP, otherwise its text
    /// as UTF-8.
    /// </summary>
    public Byte[] GetBytes()
        => RawBytes ?? Encoding.UTF8.GetBytes(RawMessage);


    public static EMailMessage Parse(String rawMessage)
        => Parse(rawMessage, null);

    /// <summary>
    /// Parse a message from the octets it arrived as. The headers are read from its UTF-8
    /// decoding, as with <see cref="Parse(String)"/>; the octets themselves are kept unchanged
    /// in <see cref="RawBytes"/>.
    /// </summary>
    public static EMailMessage Parse(Byte[] rawBytes)
        => Parse(Encoding.UTF8.GetString(rawBytes), rawBytes);

    private static EMailMessage Parse(String rawMessage, Byte[]? rawBytes)
    {

        var message = new EMailMessage { RawMessage = rawMessage, RawBytes = rawBytes };

        var headerBodySplit = rawMessage.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (headerBodySplit < 0)
            headerBodySplit = rawMessage.IndexOf("\n\n", StringComparison.Ordinal);

        string headerSection, bodySection;
        if (headerBodySplit > 0)
        {
            headerSection = rawMessage[..headerBodySplit];
            bodySection = rawMessage[(headerBodySplit + (rawMessage[headerBodySplit] == '\r' ? 4 : 2))..];
        }
        else
        {
            headerSection = rawMessage;
            bodySection = "";
        }

        message.Body = bodySection;

        // Parse headers (handle folded headers)
        var unfoldedHeaders = Regex.Replace(headerSection, @"\r?\n[ \t]+", " ");
        var headerLines = unfoldedHeaders.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in headerLines)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {

                var name = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();
                message.Headers.Add(new KeyValuePair<string, string>(name, value));

                switch (name.ToLowerInvariant())
                {
                    case "from":
                        message.From = MailAddressParser.ParseSingle(value);
                        break;
                    case "to":
                        message.To.AddRange(MailAddressParser.ParseAddressList(value));
                        break;
                    case "subject":
                        message.Subject = value;
                        break;
                    case "message-id":
                        message.MessageId = value.Trim('<', '>');
                        break;
                }

            }
        }

        return message;

    }

}
