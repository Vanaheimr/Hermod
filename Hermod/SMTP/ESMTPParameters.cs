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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    /// <summary>
    /// A refusal of MAIL or RCPT parameters: 555 for a parameter the server does not
    /// recognise or implement (RFC 5321 §4.1.1.11), 501 for one it knows with a value
    /// that is not valid.
    /// </summary>
    public sealed record ESMTPParameterError(Int32   Code,
                                             String  Text);


    /// <summary>
    /// The Mail-parameters and Rcpt-parameters of RFC 5321 §4.1.2, checked against the
    /// extensions this server advertises.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 5321 §4.1.1.11: "If the server SMTP does not recognize or cannot implement one
    /// or more of the parameters associated with a particular MAIL FROM or RCPT TO
    /// command, it will return code 555." Silently ignoring one is the worse answer: the
    /// client believes a request was accepted - a DSN, a size, a body type - that the
    /// server never honours.
    /// </para>
    /// <para>
    /// Grammar: <c>esmtp-param = esmtp-keyword ["=" esmtp-value]</c>,
    /// <c>esmtp-keyword = (ALPHA / DIGIT) *(ALPHA / DIGIT / "-")</c>,
    /// <c>esmtp-value = 1*(%d33-60 / %d62-126)</c> - extended to UTF-8 by RFC 6531 §3.3.
    /// Keywords are case-insensitive; a keyword given twice with different values is an error.
    /// </para>
    /// </remarks>
    public static partial class ESMTPParameters
    {

        [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9-]*$")]
        private static partial Regex Keyword();

        // xtext (RFC 3461 §4): xchar = "!".."~" except "+" and "="; hexchar = "+" 2(%x30-39 / %x41-46)
        [GeneratedRegex(@"^(?:[!-*,-<>-~]|\+[0-9A-F]{2})+$")]
        private static partial Regex XText();

        // ORCPT (RFC 3461 §4.2): addr-type ";" xtext, with addr-type an atom.
        [GeneratedRegex(@"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+;(.+)$")]
        private static partial Regex OriginalRecipient();


        /// <summary>
        /// Split a parameter string into keywords (upper-cased) and values.
        /// </summary>
        /// <returns>The parameters, or the error that makes the whole list invalid.</returns>
        public static (IReadOnlyDictionary<String, String?> Parameters, ESMTPParameterError? Error) Parse(String Text)
        {

            var parameters = new Dictionary<String, String?>(StringComparer.Ordinal);

            foreach (var param in Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {

                var equals  = param.IndexOf('=');
                var keyword = (equals < 0 ? param : param[..equals]).ToUpperInvariant();
                var value   =  equals < 0 ? null  : param[(equals + 1)..];

                if (!Keyword().IsMatch(keyword))
                    return (parameters, new (501, $"5.5.4 Invalid parameter \"{param}\""));

                if (value is not null && (value.Length == 0 || value.Any(c => c < '!' || c == '=' || c == '\x7F')))
                    return (parameters, new (501, $"5.5.4 Invalid value for {keyword}"));

                // The same parameter twice with the same value is redundant, not contradictory -
                // and real: CPython's smtplib adds SMTPUTF8 on its own when an address needs it,
                // next to the one its caller passed. Two different values are a contradiction.
                if (!parameters.TryAdd(keyword, value) &&
                    !String.Equals(parameters[keyword], value, StringComparison.OrdinalIgnoreCase))
                    return (parameters, new (501, $"5.5.4 Parameter {keyword} given twice with different values"));

            }

            return (parameters, null);

        }


        /// <summary>
        /// Check the parameters of a MAIL command.
        /// </summary>
        /// <param name="Parameters">The parsed parameters.</param>
        /// <param name="ExtendedSmtp">Whether the session began with EHLO. After HELO no extension was negotiated, so no parameter is.</param>
        public static ESMTPParameterError? ValidateMail(IReadOnlyDictionary<String, String?> Parameters,
                                                        Boolean                              ExtendedSmtp)
        {

            foreach (var (keyword, value) in Parameters)
            {

                if (!ExtendedSmtp)
                    return new (555, $"5.5.4 {keyword} not available after HELO");

                var error = keyword switch {

                    // RFC 1870 §3: size-value ::= 1*20DIGIT
                    "SIZE"        => value is not null && value.Length <= 20 && value.All(Char.IsAsciiDigit)
                                         ? null
                                         : "5.5.4 Invalid SIZE value",

                    // RFC 6152 §2: body-value ::= "7BIT" / "8BITMIME". BINARYMIME (RFC 3030 §3)
                    // is not advertised, so it is a value this server does not implement.
                    "BODY"        => value?.ToUpperInvariant() switch {
                                         "7BIT" or "8BITMIME" => null,
                                         "BINARYMIME"         => "!5.5.4 BODY=BINARYMIME not supported",
                                         _                    => "5.5.4 Invalid BODY value"
                                     },

                    // RFC 6531 §3.4, RFC 8689 §4.1: keywords without a value.
                    "SMTPUTF8"    => value is null ? null : "5.5.4 SMTPUTF8 takes no value",
                    "REQUIRETLS"  => value is null ? null : "5.5.4 REQUIRETLS takes no value",

                    // RFC 3461 §4.3, §4.4
                    "RET"         => value?.ToUpperInvariant() is "FULL" or "HDRS"
                                         ? null
                                         : "5.5.4 Invalid RET value",
                    "ENVID"       => value is not null && value.Length <= 100 && XText().IsMatch(value)
                                         ? null
                                         : "5.5.4 Invalid ENVID value",

                    // RFC 6710 §3: priority-value = [ "+" / "-" ] 1DIGIT
                    "MT-PRIORITY" => value is not null && Int32.TryParse(value, out var priority) && priority is >= -9 and <= 9 && value.Length <= 2
                                         ? null
                                         : "5.5.4 Invalid MT-PRIORITY value",

                    // RFC 4954 §5: AUTH=<> or AUTH=xtext
                    "AUTH"        => value is not null && (value == "<>" || XText().IsMatch(value))
                                         ? null
                                         : "5.5.4 Invalid AUTH value",

                    _             => $"!5.5.4 Unsupported parameter {keyword}"

                };

                if (error is not null)
                    return error.StartsWith('!')
                               ? new (555, error[1..])
                               : new (501, error);

            }

            return null;

        }


        /// <summary>
        /// Check the parameters of a RCPT command.
        /// </summary>
        /// <param name="Parameters">The parsed parameters.</param>
        /// <param name="ExtendedSmtp">Whether the session began with EHLO.</param>
        public static ESMTPParameterError? ValidateRcpt(IReadOnlyDictionary<String, String?> Parameters,
                                                        Boolean                              ExtendedSmtp)
        {

            foreach (var (keyword, value) in Parameters)
            {

                if (!ExtendedSmtp)
                    return new (555, $"5.5.4 {keyword} not available after HELO");

                switch (keyword)
                {

                    // RFC 3461 §4.1: "NEVER" alone, or a list of SUCCESS, FAILURE, DELAY.
                    case "NOTIFY":
                        var values = (value ?? "").ToUpperInvariant().Split(',');
                        var valid  = values is [ "NEVER" ] ||
                                     (values.Length > 0 && values.All(v => v is "SUCCESS" or "FAILURE" or "DELAY") && values.Distinct().Count() == values.Length);
                        if (!valid)
                            return new (501, "5.5.4 Invalid NOTIFY value");
                        break;

                    // RFC 3461 §4.2: ORCPT=addr-type;xtext
                    case "ORCPT":
                        var match = value is not null ? OriginalRecipient().Match(value) : null;
                        if (match is null || !match.Success || !XText().IsMatch(match.Groups[1].Value))
                            return new (501, "5.5.4 Invalid ORCPT value");
                        break;

                    default:
                        return new (555, $"5.5.4 Unsupported parameter {keyword}");

                }

            }

            return null;

        }

    }

}
