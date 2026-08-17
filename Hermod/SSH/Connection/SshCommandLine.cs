/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Safe POSIX-shell argument quoting for composing remote command lines. The remote <c>exec</c> string is
    /// interpreted by the peer's shell, so any argument carrying spaces, quotes, <c>$</c>, backticks, globs or
    /// newlines must be quoted to survive intact — this is the injection boundary for remote command building.
    /// </summary>
    public static class SshCommandLine
    {

        // Characters that never need quoting in a POSIX shell.
        private const String Safe = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_@%+=:,./-";

        #region Quote(Argument)

        /// <summary>
        /// Quote a single argument for a POSIX shell. Simple arguments are returned unchanged; everything else
        /// is single-quoted (with embedded single quotes rendered as <c>'\''</c>), which disables all shell
        /// interpretation of the contents.
        /// </summary>
        public static String Quote(String Argument)
        {

            if (Argument.Length == 0)
                return "''";

            var needsQuoting = false;
            foreach (var c in Argument)
                if (Safe.IndexOf(c) < 0)
                {
                    needsQuoting = true;
                    break;
                }

            if (!needsQuoting)
                return Argument;

            var builder = new StringBuilder(Argument.Length + 2);
            builder.Append('\'');
            foreach (var c in Argument)
            {
                if (c == '\'')
                    builder.Append("'\\''");   // close quote, escaped literal ', reopen quote
                else
                    builder.Append(c);
            }
            builder.Append('\'');

            return builder.ToString();

        }

        #endregion

        #region Join(Arguments)

        /// <summary>
        /// Quote and space-join arguments into a single command line safe for a remote <c>exec</c>.
        /// </summary>
        public static String Join(params String[] Arguments)
            => String.Join(' ', Arguments.Select(Quote));

        /// <summary>
        /// Quote and space-join arguments into a single command line safe for a remote <c>exec</c>.
        /// </summary>
        public static String Join(IEnumerable<String> Arguments)
            => String.Join(' ', Arguments.Select(Quote));

        #endregion

    }

}
