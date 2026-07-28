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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;

/// <summary>
/// Key log in the NSS key log format — the file Wireshark reads to decrypt our own QUIC traffic
/// (as do curl, Firefox and Chrome). One line per derived secret:
/// <code>
/// &lt;LABEL&gt; &lt;client_random in hex&gt; &lt;secret in hex&gt;
/// </code>
/// The client random of the ClientHello identifies the connection; the labels are those of TLS 1.3
/// (RFC 8446 §7.1), see the constants below.
/// <para>
/// <b>SECURITY:</b> this writes the traffic secrets of a connection IN PLAIN TEXT. Anyone with the
/// file can decrypt the recorded traffic — and with <see cref="ClientEarlyTrafficSecret"/> even
/// replay 0-RTT data. It is therefore deliberately NOT enabled by an environment variable alone:
/// the application has to pass a key log explicitly. <see cref="FromEnvironment"/> merely provides
/// the customary <c>SSLKEYLOGFILE</c> convention for those who want it — use it for debugging and
/// tests only, never in production.
/// </para>
/// </summary>
public sealed class KeyLog
{
    /// <summary>
    /// Environment variable of the customary convention (curl/Firefox/Chrome/NSS).
    /// </summary>
    public const string EnvironmentVariable = "SSLKEYLOGFILE";

    // Labels of the NSS key log format (the TLS 1.3 secrets of RFC 8446 §7.1).
    public const string ClientEarlyTrafficSecret = "CLIENT_EARLY_TRAFFIC_SECRET";
    public const string ClientHandshakeTrafficSecret = "CLIENT_HANDSHAKE_TRAFFIC_SECRET";
    public const string ServerHandshakeTrafficSecret = "SERVER_HANDSHAKE_TRAFFIC_SECRET";
    public const string ClientTrafficSecret0 = "CLIENT_TRAFFIC_SECRET_0";
    public const string ServerTrafficSecret0 = "SERVER_TRAFFIC_SECRET_0";
    public const string ExporterSecret = "EXPORTER_SECRET";

    private readonly Action<string> _writeLine;
    private readonly Lock _lock = new();

    /// <summary>
    /// A key log with an arbitrary sink — the line arrives WITHOUT a trailing newline. Used by the
    /// tests to check the format without touching the file system.
    /// </summary>
    public KeyLog(Action<string> lineSink)
    {
        _writeLine = lineSink;
    }

    /// <summary>
    /// Appends to a file (created if missing). Every line is flushed individually so a recording
    /// stays usable even if the process is killed.
    /// </summary>
    public static KeyLog ToFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new KeyLog(line => File.AppendAllText(path, line + Environment.NewLine));
    }

    /// <summary>
    /// The <c>SSLKEYLOGFILE</c> convention: returns a file key log when the variable is set,
    /// otherwise <c>null</c>. Deliberately NOT evaluated automatically anywhere — the application
    /// has to call this and pass the result on, so secrets can never end up on disk unnoticed.
    /// </summary>
    public static KeyLog? FromEnvironment(string variable = EnvironmentVariable)
        => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } path ? ToFile(path) : null;

    /// <summary>
    /// Writes one line. Empty secrets or client randoms are skipped (nothing to log yet).
    /// </summary>
    public void Write(string label, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> secret)
    {
        if (clientRandom.IsEmpty || secret.IsEmpty)
            return;

        string line = $"{label} {Convert.ToHexStringLower(clientRandom)} {Convert.ToHexStringLower(secret)}";
        lock (_lock)
            _writeLine(line);
    }
}
