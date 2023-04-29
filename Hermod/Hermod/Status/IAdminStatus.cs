/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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
    /// The common admin status interface.
    /// </summary>
    public interface IAdminStatus
    { }


    /// <summary>
    /// The common generic admin status interface.
    /// </summary>
    /// <typeparam name="TAdminStatusType">The type of the admin status.</typeparam>
    public interface IAdminStatus<TAdminStatusType> : IAdminStatus
    {

        /// <summary>
        /// The admin status.
        /// </summary>
        Timestamped<TAdminStatusType>  AdminStatus    { get; set; }

        /// <summary>
        /// The admin status schedule (history).
        /// </summary>
        /// <param name="TimestampFilter">An optional admin status timestamp filter.</param>
        /// <param name="AdminStatusFilter">An optional admin status value filter.</param>
        /// <param name="Skip">The number of admin status entries to skip.</param>
        /// <param name="Take">The number of admin status entries to return.</param>
        IEnumerable<Timestamped<TAdminStatusType>>  AdminStatusSchedule(Func<DateTime, Boolean>?          TimestampFilter     = null,
                                                                        Func<TAdminStatusType, Boolean>?  AdminStatusFilter   = null,
                                                                        UInt64?                           Skip                = null,
                                                                        UInt64?                           Take                = null);



        void SetAdminStatus(TAdminStatusType                            NewAdminStatus,
                            Context?                                    DataSource     = null);
        void SetAdminStatus(Timestamped<TAdminStatusType>               NewTimestampedAdminStatus,
                            Context?                                    DataSource     = null);
        void SetAdminStatus(IEnumerable<Timestamped<TAdminStatusType>>  NewAdminStatusList,
                            ChangeMethods                               ChangeMethod   = ChangeMethods.Replace,
                            Context?                                    DataSource     = null);
        void SetAdminStatus(TAdminStatusType                            NewAdminStatus,
                            DateTime                                    Timestamp,
                            Context?                                    DataSource     = null);

    }

}
