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

using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Tls;

using org.GraphDefined.Vanaheimr.Illias;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{


    public class NTSClient
    {

        #region Data

        public const    UInt16   DefaultNTSKE_Port  = 4460;
        public const    UInt16   DefaultNTP_Port    = 123;
        public readonly TimeSpan DefaultTimeout     = TimeSpan.FromSeconds(3);

        #endregion

        #region Properties

        public String     Host              { get; }
        public UInt16     NTSKE_Port        { get; }
        public UInt16     NTP_Port          { get; }
        public TimeSpan?  Timeout           { get; set; }
        public Byte[]     C2S_Key           { get; set; }
        public Byte[]     S2C_Key           { get; set; }

        #endregion

        #region NTSClient(Host, NTSKE_Port = 4460, NTP_Port = 123, Timeout = null)

        public NTSClient(String     Host,
                         UInt16     NTSKE_Port   = DefaultNTSKE_Port,
                         UInt16     NTP_Port     = DefaultNTP_Port,
                         TimeSpan?  Timeout      = null)
        {

            this.Host        = Host;
            this.NTSKE_Port  = NTSKE_Port;
            this.NTP_Port    = NTP_Port;
            this.Timeout     = Timeout;
            this.C2S_Key     = [];
            this.S2C_Key     = [];

        }

        #endregion


        #region GetNTSKERecords(Timeout = null)

        /// <summary>
        /// Get NTS-KE records from the server.
        /// </summary>
        /// <param name="Timeout">An optional timeout.</param>
        public NTSKE_Response GetNTSKERecords(TimeSpan? Timeout = null)
        {
            try
            {

                var timeout              = Timeout ?? this.Timeout ?? DefaultTimeout;

                using var tcpClient      = new TcpClient(Host, NTSKE_Port) {
                                               ReceiveTimeout = (Int32) timeout.TotalMilliseconds
                                           };

                using var networkStream  = tcpClient.GetStream();

                var tlsClientProtocol    = new TlsClientProtocol(networkStream);
                var ntsTlsClient         = new NTSKE_TLSClient();

                tlsClientProtocol.Connect(ntsTlsClient);

                C2S_Key                  = ntsTlsClient.NTS_C2S_Key ?? [];
                S2C_Key                  = ntsTlsClient.NTS_S2C_Key ?? [];

                var ntsKeRequest = BuildNTSKERequest();
                tlsClientProtocol.Stream.Write(ntsKeRequest, 0, ntsKeRequest.Length);
                tlsClientProtocol.Stream.Flush();

                var buffer               = new Byte[4096];

                var readTask             = Task.Run(() => tlsClientProtocol.Stream.Read(buffer, 0, buffer.Length));
                if (!readTask.Wait(timeout))
                {
                    throw new TimeoutException("Read operation timed out.");
                }

                var bytesRead            = readTask.Result;
                if (bytesRead > 0)
                {

                    Array.Resize(ref buffer, bytesRead);

                    if (TryParseNTSKE_Response(buffer, out var record, out var errorResponse))
                        return new NTSKE_Response(
                                   record,
                                   C2S_Key,
                                   S2C_Key
                               );

                }
                else
                {
                    DebugX.Log($"No response received from {Host}!");
                }

            }
            catch (Exception ex)
            {
                DebugX.Log("Exception: " + ex.Message);
            }

            return new NTSKE_Response([], [], []);

        }

        #endregion


        #region ValidateServerCertificate(...)

        /// <summary>
        /// Certificate validation callback.
        /// In this demo, all certificates are accepted.
        /// In production, validate the certificate properly.
        /// </summary>
        [Obsolete("Can not access TLS key material!")]
        public static Boolean ValidateServerCertificate(Object sender,
                                                        X509Certificate? certificate,
                                                        X509Chain? chain,
                                                        SslPolicyErrors sslPolicyErrors)
        {

            DebugX.Log("Server certificate received.");

            return true;

        }

        #endregion

        #region GetNTSKERecords_dotNET()

        [Obsolete("Can not access TLS key material!")]
        public IEnumerable<NTSKE_Record> GetNTSKERecords_dotNET()
        {

            try
            {
                using (var tcpClient = new TcpClient(Host, NTSKE_Port))
                {

                    using (var sslStream = new SslStream(
                                               tcpClient.GetStream(),
                                               leaveInnerStreamOpen: false,
                                               ValidateServerCertificate,
                                               userCertificateSelectionCallback: null
                                           ))
                    {

                        sslStream.ReadTimeout = 5000;

                        var sslOptions = new SslClientAuthenticationOptions {
                                             TargetHost                      = Host,
                                             EnabledSslProtocols             = SslProtocols.Tls13,
                                             ApplicationProtocols            = [ new SslApplicationProtocol("ntske/1") ],
                                             CertificateRevocationCheckMode  = X509RevocationMode.NoCheck
                                         };

                        sslStream.AuthenticateAsClient(sslOptions);

                        //// Angenommen, du hast den AEAD-Algorithmus aus der NTS-KE-Antwort (Record Type 4)
                        //// ermittelt – z.B. 0x000F für AES-SIV-CMAC-256.
                        //ushort chosenAead = 0x000F; // Dieser Wert sollte aus der Serverantwort entnommen werden.

                        //// Erstelle den per-association Context (5 Byte):
                        //byte[] associationContext = new byte[5];
                        //associationContext[0] = 0x00; // High Byte der Protocol ID (NTPv4: 0x0000)
                        //associationContext[1] = 0x00; // Low Byte der Protocol ID
                        //associationContext[2] = (byte)((chosenAead >> 8) & 0xFF);  // High Byte des AEAD-ID
                        //associationContext[3] = (byte)(chosenAead & 0xFF);           // Low Byte des AEAD-ID
                        //associationContext[4] = 0x00; // 0x00 für C2S, 0x01 wäre für S2C

                        //// Jetzt rufst du ExportKeyingMaterial auf. Beispielsweise benötigst du 32 Byte (für AES-SIV-CMAC-256):
                        //int keyLength = 32;
                        //byte[] c2sKey = sslStream.ExportKeyingMaterial("EXPORTER-network-time-security", associationContext, keyLength);

                        //// Der c2sKey steht nun für die Verschlüsselung der NTS-Erweiterungen in deinem NTP-Request zur Verfügung.
                        //DebugX.Log("C2S-Key abgeleitet: " + BitConverter.ToString(c2sKey));


                        var ntsKeRequest = BuildNTSKERequest();
                        sslStream.Write(ntsKeRequest, 0, ntsKeRequest.Length);
                        sslStream.Flush();

                        var buffer    = new Byte[4096];
                        var bytesRead = sslStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {

                            Array.Resize(ref buffer, bytesRead);

                            if (TryParseNTSKE_Response(buffer, out var record, out var errorResponse))
                                return record;

                        }
                        else
                        {
                            DebugX.Log($"No response received from {Host}!");
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                DebugX.Log("Exception: " + ex.Message);
            }

            return [];

        }

        #endregion


        #region BuildNTSKERequest()

        /// <summary>
        /// Erzeugt das NTS‑KE Request PDU im TLV‑Format.
        /// Record Aufbau:
        ///   - Record Type:   0x0001 (Next Protocol Negotiation)
        ///   - Record Length: 0x0002 (2 Byte für Protocol ID)
        ///   - Protocol ID:   0x0002 (NTP)
        /// </summary>
        /// <returns>Byte-Array mit dem Request</returns>
        private static Byte[] BuildNTSKERequest()
        {
            using (var ms = new MemoryStream())
            {

                // 1) NTS Next Protocol Negotiation (record type=1)
                //    - Body = 16-bit Protocol ID(s) in network order.
                //    - For NTPv4, Protocol ID = 0x0000.
                //    => length = 2, body = { 00 00 }
                byte[] nextProtocolNegotiation = [
                    0x80, 0x01, // Record-Type = 1 (NTS Next Protocol Negotiation)
                    0x00, 0x02, // Body Length = 2
                    0x00, 0x00  // Protocol ID for NTPv4 = 0
                ];

                // 2) AEAD Algorithm Negotiation (record type=4)
                //    - Body = 16-bit AEAD Algorithm ID in network order.
                //    - For AES-SIV-CMAC-256, ID = 0x000F
                //    => length = 2, body = { 00 0F }
                byte[] aeadOffer = [
                    0x80, 0x04, // Record-Type = 4 (AEAD Algorithm Negotiation)
                    0x00, 0x02, // Body Length = 2
                    0x00, 0x0F  // AEAD Algorithm ID = 15 (AES-SIV-CMAC-256)
                ];

                // 3) End of Message (record type=0)
                //    => length=0, no body
                byte[] eom = [
                    0x80, 0x00, // Record-Type = 0 (End of Message)
                    0x00, 0x00  // Body Length = 0
                ];

                ms.Write(nextProtocolNegotiation, 0, nextProtocolNegotiation.Length);
                ms.Write(aeadOffer,               0, aeadOffer.              Length);
                //ms.Write(cookieRequest,           0, cookieRequest.Length);
                ms.Write(eom,                     0, eom.                    Length);

                return ms.ToArray();

            }
        }

        #endregion

        #region TryParseNTSKE_Response(Buffer, out NTSKERecords, out ErrorResponse)

        // 8001 0002 0000
        // 8004 0002 000f
        // 0005 0064 e157c67c54f94390fbb930a259d5438e5bd89c18c0e3c5e0d18c0c4a72741e7d634d1a06ae5539805515b03aa756462ca77f479fb368d026dcebf0af426b073936506ae693f169327c5a5eba8b7f4254c9dd382aea59fa1f7dd47a681d4105316ef63153
        // 0005 0064 e157c67c932d10869d717e0b9c864d07faa7478f55e64e3bfea56448dc8f72d57172db5428bb2a4b2f7aa9d32fe3b2c31134e3113aa36c5ce0b618a9634463653960fe672c78bf5846c6f16b34cc20246fd0a11625af9085a159b07851454f0241e3828a
        // 0005 0064 e157c67c6113c3776e098e743a8aecffce82e880496daccaaf9440a494157d82be894a03c59f5cd6bfc0b93145367400e00db6d334c912184a03eecbd1db14bf1f26e7fb12556dc7ff8e0dde49972de5db2c4825f323ba668e5c36419694771446654746
        // 0005 0064 e157c67c22aedc90c00997fbeb508f6e6923460fc5130036f13d807da55910fa8d9ad7b24d4636dd822b59e5df274c380536c5d0080561cca3758eda5015422b9857b89e3cf3f075242bc25ae6725c779ede7a006617f2959380da32b2b44ff32499db59
        // 0005 0064 e157c67c8cb8afc1a79c90a0bd88e7d3ea24fa8182cdc750e37ac4f6f515302b10cdfdf972845221fe86f409ac225841c5404f360c6f680fe50f7c91bc2dde900f0741cc198d6073963316ea9400f4881c6c359cf6524ed09d98829bc862dbfda137e1fc
        // 0005 0064 e157c67cac5362c0d8e4d8f043557871eed408ac4eb361f39ff6aa5c12f11563584e8103e1351cf2a4672845fc5bed6128e2ffb54a5bc402cb3f1f7c09b69ab35ffe096072d5767722d011c8a60ca9fe1963f68c5887f163b5430af96e22aa62943fe29d
        // 0005 0064 e157c67c8869d5681d34853d66b1f41147650aaf0d33c0979b7f0aa1a99259674035913ea10585923a7f468b4a1e7d1e0c300e6e476c09e2ff93a0fa4161696b32c6f7e84e58866be6aa8a42fbad4bb1d4af15d0dd6a04c4a43a2f31bc6f633e6140e528
        // 0005 0064 e157c67c6995cfd339caeca4c4deb45f8ffdbb6a10b56f62c5e34dc2a2868e05e1376b44b22904f0f23070cabcdf6d70b4d5a2170aef53acae00edb1ee37bb50368e140593022582ea50c8149afa4a64cf1451168700ba94b8a2722c45be3f72f18ff74e
        // 8000 0000

        /// <summary>
        /// Try to parse the NTS-KE response records.
        /// </summary>
        /// <param name="Buffer">The raw NTS-KE data from the server.</param>
        /// <param name="NTSKERecords">The parsed NTS-KE records.</param>
        /// <param name="ErrorResponse">An optional error message.</param>
        public static Boolean TryParseNTSKE_Response(Byte[]                                               Buffer,
                                                     [NotNullWhen(true)]  out IEnumerable<NTSKE_Record>?  NTSKERecords,
                                                     [NotNullWhen(false)] out String?                     ErrorResponse)
        {

            ErrorResponse  = null;
            NTSKERecords   = [];

            var records    = new List<NTSKE_Record>();
            var offset     = 0;

            while (offset + 4 <= Buffer.Length)
            {

                // RFC 8915:
                // 16 bits: [CriticalBit (1) + RecordType (15)]
                // 16 bits: BodyLength (big-endian)
                // Body:    [BodyLength bytes]
                var critical    =            (Buffer[offset] & 0x80) != 0;
                var type        = (UInt16) (((Buffer[offset] & 0x7F) << 8) | Buffer[offset + 1]);
                offset += 2;

                var bodyLength  = (UInt16)  ((Buffer[offset]         << 8) | Buffer[offset + 1]);
                offset += 2;

                if (offset + bodyLength > Buffer.Length)
                {
                    ErrorResponse = "NTS-KE record claims more body bytes than available!";
                    return false;
                }

                var body = new Byte[bodyLength];
                Array.Copy(Buffer, offset, body, 0, bodyLength);
                offset += bodyLength;

                records.Add(
                    new NTSKE_Record(
                        critical,
                        type,
                        body
                    )
                );

            }

            NTSKERecords = records;
            return true;

        }

        #endregion






        /// <summary>
        /// Sends a single NTP request (mode=3) with NTS extension fields:
        ///  1) Unique Identifier extension field
        ///  2) NTS Cookie extension field (cleartext for server)
        ///  3) NTS Authenticator & Encrypted extension field (placeholder)
        /// and reads a single response.
        /// </summary>
        public async Task<NTPPacket?> QueryTime(TimeSpan?          Timeout             = null,
                                                NTSKE_Response?    NTSKEResponse       = null,
                                                CancellationToken  CancellationToken   = default)
        {

            // NTS request
            // 230008200000000000000000000000000000000000000000000000000000000000000000000000005001ac7cd6000835
            // 0104 0024 2027e75e68914d89bdd2461d6c18a87914ae432326ae452516f1af36876c37e2
            // 0204 0068 9dad3e6fcd545c8fc9a6eb945be9e2a600760641ea6e3d89c47fc692135e9ba4ca075866699e30a46b4b31f195f6d7cf8c72a4556189029c19d3c2eedda04969441c47a62004307a62c9b57cae3dc4a4af2be69757c30bd5c917e3e25564dfa3a3e283a0
            // 0404 0028 0010 0010 768f82009746999ea26472c70d9e4906 3b474cf41d387f62e78ae20224c53209

            // NTS response
            // 240308e7000001a00000003974cb60e3eb51b89a96d03cb65001ac7cd6000835eb51b99eb19a6fd1eb51b99eb19e575e
            // 0104 0024 2027e75e68914d89bdd2461d6c18a87914ae432326ae452516f1af36876c37e2
            // 0404 0090 0010 0078 c562375b4cf5e6338cecf184f1c9b739ecc6daa3e27bbda9935a184f9089bc5ad6060a80afd71b5dcd421b332f4f26fdb53d9a1d092662595944696573fea2c1ae33761b04f5b399f504779bf4745caab96ac43c10595f0abe61aedbb6471b806e737cba62035e8bfd44279ed869996102168d9c68edf37cba02d3db49ca6aaf28923d67bb43e0ba

            var uniqueId = new Byte[32];
            RandomNumberGenerator.Fill(uniqueId);

            var requestPacket = BuildNTPRequest(NTSKEResponse, uniqueId);
            var requestData   = requestPacket.ToByteArray();

            using (var udpClient = new UdpClient())
            {

                try
                {

                    await udpClient.SendAsync(
                              requestData,
                              Host,
                              NTP_Port,
                              CancellationToken
                          );

                    var timeout       = Timeout ?? this.Timeout ?? DefaultTimeout;
                    var receiveTask   = udpClient.ReceiveAsync(CancellationToken).AsTask();
                    var timeoutTask   = Task.Delay(timeout, CancellationToken);
                    var finishedTask  = await Task.WhenAny(receiveTask, timeoutTask);

                    if (finishedTask == timeoutTask)
                    {
                        DebugX.Log($"No NTP response within {Math.Round(timeout.TotalSeconds, 2)} seconds timeout!");
                        return null;
                    }

                    var receiveResult = await receiveTask;

                    DebugX.Log($"Got {receiveResult.Buffer.Length}-byte response from {receiveResult.RemoteEndPoint}");

                    if (NTPPacket.TryParse(receiveResult.Buffer, out var ntpResponse, out var errorResponse))
                    {

                        var uid = ntpResponse.Extensions.FirstOrDefault(e => e.Type == ExtensionTypes.UniqueIdentifier)?.Value ?? [];

                        if (!requestPacket.Extensions.Any(extension => extension.Type == ExtensionTypes.UniqueIdentifier) ||
                            uniqueId.ToHexString() == uid.ToHexString())
                            //ToDo: Validate S2C AEAD data
                            DebugX.Log("Serverzeit (UTC): " + NTPPacket.NTPTimestampToDateTime(ntpResponse.TransmitTimestamp.Value).ToString("o"));

                        else
                            DebugX.Log("UniqueId mismatch!");

                        return ntpResponse;

                    }

                }
                catch (Exception e)
                {
                    DebugX.Log("NTP receive exception: " + e.Message);
                }

            }

            return null;

        }


        #region BuildNTSRequest(NTSKEResponse = null, UniqueId = null)

        /// <summary>
        /// Builds an NTP mode=3 request with minimal NTS EFs:
        ///  1) Unique ID (0104)
        ///  2) NTS Cookie (0204)
        ///  3) NTS Auth & Encrypted (0404) - with placeholder AEAD data
        /// </summary>
        private static NTPPacket BuildNTPRequest(NTSKE_Response?  NTSKEResponse   = null,
                                                 Byte[]?          UniqueId        = null)
        {

            var ntpPacket1 = new NTPPacket(
                                 TransmitTimestamp: NTPPacket.GetCurrentNTPTimestamp()
                             );

            var extensions = new List<NTPExtension>();

            if (NTSKEResponse is not null &&
                NTSKEResponse.Cookies.Any() &&
                NTSKEResponse.C2SKey.Length > 0)
            {

                var uniqueIdExtension  = NTPExtension.UniqueIdentifier(UniqueId);
                var cookieExtension    = NTPExtension.NTSCookie(NTSKEResponse.Cookies.First());

                extensions.Add(
                    uniqueIdExtension
                );

                extensions.Add(
                    NTPExtension.NTSCookie(NTSKEResponse.Cookies.First())
                );

                // Basically this extension validates all data (NTP header + extensions) which came before it!
                extensions.Add(
                    CreateNTSAuthenticatorExtension(
                        NTSKEResponse,
                        [
                            ntpPacket1.       ToByteArray(),
                            uniqueIdExtension.ToByteArray(),
                            cookieExtension.  ToByteArray()
                        ]
                    )
                );

                var xx = TryValidateNTSAuthenticatorExtension(
                             extensions.Last().Value,
                             [
                                 ntpPacket1.       ToByteArray(),
                                 uniqueIdExtension.ToByteArray(),
                                 cookieExtension.  ToByteArray()
                             ],
                             NTSKEResponse.C2SKey,
                             [],
                             out var err
                         );

            }

            var ntpPacket = new NTPPacket(
                                ntpPacket1,
                                Extensions: extensions
                            );

            return ntpPacket;

        }

        #endregion

        #region GetCurrentNTPTimestamp(Timestamp = null)

        /// <summary>
        /// Converts DateTime.UtcNow to a 64-bit NTP time format (seconds since 1900).
        /// The upper 32 bits contain the seconds, the lower 32 bits the fraction of a second as 32-bit fixed-point (2^32 is 1 second).
        /// </summary>
        /// <param name="Timestamp">An optional timestamp (UTC) to be converted to a NTP timestamp.</param>
        public static UInt64 GetCurrentNTPTimestamp(DateTime? Timestamp = null)
        {

            var ntpEpoch  = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var now       = Timestamp ?? DateTime.UtcNow;
            var ts        = now - ntpEpoch;

            var seconds   = (UInt64) ts.TotalSeconds;
            var fraction  = (UInt64) ((ts.TotalSeconds - seconds) * 0x100000000L);

            return (seconds << 32) | fraction;

        }

        #endregion


        #region CreateUniqueIdExtension(UniqueId = null)

        ///// <summary>
        ///// Creates a Unique Identifier Extension (type=0x0104).
        ///// Must be at least 32 random bytes in the body.
        ///// </summary>
        ///// <param name="UniqueId">An optional 32-byte unique identification.</param>
        //private static NTPExtension CreateUniqueIdExtension(Byte[]? UniqueId = null)
        //{

        //    UniqueId ??= new Byte[32];
        //    RandomNumberGenerator.Fill(UniqueId);

        //    return NTPExtension.UniqueIdentifier(UniqueId);

        //}

        #endregion

        #region CreateNTSAuthenticatorExtension(NTSKEResponse, AssociatedData, Plainttext = null)

        /// <summary>
        /// Create a "NTS Authenticator and Encrypted Extension Fields" extension (type=0x0404)
        /// 
        /// In a real implementation:
        /// 1. The associated data (A) is computed as: [NTP header || UniqueId EF || Cookie EF].
        /// 2. The plaintext (P) is set to the internal extension fields that need confidentiality (here we use an empty plaintext).
        /// 3. A random nonce is not needed separately since AES-SIV computes a synthetic IV (SIV) deterministically.
        /// 4. The AEAD encryption is run with the C2S key (extracted from the TLS session during NTS-KE).
        /// 5. The resulting output is SIV || C. Here, with an empty plaintext, only SIV (16 bytes) is produced.
        /// 6. The extension field value is then built as:
        ///    [NonceLength (2 bytes) || CiphertextLength (2 bytes) || padded(Nonce) || padded(Ciphertext)]
        ///    where each of nonce and ciphertext is padded to a 4-byte boundary.
        /// 7. Finally, an NTPExtension (with 4-byte header containing type and total length) is returned.
        /// </summary>
        /// <param name="NTSKEResponse">A Network Time Security Key Establishment (NTS-KE) response containing the C2S key.</param>
        /// <param name="AssociatedData">An array of byte arrays to be authenticated but not encrypted.</param>
        /// <param name="Plainttext">The optional plaintext to be encrypted (e.g. internal extension fields).</param>
        private static NTPExtension CreateNTSAuthenticatorExtension(NTSKE_Response  NTSKEResponse,
                                                                    IList<Byte[]>   AssociatedData,
                                                                    Byte[]?         Plainttext   = null)
        {

            var plaintext            = Plainttext ?? [];
            var c2sKey               = NTSKEResponse.C2SKey;
            var aesSiv               = new AES_SIV(c2sKey); // AES-SIV-CMAC-256
            var sivAndCiphertext     = aesSiv.Encrypt(AssociatedData, plaintext);

            // We consider the SIV as the "nonce"...
            var nonceLen             = 16;
            var ciphertextLen        = Math.Max(sivAndCiphertext.Length - 16, 16);
            var paddedNonceLen       = (nonceLen      + 3) & ~3;
            var paddedCiphertextLen  = (ciphertextLen + 3) & ~3;
            var value                = new Byte[4 + paddedNonceLen + paddedCiphertextLen];

            value[0] = (Byte) ((nonceLen      >> 8) & 0xff);
            value[1] = (Byte)  (nonceLen            & 0xff);

            value[2] = (Byte) ((ciphertextLen >> 8) & 0xff);
            value[3] = (Byte)  (ciphertextLen       & 0xff);

            Buffer.BlockCopy(sivAndCiphertext, 0, value, 4,                  nonceLen);
            Buffer.BlockCopy(sivAndCiphertext, 0, value, 4 + paddedNonceLen, nonceLen);

            if (sivAndCiphertext.Length > nonceLen)
                Buffer.BlockCopy(sivAndCiphertext, nonceLen, value, 4 + paddedNonceLen, ciphertextLen);

            return NTPExtension.AuthenticatorAndEncrypted(value);

        }

        #endregion

        #region TryValidateNTSAuthenticatorExtension(ReceivedValue, AssociatedData, C2SKey, ExpectedPlaintext, out ErrorResponse)

        /// <summary>
        /// Validates the NTS Authenticator and Encrypted Extension Field received from an NTP request.
        /// The extension value should have the format:
        /// [NonceLength (2 bytes) || CiphertextLength (2 bytes) || padded(Nonce) || padded(Ciphertext)]
        /// where each of nonce and ciphertext is padded to a 4-byte boundary.
        /// The validation is performed by re-computing the AEAD encryption using the provided C2S key,
        /// the expected associated data (e.g. NTP header || UniqueId EF || Cookie EF)
        /// and the expected plaintext.
        /// </summary>
        /// <param name="ReceivedValue">
        /// The raw value bytes of the authenticator extension (excluding the 4-byte NTPExtension header).
        /// </param>
        /// <param name="AssociatedData">
        /// The associated data as a list of byte arrays (for example: [NTP header, UniqueId extension, Cookie extension]).
        /// </param>
        /// <param name="C2SKey">The client-to-server key derived from the TLS session (e.g. 32 bytes for AES-SIV).</param>
        /// <param name="ExpectedPlaintext">
        /// The plaintext that was encrypted (for example, in testing it might be "Hello world!" as UTF8 bytes).
        /// In a real implementation, this would be the concatenation of confidential internal extension fields.
        /// </param>
        public static Boolean TryValidateNTSAuthenticatorExtension(Byte[]         ReceivedValue,
                                                                   IList<Byte[]>  AssociatedData,
                                                                   Byte[]         C2SKey,
                                                                   Byte[]         ExpectedPlaintext,
                                                                   out String?    ErrorResponse)
        {

            ErrorResponse = null;

            if (ReceivedValue == null || ReceivedValue.Length < 4)
            {
                ErrorResponse = "NTS Authenticator and Encrypted extension value is null or too short!";
                return false;
            }

            var nonceLen                  = (UInt16) ((ReceivedValue[0] << 8) | ReceivedValue[1]);
            var ciphertextLen             = (UInt16) ((ReceivedValue[2] << 8) | ReceivedValue[3]);

            var paddedNonceLen            = ((nonceLen      + 3) / 4) * 4;
            var paddedCiphertextLen       = ((ciphertextLen + 3) / 4) * 4;

            // Verify that the total length of the received value matches expectations:
            var expectedTotalValueLength  = 4 + paddedNonceLen + paddedCiphertextLen;
            if (ReceivedValue.Length != expectedTotalValueLength)
            {
                ErrorResponse = "NTS Authenticator and Encrypted extension value has unexpected length!";
                return false;
            }

            var receivedNonce             = new Byte[nonceLen];
            Buffer.BlockCopy(ReceivedValue, 4, receivedNonce, 0, nonceLen);

            var receivedCiphertext        = new Byte[ciphertextLen];
            if (ciphertextLen > 0)
                Buffer.BlockCopy(ReceivedValue, 4 + paddedNonceLen, receivedCiphertext, 0, ciphertextLen);

            // Recompute the AEAD output using AES-SIV.
            // Our AesSiv class expects an IList<byte[]> as associated data.
            var aesSiv                    = new AES_SIV(C2SKey);
            var computedOutput            = aesSiv.Encrypt(AssociatedData, ExpectedPlaintext);

            // computedOutput should be SIV || Ciphertext.
            // Let’s assume that our implementation produces a computedOutput of length = (nonceLen + ciphertextLen)
            // (e.g. if plaintext is non-empty, computedOutput includes both parts).
            if (computedOutput.Length < nonceLen)
            {
                ErrorResponse = "Computed AEAD output is too short!";
                return false;
            }

            var computedNonce             = new Byte[nonceLen];
            Buffer.BlockCopy(computedOutput, 0, computedNonce, 0, nonceLen);

            var computedCiphertextLen     = Math.Max(computedOutput.Length - nonceLen, 16);
            var computedCiphertext        = new Byte[computedCiphertextLen];
            if (computedOutput.Length > nonceLen)
                Buffer.BlockCopy(computedOutput, nonceLen, computedCiphertext, 0, computedCiphertextLen);

            var nonceMatch                = AreEqual(receivedNonce,      computedNonce);
            var ciphertextMatch           = AreEqual(receivedCiphertext, computedCiphertext);

            return nonceMatch && ciphertextMatch;

        }

        #endregion


        #region (private static) AreEqual(a, b)

        /// <summary>
        /// Compares two byte arrays for equality.
        /// </summary>
        private static Boolean AreEqual(Byte[] a, Byte[] b)
        {

            if (a == null || b == null || a.Length != b.Length)
                return false;

            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;

        }

        #endregion

        #region (private static) Concat(params arrays)

        /// <summary>
        /// Helper: Concatenate multiple byte arrays.
        /// </summary>
        private static byte[] Concat(params byte[][] arrays)
        {

            var totalLen = 0;

            foreach (var arr in arrays)
                totalLen += arr.Length;

            var result = new Byte[totalLen];
            int offset = 0;

            foreach (var arr in arrays)
            {
                Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            }

            return result;

        }

        #endregion


    }

}
