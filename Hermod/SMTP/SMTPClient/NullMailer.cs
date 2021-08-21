/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// A SMTP client for NOT sending, but logging e-mails.
    /// </summary>
    public class NullMailer : ISMTPClient
    {

        #region Data

        private static readonly Random                       _Random               = new Random();
        private static readonly SHA256CryptoServiceProvider  _SHAHasher            = new SHA256CryptoServiceProvider();
        private static readonly SemaphoreSlim                EMailsSemaphore       = new SemaphoreSlim(1, 1);
        public  static readonly TimeSpan                     SemaphoreSlimTimeout  = TimeSpan.FromSeconds(30);

        #endregion

        #region Properties

        public SmtpCapabilities Capabilities;

        #endregion

        #region Events


        public event OnSendEMailRequestDelegate   OnSendEMailRequest;


        public event OnSendEMailResponseDelegate  OnSendEMailResponse;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SMTP client for NOT sending, but logging e-mails.
        /// </summary>
        public NullMailer()
        {

        }

        #endregion



        #region EMails

        #region Data

        private readonly Dictionary<Message_Id, EMailEnvelop> _EMails = new Dictionary<Message_Id, EMailEnvelop>();

        /// <summary>
        /// An enumeration of all sent e-mails.
        /// </summary>
        public IEnumerable<EMailEnvelop> EMails
        {
            get
            {

                if (EMailsSemaphore.Wait(SemaphoreSlimTimeout))
                {
                    try
                    {

                        return _EMails.Values.ToArray();

                    }
                    finally
                    {
                        try
                        {
                            EMailsSemaphore.Release();
                        }
                        catch
                        { }
                    }
                }

                return new EMailEnvelop[0];

            }
        }

        #endregion

        #endregion



        #region (private) GenerateMessageId(Mail, DomainPart = null)

        private Message_Id GenerateMessageId(EMail Mail, String DomainPart = null)
        {

            if (DomainPart != null)
                DomainPart = DomainPart.Trim();

            var RandomBytes  = new Byte[16];
            _Random.NextBytes(RandomBytes);

            var HashedBytes = _SHAHasher.ComputeHash(RandomBytes.
                                                     Concat(Mail.From.   ToString(). ToUTF8Bytes()).
                                                     Concat(Mail.Subject.            ToUTF8Bytes()).
                                                     Concat(Mail.Date.   ToIso8601().ToUTF8Bytes()).
                                                     ToArray());

            return Message_Id.Parse(HashedBytes.ToHexString().Substring(0, 24),
                                    DomainPart.IsNeitherNullNorEmpty() ? DomainPart : "NullMailer");

        }

        #endregion


        #region Send(EMail,        NumberOfRetries = 3, RequestTimeout = null)

        public Task<MailSentStatus> Send(EMail      EMail,
                                         Byte       NumberOfRetries  = 3,
                                         TimeSpan?  RequestTimeout   = null)

            => Send(new EMailEnvelop(EMail ?? throw new ArgumentNullException(nameof(EMail), "The given e-mail must not be null!")),
                    NumberOfRetries,
                    RequestTimeout);

        #endregion

        #region Send(EMailEnvelop, NumberOfRetries = 3, RequestTimeout = null)

        public async Task<MailSentStatus> Send(EMailEnvelop  EMailEnvelop,
                                               Byte          NumberOfRetries  = 3,
                                               TimeSpan?     RequestTimeout   = null)
        {

            #region Initial checks

            if (EMailEnvelop is null)
                throw new ArgumentNullException(nameof(EMailEnvelop), "The given e-mail envelop must not be null!");

            var result = MailSentStatus.failed;

            #endregion

            #region Send OnSendEMailRequest event

            var StartTime = DateTime.UtcNow;

            try
            {

                if (OnSendEMailRequest != null)
                    await Task.WhenAll(OnSendEMailRequest.GetInvocationList().
                                        Cast<OnSendEMailRequestDelegate>().
                                        Select(e => e(StartTime,
                                                      this,
                                                      EMailEnvelop.EventTrackingId,
                                                      EMailEnvelop,
                                                      RequestTimeout))).
                                        ConfigureAwait(false);

            }
            catch (Exception e)
            {
                DebugX.Log(e, nameof(NullMailer) + "." + nameof(OnSendEMailRequest));
            }

            #endregion


            if (await EMailsSemaphore.WaitAsync(RequestTimeout ?? TimeSpan.FromSeconds(60)))
            {
                try
                {

                    _EMails.Add(EMailEnvelop.Mail.MessageId ?? Message_Id.Random("opendata.social"), EMailEnvelop);

                    result = MailSentStatus.ok;

                }
                catch (Exception e)
                {
                    DebugX.LogException(e);
                    result = MailSentStatus.ExceptionOccured;
                }
                finally
                {
                    EMailsSemaphore.Release();
                }
            }

            #region Send OnSendEMailResponse event

            var Endtime = DateTime.UtcNow;

            try
            {

                if (OnSendEMailResponse != null)
                    await Task.WhenAll(OnSendEMailResponse.GetInvocationList().
                                       Cast<OnSendEMailResponseDelegate>().
                                       Select(e => e(Endtime,
                                                     this,
                                                     EMailEnvelop.EventTrackingId,
                                                     EMailEnvelop,
                                                     RequestTimeout,
                                                     result,
                                                     Endtime - StartTime))).
                                       ConfigureAwait(false);

            }
            catch (Exception e)
            {
                DebugX.Log(e, nameof(NullMailer) + "." + nameof(OnSendEMailResponse));
            }

            #endregion

            return result;

        }

        #endregion


        #region Dispose()

        public void Dispose()
        {
            
        }

        #endregion

    }

}
