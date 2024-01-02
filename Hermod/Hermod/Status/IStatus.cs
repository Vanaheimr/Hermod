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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// The common status interface.
    /// </summary>
    public interface IStatus
    { }


    /// <summary>
    /// The common generic status interface.
    /// </summary>
    /// <typeparam name="TStatusType">The type of the admin status.</typeparam>
    public interface IStatus<TStatusType> : IStatus
    {

        /// <summary>
        /// The status.
        /// </summary>
        Timestamped<TStatusType>  Status    { get; set; }

        /// <summary>
        /// The admin status schedule (history).
        /// </summary>
        /// <param name="TimestampFilter">An optional status timestamp filter.</param>
        /// <param name="StatusFilter">An optional status value filter.</param>
        /// <param name="Skip">The number of status entries to skip.</param>
        /// <param name="Take">The number of status entries to return.</param>
        IEnumerable<Timestamped<TStatusType>>  StatusSchedule(Func<DateTime, Boolean>?     TimestampFilter   = null,
                                                              Func<TStatusType, Boolean>?  StatusFilter      = null,
                                                              UInt64?                      Skip              = null,
                                                              UInt64?                      Take              = null);

        void SetStatus(TStatusType                            NewStatus,
                       Context?                               DataSource     = null);
        void SetStatus(Timestamped<TStatusType>               NewTimestampedStatus,
                       Context?                               DataSource     = null);
        void SetStatus(IEnumerable<Timestamped<TStatusType>>  NewStatusList,
                       ChangeMethods                          ChangeMethod   = ChangeMethods.Replace,
                       Context?                               DataSource     = null);
        void SetStatus(TStatusType                            NewStatus,
                       DateTime                               Timestamp,
                       Context?                               DataSource     = null);

    }

}
