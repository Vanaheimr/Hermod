/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    /// <summary>
    /// A list of e-mail addresses with owner names and cryptographic keys.
    /// </summary>
    public class EMailAddressList : IEnumerable<EMailAddress>
    {

        #region Data

        private readonly List<EMailAddress> _MailAddressList;

        #endregion

        #region Constructor(s)

        #region EMailAddressList(EMailAddressList)

        /// <summary>
        /// Create a new e-mail address list.
        /// </summary>
        /// <param name="EMailAddressList">Another e-mail address list.</param>
        public EMailAddressList(EMailAddressList EMailAddressList)
        {
            this._MailAddressList = new List<EMailAddress>(EMailAddressList);
        }

        #endregion

        #region EMailAddressList(EMailAddressList)

        /// <summary>
        /// Create a new e-mail address list.
        /// </summary>
        /// <param name="EMailAddressList">A list of e-mail addresses.</param>
        public EMailAddressList(params EMailAddress[] EMailAddressList)
        {

            this._MailAddressList = EMailAddressList != null
                                        ? new List<EMailAddress>(EMailAddressList)
                                        : new List<EMailAddress>();

        }

        #endregion

        #region EMailAddressList(EMailAddressList)

        /// <summary>
        /// Create a new e-mail address list.
        /// </summary>
        /// <param name="EMailAddressList">A list of e-mail addresses.</param>
        public EMailAddressList(IEnumerable<EMailAddress> EMailAddressList)
        {
            this._MailAddressList = new List<EMailAddress>(EMailAddressList);
        }

        #endregion

        #endregion


        #region Add(params EMailAddresses)

        /// <summary>
        /// Add new e-mail addresses to the e-mail address list.
        /// </summary>
        /// <param name="EMailAddresses">A list of e-mail addresses.</param>
        public EMailAddressList Add(params EMailAddress[] EMailAddresses)
        {

            if (EMailAddresses != null)
                _MailAddressList.AddRange(EMailAddresses);

            return this;

        }

        #endregion

        #region Add(MailAddresses)

        /// <summary>
        /// Add new e-mail addresses to the e-mail address list.
        /// </summary>
        /// <param name="MailAddresses">An enumeration of e-mail addresses.</param>
        public EMailAddressList Add(IEnumerable<EMailAddress> MailAddresses)
        {

            _MailAddressList.AddRange(MailAddresses);

            return this;

        }

        #endregion

        #region Add(MailAddressList)

        /// <summary>
        /// Add another e-mail address list to the e-mail address list.
        /// </summary>
        /// <param name="MailAddresses">A e-mail addresses list.</param>
        public EMailAddressList Add(EMailAddressList MailAddressList)
        {

            _MailAddressList.AddRange(MailAddressList._MailAddressList);

            return this;

        }

        #endregion


        #region Clear()

        /// <summary>
        /// Removes all elements from the e-mail address list.
        /// </summary>
        public EMailAddressList Clear()
        {

            _MailAddressList.Clear();

            return this;

        }

        #endregion


        #region Implicitly convert EMailAddress -> EMailAddressList

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddress.
        /// </summary>
        /// <param name="EMailAddress">An e-mail address.</param>
        /// <returns>A new e-mail address list.</returns>
        public static implicit operator EMailAddressList(EMailAddress EMailAddress)
        {
            return new EMailAddressList(EMailAddress);
        }

        #endregion

        #region Implicitly convert SimpleEMailAddress -> EMailAddressList

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddressList.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <returns>A new e-mail address list.</returns>
        public static implicit operator EMailAddressList(SimpleEMailAddress SimpleEMailAddress)
        {
            return new EMailAddressList(SimpleEMailAddress);
        }

        #endregion


        #region GetEnumerator()

        /// <summary>
        /// Return an enumerator for the list of e-mail addresses.
        /// </summary>
        public IEnumerator<EMailAddress> GetEnumerator()
        {
            return _MailAddressList.GetEnumerator();
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
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {

            if (_MailAddressList == null || !_MailAddressList.Any())
                return String.Empty;

            return _MailAddressList.
                       Select(EMA => EMA.OwnerName.IsNotNullOrEmpty()
                                        ? EMA.OwnerName + " <" + EMA.Address.Value + ">"
                                        : "<" + EMA.Address.Value + ">").
                       Aggregate((a, b) => a + ", " + b).
                       Trim();

        }

        #endregion

    }

}
