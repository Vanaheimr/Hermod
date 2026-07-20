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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    public static class Tools
    {

        #region TryParseMail(MailText, out MailHeaders, out MailBody)

        public static Boolean TryParseMail(String                                    MailText,
                                           out ConcurrentDictionary<String, String>  MailHeaders,
                                           out List<String>                          MailBody)

            => Tools.TryParseMail(
                   MailText.Split([ "\r\n", "\r", "\n" ], StringSplitOptions.None),
                   out MailHeaders,
                   out MailBody
               );

        #endregion

        #region TryParseMail(MailText, out MailHeaders, out MailBody)

        public static Boolean TryParseMail(IEnumerable<String>                       MailText,
                                           out ConcurrentDictionary<String, String>  MailHeaders,
                                           out List<String>                          MailBody)
        {

            try
            {

                MailHeaders     = [];
                var MailHeader  = MailText.TakeWhile(line => line.IsNeitherNullNorEmpty()).ToList();

                var Key         = "";
                var Value       = "";
                var space       = ' ';
                var tab         = '\t';
                var splitter    = new Char[1] { ':' };
                String[] Splitted;

                foreach (var mailHeaderLine in MailHeader)
                {

                    if (mailHeaderLine.StartsWith(space) ||
                        mailHeaderLine.StartsWith(tab))
                    {
                        Value += " " + mailHeaderLine.Trim();
                    }

                    else
                    {

                        if (Key != "")
                        {
                            MailHeaders.TryAdd(Key, Value);
                            Key   = "";
                            Value = "";
                        }

                        Splitted = mailHeaderLine.Split(splitter, 2);
                        Key      = Splitted[0].Trim();
                        Value    = Splitted[1].Trim();

                    }

                }

                if (Key != "")
                    MailHeaders.TryAdd(Key, Value);

                MailBody = [.. MailText.SkipWhile(line => line.IsNeitherNullNorEmpty()).Skip(1)];

            }
            catch
            {
                MailHeaders = [];
                MailBody    = [];
                return false;
            }

            return true;

        }

        #endregion


        public static T SetEMailHeader<T>(this T EMail, String Key, String Value)
            where T : AbstractEMail
        {

            EMail.RemoveEMailHeader(Key);
            EMail.AddEMailHeader(Key, Value);

            return EMail;

        }

    }

}
