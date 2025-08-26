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

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A mailing list e-mail builder.
    /// </summary>
    public class MailingListEMailBuilder : AbstractEMailBuilder
    {

        #region Properties

        #region ListId

        private ListId? listId;

        /// <summary>
        /// The unique identification of the mailing list.
        /// </summary>
        public ListId? ListId
        {

            get
            {

                if (listId is not null)
                    return listId;

                var listIdString = MailHeaders.
                                       Where(kvp => kvp.Key.Equals("list-id", StringComparison.CurrentCultureIgnoreCase)).
                                       FirstOrDefault();

                if (listIdString.Key is not null)
                {
                    listId = ListId.Parse(listIdString.Value);
                    return listId;
                }

                return null;

            }

            set
            {

                if (value is not null)
                {

                    listId = value;

                    this.SetEMailHeader("List-Id", value.ToString());

                }

            }

        }

        #endregion

        #region ListPost

        private SimpleEMailAddress? listPost;

        /// <summary>
        /// The e-mail address of the mailing list for posting new e-mails.
        /// </summary>
        public SimpleEMailAddress? ListPost
        {

            get
            {

                if (listPost is not null)
                    return listPost;

                var listPostString = MailHeaders.
                                         Where(kvp => kvp.Key.Equals("list-post", StringComparison.CurrentCultureIgnoreCase)).
                                         First();

                if (listPostString.Key != "")
                {
                    listPost = SimpleEMailAddress.Parse(listPostString.Value.Replace("mailto:", ""));
                    return listPost;
                }

                return default(SimpleEMailAddress);

            }

            set
            {

                if (value is not null)
                {

                    listPost = value;

                    this.SetEMailHeader("List-Post", $"<mailto:{value}>");

                }

            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        #region AbstractEMailBuilder()

        /// <summary>
        /// Create a new mailing list e-mail builder.
        /// </summary>
        public MailingListEMailBuilder()
        { }

        #endregion

        #region MailinglistEMailBuilder(EMail)

        /// <summary>
        /// Parse the e-mail from the given e-mail.
        /// </summary>
        /// <param name="EMail">An e-mail.</param>
        public MailingListEMailBuilder(EMail EMail)
            : base(EMail)
        { }

        #endregion

        #region MailinglistEMailBuilder(MailText)

        /// <summary>
        /// Parse the e-mail from the given text lines.
        /// </summary>
        /// <param name="MailText">The E-Mail as an enumeration of strings.</param>
        public MailingListEMailBuilder(IEnumerable<String> MailText)
            : base(MailText)
        { }

        #endregion

        #endregion


        #region (protected, override) _EncodeBodyparts()

        protected override EMailBodypart _EncodeBodyparts()
        {
            return this.Body;
        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"E-Mail '{Subject}' from '{Date}'";

        #endregion

    }

}
