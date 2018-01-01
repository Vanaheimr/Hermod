/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using System.Threading.Tasks;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A list of timestamped status entries.
    /// </summary>
    /// <typeparam name="T">The type of the status entries.</typeparam>
    public class StatusSchedule<T> : IEnumerable<Timestamped<T>>
    {

        #region Data

        /// <summary>
        /// The maximum size of the status history.
        /// </summary>
        public const UInt16 DefaultMaxStatusListSize = 100;

        private readonly List<Timestamped<T>> _StatusSchedule;

        #endregion

        #region Properties

        #region CurrentStatus

        private Timestamped<T> _CurrentStatus;

        /// <summary>
        /// The current status entry.
        /// </summary>
        public Timestamped<T> CurrentStatus

            => CheckCurrentStatus();

        #endregion

        #region CurrentValue

        /// <summary>
        /// The current status value.
        /// </summary>
        public T CurrentValue

            => CheckCurrentStatus().Value;

        #endregion

        #region NextStatus

        private Timestamped<T>? _NextStatus;

        /// <summary>
        /// The next status entry.
        /// </summary>
        public Timestamped<T>? NextStatus

            => _NextStatus;

        #endregion

        #region MaxStatusHistorySize

        /// <summary>
        /// The maximum number of stored status entries.
        /// </summary>
        public UInt16 MaxStatusHistorySize { get; }

        #endregion

        #endregion

        #region Events

        /// <summary>
        /// A delegate called whenever the current status changed.
        /// </summary>
        /// <param name="Timestamp">The timestamp when this change was detected.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        /// <param name="StatusSchedule">The status schedule.</param>
        /// <param name="OldStatus">The old timestamped status.</param>
        /// <param name="NewStatus">The new timestamped status.</param>
        public delegate Task OnStatusChangedDelegate(DateTime           Timestamp,
                                                     EventTracking_Id   EventTrackingId,
                                                     StatusSchedule<T>  StatusSchedule,
                                                     Timestamped<T>     OldStatus,
                                                     Timestamped<T>     NewStatus);

        /// <summary>
        /// An event fired whenever the current status changed.
        /// </summary>
        public event OnStatusChangedDelegate OnStatusChanged;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new status schedule.
        /// </summary>
        /// <param name="MaxStatusListSize">The maximum number of stored status entries.</param>
        public StatusSchedule(UInt16 MaxStatusListSize = DefaultMaxStatusListSize)
        {

            this.MaxStatusHistorySize  = MaxStatusListSize;
            this._StatusSchedule       = new List<Timestamped<T>>();

        }

        #endregion


        #region Insert(NewStatus)

        /// <summary>
        /// Insert a new status entry.
        /// </summary>
        /// <param name="NewStatus">A new status.</param>
        public void Insert(T NewStatus)
        {
            Insert(NewStatus, DateTime.UtcNow);
        }

        #endregion

        #region Insert(NewTimestampedStatus)

        /// <summary>
        /// Insert a new status entry.
        /// </summary>
        /// <param name="NewTimestampedStatus">A new timestamped status.</param>
        public void Insert(Timestamped<T> NewTimestampedStatus)
        {

            Insert(NewTimestampedStatus.Value,
                   NewTimestampedStatus.Timestamp);

        }

        #endregion

        #region Insert(Value, Timestamp)

        /// <summary>
        /// Insert a new status entry.
        /// </summary>
        /// <param name="Value">The value of the new status entry.</param>
        /// <param name="Timestamp">The timestamp of the new status entry.</param>
        public void Insert(T         Value,
                           DateTime  Timestamp)
        {

            lock (_StatusSchedule)
            {

                // Ignore 'insert' if the values are the same
                if (_StatusSchedule.Count == 0 ||
                    !EqualityComparer<T>.Default.Equals(Value, _StatusSchedule.First().Value))
                {

                    var _OldStatus = _CurrentStatus;

                    // Remove any old status having the same timestamp!
                    var NewStatusSchedule = _StatusSchedule.
                                                Where(status => status.Timestamp.ToIso8601() != Timestamp.ToIso8601()).
                                                ToList();

                    NewStatusSchedule.Add(new Timestamped<T>(Timestamp, Value));

                    _StatusSchedule.Clear();
                    _StatusSchedule.AddRange(NewStatusSchedule.
                                                 OrderByDescending(v => v.Timestamp).
                                                 Take((Int32) MaxStatusHistorySize));

                    // Will call the change-events.
                    CheckCurrentStatus();

                }

            }

        }

        #endregion

        #region Insert(StatusList, ChangeMethod = Replace)

        /// <summary>
        /// Insert the given enumeration of status entries.
        /// </summary>
        /// <param name="StatusList">An enumeration of status entries.</param>
        /// <param name="ChangeMethod">A change method.</param>
        public void Insert(IEnumerable<Timestamped<T>>  StatusList,
                           ChangeMethods                ChangeMethod  = ChangeMethods.Replace)
        {

            lock (_StatusSchedule)
            {

                var _OldStatus = _CurrentStatus;

                switch (ChangeMethod)
                {

                    case ChangeMethods.Insert:
                        break;

                    case ChangeMethods.Replace:
                        _StatusSchedule.Clear();
                        break;

                }

                _StatusSchedule.AddRange(StatusList);

                // Remove any old status having the same timestamp!
                var NewStatusSchedule = _StatusSchedule.
                                            GroupBy(status => status.Timestamp).
                                            Select (group  => group.
                                                                  OrderByDescending(status => status.Timestamp).
                                                                  First()).
                                            OrderByDescending(v => v.Timestamp).
                                            Take((Int32) MaxStatusHistorySize).
                                            ToArray();

                _StatusSchedule.Clear();
                _StatusSchedule.AddRange(NewStatusSchedule);

                CheckCurrentStatus(_OldStatus);

            }

        }

        #endregion


        #region (private) CheckCurrentStatus(OldStatus = null)

        private Timestamped<T> CheckCurrentStatus(Timestamped<T>? OldStatus = null)
        {

            lock (_StatusSchedule)
            {

                var _OldStatus   = OldStatus.HasValue ? OldStatus.Value : _CurrentStatus;
                var Now          = DateTime.UtcNow;

                var HistoryList  = _StatusSchedule.Where(status => status.Timestamp <= Now).ToArray();
                _CurrentStatus   = HistoryList.Any()
                                       ? HistoryList.First()
                                       : new Timestamped<T>(DateTime.UtcNow, default(T));

                var FutureList   = _StatusSchedule.Where(status => status.Timestamp > Now).ToArray();
                _NextStatus      = FutureList.Any()
                                       ? FutureList.Last()
                                       : new Timestamped<T>?();

                if (!EqualityComparer<T>.Default.Equals(_CurrentStatus.Value, _OldStatus.Value))
                    OnStatusChanged?.Invoke(DateTime.UtcNow,
                                            EventTracking_Id.New,
                                            this,
                                            _OldStatus,
                                            _CurrentStatus);

            }

            return _CurrentStatus;

        }

        #endregion


        #region IEnumerable<Timestamped<T>> Members

        /// <summary>
        /// Return a status enumerator.
        /// </summary>
        public IEnumerator<Timestamped<T>> GetEnumerator()
        {

            return _StatusSchedule.
                       OrderByDescending(status => status.Timestamp).
                       GetEnumerator();

        }

        /// <summary>
        /// Return a status enumerator.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {

            return _StatusSchedule.
                       OrderByDescending(status => status.Timestamp).
                       GetEnumerator();

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
            => CurrentStatus.ToString();

        #endregion

    }

}
