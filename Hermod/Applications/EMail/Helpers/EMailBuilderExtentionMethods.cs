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
using System.IO;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    public static class EMailBuilderExtentionMethods
    {

        #region AddAttachment(ByteArray, Filename = null, ContentType = GlobalMIMETypes.text_plain, ContentLanguage = null)

        public static AbstractEMailBuilder AddAttachment(this AbstractEMailBuilder  MailBuilder,
                                                         Byte[]                     ByteArray,
                                                         String                     Filename         = null,
                                                         MailContentTypes            ContentType      = MailContentTypes.text_plain,
                                                         String                     ContentLanguage  = null)
        {

            return MailBuilder.AddAttachment(
                new EMailBodypart(ContentType:              ContentType,
                                  ContentTransferEncoding:  "base64",
                                  Charset:                  "utf-8",
                                  ContentLanguage:          ContentLanguage,
                                  AdditionalHeaders:        new List<KeyValuePair<String, String>>() {
                                                                new KeyValuePair<String, String>("Content-Disposition", ContentDispositions.attachment.ToString() + "; filename=\"" + Filename + "\"")
                                                            },
                                  Content:                  new MailBodyString(Convert.ToBase64String(ByteArray))));

        }

        #endregion

        #region AddAttachment(FileStream, Filename = null, ContentType = GlobalMIMETypes.text_plain, ContentLanguage = null)

        public static AbstractEMailBuilder AddAttachment(this AbstractEMailBuilder  MailBuilder,
                                                         Stream                     FileStream,
                                                         String                     Filename         = null,
                                                         MailContentTypes            ContentType      = MailContentTypes.text_plain,
                                                         String                     ContentLanguage  = null)
        {

            return MailBuilder.AddAttachment(
                new EMailBodypart(ContentType:              ContentType,
                                  ContentTransferEncoding:  "base64",
                                  Charset:                  "utf-8",
                                  ContentLanguage:          ContentLanguage,
                                  AdditionalHeaders:        new List<KeyValuePair<String, String>>() {
                                                                new KeyValuePair<String, String>("Content-Disposition", ContentDispositions.attachment.ToString() + "; filename=\"" + Filename + "\"")
                                                            },
                                  Content:                  new MailBodyString(Convert.ToBase64String("FileStream".ToUTF8Bytes()))));

        }

        #endregion

        #region AddAttachment(FileInfo, Filename = null, ContentType = GlobalMIMETypes.text_plain, ContentLanguage = null)

        public static AbstractEMailBuilder AddAttachment(this AbstractEMailBuilder  MailBuilder,
                                                         FileInfo                   FileInfo,
                                                         String                     Filename         = null,
                                                         MailContentTypes            ContentType      = MailContentTypes.text_plain,
                                                         String                     ContentLanguage  = null)
        {

            return MailBuilder.AddAttachment(
                new EMailBodypart(ContentType:              ContentType,
                                  ContentTransferEncoding:  "base64",
                                  Charset:                  "utf-8",
                                  ContentLanguage:          ContentLanguage,
                                  AdditionalHeaders:        new List<KeyValuePair<String, String>>() {
                                                                new KeyValuePair<String, String>("Content-Disposition", ContentDispositions.attachment.ToString() + "; filename=\"" + Filename + "\"")
                                                            },
                                  Content:                  new MailBodyString(Convert.ToBase64String(File.ReadAllBytes(FileInfo.FullName)))));

        }

        #endregion

    }

}
