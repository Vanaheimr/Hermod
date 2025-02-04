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

using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{


    public class NTSClient
    {

        #region Properties

        public String  Host          { get; }
        public UInt16  NTSKE_Port    { get; } = 4460;
        public UInt16  NTP_Port      { get; } = 123;

        #endregion

        #region NTSClient(Host)

        public NTSClient(String Host)
        {
            this.Host = Host;
        }

        #endregion


        #region ValidateServerCertificate(...)

        /// <summary>
        /// Certificate validation callback.
        /// In this demo, all certificates are accepted.
        /// In production, validate the certificate properly.
        /// </summary>
        public static Boolean ValidateServerCertificate(Object            sender,
                                                        X509Certificate?  certificate,
                                                        X509Chain?        chain,
                                                        SslPolicyErrors   sslPolicyErrors)
        {

            DebugX.Log("Server certificate received.");

            return true;

        }

        #endregion


        #region GetCookies()

        public IEnumerable<NTSKERecord> GetCookies()
        {

            try
            {
                using (var tcpClient = new TcpClient(Host, NTSKE_Port))
                {

                    //tcpClient.ReceiveTimeout = 5000;

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

                        var ntsKeRequest = BuildNtsKeRequest();
                        sslStream.Write(ntsKeRequest, 0, ntsKeRequest.Length);
                        sslStream.Flush();

                        var buffer    = new Byte[4096];
                        var bytesRead = sslStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            Array.Resize(ref buffer, bytesRead);
                            return ParseNtsKeResponse(buffer, (UInt32) bytesRead);
                        }
                        else
                        {
                            DebugX.Log("No response received.");
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


        #region BuildNtsKeRequest()

        /// <summary>
        /// Erzeugt das NTS‑KE Request PDU im TLV‑Format.
        /// Record Aufbau:
        ///   - Record Type: 0x0001 (Next Protocol Negotiation)
        ///   - Record Length: 0x0002 (2 Byte für Protocol ID)
        ///   - Protocol ID: 0x0002 (NTP)
        /// </summary>
        /// <returns>Byte-Array mit dem Request</returns>
        private static byte[] BuildNtsKeRequest()
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

                //// Record #1: Cookie Request (Record-Type = 0x0002)
                ////  - Length = 2
                ////  - Data: 0x0004 (Anzahl angeforderter Cookies = 4)
                //byte[] cookieRequest = [
                //    0x00, 0x02,  // Record-Type = 2
                //    0x00, 0x02,  // Length = 2
                //    0x00, 0x04   // Fordere 4 Cookies
                //];

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

        #region ParseNtsKeResponse()

        // 80010002000080040002000f00050064e157c67c54f94390fbb930a259d5438e5bd89c18c0e3c5e0d18c0c4a72741e7d634d1a06ae5539805515b03aa756462ca77f479fb368d026dcebf0af426b073936506ae693f169327c5a5eba8b7f4254c9dd382aea59fa1f7dd47a681d4105316ef6315300050064e157c67c932d10869d717e0b9c864d07faa7478f55e64e3bfea56448dc8f72d57172db5428bb2a4b2f7aa9d32fe3b2c31134e3113aa36c5ce0b618a9634463653960fe672c78bf5846c6f16b34cc20246fd0a11625af9085a159b07851454f0241e3828a00050064e157c67c6113c3776e098e743a8aecffce82e880496daccaaf9440a494157d82be894a03c59f5cd6bfc0b93145367400e00db6d334c912184a03eecbd1db14bf1f26e7fb12556dc7ff8e0dde49972de5db2c4825f323ba668e5c3641969477144665474600050064e157c67c22aedc90c00997fbeb508f6e6923460fc5130036f13d807da55910fa8d9ad7b24d4636dd822b59e5df274c380536c5d0080561cca3758eda5015422b9857b89e3cf3f075242bc25ae6725c779ede7a006617f2959380da32b2b44ff32499db5900050064e157c67c8cb8afc1a79c90a0bd88e7d3ea24fa8182cdc750e37ac4f6f515302b10cdfdf972845221fe86f409ac225841c5404f360c6f680fe50f7c91bc2dde900f0741cc198d6073963316ea9400f4881c6c359cf6524ed09d98829bc862dbfda137e1fc00050064e157c67cac5362c0d8e4d8f043557871eed408ac4eb361f39ff6aa5c12f11563584e8103e1351cf2a4672845fc5bed6128e2ffb54a5bc402cb3f1f7c09b69ab35ffe096072d5767722d011c8a60ca9fe1963f68c5887f163b5430af96e22aa62943fe29d00050064e157c67c8869d5681d34853d66b1f41147650aaf0d33c0979b7f0aa1a99259674035913ea10585923a7f468b4a1e7d1e0c300e6e476c09e2ff93a0fa4161696b32c6f7e84e58866be6aa8a42fbad4bb1d4af15d0dd6a04c4a43a2f31bc6f633e6140e52800050064e157c67c6995cfd339caeca4c4deb45f8ffdbb6a10b56f62c5e34dc2a2868e05e1376b44b22904f0f23070cabcdf6d70b4d5a2170aef53acae00edb1ee37bb50368e140593022582ea50c8149afa4a64cf1451168700ba94b8a2722c45be3f72f18ff74e80000000

        // 8001 0002 0000
        // 8004 0002 000f
        // 0005 0064 e157 c67c 28 4c 75 81 ... [100 bytes total] ...
        // 0005 0064 e157 c67c bb 52 54 94 ... [100 bytes total] ...
        // 0005 0064 e157 c67c a7 cb b9 a3 ... [100 bytes total] ...
        // 0005 0064 e157 c67c 93 9c 9c 97 ... [100 bytes total] ...
        // 0005 0064 e157 c67c 62 ae e6 bb ... [100 bytes total] ...
        // 0005 0064 e157 c67c 60 7e 78 dd ... [100 bytes total] ...
        // 0005 0064 e157 c67c 0b dd 68 55 ... [100 bytes total] ...
        // 0005 0064 e157 c67c 93 46 be 89 ... [100 bytes total] ...
        // 8000 0000




        /// <summary>
        /// Parses the NTS-KE response data into a list of records.
        /// RFC 8915 defines the record format as:
        ///
        ///   16 bits: [CriticalBit (1) + RecordType (15)]
        ///   16 bits: BodyLength (big-endian)
        ///   Body:    [BodyLength bytes]
        ///
        /// Returns a list of NtsKeRecord objects.
        /// </summary>
        /// <param name="Buffer">The raw NTS-KE data from the server.</param>
        /// <param name="Length">Number of valid bytes in 'buffer'.</param>
        /// <returns>List of parsed records.</returns>
        public static List<NTSKERecord> ParseNtsKeResponse(Byte[]  Buffer,
                                                           UInt32  Length)
        {

            var records = new List<NTSKERecord>();
            var offset  = 0;

            while (offset + 4 <= Length)
            {

                // Read the 16-bit record-type field (big-endian)
                // The first byte has the critical bit (most significant bit).
                var hi          = Buffer[offset];
                var lo          = Buffer[offset + 1];

                // Extract the "critical" bit (bit 7 in 'hi'), then the lower 15 bits
                var critical    = (hi & 0x80) != 0;   // top bit
                var type        = (UInt16) (((hi & 0x7F) << 8) | lo);

                offset += 2;

                // Read the 16-bit "bodyLength" in big-endian
                var lenHi       = Buffer[offset];
                var lenLo       = Buffer[offset + 1];
                var bodyLength  = (UInt16) ((lenHi << 8) | lenLo);

                offset += 2;

                // Ensure we have enough bytes left for the body
                if (offset + bodyLength > Length)
                {
                    // Truncated buffer
                    throw new Exception("NTS-KE record claims more body bytes than available!");
                }

                var body = new Byte[bodyLength];
                Array.Copy(Buffer, offset, body, 0, bodyLength);
                offset += bodyLength;

                records.Add(
                    new NTSKERecord(
                        critical,
                        type,
                        body
                    )
                );

            }

            return records;

        }

        #endregion






        /// <summary>
        /// Sends a single NTP request (mode=3) with NTS extension fields:
        ///  1) Unique Identifier extension field
        ///  2) NTS Cookie extension field (cleartext for server)
        ///  3) NTS Authenticator & Encrypted extension field (placeholder)
        /// and reads a single response.
        /// </summary>
        public Tuple<NTPHeader, IEnumerable<NTPExtensionField>> QueryTime(Byte[] NTSCookie, Byte[] c2sKey)
        {

            var requestPacket = BuildNTSRequest(NTSCookie, c2sKey);

            using (var udpClient = new UdpClient())
            {

                udpClient.Client.ReceiveTimeout = 5000;
                udpClient.Connect(Host, NTP_Port);
                udpClient.Send(requestPacket, requestPacket.Length);

                try
                {

                    IPEndPoint? remote = null;
                    var response = udpClient.Receive(ref remote);

                    DebugX.Log($"Got {response.Length}-byte response from {remote}");

                    // Parse response for time offset, etc...
                    // Also parse extension fields and verify authenticity with S2C key
                    if (NTPHeader.TryParseNTPHeader(response, out var ntpHeader, out var errorResponse))
                    {

                        var fields = new List<NTPExtensionField>();
                        var offset = 48; // Start of extension fields

                        // Solange noch mindestens 4 Byte für ein weiteres Extension Field vorhanden sind...
                        while (offset + 4 <= response.Length)
                        {

                            var type   = (UInt16) ((response[offset]     << 8) | response[offset + 1]);
                            var length = (UInt16) ((response[offset + 2] << 8) | response[offset + 3]);

                            if (length < 4)
                                throw new Exception($"Illegal length of extension {length} at offset {offset}!");

                            if (offset + length > response.Length)
                                break;

                            var data = new Byte[length - 4];
                            Array.Copy(response, offset + 4, data, 0, length - 4);

                            fields.Add(
                                new NTPExtensionField(
                                    type,
                                    length,
                                    data
                                )
                            );

                            offset += length;

                        }

                        var serverTimeUtc = NTPTimestampToDateTime(ntpHeader.Value.TransmitTimestamp);
                        DebugX.Log("Serverzeit (UTC): " + serverTimeUtc.ToString("o"));

                        return new Tuple<NTPHeader, IEnumerable<NTPExtensionField>>(ntpHeader.Value, fields);

                    }

                }
                catch (SocketException ex)
                {
                    DebugX.Log($"No response within timeout. {ex.Message}");
                }

            }

            return new Tuple<NTPHeader, IEnumerable<NTPExtensionField>>(default, []);

        }

        

        /// <summary>
        /// Konvertiert einen 64-Bit NTP-Zeitstempel in ein DateTime (UTC).
        /// Die oberen 32 Bit sind die Sekunden seit 1.1.1900,
        /// die unteren 32 Bit sind der Bruchteil einer Sekunde.
        /// </summary>
        public static DateTime NTPTimestampToDateTime(ulong ntpTimestamp)
        {
            // Extrahiere die oberen 32 Bit (Sekunden)
            uint seconds = (uint)(ntpTimestamp >> 32);

            // Extrahiere die unteren 32 Bit (Bruchteil)
            uint fraction = (uint)(ntpTimestamp & 0xFFFFFFFF);

            // Berechne den Bruchteil als Double (Sekunden)
            double fractionSeconds = fraction / (double)0x100000000L; // 2^32

            // NTP-Epoch: 1. Januar 1900 UTC
            DateTime ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Addiere Sekunden + Bruchteile
            DateTime result = ntpEpoch.AddSeconds(seconds + fractionSeconds);
            return result;
        }





        #region BuildNTSRequest(Cookie, c2sKey)

        /// <summary>
        /// Builds an NTP mode=3 request with minimal NTS EFs:
        ///  1) Unique ID (0104)
        ///  2) NTS Cookie (0204)
        ///  3) NTS Auth & Encrypted (0404) - with placeholder AEAD data
        /// </summary>
        private static Byte[] BuildNTSRequest(Byte[] NTSCookie, Byte[] c2sKey)
        {

            var packet       = new Byte[48];

            // [LI(2 bits)=0, Version(3 bits)=4, Mode(3 bits)=3 (client), ...] => 0x23
            packet[0]        = 0x23;

            // 3. Byte:          Poll (z.B. 4 → 16 sec interval)
            packet[2]        = 4;

            // 4. Byte:          Precision (z.B. -6; in 8-bit Zweierkomplement entspricht das 0xFA)
            packet[3]        = 0xFA;

            // 11. Bytes 40-47:  Transmit Timestamp
            var ntpTimestamp = GetCurrentNtpTimestamp();

            // Write as big-endian...
            for (var i = 0; i < 8; i++)
                packet[40 + i] = (Byte) (ntpTimestamp >> (56 - i * 8));


            return packet;

            var uniqueIdExt  = CreateUniqueIdExtension();
            var cookieExt    = CreateCookieExtension(NTSCookie);
            var authExt      = CreateNTSAuthenticatorExtension(c2sKey, packet, uniqueIdExt, cookieExt);

            // Combine header + uniqueIdExt + cookieExt + authExt
            var final = new Byte[48 + uniqueIdExt.Length + cookieExt.Length + authExt.Length];
            Buffer.BlockCopy(packet, 0, final, 0, 48);

            var offset = 48;
            Buffer.BlockCopy(uniqueIdExt, 0, final, offset, uniqueIdExt.Length);
            offset += uniqueIdExt.Length;

            Buffer.BlockCopy(cookieExt,   0, final, offset, cookieExt.  Length);
            offset += cookieExt.Length;

            Buffer.BlockCopy(authExt,     0, final, offset, authExt.    Length);

            return final;

        }

        #endregion

        #region GetCurrentNtpTimestamp()

        /// <summary>
        /// Konvertiert DateTime.UtcNow in ein 64-Bit NTP-Zeitformat (Sekunden seit 1900).
        /// Die oberen 32 Bit enthalten die Sekunden, die unteren 32 Bit den Bruchteil einer Sekunde.
        /// </summary>
        private static UInt64 GetCurrentNtpTimestamp()
        {

            // NTP-Zeit beginnt am 1. Januar 1900
            var ntpEpoch  = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var now       = DateTime.UtcNow;
            var ts        = now - ntpEpoch;

            // Ganze Sekunden
            var seconds   = (UInt64) ts.TotalSeconds;
            // Bruchteile als 32-Bit-Festkomma (2^32 entspricht 1 Sekunde)
            var fraction  = (UInt64) ((ts.TotalSeconds - seconds) * 0x100000000L);

            return (seconds << 32) | fraction;

        }

        #endregion

        #region CreateUniqueIdExtension()

        /// <summary>
        /// Creates a Unique Identifier Extension Field (type=0x0104).
        /// Must be at least 32 random bytes in the body. The EF length is (body size + 4).
        /// Also must be multiple of 4 overall.
        /// </summary>
        private static Byte[] CreateUniqueIdExtension()
        {

            var uniqueId = new Byte[32];
            RandomNumberGenerator.Fill(uniqueId);

            // 4 = type+length
            var totalLen = 4 + uniqueId.Length;

            // Must be multiple of 4, so if needed, pad up (rarely needed for 32).
            //while ((totalLen % 4) != 0)
            //    totalLen++;

            var data = new Byte[totalLen];

            // EF Type = 0x0104 (Unique ID). For network byte order:
            // We'll do big-endian: [0x01, 0x04]
            data[0] = 0x01;
            data[1] = 0x04;
            // EF Length (2 bytes, big-endian) = totalLen
            // BUT recall that for an NTP extension field, "Length" includes
            // the entire EF, including the 4-byte header. So:
            data[2] = (Byte) ((totalLen >> 8) & 0xFF);
            data[3] = (Byte)  (totalLen       & 0xFF);

            Buffer.BlockCopy(uniqueId, 0, data, 4, uniqueId.Length);

            return data;

        }

        #endregion

        #region CreateCookieExtension(NTSCookie)

        /// <summary>
        /// Creates the NTS Cookie Extension Field (type=0x0204) for the given cookie.
        /// This is stored in the clear (unencrypted) in client->server direction.
        /// </summary>
        private static Byte[] CreateCookieExtension(Byte[] NTSCookie)
        {

            // total length = 4 (header) + cookie.Length
            var totalLen = 4 + NTSCookie.Length;

            // pad to multiple of 4
            while ((totalLen % 4) != 0)
                totalLen++;

            var data = new Byte[totalLen];

            // EF Type = 0x0204
            data[0] = 0x02;
            data[1] = 0x04;
            // EF length includes entire extension
            data[2] = (Byte) ((totalLen >> 8) & 0xFF);
            data[3] = (Byte)  (totalLen       & 0xFF);

            Buffer.BlockCopy(NTSCookie, 0, data, 4, NTSCookie.Length);

            return data;

        }

        #endregion

        #region CreateNtsAuthenticatorExtension(...)

        /// <summary>
        /// Creates the "NTS Authenticator and Encrypted Extension Fields" extension (type=0x0404),
        /// which holds AEAD nonce, ciphertext, etc.
        ///
        /// In a real implementation:
        ///   - You compute "associated data" as everything up to this EF (the header + unique ID EF + cookie EF).
        ///   - You set plaintext if you want to encrypt more extension fields.
        ///   - You run AES-SIV (or whichever AEAD) with c2sKey.
        ///   - Fill NonceLength(2 bytes), CiphertextLength(2 bytes), Nonce[], Ciphertext[], plus any zero padding.
        ///
        /// Here we only show a minimal placeholder to demonstrate the correct structure.
        /// </summary>
        private static byte[] CreateNTSAuthenticatorExtension(Byte[] c2sKey,
                                                              Byte[] ntpHeader,
                                                              Byte[] uniqueIdExt,
                                                              Byte[] cookieExt)
        {

            // In real code, you'd do something like:
            //  1) Construct Associated Data (A) = [NTP header + UniqueId EF + Cookie EF]
            //  2) Construct your plaintext (P) if you want to encrypt any internal extension fields
            //  3) Generate a random nonce
            //  4) Run AES-SIV using c2sKey, to produce ciphertext + authentication tag
            // For now, let's create a dummy 16-byte "nonce" and 16-byte "ciphertext" to show the format.

            var dummyNonce           = new Byte[16];
            RandomNumberGenerator.Fill(dummyNonce);

            var dummyCiphertext      = new Byte[16];
            // In real code, you'd fill this with AEAD output from the encryption of some plaintext.

            // The EF's internal structure is:
            //   2 bytes: Nonce Length
            //   2 bytes: Ciphertext Length
            //   Nonce (nonce length, pad up to multiple-of-4)
            //   Ciphertext (ciphertext length, pad up to multiple-of-4)
            //   Possibly additional padding so that the entire EF is multiple of 4
            //
            // For simplicity, let's do no extra padding other than the 4 boundary for each part.
            // So, NonceLength = 16, CiphertextLength=16.

            var nonceLen             = (ushort) dummyNonce.Length;
            var ciphertextLen        = (ushort) dummyCiphertext.Length;

            // We'll compute how many bytes for the Nonce block, round up to nearest 4
            var paddedNonceLen       = ((nonceLen + 3) / 4) * 4;
            var paddedCiphertextLen  = ((ciphertextLen + 3) / 4) * 4;

            // So the total EF size = 4 (EF header) + 4 (nonce/ciphertext length fields)
            //   + paddedNonceLen + paddedCiphertextLen
            var bodySize             = 4 + paddedNonceLen + paddedCiphertextLen;
            var totalLen             = 4 + bodySize; // 4 for EF type+length

            var data                 = new Byte[totalLen];

            // 1) EF type=0x0404
            data[0] = 0x04;
            data[1] = 0x04;
            // 2) EF length (entire extension, includes 4 header bytes) 
            data[2] = (byte)((totalLen >> 8) & 0xFF);
            data[3] = (byte)(totalLen & 0xFF);

            // Next 2 bytes = NonceLength (big-endian)
            data[4] = (byte)((nonceLen >> 8) & 0xFF);
            data[5] = (byte)(nonceLen & 0xFF);
            // Next 2 bytes = CiphertextLength (big-endian)
            data[6] = (byte)((ciphertextLen >> 8) & 0xFF);
            data[7] = (byte)(ciphertextLen & 0xFF);

            // Copy the Nonce (16 bytes) at offset=8
            Buffer.BlockCopy(dummyNonce, 0, data, 8, nonceLen);
            var offset = 8 + paddedNonceLen;

            // Copy the ciphertext (16 bytes) next
            Buffer.BlockCopy(dummyCiphertext, 0, data, offset, ciphertextLen);
            offset += paddedCiphertextLen;

            // Any leftover is 0 padding, if needed.

            return data;

        }

        #endregion

    }

}
