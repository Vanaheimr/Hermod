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

    /// <summary>
    /// A SMTP client for NOT sending, but logging e-mails.
    /// </summary>
    public class NullMailer : ISMTPClient
    {

        #region Data

        private readonly        Dictionary<Message_Id, EMailEnvelop>  eMailEnvelops;
        private static readonly SemaphoreSlim                         eMailEnvelopsSemaphore   = new (1, 1);
        public  static readonly TimeSpan                              SemaphoreSlimTimeout     = TimeSpan.FromSeconds(30);

        #endregion

        #region Properties

        public SmtpCapabilities Capabilities;

        #region EMails

        /// <summary>
        /// An enumeration of all e-mails sent.
        /// </summary>
        public IEnumerable<EMailEnvelop> EMailEnvelops
        {
            get
            {

                if (eMailEnvelopsSemaphore.Wait(SemaphoreSlimTimeout))
                {
                    try
                    {

                        return eMailEnvelops.Values.ToArray();

                    }
                    finally
                    {
                        try
                        {
                            eMailEnvelopsSemaphore.Release();
                        }
                        catch
                        { }
                    }
                }

                return Array.Empty<EMailEnvelop>();

            }
        }

        #endregion

        #endregion

        #region Events


        public event OnSendEMailRequestDelegate?   OnSendEMailRequest;


        public event OnSendEMailResponseDelegate?  OnSendEMailResponse;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SMTP client for NOT sending, but logging e-mails.
        /// </summary>
        public NullMailer()
        {

            this.eMailEnvelops = new Dictionary<Message_Id, EMailEnvelop>();

        }

        #endregion


        #region (private) GenerateMessageId(Mail, DomainPart = null)

        private Message_Id GenerateMessageId(EMail    Mail,
                                             String?  DomainPart   = null)
        {

            DomainPart       = DomainPart?.Trim();

            var randomBytes  = new Byte[16];
            Random.Shared.NextBytes(randomBytes);

            var hashedBytes  = SHA256.Create().ComputeHash(randomBytes.
                                                               Concat(Mail.From.   ToString(). ToUTF8Bytes()).
                                                               Concat(Mail.Subject.            ToUTF8Bytes()).
                                                               Concat(Mail.Date.   ToIso8601().ToUTF8Bytes()).
                                                               ToArray());

            return Message_Id.Parse(hashedBytes.ToHexString()[..24],
                                    DomainPart is not null ? DomainPart : "NullMailer");

        }

        #endregion


        #region Send(EMail,        NumberOfRetries = 3, EventTrackingId = null, RequestTimeout = null)

        public Task<MailSentStatus> Send(EMail              EMail,
                                         Byte               NumberOfRetries   = 3,
                                         EventTracking_Id?  EventTrackingId   = null,
                                         TimeSpan?          RequestTimeout    = null)

            => Send(new EMailEnvelop(EMail),
                    NumberOfRetries,
                    EventTrackingId,
                    RequestTimeout);

        #endregion

        #region Send(EMailEnvelop, NumberOfRetries = 3, EventTrackingId = null, RequestTimeout = null)

        public async Task<MailSentStatus> Send(EMailEnvelop       EMailEnvelop,
                                               Byte               NumberOfRetries   = 3,
                                               EventTracking_Id?  EventTrackingId   = null,
                                               TimeSpan?          RequestTimeout    = null)
        {

            var eventTrackingId = EventTrackingId ?? EventTracking_Id.New;

            #region Send OnSendEMailRequest event

            var startTime = Timestamp.Now;

            try
            {

                if (OnSendEMailRequest is not null)
                    await Task.WhenAll(OnSendEMailRequest.GetInvocationList().
                                        Cast<OnSendEMailRequestDelegate>().
                                        Select(e => e(startTime,
                                                      this,
                                                      eventTrackingId,
                                                      EMailEnvelop,
                                                      RequestTimeout))).
                                        ConfigureAwait(false);

            }
            catch (Exception e)
            {
                DebugX.Log(e, nameof(NullMailer) + "." + nameof(OnSendEMailRequest));
            }

            #endregion


            var result = MailSentStatus.failed;

            if (await eMailEnvelopsSemaphore.WaitAsync(RequestTimeout ?? TimeSpan.FromSeconds(60)))
            {
                try
                {

                    eMailEnvelops.Add(EMailEnvelop.Mail.MessageId ?? Message_Id.Random("opendata.social"), EMailEnvelop);

                    result = MailSentStatus.ok;

                }
                catch (Exception e)
                {
                    DebugX.LogException(e);
                    result = MailSentStatus.ExceptionOccured;
                }
                finally
                {
                    eMailEnvelopsSemaphore.Release();
                }
            }

            #region Send OnSendEMailResponse event

            var endTime = Timestamp.Now;

            try
            {

                if (OnSendEMailResponse is not null)
                    await Task.WhenAll(OnSendEMailResponse.GetInvocationList().
                                       Cast<OnSendEMailResponseDelegate>().
                                       Select(e => e(endTime,
                                                     this,
                                                     eventTrackingId,
                                                     EMailEnvelop,
                                                     RequestTimeout,
                                                     result,
                                                     endTime - startTime))).
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


        #region Clear()

        public void Clear()
        {
            if (eMailEnvelopsSemaphore.Wait(TimeSpan.FromSeconds(60)))
            {
                try
                {
                    eMailEnvelops.Clear();
                }
                catch (Exception e)
                {
                    DebugX.LogException(e);
                }
                finally
                {
                    eMailEnvelopsSemaphore.Release();
                }
            }
        }

        #endregion


        #region Dispose()

        public void Dispose()
        {
            
        }

        #endregion

    }

}
