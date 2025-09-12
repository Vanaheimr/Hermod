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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Warden
{

    /// <summary>
    /// Extensions methods for service checking and monitoring.
    /// </summary>
    public static class WardenExtensions
    {

        #region Check(TimeCheck, SleepTime, ServiceChecker, ...)

        public static Warden Check(this Warden                                    Warden,
                                   TimeCheckDelegate                              TimeCheck,
                                   TimeSpan                                       SleepTime,
                                   Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.Check((timestamp, serviceproperties) => TimeCheck(timestamp),
                            SleepTime,
                            ServiceChecker);

        public static Warden Check<TResult>(this Warden                                             Warden,
                                            TimeCheckDelegate                                       TimeCheck,
                                            TimeSpan                                                SleepTime,
                                            Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                            params Action<TResult>[]                                ResultConsumers)

            => Warden.Check((timestamp, serviceproperties) => TimeCheck(timestamp),
                            SleepTime,
                            ServiceChecker,
                            ResultConsumers);

        #endregion

        #region Check(TimeCheck, SleepTime, ServiceChecker, ...)

        public static Warden Check<TResult>(this Warden                                                Warden,
                                            TimeCheckDelegate                                          TimeCheck,
                                            TimeSpan                                                   SleepTime,
                                            Func<DateTimeOffset, IDNSClient, CancellationToken, Task>  ServiceChecker)

            => Warden.Check((timestamp, serviceproperties) => TimeCheck(timestamp),
                            SleepTime,
                            ServiceChecker);

        public static Warden Check<TResult>(this Warden                                                         Warden,
                                            TimeCheckDelegate                                                   TimeCheck,
                                            TimeSpan                                                            SleepTime,
                                            Func<DateTimeOffset, IDNSClient, CancellationToken, Task<TResult>>  ServiceChecker,
                                            params Action<TResult>[]                                            ResultConsumers)

            => Warden.Check((timestamp, serviceproperties) => TimeCheck(timestamp),
                            SleepTime,
                            ServiceChecker,
                            ResultConsumers);

        #endregion

        #region Check(TimeCheck, SleepTime, Entity, ServiceChecker, ...)

        public static Warden Check<TEntity>(this Warden                                             Warden,
                                            TimeCheckDelegate                                       TimeCheck,
                                            TimeSpan                                                SleepTime,
                                            TEntity                                                 Entity,
                                            Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.Check(
                   (timestamp, serviceproperties) => TimeCheck(timestamp),
                   SleepTime,
                   Entity,
                   ServiceChecker
               );


        public static Warden Check<TEntity, TResult>(this Warden                                                      Warden,
                                                     TimeCheckDelegate                                                TimeCheck,
                                                     TimeSpan                                                         SleepTime,
                                                     TEntity                                                          Entity,
                                                     Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                     params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.Check((timestamp, serviceproperties) => TimeCheck(timestamp),
                            SleepTime,
                            Entity,
                            ServiceChecker,
                            ResultConsumers);

        #endregion

        #region Check(TimeCheck, SleepTime, Entity, ServiceChecker)

        public static Warden Check<TEntity>(this Warden                                                         Warden,
                                            TimeCheckDelegate                                                   TimeCheck,
                                            TimeSpan                                                            SleepTime,
                                            TEntity                                                             Entity,
                                            Func<DateTimeOffset, IDNSClient, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.Check((timestamp, serviceproperties) => TimeCheck(timestamp),
                            SleepTime,
                            Entity,
                            ServiceChecker);

        public static Warden Check<TEntity, TResult>(this Warden                                                                  Warden,
                                                     TimeCheckDelegate                                                            TimeCheck,
                                                     TimeSpan                                                                     SleepTime,
                                                     TEntity                                                                      Entity,
                                                     Func<DateTimeOffset, IDNSClient, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                     params Action<TEntity, TResult>[]                                            ResultConsumers)

            where TEntity : class

            => Warden.Check((timestamp, serviceproperties) => TimeCheck(timestamp),
                            SleepTime,
                            Entity,
                            ServiceChecker,
                            ResultConsumers);

        #endregion


        #region EverySeconds(...)

        public static Warden EverySeconds(this Warden                                    Warden,
                                          UInt16                                         Seconds,
                                          Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.EverySeconds(Seconds,
                                   0,
                                   ServiceChecker);

        public static Warden EverySeconds(this Warden                                    Warden,
                                          UInt16                                         Seconds,
                                          UInt16                                         Offset,
                                          Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.Check(timestamp => timestamp.Minute % Seconds == Offset,
                            TimeSpan.FromSeconds(1),
                            ServiceChecker);

        public static Warden EverySeconds(this Warden                                    Warden,
                                          UInt16                                         Seconds,
                                          PropertyCheckDelegate                          PropertyChecker,
                                          Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.EverySeconds(Seconds,
                                   0,
                                   PropertyChecker,
                                   ServiceChecker);

        public static Warden EverySeconds(this Warden                                    Warden,
                                          UInt16                                         Seconds,
                                          UInt16                                         Offset,
                                          PropertyCheckDelegate                          PropertyChecker,
                                          Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.Check((timestamp, properties) => timestamp.Minute % Seconds == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromSeconds(1),
                            ServiceChecker);

        #endregion

        #region EverySeconds(...Entity)

        public static Warden EverySeconds<TEntity>(this Warden                                             Warden,
                                                   UInt16                                                  Seconds,
                                                   TEntity                                                 Entity,
                                                   Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.EverySeconds(Seconds,
                                   0,
                                   Entity,
                                   ServiceChecker);

        public static Warden EverySeconds<TEntity>(this Warden                                             Warden,
                                                   UInt16                                                  Seconds,
                                                   UInt16                                                  Offset,
                                                   TEntity                                                 Entity,
                                                   Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.Check(timestamp => timestamp.Minute % Seconds == Offset,
                            TimeSpan.FromSeconds(1),
                            Entity,
                            ServiceChecker);

        public static Warden EverySeconds<TEntity>(this Warden                                             Warden,
                                                   UInt16                                                  Seconds,
                                                   PropertyCheckDelegate                                   PropertyChecker,
                                                   TEntity                                                 Entity,
                                                   Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.EverySeconds(Seconds,
                                   0,
                                   PropertyChecker,
                                   Entity,
                                   ServiceChecker);

        public static Warden EverySeconds<TEntity>(this Warden                                             Warden,
                                                   UInt16                                                  Seconds,
                                                   UInt16                                                  Offset,
                                                   PropertyCheckDelegate                                   PropertyChecker,
                                                   TEntity                                                 Entity,
                                                   Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.Check((timestamp, properties) => timestamp.Minute % Seconds == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromSeconds(1),
                            Entity,
                            ServiceChecker);

        #endregion

        #region EverySeconds(..., ResultConsumers)

        public static Warden EverySeconds<TResult>(this Warden                                             Warden,
                                                   UInt16                                                  Seconds,
                                                   Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                   params Action<TResult>[]                                ResultConsumers)

            => Warden.EverySeconds(Seconds,
                                   0,
                                   ServiceChecker,
                                   ResultConsumers);

        public static Warden EverySeconds<TResult>(this Warden                                             Warden,
                                                   UInt16                                                  Seconds,
                                                   UInt16                                                  Offset,
                                                   Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                   params Action<TResult>[]                                ResultConsumers)

            => Warden.Check(timestamp => timestamp.Minute % Seconds == Offset,
                            TimeSpan.FromSeconds(1),
                            ServiceChecker,
                            ResultConsumers);

        public static Warden EverySeconds<TResult>(this Warden                                             Warden,
                                                   UInt16                                                  Seconds,
                                                   PropertyCheckDelegate                                   PropertyChecker,
                                                   Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                   params Action<TResult>[]                                ResultConsumers)

            => Warden.EverySeconds(Seconds,
                                   0,
                                   PropertyChecker,
                                   ServiceChecker,
                                   ResultConsumers);

        public static Warden EverySeconds<TResult>(this Warden                                             Warden,
                                                   UInt16                                                  Seconds,
                                                   UInt16                                                  Offset,
                                                   PropertyCheckDelegate                                   PropertyChecker,
                                                   Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                   params Action<TResult>[]                                ResultConsumers)

            => Warden.Check((timestamp, properties) => timestamp.Minute % Seconds == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromSeconds(1),
                            ServiceChecker,
                            ResultConsumers);

        #endregion

        #region EverySeconds(...Entity, ..., ResultConsumers)

        public static Warden EverySeconds<TEntity, TResult>(this Warden                                                      Warden,
                                                            UInt16                                                           Seconds,
                                                            TEntity                                                          Entity,
                                                            Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                            params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.EverySeconds(Seconds,
                                   0,
                                   Entity,
                                   ServiceChecker,
                                   ResultConsumers);

        public static Warden EverySeconds<TEntity, TResult>(this Warden                                                      Warden,
                                                            UInt16                                                           Seconds,
                                                            UInt16                                                           Offset,
                                                            TEntity                                                          Entity,
                                                            Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                            params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.Check(timestamp => timestamp.Second % Seconds == Offset,
                            TimeSpan.FromSeconds(1),
                            Entity,
                            ServiceChecker,
                            ResultConsumers);

        public static Warden EverySeconds<TEntity, TResult>(this Warden                                                      Warden,
                                                            UInt16                                                           Seconds,
                                                            PropertyCheckDelegate                                            PropertyChecker,
                                                            TEntity                                                          Entity,
                                                            Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                            params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.EverySeconds(Seconds,
                                   0,
                                   PropertyChecker,
                                   Entity,
                                   ServiceChecker,
                                   ResultConsumers);

        public static Warden EverySeconds<TEntity, TResult>(this Warden                                                      Warden,
                                                            UInt16                                                           Seconds,
                                                            UInt16                                                           Offset,
                                                            PropertyCheckDelegate                                            PropertyChecker,
                                                            TEntity                                                          Entity,
                                                            Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                            params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.Check((timestamp, properties) => timestamp.Second % Seconds == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromSeconds(1),
                            Entity,
                            ServiceChecker,
                            ResultConsumers);

        #endregion


        #region EveryMinutes(...)

        public static Warden EveryMinutes(this Warden                                    Warden,
                                          UInt16                                         Minutes,
                                          Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.EveryMinutes(Minutes,
                                   0,
                                   ServiceChecker);

        public static Warden EveryMinutes(this Warden                                    Warden,
                                          UInt16                                         Minutes,
                                          UInt16                                         Offset,
                                          Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.Check(timestamp => timestamp.Minute % Minutes == Offset,
                            TimeSpan.FromMinutes(1),
                            ServiceChecker);

        public static Warden EveryMinutes(this Warden                                    Warden,
                                          UInt16                                         Minutes,
                                          PropertyCheckDelegate                          PropertyChecker,
                                          Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.EveryMinutes(Minutes,
                                   0,
                                   PropertyChecker,
                                   ServiceChecker);

        public static Warden EveryMinutes(this Warden                                    Warden,
                                          UInt16                                         Minutes,
                                          UInt16                                         Offset,
                                          PropertyCheckDelegate                          PropertyChecker,
                                          Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.Check((timestamp, properties) => timestamp.Minute % Minutes == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromMinutes(1),
                            ServiceChecker);

        #endregion

        #region EveryMinutes(...Entity)

        public static Warden EveryMinutes<TEntity>(this Warden                                             Warden,
                                                   UInt16                                                  Minutes,
                                                   TEntity                                                 Entity,
                                                   Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.EveryMinutes(
                   Minutes,
                   0,
                   Entity,
                   ServiceChecker
               );

        public static Warden EveryMinutes<TEntity>(this Warden                                             Warden,
                                                   UInt16                                                  Minutes,
                                                   UInt16                                                  Offset,
                                                   TEntity                                                 Entity,
                                                   Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.Check(
                   timestamp => timestamp.Minute % Minutes == Offset,
                   TimeSpan.FromMinutes(1),
                   Entity,
                   ServiceChecker
               );

        public static Warden EveryMinutes<TEntity>(this Warden                                             Warden,
                                                   UInt16                                                  Minutes,
                                                   PropertyCheckDelegate                                   PropertyChecker,
                                                   TEntity                                                 Entity,
                                                   Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.EveryMinutes(Minutes,
                                   0,
                                   PropertyChecker,
                                   Entity,
                                   ServiceChecker);

        public static Warden EveryMinutes<TEntity>(this Warden                                             Warden,
                                                   UInt16                                                  Minutes,
                                                   UInt16                                                  Offset,
                                                   PropertyCheckDelegate                                   PropertyChecker,
                                                   TEntity                                                 Entity,
                                                   Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.Check((timestamp, properties) => timestamp.Minute % Minutes == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromMinutes(1),
                            Entity,
                            ServiceChecker);

        #endregion

        #region EveryMinutes(..., ResultConsumers)

        public static Warden EveryMinutes<TResult>(this Warden                                             Warden,
                                                   UInt16                                                  Minutes,
                                                   Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                   params Action<TResult>[]                                ResultConsumers)

            => Warden.EveryMinutes(Minutes,
                                   0,
                                   ServiceChecker,
                                   ResultConsumers);

        public static Warden EveryMinutes<TResult>(this Warden                                             Warden,
                                                   UInt16                                                  Minutes,
                                                   UInt16                                                  Offset,
                                                   Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                   params Action<TResult>[]                                ResultConsumers)

            => Warden.Check(timestamp => timestamp.Minute % Minutes == Offset,
                            TimeSpan.FromMinutes(1),
                            ServiceChecker,
                            ResultConsumers);

        public static Warden EveryMinutes<TResult>(this Warden                                             Warden,
                                                   UInt16                                                  Minutes,
                                                   PropertyCheckDelegate                                   PropertyChecker,
                                                   Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                   params Action<TResult>[]                                ResultConsumers)

            => Warden.EveryMinutes(Minutes,
                                   0,
                                   PropertyChecker,
                                   ServiceChecker,
                                   ResultConsumers);

        public static Warden EveryMinutes<TResult>(this Warden                                             Warden,
                                                   UInt16                                                  Minutes,
                                                   UInt16                                                  Offset,
                                                   PropertyCheckDelegate                                   PropertyChecker,
                                                   Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                   params Action<TResult>[]                                ResultConsumers)

            => Warden.Check((timestamp, properties) => timestamp.Minute % Minutes == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromMinutes(1),
                            ServiceChecker,
                            ResultConsumers);

        #endregion

        #region EveryMinutes(...Entity, ..., ResultConsumers)

        public static Warden EveryMinutes<TEntity, TResult>(this Warden                                                      Warden,
                                                            UInt16                                                           Minutes,
                                                            TEntity                                                          Entity,
                                                            Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                            params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.EveryMinutes(Minutes,
                                   0,
                                   Entity,
                                   ServiceChecker,
                                   ResultConsumers);

        public static Warden EveryMinutes<TEntity, TResult>(this Warden                                                      Warden,
                                                            UInt16                                                           Minutes,
                                                            UInt16                                                           Offset,
                                                            TEntity                                                          Entity,
                                                            Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                            params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.Check(timestamp => timestamp.Minute % Minutes == Offset,
                            TimeSpan.FromMinutes(1),
                            Entity,
                            ServiceChecker,
                            ResultConsumers);

        public static Warden EveryMinutes<TEntity, TResult>(this Warden                                                      Warden,
                                                            UInt16                                                           Minutes,
                                                            PropertyCheckDelegate                                            PropertyChecker,
                                                            TEntity                                                          Entity,
                                                            Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                            params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.EveryMinutes(Minutes,
                                   0,
                                   PropertyChecker,
                                   Entity,
                                   ServiceChecker,
                                   ResultConsumers);

        public static Warden EveryMinutes<TEntity, TResult>(this Warden                                                      Warden,
                                                            UInt16                                                           Minutes,
                                                            UInt16                                                           Offset,
                                                            PropertyCheckDelegate                                            PropertyChecker,
                                                            TEntity                                                          Entity,
                                                            Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                            params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.Check((timestamp, properties) => timestamp.Minute % Minutes == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromMinutes(1),
                            Entity,
                            ServiceChecker,
                            ResultConsumers);

        #endregion


        #region EveryHours(...)

        public static Warden EveryHours(this Warden                                    Warden,
                                        UInt16                                         Hours,
                                        Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.EveryHours(Hours,
                                 0,
                                 ServiceChecker);

        public static Warden EveryHours(this Warden                                    Warden,
                                        UInt16                                         Hours,
                                        UInt16                                         Offset,
                                        Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.Check(timestamp => timestamp.Hour % Hours == Offset,
                            TimeSpan.FromHours(1),
                            ServiceChecker);

        public static Warden EveryHours(this Warden                                    Warden,
                                        UInt16                                         Hours,
                                        PropertyCheckDelegate                          PropertyChecker,
                                        Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.EveryHours(Hours,
                                 0,
                                 PropertyChecker,
                                 ServiceChecker);

        public static Warden EveryHours(this Warden                                    Warden,
                                        UInt16                                         Hours,
                                        UInt16                                         Offset,
                                        PropertyCheckDelegate                          PropertyChecker,
                                        Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

            => Warden.Check((timestamp, properties) => timestamp.Hour % Hours == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromHours(1),
                            ServiceChecker);

        #endregion

        #region EveryHours(...Entity)

        public static Warden EveryHours<TEntity>(this Warden                                             Warden,
                                                 UInt16                                                  Hours,
                                                 TEntity                                                 Entity,
                                                 Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.EveryHours(Hours,
                                 0,
                                 Entity,
                                 ServiceChecker);

        public static Warden EveryHours<TEntity>(this Warden                                             Warden,
                                                 UInt16                                                  Hours,
                                                 UInt16                                                  Offset,
                                                 TEntity                                                 Entity,
                                                 Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.Check(timestamp => timestamp.Hour % Hours == Offset,
                            TimeSpan.FromHours(1),
                            Entity,
                            ServiceChecker);

        public static Warden EveryHours<TEntity>(this Warden                                             Warden,
                                                 UInt16                                                  Hours,
                                                 PropertyCheckDelegate                                   PropertyChecker,
                                                 TEntity                                                 Entity,
                                                 Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.EveryHours(Hours,
                                 0,
                                 PropertyChecker,
                                 Entity,
                                 ServiceChecker);

        public static Warden EveryHours<TEntity>(this Warden                                             Warden,
                                                 UInt16                                                  Hours,
                                                 UInt16                                                  Offset,
                                                 PropertyCheckDelegate                                   PropertyChecker,
                                                 TEntity                                                 Entity,
                                                 Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

            => Warden.Check((timestamp, properties) => timestamp.Hour % Hours == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromHours(1),
                            Entity,
                            ServiceChecker);

        #endregion

        #region EveryHours(..., ResultConsumers)

        public static Warden EveryHours<TResult>(this Warden                                             Warden,
                                                 UInt16                                                  Hours,
                                                 Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                 params Action<TResult>[]                                ResultConsumers)

            => Warden.EveryHours(Hours,
                                 0,
                                 ServiceChecker,
                                 ResultConsumers);

        public static Warden EveryHours<TResult>(this Warden                                             Warden,
                                                 UInt16                                                  Hours,
                                                 UInt16                                                  Offset,
                                                 Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                 params Action<TResult>[]                                ResultConsumers)

            => Warden.Check(timestamp => timestamp.Hour % Hours == Offset,
                            TimeSpan.FromHours(1),
                            ServiceChecker,
                            ResultConsumers);

        public static Warden EveryHours<TResult>(this Warden                                             Warden,
                                                 UInt16                                                  Hours,
                                                 PropertyCheckDelegate                                   PropertyChecker,
                                                 Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                 params Action<TResult>[]                                ResultConsumers)

            => Warden.EveryHours(Hours,
                                 0,
                                 PropertyChecker,
                                 ServiceChecker,
                                 ResultConsumers);

        public static Warden EveryHours<TResult>(this Warden                                             Warden,
                                                 UInt16                                                  Hours,
                                                 UInt16                                                  Offset,
                                                 PropertyCheckDelegate                                   PropertyChecker,
                                                 Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                                 params Action<TResult>[]                                ResultConsumers)

            => Warden.Check((timestamp, properties) => timestamp.Hour % Hours == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromHours(1),
                            ServiceChecker,
                            ResultConsumers);

        #endregion

        #region EveryHours(...Entity, ..., ResultConsumers)

        public static Warden EveryHours<TEntity, TResult>(this Warden                                                      Warden,
                                                          UInt16                                                           Hours,
                                                          TEntity                                                          Entity,
                                                          Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                          params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.EveryHours(Hours,
                                 0,
                                 Entity,
                                 ServiceChecker,
                                 ResultConsumers);

        public static Warden EveryHours<TEntity, TResult>(this Warden                                                      Warden,
                                                          UInt16                                                           Hours,
                                                          UInt16                                                           Offset,
                                                          TEntity                                                          Entity,
                                                          Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                          params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.Check(timestamp => timestamp.Hour % Hours == Offset,
                            TimeSpan.FromHours(1),
                            Entity,
                            ServiceChecker,
                            ResultConsumers);

        public static Warden EveryHours<TEntity, TResult>(this Warden                                                      Warden,
                                                          UInt16                                                           Hours,
                                                          PropertyCheckDelegate                                            PropertyChecker,
                                                          TEntity                                                          Entity,
                                                          Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                          params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.EveryHours(Hours,
                                 0,
                                 PropertyChecker,
                                 Entity,
                                 ServiceChecker,
                                 ResultConsumers);

        public static Warden EveryHours<TEntity, TResult>(this Warden                                                      Warden,
                                                          UInt16                                                           Hours,
                                                          UInt16                                                           Offset,
                                                          PropertyCheckDelegate                                            PropertyChecker,
                                                          TEntity                                                          Entity,
                                                          Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                                          params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

            => Warden.Check((timestamp, properties) => timestamp.Hour % Hours == Offset && (PropertyChecker?.Invoke(properties) ?? false),
                            TimeSpan.FromHours(1),
                            Entity,
                            ServiceChecker,
                            ResultConsumers);

        #endregion

    }

}
