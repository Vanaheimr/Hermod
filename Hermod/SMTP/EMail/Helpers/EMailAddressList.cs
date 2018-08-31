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
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A list of e-mail addresses with owner names and cryptographic keys.
    /// </summary>
    public class EMailAddressList : IEnumerable<EMailAddress>
    {

        #region Data

        private readonly EMailAddress[] _MailAddressList;

        #endregion

        #region Properties

        /// <summary>
        /// The number of stored e-mail addresses.
        /// </summary>
        public UInt64 Length
        {
            get
            {
                return (UInt64) _MailAddressList.Length;
            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new e-mail address list.
        /// </summary>
        /// <param name="EMailAddressList">A list of e-mail addresses.</param>
        private EMailAddressList(EMailAddress[] EMailAddressList = null)
        {

            _MailAddressList = EMailAddressList ?? new EMailAddress[0];

            if (_MailAddressList.Length > 0)
                _MailAddressList = _MailAddressList.
                                        Where(address => address != null).
                                        ToArray();

        }

        #endregion


        #region Add(params EMailAddresses)

        /// <summary>
        /// Add new e-mail addresses to the e-mail address list.
        /// </summary>
        /// <param name="EMailAddresses">A list of e-mail addresses.</param>
        public EMailAddressList Add(params EMailAddress[] EMailAddresses)
        {

            if (EMailAddresses != null && EMailAddresses.Length > 0)
            {
                var _List = new List<EMailAddress>(_MailAddressList);
                _List.AddRange(EMailAddresses);
                return new EMailAddressList(_List.ToArray());
            }

            return this;

        }

        #endregion

        #region Add(EMailAddresses)

        /// <summary>
        /// Add new e-mail addresses to the e-mail address list.
        /// </summary>
        /// <param name="EMailAddresses">An enumeration of e-mail addresses.</param>
        public EMailAddressList Add(IEnumerable<EMailAddress> EMailAddresses)
        {

            if (EMailAddresses != null && EMailAddresses.Any())
            {
                var _List = new List<EMailAddress>(_MailAddressList);
                _List.AddRange(EMailAddresses);
                return new EMailAddressList(_List.ToArray());
            }

            return this;

        }

        #endregion

        #region Add(EMailAddressList)

        /// <summary>
        /// Add another e-mail address list to the e-mail address list.
        /// </summary>
        /// <param name="EMailAddressList">A e-mail addresses list.</param>
        public EMailAddressList Add(EMailAddressList EMailAddressList)
        {

            if (EMailAddressList._MailAddressList.Length > 0)
            {
                var _List = new List<EMailAddress>(_MailAddressList);
                _List.AddRange(EMailAddressList._MailAddressList);
                return new EMailAddressList(_List.ToArray());
            }

            return this;

        }

        #endregion


        public static EMailAddressList Empty

            => new EMailAddressList();


        public static EMailAddressList Create(params EMailAddress[] EMailAddressList)

            => new EMailAddressList(EMailAddressList);


        public static EMailAddressList Create(IEnumerable<EMailAddress> EMailAddressList)

            => EMailAddressList != null && EMailAddressList.Any()
                   ? new EMailAddressList(EMailAddressList.ToArray())
                   : new EMailAddressList();


        public static EMailAddressList Parse(String EMailAddressListString)

            => new EMailAddressList(EMailAddressListString.
                                        Split (new String[] { ",", ";" }, StringSplitOptions.None).
                                        Select(textaddr  => EMailAddress.Parse(textaddr.Trim())).
                                        Where (addresses => addresses != null).
                                        ToArray());


        #region Implicitly convert EMailAddress            -> EMailAddressList

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddress.
        /// </summary>
        /// <param name="EMailAddress">An e-mail address.</param>
        /// <returns>A new e-mail address list.</returns>
        public static implicit operator EMailAddressList(EMailAddress EMailAddress)

            => new EMailAddressList(new EMailAddress[] { EMailAddress });

        #endregion

        #region Implicitly convert SimpleEMailAddress      -> EMailAddressList

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddressList.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <returns>A new e-mail address list.</returns>
        public static implicit operator EMailAddressList(SimpleEMailAddress SimpleEMailAddress)

            => new EMailAddressList(new EMailAddress[] { SimpleEMailAddress });

        #endregion

        #region Implicitly convert EMailAddressListBuilder -> EMailAddressList

        public static implicit operator EMailAddressList(EMailAddressListBuilder Builder)

            => new EMailAddressList(Builder.ToArray());

        #endregion


        #region GetEnumerator()

        /// <summary>
        /// Return an enumerator for the list of e-mail addresses.
        /// </summary>
        public IEnumerator<EMailAddress> GetEnumerator()
        {
            foreach (var emailaddress in _MailAddressList)
                yield return emailaddress;
        }

        /// <summary>
        /// Return an enumerator for the list of e-mail addresses.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _MailAddressList.GetEnumerator();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            if (_MailAddressList == null || _MailAddressList.Length == 0)
                return String.Empty;

            return _MailAddressList.
                       Select(EMA => EMA.OwnerName.IsNotNullOrEmpty()
                                        ? EMA.OwnerName + " <" + EMA.Address.Value + ">"
                                        : "<" + EMA.Address.Value + ">").
                       AggregateWith(", ").
                       Trim();

        }

        #endregion

    }


    /// <summary>
    /// A list of e-mail addresses with owner names and cryptographic keys.
    /// </summary>
    public class EMailAddressListBuilder : IEnumerable<EMailAddress>
    {

        #region Data

        private readonly List<EMailAddress> _MailAddressList;

        #endregion

        #region Properties

        /// <summary>
        /// The number of stored e-mail addresses.
        /// </summary>
        public UInt64 Length
        {
            get
            {
                return (UInt64) _MailAddressList.Count;
            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new e-mail address list.
        /// </summary>
        /// <param name="EMailAddressList">A list of e-mail addresses.</param>
        private EMailAddressListBuilder(IEnumerable<EMailAddress> EMailAddressList = null)
        {
            this._MailAddressList = EMailAddressList != null
                                        ? new List<EMailAddress>(EMailAddressList)
                                        : new List<EMailAddress>();
        }

        #endregion


        public static EMailAddressListBuilder Empty
            => new EMailAddressListBuilder();

        public static EMailAddressListBuilder Create(params EMailAddress[] EMailAddressListBuilder)

            => new EMailAddressListBuilder(EMailAddressListBuilder);

        public static EMailAddressListBuilder Create(IEnumerable<EMailAddress> EMailAddressListBuilder)

            => EMailAddressListBuilder != null && EMailAddressListBuilder.Any()
                   ? new EMailAddressListBuilder(EMailAddressListBuilder.ToArray())
                   : new EMailAddressListBuilder();


        #region Add(params EMailAddresses)

        /// <summary>
        /// Add new e-mail addresses to the e-mail address list.
        /// </summary>
        /// <param name="EMailAddresses">A list of e-mail addresses.</param>
        public EMailAddressListBuilder Add(params EMailAddress[] EMailAddresses)
        {

            if (EMailAddresses != null && EMailAddresses.Length > 0)
                _MailAddressList.AddRange(EMailAddresses);

            return this;

        }

        #endregion

        #region Add(EMailAddresses)

        /// <summary>
        /// Add new e-mail addresses to the e-mail address list.
        /// </summary>
        /// <param name="EMailAddresses">An enumeration of e-mail addresses.</param>
        public EMailAddressListBuilder Add(IEnumerable<EMailAddress> EMailAddresses)
        {

            if (EMailAddresses != null && EMailAddresses.Any())
                _MailAddressList.AddRange(EMailAddresses);

            return this;

        }

        #endregion

        #region Add(EMailAddressListBuilder)

        /// <summary>
        /// Add another e-mail address list to the e-mail address list.
        /// </summary>
        /// <param name="EMailAddressListBuilder">A e-mail addresses list.</param>
        public EMailAddressListBuilder Add(EMailAddressListBuilder EMailAddressListBuilder)
        {

            if (EMailAddressListBuilder._MailAddressList.Count > 0)
                _MailAddressList.AddRange(EMailAddressListBuilder._MailAddressList);

            return this;

        }

        #endregion

        #region CLear()

        /// <summary>
        /// Removes all e-mail addresses from the list.
        /// </summary>
        public EMailAddressListBuilder Clear()
        {

            this._MailAddressList.Clear();

            return this;

        }

        #endregion



        public static EMailAddressListBuilder Parse(String EMailAddressListBuilderString)

            => new EMailAddressListBuilder(EMailAddressListBuilderString.
                                        Split (new String[] { ",", ";" }, StringSplitOptions.None).
                                        Select(textaddr  => EMailAddress.Parse(textaddr.Trim())).
                                        Where (addresses => addresses != null).
                                        ToArray());




        #region Implicitly convert EMailAddress       -> EMailAddressListBuilder

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddress.
        /// </summary>
        /// <param name="EMailAddress">An e-mail address.</param>
        /// <returns>A new e-mail address list.</returns>
        public static implicit operator EMailAddressListBuilder(EMailAddress EMailAddress)

            => new EMailAddressListBuilder(new EMailAddress[] { EMailAddress });

        #endregion

        #region Implicitly convert SimpleEMailAddress -> EMailAddressListBuilder

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddressListBuilder.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <returns>A new e-mail address list.</returns>
        public static implicit operator EMailAddressListBuilder(SimpleEMailAddress SimpleEMailAddress)

            => new EMailAddressListBuilder(new EMailAddress[] { SimpleEMailAddress });

        #endregion

        #region Implicitly convert EMailAddressList   -> EMailAddressListBuilder

        public static implicit operator EMailAddressListBuilder(EMailAddressList List)

            => new EMailAddressListBuilder(List.ToArray());

        #endregion


        #region GetEnumerator()

        /// <summary>
        /// Return an enumerator for the list of e-mail addresses.
        /// </summary>
        public IEnumerator<EMailAddress> GetEnumerator()
        {
            foreach (var emailaddress in _MailAddressList)
                yield return emailaddress;
        }

        /// <summary>
        /// Return an enumerator for the list of e-mail addresses.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _MailAddressList.GetEnumerator();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            if (_MailAddressList == null || _MailAddressList.Count == 0)
                return String.Empty;

            return _MailAddressList.
                       Select(EMA => EMA.OwnerName.IsNotNullOrEmpty()
                                        ? EMA.OwnerName + " <" + EMA.Address.Value + ">"
                                        : "<" + EMA.Address.Value + ">").
                       AggregateWith(", ").
                       Trim();

        }

        #endregion

    }

}
