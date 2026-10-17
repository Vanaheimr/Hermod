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

using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// A DNS blocklist that names an address, and what it answered - 127.0.0.4, say,
    /// which tells its own reason.
    /// </summary>
    public sealed record DnsBlocklisting(String  Zone,
                                         String  Answer);


    public sealed partial class DNSVerifier
    {

        #region CheckBlocklistsAsync(ClientIp, Config, ...)

        /// <summary>
        /// The DNS blocklists (RFC 5782) that name the given address - all asked at
        /// once, each waited for as long as the configuration says.
        /// </summary>
        /// <param name="ClientIp">The address of the client.</param>
        /// <param name="Config">The lists, and how long to wait for them.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<IReadOnlyList<DnsBlocklisting>> CheckBlocklistsAsync(System.Net.IPAddress  ClientIp,
                                                                               DnsBlocklistConfig    Config,
                                                                               CancellationToken     CancellationToken = default)
        {

            var ip = ClientIp.IsIPv4MappedToIPv6 ? ClientIp.MapToIPv4() : ClientIp;

            if (!Config.CheckPrivateAddresses && IsPrivate(ip))
                return [];

            var reversed = ReversedName(ip);
            var answers  = await Task.WhenAll(Config.Zones.
                                                     Select (zone => zone.Trim().TrimEnd('.')).
                                                     Where  (zone => zone.Length > 0).
                                                     Distinct(StringComparer.OrdinalIgnoreCase).
                                                     Select (zone => AskBlocklistAsync(reversed, zone, Config.Timeout, CancellationToken)));

            return [.. answers.OfType<DnsBlocklisting>()];

        }

        #endregion

        #region (private) AskBlocklistAsync(Reversed, Zone, Timeout, ...)

        private async Task<DnsBlocklisting?> AskBlocklistAsync(String             Reversed,
                                                               String             Zone,
                                                               TimeSpan           Timeout,
                                                               CancellationToken  CancellationToken)
        {

            try
            {

                var response = await DNSClient.Query(
                                         DNSServiceName.Parse($"{Reversed}.{Zone}"),
                                         [ DNSResourceRecordTypes.A ],
                                         Timeout:            Timeout,
                                         CancellationToken:  CancellationToken
                                     );

                foreach (var a in response.Answers.OfType<A>())
                {

                    var octets = a.IPv4Address.GetBytes();
                    var answer = String.Join('.', octets);

                    // RFC 5782 §2.1: an entry is an address in 127.0.0.0/8.
                    if (octets[0] != 127)
                    {
                        Logger.Log(LogLevel.Warning, $"DNS blocklist {Zone} answered {answer} - no answer of a blocklist (a resolver that answers every name?); not counted");
                        continue;
                    }

                    // Spamhaus' "you may not ask": a public resolver, or too many questions.
                    if (octets[1] == 255 && octets[2] == 255)
                    {
                        Logger.Log(LogLevel.Warning, $"DNS blocklist {Zone} refused the question ({answer}) - asked through a public resolver, or too often?; not counted");
                        continue;
                    }

                    return new DnsBlocklisting(Zone, answer);

                }

                return null;

            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Info, $"DNS blocklist {Zone} not asked: {e.Message}");
                return null;
            }

        }

        #endregion

        #region (private static) IsPrivate(IP)

        // No list names these: loopback, RFC 1918, link-local, shared (RFC 6598), unique local.
        private static Boolean IsPrivate(System.Net.IPAddress IP)
        {

            if (System.Net.IPAddress.IsLoopback(IP))
                return true;

            if (IP.AddressFamily == AddressFamily.InterNetwork)
            {
                var o = IP.GetAddressBytes();
                return o[0] == 10 ||
                      (o[0] == 172 && o[1] >= 16 && o[1] <= 31) ||
                      (o[0] == 192 && o[1] == 168) ||
                      (o[0] == 169 && o[1] == 254) ||
                      (o[0] == 100 && o[1] >= 64 && o[1] <= 127) ||
                       o[0] == 0;
            }

            return IP.IsIPv6LinkLocal || IP.IsIPv6UniqueLocal || IP.IsIPv6SiteLocal;

        }

        #endregion

    }

}
