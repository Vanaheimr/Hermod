/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
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
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Bcpg.OpenPgp;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    public class HTTPNotificationSender : ANotificationSender
    {

        #region Data

        public static readonly IPPort DefaultHTTPPort = IPPort.Parse(80);

        public static readonly Regex JSONWhitespaceRegEx = new Regex(@"(\s)+", RegexOptions.IgnorePatternWhitespace);

        #endregion

        #region Properties

        public HTTPHostname  Hostname   { get; }

        public IPPort        HTTPPort   { get; }

        /// <summary>
        /// The current hash value of the API.
        /// </summary>
        public String CurrentDatabaseHashValue { get; private set; }

        #endregion

        #region Constructor(s)

        public HTTPNotificationSender(HTTPExtAPI         HTTPExtAPI,
                                      HTTPHostname       Hostname,
                                      IPPort?            HTTPPort                   = null,
                                      TimeSpan?          SendNotificationsEvery     = null,
                                      Boolean            DisableSendNotifications   = false,
                                      PgpPublicKeyRing?  PublicKeyRing              = null,
                                      PgpSecretKeyRing?  SecretKeyRing              = null,
                                      DNSClient?         DNSClient                  = null)

            : base(HTTPExtAPI,
                   SendNotificationsEvery,
                   DisableSendNotifications,
                   PublicKeyRing,
                   SecretKeyRing,
                   DNSClient)

        {

            this.Hostname  = Hostname;
            this.HTTPPort  = HTTPPort ?? DefaultHTTPPort;

        }

        #endregion


        #region (timer) SendNotifications(State)

        public override async Task SendNotifications(IEnumerable<JObject> JSONData)
        {

            var json = JSONData.ToArray();

            if (json.Length > 0)
            {

                var JSON = new JObject(
                               new JProperty("messages",    new JArray(JSONData)),
                               new JProperty("writer",      HTTPExtAPI.SystemId),
                               new JProperty("timestamp",   Timestamp.Now.ToIso8601()),
                               new JProperty("nonce",       Guid.NewGuid().ToString().Replace("-", "")),
                               new JProperty("parentHash",  CurrentDatabaseHashValue)
                           );

                CurrentDatabaseHashValue  = SHA256.HashData(Encoding.Unicode.GetBytes(JSONWhitespaceRegEx.Replace(JSON.ToString(), " "))).
                                                   Select(value => String.Format("{0:x2}", value)).
                                                   Aggregate();

                JSON.Add(new JProperty("hashValue", CurrentDatabaseHashValue));

            }

        }

        #endregion

    }

}
