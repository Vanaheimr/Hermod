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

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A list of e-mail addresses with owner names and cryptographic keys.
    /// </summary>
    public class EMailAddressList : IEnumerable<EMailAddress>
    {

        #region Data

        private        readonly EMailAddress[]  mailAddressList = [];

        private static readonly String[]        separators      = [ ",", ";" ];

        #endregion

        #region Properties

        /// <summary>
        /// The number of stored e-mail addresses.
        /// </summary>
        public UInt64 Length
            => (UInt64) mailAddressList.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new e-mail address list.
        /// </summary>
        /// <param name="EMailAddressList">A list of e-mail addresses.</param>
        private EMailAddressList(params EMailAddress[] EMailAddressList)
        {

            if (EMailAddressList is not null && EMailAddressList.Length != 0)
                mailAddressList = EMailAddressList;

        }

        #endregion


        #region Add(params EMailAddresses)

        /// <summary>
        /// Add new e-mail addresses to the e-mail address list.
        /// </summary>
        /// <param name="EMailAddresses">A list of e-mail addresses.</param>
        public EMailAddressList Add(params EMailAddress[] EMailAddresses)
        {

            if (EMailAddresses is not null && EMailAddresses.Length > 0)
                return new EMailAddressList([.. mailAddressList, .. EMailAddresses]);

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

            if (EMailAddresses is not null && EMailAddresses.Any())
                return new EMailAddressList([.. mailAddressList, .. EMailAddresses]);

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

            if (EMailAddressList.Length > 0)
                return new EMailAddressList([.. mailAddressList, .. EMailAddressList]);

            return this;

        }

        #endregion


        public static EMailAddressList Empty
            => new ();

        public static EMailAddressList Create(params EMailAddress[] EMailAddressList)
            => new (EMailAddressList);


        public static EMailAddressList Create(IEnumerable<EMailAddress> EMailAddressList)
            => EMailAddressList is not null && EMailAddressList.Any()
                   ? new EMailAddressList(EMailAddressList.ToArray())
                   : new EMailAddressList();


        public static EMailAddressList Parse(String EMailAddressListString)

            => new (EMailAddressListString.
                        Split (separators, StringSplitOptions.None).
                        Select(textaddr  => EMailAddress.Parse(textaddr.Trim())).
                        Where (addresses => addresses is not null).
                        Cast<EMailAddress>().
                        ToArray());


        #region Implicitly convert EMailAddress            -> EMailAddressList

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddress.
        /// </summary>
        /// <param name="EMailAddress">An e-mail address.</param>
        /// <returns>A new e-mail address list.</returns>
        public static implicit operator EMailAddressList(EMailAddress EMailAddress)

            => new ([ EMailAddress ]);

        #endregion

        #region Implicitly convert SimpleEMailAddress      -> EMailAddressList

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddressList.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <returns>A new e-mail address list.</returns>
        public static implicit operator EMailAddressList(SimpleEMailAddress SimpleEMailAddress)

            => new ([ SimpleEMailAddress ]);

        #endregion

        #region Implicitly convert EMailAddressListBuilder -> EMailAddressList

        public static implicit operator EMailAddressList(EMailAddressListBuilder Builder)

            => new (Builder.ToArray());

        #endregion


        #region GetEnumerator()

        /// <summary>
        /// Return an enumerator for the list of e-mail addresses.
        /// </summary>
        public IEnumerator<EMailAddress> GetEnumerator()
        {
            foreach (var emailaddress in mailAddressList)
                yield return emailaddress;
        }

        /// <summary>
        /// Return an enumerator for the list of e-mail addresses.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return mailAddressList.GetEnumerator();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            if (mailAddressList is null || mailAddressList.Length == 0)
                return String.Empty;

            return mailAddressList.
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

        private        readonly List<EMailAddress>  mailAddressList  = [];
        private static readonly String[]            separators       = [ ",", ";" ];

        #endregion

        #region Properties

        /// <summary>
        /// The number of stored e-mail addresses.
        /// </summary>
        public UInt64 Length
            => (UInt64) mailAddressList.Count;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new e-mail address list.
        /// </summary>
        /// <param name="EMailAddressList">A list of e-mail addresses.</param>
        private EMailAddressListBuilder(IEnumerable<EMailAddress>? EMailAddressList = null)
        {

            if (EMailAddressList is not null && EMailAddressList.Any())
                mailAddressList.AddRange(EMailAddressList);

        }

        #endregion


        public static EMailAddressListBuilder Empty
            => new ();

        public static EMailAddressListBuilder Create(params EMailAddress[] EMailAddressListBuilder)
            => new (EMailAddressListBuilder);

        public static EMailAddressListBuilder Create(IEnumerable<EMailAddress> EMailAddressListBuilder)
            => new (EMailAddressListBuilder);


        #region Add(params EMailAddresses)

        /// <summary>
        /// Add new e-mail addresses to the e-mail address list.
        /// </summary>
        /// <param name="EMailAddresses">A list of e-mail addresses.</param>
        public EMailAddressListBuilder Add(params EMailAddress[] EMailAddresses)
        {

            if (EMailAddresses is not null && EMailAddresses.Length > 0)
                mailAddressList.AddRange(EMailAddresses);

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

            if (EMailAddresses is not null && EMailAddresses.Any())
                mailAddressList.AddRange(EMailAddresses);

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

            if (EMailAddressListBuilder.Length > 0)
                mailAddressList.AddRange(EMailAddressListBuilder.mailAddressList);

            return this;

        }

        #endregion

        #region CLear()

        /// <summary>
        /// Removes all e-mail addresses from the list.
        /// </summary>
        public EMailAddressListBuilder Clear()
        {

            mailAddressList.Clear();

            return this;

        }

        #endregion



        public static EMailAddressListBuilder Parse(String EMailAddressListBuilderString)

            => new (EMailAddressListBuilderString.
                        Split (separators, StringSplitOptions.None).
                        Select(textaddr  => EMailAddress.Parse(textaddr.Trim())).
                        Where (addresses => addresses is not null).
                        Cast<EMailAddress>().
                        ToArray());




        #region Implicitly convert EMailAddress       -> EMailAddressListBuilder

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddress.
        /// </summary>
        /// <param name="EMailAddress">An e-mail address.</param>
        /// <returns>A new e-mail address list.</returns>
        public static implicit operator EMailAddressListBuilder(EMailAddress EMailAddress)

            => new ([ EMailAddress ]);

        #endregion

        #region Implicitly convert SimpleEMailAddress -> EMailAddressListBuilder

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddressListBuilder.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <returns>A new e-mail address list.</returns>
        public static implicit operator EMailAddressListBuilder(SimpleEMailAddress SimpleEMailAddress)

            => new ([ SimpleEMailAddress ]);

        #endregion

        #region Implicitly convert EMailAddressList   -> EMailAddressListBuilder

        public static implicit operator EMailAddressListBuilder(EMailAddressList List)

            => new (List);

        #endregion


        #region GetEnumerator()

        /// <summary>
        /// Return an enumerator for the list of e-mail addresses.
        /// </summary>
        public IEnumerator<EMailAddress> GetEnumerator()
        {
            foreach (var emailaddress in mailAddressList)
                yield return emailaddress;
        }

        /// <summary>
        /// Return an enumerator for the list of e-mail addresses.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return mailAddressList.GetEnumerator();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            if (mailAddressList is null || mailAddressList.Count == 0)
                return String.Empty;

            return mailAddressList.
                       Select(EMA => EMA.OwnerName.IsNotNullOrEmpty()
                                        ? EMA.OwnerName + " <" + EMA.Address.Value + ">"
                                        : "<" + EMA.Address.Value + ">").
                       AggregateWith(", ").
                       Trim();

        }

        #endregion

    }

}
