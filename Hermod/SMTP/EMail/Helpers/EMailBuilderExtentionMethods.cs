/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.IO;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// Extention methods for e-mail builders.
    /// </summary>
    public static class EMailBuilderExtentionMethods
    {

        #region AddAttachment(this MailBuilder, ByteArray, Filename, ContentType = GlobalMIMETypes.text_plain, ContentLanguage = null)

        /// <summary>
        /// Adds the given array of bytes as a file attachment to the given e-mail.
        /// </summary>
        /// <typeparam name="T">The type of the e-mail builder.</typeparam>
        /// <param name="MailBuilder">An e-mail builder.</param>
        /// <param name="ByteArray">An array of bytes as file attachment.</param>
        /// <param name="Filename">The name of the file within the e-mail attachment.</param>
        /// <param name="ContentType">The content type of the file within the e-mail attachment.</param>
        /// <param name="ContentLanguage">The content language of the file within the e-mail attachment.</param>
        public static T AddAttachment<T>(this T            MailBuilder,
                                         Byte[]            ByteArray,
                                         String            Filename,
                                         MailContentTypes  ContentType      = MailContentTypes.text_plain,
                                         String            ContentLanguage  = null)

            where T : AbstractEMailBuilder

        {

            #region Initial checks

            if (MailBuilder == null)
                throw new ArgumentNullException("The given e-mail builder must not be null!");

            if (ByteArray == null || ByteArray.Length == 0)
                throw new ArgumentNullException("The given byte array name must not be null or empty!");

            if (Filename.IsNullOrEmpty())
                throw new ArgumentNullException("The given file name must not be null or empty!");

            #endregion

            return MailBuilder.AddAttachment<T>(
                new EMailBodypart(ContentTypeBuilder:       AMail => new MailContentType(AMail, ContentType) { CharSet = "utf-8" },
                                  ContentTransferEncoding:  "base64",
                                  ContentLanguage:          ContentLanguage,
                                  Content:                  new String[] { Convert.ToBase64String(ByteArray) }).
                                  SetEMailHeader("Content-Disposition", ContentDispositions.attachment.ToString() + "; filename=\"" + Filename + "\"")  );

        }

        #endregion

        #region AddAttachment(this MailBuilder, FileStream, Filename, ContentType = GlobalMIMETypes.text_plain, ContentLanguage = null)

        /// <summary>
        /// Adds the given file as an attachment to the given e-mail.
        /// </summary>
        /// <typeparam name="T">The type of the e-mail builder.</typeparam>
        /// <param name="MailBuilder">An e-mail builder.</param>
        /// <param name="FileStream">The file attachment as a stream of bytes.</param>
        /// <param name="Filename">The name of the file to add.</param>
        /// <param name="ContentType">The content type of the file to add.</param>
        /// <param name="ContentLanguage">The content language of the file to add.</param>
        public static T AddAttachment<T>(this T            MailBuilder,
                                         Stream            FileStream,
                                         String            Filename,
                                         MailContentTypes  ContentType      = MailContentTypes.text_plain,
                                         String            ContentLanguage  = null)

            where T : AbstractEMailBuilder
        {

            #region Initial checks

            if (MailBuilder == null)
                throw new ArgumentNullException("The given e-mail builder must not be null!");

            if (FileStream == null)
                throw new ArgumentNullException("The given FileStream must not be null!");

            if (Filename.IsNullOrEmpty())
                throw new ArgumentNullException("The given file name must not be null or empty!");

            #endregion

            return MailBuilder.AddAttachment<T>(

                new EMailBodypart(ContentTypeBuilder:       AMail => new MailContentType(AMail, ContentType) { CharSet = "utf-8" },
                                  ContentTransferEncoding:  "base64",
                                  ContentLanguage:          ContentLanguage,
                                  Content:                  new String[] { Convert.ToBase64String("FileStream".ToUTF8Bytes()) }).

                                  SetEMailHeader("Content-Disposition", ContentDispositions.attachment.ToString() + "; filename=\"" + Filename + "\""));

        }

        #endregion

        #region AddAttachment(this MailBuilder, FileInfo, ContentType = GlobalMIMETypes.text_plain, ContentLanguage = null)

        /// <summary>
        /// Adds the given file as an attachment to the given e-mail.
        /// </summary>
        /// <typeparam name="T">The type of the e-mail builder.</typeparam>
        /// <param name="MailBuilder">An e-mail builder.</param>
        /// <param name="FileInfo">The file to add.</param>
        /// <param name="ContentType">The content type of the file to add.</param>
        /// <param name="ContentLanguage">The content language of the file to add.</param>
        public static T AddAttachment<T>(this AbstractEMailBuilder  MailBuilder,
                                         FileInfo                   FileInfo,
                                         MailContentTypes           ContentType      = MailContentTypes.text_plain,
                                         String                     ContentLanguage  = null)

            where T : AbstractEMailBuilder

        {

            #region Initial checks

            if (MailBuilder == null)
                throw new ArgumentNullException("The given e-mail builder must not be null!");

            if (FileInfo == null)
                throw new ArgumentNullException("The given file name must not be null or empty!");

            if (!FileInfo.Exists)
                throw new ArgumentNullException("The given file does not exist!");

            #endregion

            return MailBuilder.AddAttachment<T>(

                new EMailBodypart(ContentTypeBuilder:       AMail => new MailContentType(AMail, ContentType) { CharSet = "utf-8" },
                                  ContentTransferEncoding:  "base64",
                                  ContentLanguage:          ContentLanguage,
                                  Content:                  new String[] { Convert.ToBase64String(File.ReadAllBytes(FileInfo.FullName)) }).

                                  SetEMailHeader("Content-Disposition", ContentDispositions.attachment.ToString() + "; filename=\"" + FileInfo.Name + "\""));

        }

        #endregion

    }

}
