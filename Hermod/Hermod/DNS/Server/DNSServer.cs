/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using Org.BouncyCastle.Bcpg;
using System.Net;
using System.Net.Sockets;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Generic resource record for unsupported types.
    /// </summary>
    public class GenericRecord : DNSResourceRecord
    {
        public GenericRecord(DomainName domainName, DNSResourceRecordType type, DNSQueryClasses cls, TimeSpan ttl, UInt16 rdLength, byte[] packet, int rdataOffset)
            : base(domainName, type, cls, ttl, rdLength, packet, rdataOffset)
        {
        }

        public override object ParseRData()
        {
            byte[] rdata = new byte[RDLength];
            Array.Copy(Packet, RDataOffset, rdata, 0, RDLength);
            return rdata; // Raw bytes
        }

    }

    /// <summary>
    /// OPT pseudo resource record (EDNS) from Extension Mechanisms for DNS (EDNS)
    /// RFC 6891
    /// </summary>
    public class OPTRecord : DNSResourceRecord
    {
        public Byte          ExtendedRCODE    { get; }
        public Byte          Version          { get; }
        public UInt16        Flags            { get; }
        public List<Object>  Options          { get; }

        public OPTRecord(DomainName domainName, DNSQueryClasses udpSize, UInt32 ttl, UInt16 rdLength, byte[] packet, int rdataOffset)
            : base(domainName, DNSResourceRecordType.OPT, udpSize, TimeSpan.FromSeconds(ttl), rdLength, packet, rdataOffset)
        {

            // For OPT, TTL is encoded: Extended RCODE (8 bits), Version (8 bits), Flags (16 bits)
            // Convert TimeSpan to seconds (UInt32) for parsing

            ExtendedRCODE = (Byte)    (ttl >> 24);
            Version       = (Byte)   ((ttl >> 16) & 0xFF);
            Flags         = (UInt16)  (ttl & 0xFFFF);

            Options       = new List<object>();

        }

        public override object ParseRData()
        {
            var options = new List<object>();
            int pos = RDataOffset;
            while (pos < RDataOffset + RDLength)
            {
                UInt16 code = (UInt16) ((Packet[pos++] << 8) | Packet[pos++]);
                UInt16 len  = (UInt16) ((Packet[pos++] << 8) | Packet[pos++]);
                byte[] data = new byte[len];
                Array.Copy(Packet, pos, data, 0, len);
                pos += len;
                options.Add(new { Code = code, Data = data });
            }
            return options;
        }

    }


    /// <summary>
    /// A resource record (IPv4 address).
    /// </summary>
    public class ARecord : DNSResourceRecord
    {
        public ARecord(DNSServiceName domainName, DNSQueryClasses cls, TimeSpan ttl, UInt16 rdLength, byte[] packet, int rdataOffset)
            : base(domainName, DNSResourceRecordType.A, cls, ttl, rdLength, packet, rdataOffset)
        {
        }

        public override object ParseRData()
        {
            if (RDLength != 4) throw new InvalidDataException("Invalid A RData length");

            byte[] addressBytes = new byte[4];
            Array.Copy(Packet, RDataOffset, addressBytes, 0, 4);
            return new System.Net.IPAddress(addressBytes);
        }
    }

    /// <summary>
    /// AAAA resource record (IPv6 address).
    /// </summary>
    public class AAAARecord : DNSResourceRecord
    {
        public AAAARecord(DNSServiceName domainName, DNSQueryClasses cls, TimeSpan ttl, UInt16 rdLength, byte[] packet, int rdataOffset)
            : base(domainName, DNSResourceRecordType.AAAA, cls, ttl, rdLength, packet, rdataOffset)
        {
        }

        public override object ParseRData()
        {
            if (RDLength != 16) throw new InvalidDataException("Invalid AAAA RData length");

            byte[] addressBytes = new byte[16];
            Array.Copy(Packet, RDataOffset, addressBytes, 0, 16);
            return new System.Net.IPAddress(addressBytes);
        }
    }

    /// <summary>
    /// SRV resource record.
    /// </summary>
    public class SRVRecord : DNSResourceRecord
    {
        public SRVRecord(DomainName domainName, DNSQueryClasses cls, TimeSpan ttl, UInt16 rdLength, byte[] packet, int rdataOffset)
            : base(domainName, DNSResourceRecordType.SRV, cls, ttl, rdLength, packet, rdataOffset)
        {
        }

        public override object ParseRData()
        {

            var pos       = RDataOffset;
            var priority  = (UInt16) ((Packet[pos++] << 8) | Packet[pos++]);
            var weight    = (UInt16) ((Packet[pos++] << 8) | Packet[pos++]);
            var port      = (UInt16) ((Packet[pos++] << 8) | Packet[pos++]);
            var target    = DNSTools.ReadDomainName(Packet, ref pos);

            return new { Priority = priority, Weight = weight, Port = port, Target = DNS.DomainName.Parse(target) };

        }

    }

    /// <summary>
    /// URI resource record.
    /// </summary>
    public class URIRecord : DNSResourceRecord
    {
        public URIRecord(DomainName domainName, DNSQueryClasses cls, TimeSpan ttl, UInt16 rdLength, byte[] packet, int rdataOffset)
            : base(domainName, DNSResourceRecordType.URI, cls, ttl, rdLength, packet, rdataOffset)
        {
        }

        public override object ParseRData()
        {
            int pos = RDataOffset;
            ushort priority = (ushort)((Packet[pos++] << 8) | Packet[pos++]);
            ushort weight = (ushort)((Packet[pos++] << 8) | Packet[pos++]);
            string target = Encoding.UTF8.GetString(Packet, pos, RDLength - 4);

            return new { Priority = priority, Weight = weight, Target = target };
        }
    }

    /// <summary>
    /// NAPTR resource record.
    /// </summary>
    public class NAPTRRecord : DNSResourceRecord
    {
        public NAPTRRecord(DomainName domainName, DNSQueryClasses cls, TimeSpan ttl, UInt16 rdLength, byte[] packet, int rdataOffset)
            : base(domainName, DNSResourceRecordType.NAPTR, cls, ttl, rdLength, packet, rdataOffset)
        {
        }

        public override object ParseRData()
        {

            var pos         = RDataOffset;
            var order       = (UInt16) ((Packet[pos++] << 8) | Packet[pos++]);
            var preference  = (UInt16) ((Packet[pos++] << 8) | Packet[pos++]);
            var flags       = ReadCharacterString(Packet, ref pos);
            var services    = ReadCharacterString(Packet, ref pos);
            var regexp      = ReadCharacterString(Packet, ref pos);
            var replacement = DNSTools.ReadDomainName(Packet, ref pos);

            return new {
                       Order        = order,
                       Preference   = preference,
                       Flags        = flags,
                       Services     = services,
                       Regexp       = regexp,
                       Replacement  = DNSServiceName.Parse(replacement)
                   };

        }

    }

    public abstract class DNSResourceRecord
    {

        public DNSServiceName          DomainName { get; }
        public DNSResourceRecordType  Type { get; }
        public DNSQueryClasses     Class { get; }
        public TimeSpan            TTL { get; }


        public UInt16 RDLength { get; }
        protected byte[] Packet { get; }
        protected int RDataOffset { get; }

        protected DNSResourceRecord(DNSServiceName domainName, DNSResourceRecordType type, DNSQueryClasses cls, TimeSpan ttl, UInt16 rdLength, byte[] packet, int rdataOffset)
        {
            DomainName = domainName;
            Type = type;
            Class = cls;
            TTL = ttl;
            RDLength = rdLength;
            Packet = packet;
            RDataOffset = rdataOffset;
        }

        protected DNSResourceRecord(DomainName domainName, DNSResourceRecordType type, DNSQueryClasses cls, TimeSpan ttl, UInt16 rdLength, byte[] packet, int rdataOffset)
        {
            DomainName = DNSServiceName.Parse(domainName.FullName);
            Type = type;
            Class = cls;
            TTL = ttl;
            RDLength = rdLength;
            Packet = packet;
            RDataOffset = rdataOffset;
        }

        protected static string ReadCharacterString(byte[] packet, ref int pos)
        {
            byte len = packet[pos++];
            string str = Encoding.ASCII.GetString(packet, pos, len);
            pos += len;
            return str;
        }

        public abstract object ParseRData();


    }




    public class DNSServer
    {

        private const Int32   UDPUnicastPort         = 63;
        private const Int32   MulticastPort          = 6363;
        private const String  MulticastGroupAddress  = "224.0.0.251";

        private UdpClient? _unicastListener;
        private UdpClient? _multicastListener;
        private CancellationTokenSource _cts;

        public async Task StartAsync()
        {
            _cts = new CancellationTokenSource();

            // Starte Unicast-Listener
            var unicastTask = ListenUnicastAsync(_cts.Token);

            // Starte Multicast-Listener
            var multicastTask = ListenMulticastAsync(_cts.Token);

            // Warte auf beide Tasks (oder Abbruch)
            await Task.WhenAll(unicastTask, multicastTask);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _unicastListener?.Dispose();
            _multicastListener?.Dispose();
        }

        private async Task ListenUnicastAsync(CancellationToken token)
        {

            _unicastListener = new UdpClient(UDPUnicastPort);
            Console.WriteLine($"Unicast-Listener gestartet auf Port {UDPUnicastPort}.");

            while (!token.IsCancellationRequested)
            {
                try
                {

                    var receiveResult  = await _unicastListener.ReceiveAsync(token);

                    var remoteEndPoint = receiveResult.RemoteEndPoint;
                    var receivedBytes  = receiveResult.Buffer;

                    Console.WriteLine($"Unicast-Anfrage empfangen von {remoteEndPoint}: {receivedBytes.Length} Bytes.");

                    var responseBytes = ProcessDNSQuery(receivedBytes);
                    if (responseBytes is not null)
                    {
                        await _unicastListener.SendAsync(new ReadOnlyMemory<Byte>(responseBytes), remoteEndPoint, token);
                        Console.WriteLine($"Unicast-Antwort gesendet an {remoteEndPoint}.");
                    }

                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler im Unicast-Listener: {ex.Message}");
                }
            }
        }

        private async Task ListenMulticastAsync(CancellationToken token)
        {

            _multicastListener = new UdpClient();
            _multicastListener.ExclusiveAddressUse = false;
            _multicastListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _multicastListener.Client.Bind(new IPEndPoint(System.Net.IPAddress.Any, MulticastPort));

            var multicastAddress = System.Net.IPAddress.Parse(MulticastGroupAddress);
            _multicastListener.JoinMulticastGroup(multicastAddress);

            Console.WriteLine($"Multicast-Listener gestartet auf Port {MulticastPort}, Gruppe {MulticastGroupAddress}.");

            while (!token.IsCancellationRequested)
            {
                try
                {

                    var receiveResult  = await _multicastListener.ReceiveAsync(token);

                    var remoteEndPoint = receiveResult.RemoteEndPoint;
                    var receivedBytes  = receiveResult.Buffer;

                    Console.WriteLine($"Multicast-Anfrage empfangen von {remoteEndPoint}: {receivedBytes.Length} Bytes.");

                    var responseBytes = ProcessDNSQuery(receivedBytes);
                    if (responseBytes is not null)
                    {
                        // Für Multicast: Antwort als Unicast zurücksenden (üblich für mDNS)
                        await _multicastListener.SendAsync(new ReadOnlyMemory<byte>(responseBytes), remoteEndPoint, token);
                        Console.WriteLine($"Multicast-Antwort gesendet an {remoteEndPoint}.");
                    }
                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler im Multicast-Listener: {ex.Message}");
                }
            }

            // Cleanup: Multicast-Gruppe verlassen
            _multicastListener.DropMulticastGroup(multicastAddress);

        }





        private Byte[] ProcessDNSQuery(Byte[] packet)
        {

            var query = DNSPacket.Parse(packet);

            query.Questions.ForEachCounted((question, i) => {
                Console.WriteLine($"Question {i+1}: Name={question.DomainName}, Type={question.QueryType}, Class={question.QueryClass}");
            });


            //// Kopiere Anfrage-ID (erste 2 Bytes)
            var response = new byte[packet.Length];
            Array.Copy(packet, response, packet.Length);

            // Setze QR-Flag auf 1 (Response), und AA auf 1 (Authoritative)
            response[2] = (byte)(response[2] | 0x80); // QR = 1
            response[3] = (byte)(response[3] | 0x04); // AA = 1 (Beispiel)

            // Für Demo: Einfach echo zurück
            return response;

        }


        


    }

}
