/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public delegate Task OnSendEMailRequestDelegate (DateTime           LogTimestamp,
                                                     ISMTPClient        Sender,
                                                     EventTracking_Id   EventTrackingId,
                                                     EMailEnvelop       EMailEnvelop,
                                                     TimeSpan?          RequestTimeout);

    public delegate Task OnSendEMailResponseDelegate(DateTime           LogTimestamp,
                                                     ISMTPClient        Sender,
                                                     EventTracking_Id   EventTrackingId,
                                                     EMailEnvelop       EMailEnvelop,
                                                     TimeSpan?          RequestTimeout,
                                                     MailSentStatus     Result,
                                                     TimeSpan           Runtime);


    public static class ISMTPClientExtensions
    {

        #region CRAM_MD5(Token, Login, Password)

        public static Byte[] CRAM_MD5(String Token, String Login, String Password)
        {

            var HMAC_MD5 = new HMACMD5(Password.ToUTF8Bytes());
            var digest   = HMAC_MD5.ComputeHash(Token.ToUTF8Bytes());

            // result := login[space]digest
            return Login.ToUTF8Bytes().
                   Concat(new Byte[1] { 0x20 }).
                   Concat(digest.ToHexString().ToUTF8Bytes()).
                   ToArray();

        }

        #endregion

        #region CRAM_MD5_(Token, Login, Password)

        public static Byte[] CRAM_MD5_(String Token, String Login, String Password)
        {

            var token       = Token.   ToUTF8Bytes();
            var password    = Password.ToUTF8Bytes();
            var ipad        = new Byte[64];
            var opad        = new Byte[64];
            var startIndex  = 0;
            var length      = token.Length;

            // see also: http://tools.ietf.org/html/rfc2195 - 2. Challenge-Response Authentication Mechanism (CRAM)
            //           http://tools.ietf.org/html/rfc2104 - 2. Definition of HMAC

            #region Copy the password into inner/outer padding and XOR it accordingly

            if (password.Length > ipad.Length)
            {
                var HashedPassword = new MD5CryptoServiceProvider().ComputeHash(password);
                Array.Copy(HashedPassword, ipad, HashedPassword.Length);
                Array.Copy(HashedPassword, opad, HashedPassword.Length);
            }
            else
            {
                Array.Copy(password, ipad, password.Length);
                Array.Copy(password, opad, password.Length);
            }

            for (var i = 0; i < ipad.Length; i++) {
                ipad[i] ^= 0x36;
                opad[i] ^= 0x5c;
            }

            #endregion

            #region Calculate the inner padding

            byte[] digest;

            using (var MD5 = new MD5CryptoServiceProvider())
            {
                MD5.TransformBlock     (ipad, 0, ipad.Length, null, 0);
                MD5.TransformFinalBlock(token, startIndex, length);
                digest = MD5.Hash;
            }

            #endregion

            #region Calculate the outer padding

            // oPAD (will use iPAD digest!)
            using (var MD5 = new MD5CryptoServiceProvider())
            {
                MD5.TransformBlock     (opad, 0, opad.Length, null, 0);
                MD5.TransformFinalBlock(digest, 0, digest.Length);
                digest = MD5.Hash;
            }

            #endregion


            // result := login[space]digest
            return Login.ToUTF8Bytes().
                   Concat(new Byte[1] { 0x20 }).
                   Concat(digest.ToHexString().ToUTF8Bytes()).
                   ToArray();

        }

        #endregion

    }

    public interface ISMTPClient
    {

        event OnSendEMailRequestDelegate   OnSendEMailRequest;
        event OnSendEMailResponseDelegate  OnSendEMailResponse;


        Task<MailSentStatus> Send(EMail              EMail,
                                  Byte               NumberOfRetries   = 3,
                                  EventTracking_Id?  EventTrackingId   = null,
                                  TimeSpan?          RequestTimeout    = null);
        Task<MailSentStatus> Send(EMailEnvelop       EMailEnvelop,
                                  Byte               NumberOfRetries   = 3,
                                  EventTracking_Id?  EventTrackingId   = null,
                                  TimeSpan?          RequestTimeout    = null);

        void Dispose();

    }

}
