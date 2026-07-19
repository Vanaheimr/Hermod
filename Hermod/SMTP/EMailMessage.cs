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

using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

public sealed class EMailMessage
{

    public String                              RawMessage      { get; init; } = "";
    public List<KeyValuePair<String, String>>  Headers         { get; }       = [];
    public String                              Body            { get; set; }  = "";
    public String?                             From            { get; set; }
    public List<String>                        To              { get; } = [];
    public String?                             Subject         { get; set; }
    public DateTimeOffset                      ReceivedAt      { get; init; } = Timestamp.Now;
    public String?                             MessageId       { get; set; }
    public DnsVerificationResult?              Verification    { get; set; }

    public static EMailMessage Parse(String rawMessage)
    {

        var message = new EMailMessage { RawMessage = rawMessage };

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
