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

using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Org.BouncyCastle.Security;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    /// <summary>
    /// The Network Time Security Key Establishment (NTS-KE) TLS client.
    /// </summary>
    public class NTSKE_TLSClient() : DefaultTlsClient(new BcTlsCrypto(new SecureRandom()))
    {

        #region Properties

        public TlsContext?  NTSKEContext    { get; private set; }

        /// <summary>
        /// The NTP-KE Client-2-Server Key
        /// </summary>
        public Byte[]?      NTS_C2S_Key     { get; private set; }

        /// <summary>
        /// The NTP-KE Server-2-Client Key
        /// </summary>
        public Byte[]?      NTS_S2C_Key     { get; private set; }

        #endregion


        #region NotifyHandshakeComplete()

        public override void NotifyHandshakeComplete()
        {

            base.NotifyHandshakeComplete();

            NTSKEContext = base.m_context;

            // Export 32 bytes for AES-SIV-CMAC-256:
            NTS_C2S_Key = NTSKEContext.ExportKeyingMaterial(
                "EXPORTER-network-time-security",
                [ 0x00, 0x00, 0x00, 0x0f, 0x00 ],
                32
            );

            NTS_S2C_Key = NTSKEContext.ExportKeyingMaterial(
                "EXPORTER-network-time-security",
                [ 0x00, 0x00, 0x00, 0x0f, 0x01 ],
                32
            );

        }

        #endregion

        #region (override) GetProtocolVersions()

        public override ProtocolVersion[] GetProtocolVersions()

            => [ ProtocolVersion.TLSv13 ];

        #endregion

        // Restrict to a subset of TLS 1.3 cipher suites
        //public override int[] GetCipherSuites()
        //
        //    => new int[] {
        //           CipherSuite.TLS_AES_256_GCM_SHA384,
        //           CipherSuite.TLS_CHACHA20_POLY1305_SHA256,
        //           CipherSuite.TLS_AES_128_GCM_SHA256,
        //       };

        #region (override) GetClientExtensions()

        public override IDictionary<Int32, Byte[]> GetClientExtensions()
        {

            var clientExtensions = base.GetClientExtensions();

            clientExtensions ??= new Dictionary<Int32, Byte[]>();

            TlsExtensionsUtilities.AddAlpnExtensionClient(
                clientExtensions,
                [ ProtocolName.Ntske_1 ]
            );

            return clientExtensions;

        }

        #endregion

        #region (override) GetAuthentication()

        public override TlsAuthentication GetAuthentication()
        {
            return new NoTLSClientAuthentication();
        }

        #endregion

    }

}
