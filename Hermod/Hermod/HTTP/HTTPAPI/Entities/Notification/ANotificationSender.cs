/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
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

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Bcpg.OpenPgp;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    public abstract class ANotificationSender
    {

        #region Data

        /// <summary>
        /// The default service check interval.
        /// </summary>
        public readonly static TimeSpan       DefaultSendNotificationsEvery   = TimeSpan.FromSeconds(31);


        protected readonly     SemaphoreSlim  SendNotificationsLock           = new (1, 1);
        protected readonly     Timer          SendNotificationsTimer;

        #endregion

        #region Properties

        public HTTPExtAPI HTTPExtAPI { get; }

        /// <summary>
        /// This service can be disabled, e.g. for debugging reasons.
        /// </summary>
        public Boolean DisableSendNotifications { get; set; }

        #region FlushNotificationsEvery

        protected UInt32 sendNotificationsEvery;

        public TimeSpan FlushNotificationsEvery
        {

            get
            {
                return TimeSpan.FromSeconds(sendNotificationsEvery);
            }

            set
            {
                sendNotificationsEvery = (UInt32) value.TotalSeconds;
            }

        }

        #endregion


        public DateTime LatestNotificationTimestamp { get; private set; }

        /// <summary>
        /// The attached DNS service.
        /// </summary>
        public DNSClient DNSClient { get; }

        #endregion

        #region Constructor(s)

        protected ANotificationSender(HTTPExtAPI         HTTPExtAPI,
                                      TimeSpan?          SendNotificationsEvery     = null,
                                      Boolean            DisableSendNotifications   = false,
                                      PgpPublicKeyRing?  PublicKeyRing              = null,
                                      PgpSecretKeyRing?  SecretKeyRing              = null,
                                      DNSClient?         DNSClient                  = null)
        {

            this.HTTPExtAPI                   = HTTPExtAPI;
            this.DisableSendNotifications     = DisableSendNotifications;

            this.sendNotificationsEvery       = (UInt32) (SendNotificationsEvery.HasValue
                                                    ? SendNotificationsEvery.Value. TotalSeconds
                                                    : DefaultSendNotificationsEvery.TotalSeconds);

            this.SendNotificationsTimer       = new Timer(
                                                    SendNotifications,
                                                    null,
                                                    sendNotificationsEvery,
                                                    sendNotificationsEvery
                                                );

            this.LatestNotificationTimestamp  = DateTime.MinValue;
            this.DNSClient                    = DNSClient ?? new DNSClient();

        }

        #endregion


        #region (timer) SendNotifications(State)

        private void SendNotifications(Object? State)
        {
            if (!DisableSendNotifications)
                SendNotifications2(State).Wait();
        }

        private async Task SendNotifications2(Object? State)
        {

            var lockTaken = await SendNotificationsLock.WaitAsync(0).ConfigureAwait(false);

            try
            {

                if (lockTaken)
                {

                    //var NotificationMessages  = HTTPExtAPI.NotificationMessages.
                    //                                     Where (notificationMessage => notificationMessage.Timestamp > LatestNotificationTimestamp).
                    //                                     ToArray();

                    //if (NotificationMessages.Length > 0)
                    //{

                    //    LatestNotificationTimestamp  = NotificationMessages.
                    //                                       Max   (notificationMessage => notificationMessage.Timestamp);

                    //    await SendNotifications(NotificationMessages.
                    //                                       Select(notificationMessage => notificationMessage.Value));

                    //}

                }

            }
            catch (Exception e)
            {

                while (e.InnerException is not null)
                    e = e.InnerException;

                //DebugX.LogT(GetType().Name + ".SendNotifications '" + Id + "' led to an exception: " + e.Message + Environment.NewLine + e.StackTrace);

                //OnWWCPCPOAdapterException?.Invoke(Timestamp.Now,
                //                                  this,
                //                                  e);

            }

            finally
            {

                if (lockTaken)
                {
                    SendNotificationsLock.Release();
               //     DebugX.LogT("SendNotificationsLock released!");
                }

                else
                    DebugX.LogT("SendNotificationsLock exited!");

            }

        }

        #endregion


        public abstract Task SendNotifications(IEnumerable<JObject> JSONData);


    }

}
