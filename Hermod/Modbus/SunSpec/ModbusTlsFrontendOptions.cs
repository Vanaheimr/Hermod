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


using System.Security.Cryptography.X509Certificates;

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <param name="ServerPfxPath">The PKCS#12 file holding the certificate this frontend shows, unless <paramref name="ServerCertificateSelector"/> says.</param>
/// <param name="CaCertPath">The CA a client certificate must chain to, unless <paramref name="ClientTrustAnchors"/> says.</param>
/// <param name="ServerCertificateSelector">Asked at every handshake which certificate to show, and which intermediates to send with it, given the SNI name the client sent. A frontend that has one of these can be given a new certificate without being restarted.</param>
/// <param name="ClientTrustAnchors">Asked at every handshake which CAs a client certificate may chain to. More than one, because a meter can be reached by peers from more than one PKI.</param>
public sealed record ModbusTlsFrontendOptions(
    System.Net.IPAddress  ListenAddress,
    Int32                 ListenPort,
    String?               ServerPfxPath,
    String?               ServerPfxPassword,
    String?               CaCertPath,
    TimeSpan              HandshakeTimeout,
    TimeSpan              IdleTimeout,
    TimeSpan              WriteTimeout,
    IReadOnlyList<ModbusTlsFrontendSNIBinding>?          SNIBindings                = null,
    Func<String?, ServerCertificateChain>?               ServerCertificateSelector  = null,
    Func<IReadOnlyCollection<X509Certificate2>>?         ClientTrustAnchors         = null
);

public sealed record ModbusTlsFrontendSNIBinding(
    String  ServerName,
    String  ServerPfxPath,
    String? ServerPfxPassword,
    String  CaCertPath
);
