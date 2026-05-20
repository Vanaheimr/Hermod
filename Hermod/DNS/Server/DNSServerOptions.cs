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

using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Runtime configuration for a DNS server instance.
    /// </summary>
    public sealed class DNSServerOptions
    {

        public Boolean   EnableUDPUnicast       { get; init; } = true;

        public Boolean   EnableUDPMulticast     { get; init; } = true;

        public Boolean   EnableTCPUnicast       { get; init; } = true;

        public Boolean   EnableTLSUnicast       { get; init; } = false;

        public IPSocket  UDPUnicastSocket       { get; init; } = new(IPvXAddress.Any, IPPort.Parse(63));

        public IPSocket  TCPUnicastSocket       { get; init; } = new(IPvXAddress.Any, IPPort.Parse(63));

        public IPSocket  TLSUnicastSocket       { get; init; } = new(IPvXAddress.Any, IPPort.DNS_TLS);

        public IPSocket  UDPMulticastSocket     { get; init; } = new(IPvXAddress.Any, IPPort.Parse(6363));

        public String    MulticastGroupAddress  { get; init; } = "224.0.0.251";

        public Int32     TCPBacklog             { get; init; } = 100;

        public TimeSpan  TCPReadTimeout         { get; init; } = TimeSpan.FromSeconds(30);

        public X509Certificate2?  TLSServerCertificate  { get; init; }

        public SslProtocols      TLSProtocols           { get; init; } = SslProtocols.Tls13 | SslProtocols.Tls12;

        public Boolean           TLSClientCertificateRequired { get; init; } = false;

        public RemoteCertificateValidationCallback? TLSClientCertificateValidator { get; init; }

        public X509RevocationMode TLSCertificateRevocationCheckMode { get; init; } = X509RevocationMode.NoCheck;

        public Boolean   UseCompression         { get; init; } = false;

    }

}
