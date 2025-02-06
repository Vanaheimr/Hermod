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

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    public class NTSKE_TLSClient() : DefaultTlsClient(new BcTlsCrypto(new SecureRandom()))
    {

        #region Properties

        public TlsContext?  MyContext      { get; private set; }
        public Byte[]?      ExportedKey    { get; private set; }

        #endregion


        #region NotifyHandshakeComplete()

        public override void NotifyHandshakeComplete()
        {

            base.NotifyHandshakeComplete();

            MyContext = base.m_context;

            // If you already know your association context, label, etc., you can export right here:
            var chosenAEAD = 0x000F;
            var associationContext = new Byte[5] {
                0x00, 0x00, (byte) (chosenAEAD >> 8), (byte) (chosenAEAD & 0xFF), 0x00
            };

            // Export 32 bytes for AES-SIV-CMAC-256:
            ExportedKey = MyContext.ExportKeyingMaterial(
                "EXPORTER-network-time-security",
                associationContext,
                32
            );

        }

        #endregion

        #region (override) GetProtocolVersions()

        public override ProtocolVersion[] GetProtocolVersions()
        {
            return [ ProtocolVersion.TLSv13 ];
        }

        #endregion

        // Restrict to a subset of TLS 1.3 cipher suites
        //public override int[] GetCipherSuites()
        //{
        //    return new int[]
        //    {
        //    CipherSuite.TLS_AES_256_GCM_SHA384,
        //    CipherSuite.TLS_CHACHA20_POLY1305_SHA256,
        //    CipherSuite.TLS_AES_128_GCM_SHA256,
        //    };
        //}

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
            return new NullTlsAuthentication();
        }

        #endregion

    }

    public class NullTlsAuthentication : TlsAuthentication
    {

        public void NotifyServerCertificate(Certificate serverCertificate)
        {
            // In real code, check serverCertificate for validity/pinning here
        }

        public TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
        {
            // If no client cert needed, return null
            return null;
        }

        public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
        {
            
        }

    }

}
