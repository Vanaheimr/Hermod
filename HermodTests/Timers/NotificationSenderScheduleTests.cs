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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Timers
{

    /// <summary>
    /// A notification sender ticks as often as its schedule says: every 31
    /// seconds by default, not every 31 milliseconds.
    /// </summary>
    /// <remarks>
    /// The schedule was kept in whole seconds, as a UInt32, and handed to the
    /// timer as it was. The timer's overload for unsigned integers takes
    /// milliseconds: every sender put a callback on the thread pool about 32
    /// times a second, for as long as it lived.
    /// </remarks>
    [TestFixture]
    public class NotificationSenderScheduleTests
    {

        #region (class) ScheduleRecorder

        /// <summary>
        /// A time provider that notes the schedule of every timer it is asked
        /// for, and leaves the timers themselves to the system's.
        /// </summary>
        private sealed class ScheduleRecorder : TimeProvider
        {

            /// <summary>
            /// The due time and period of every timer asked for.
            /// </summary>
            public ConcurrentQueue<(TimeSpan DueTime, TimeSpan Period)> Schedules { get; } = new();

            public override ITimer CreateTimer(TimerCallback  Callback,
                                               Object?        State,
                                               TimeSpan       DueTime,
                                               TimeSpan       Period)
            {

                Schedules.Enqueue((DueTime, Period));

                return TimeProvider.System.CreateTimer(Callback, State, DueTime, Period);

            }

        }

        #endregion

        #region (private) NewHTTPAPI()

        /// <summary>
        /// An HTTP API on an HTTP server that is never started, to be lent to
        /// the notification senders.
        /// </summary>
        private static HTTPExtAPI NewHTTPAPI()

            => new (
                   new HTTPServer(
                       IPAddress:  IPv4Address.Localhost,
                       TCPPort:    IPPort.Zero,
                       AutoStart:  false
                   ),
                   SkipURLTemplates:      true,
                   DisableNotifications:  true,
                   DisableLogging:        true
               );

        #endregion


        #region ANotificationSenderTicksEvery31SecondsByDefault()

        [Test]
        public async Task ANotificationSenderTicksEvery31SecondsByDefault()
        {

            var httpAPI   = NewHTTPAPI();
            var recorder  = new ScheduleRecorder();

            try
            {

                await using var sender = new HTTPNotificationSender(
                                             httpAPI,
                                             HTTPHostname.Localhost,
                                             TimeProvider:  recorder
                                         );

                Assert.Multiple(() => {

                    Assert.That(recorder.Schedules,           Is.EqualTo(new[] { (TimeSpan.FromSeconds(31), TimeSpan.FromSeconds(31)) }), "the schedule of the sender's timer");
                    Assert.That(sender.FlushNotificationsEvery, Is.EqualTo(TimeSpan.FromSeconds(31)),                                   "the schedule the sender says it keeps");

                });

            }
            finally
            {
                await httpAPI.HTTPServer.DisposeAsync();
            }

        }

        #endregion

        #region ANotificationSenderTicksAsOftenAsItWasAsked()

        [Test]
        public async Task ANotificationSenderTicksAsOftenAsItWasAsked()
        {

            var httpAPI   = NewHTTPAPI();
            var recorder  = new ScheduleRecorder();

            try
            {

                await using var sender = new HTTPNotificationSender(
                                             httpAPI,
                                             HTTPHostname.Localhost,
                                             SendNotificationsEvery:  TimeSpan.FromSeconds(5),
                                             TimeProvider:            recorder
                                         );

                Assert.Multiple(() => {

                    Assert.That(recorder.Schedules,           Is.EqualTo(new[] { (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)) }), "the schedule of the sender's timer");
                    Assert.That(sender.FlushNotificationsEvery, Is.EqualTo(TimeSpan.FromSeconds(5)),                                  "the schedule the sender says it keeps");

                });

            }
            finally
            {
                await httpAPI.HTTPServer.DisposeAsync();
            }

        }

        #endregion

    }

}
