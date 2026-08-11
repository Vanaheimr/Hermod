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

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using NUnit.Framework;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// What a peer inside WSL reported back after driving our server.
    /// </summary>
    /// <param name="Ok">Whether the peer completed the requested operation.</param>
    /// <param name="Error">The peer's error message when it did not.</param>
    /// <param name="ErrorType">The peer's exception type name — lets a test assert *how* it failed.</param>
    /// <param name="Stage">How far the session got — the first thing to look at when a peer stalls.</param>
    /// <param name="StdOut">Command output the peer received from us.</param>
    /// <param name="ExitStatus">The exit status the peer saw.</param>
    /// <param name="Listing">Directory entries the peer read over SFTP.</param>
    /// <param name="Algorithms">Best-effort view of what the peer thinks it negotiated (diagnostic only).</param>
    /// <param name="PeerVersion">The peer library's own version, recorded in the interop evidence.</param>
    /// <param name="ServerVersion">Our identification string as the peer saw it.</param>
    public sealed record PeerRunResult(

        [property: JsonPropertyName("ok")]             Boolean                             Ok,
        [property: JsonPropertyName("error")]          String?                             Error,
        [property: JsonPropertyName("error_type")]     String?                             ErrorType,
        [property: JsonPropertyName("stage")]          String?                             Stage,
        [property: JsonPropertyName("stdout")]         String?                             StdOut,
        [property: JsonPropertyName("stderr")]         String?                             StdErr,
        [property: JsonPropertyName("exit_status")]    Int32?                              ExitStatus,
        [property: JsonPropertyName("listing")]        String[]?                           Listing,
        [property: JsonPropertyName("algorithms")]     Dictionary<String, String?>?        Algorithms,
        [property: JsonPropertyName("peer_version")]   String?                             PeerVersion,
        [property: JsonPropertyName("server_version")] String?                             ServerVersion,
        [property: JsonPropertyName("debug_log")]      String[]?                           DebugLog

    )
    {

        /// <summary>
        /// A failure report carrying the peer's own protocol trace — an interop failure is a disagreement
        /// about the wire, and the peer's view shows who stopped talking first.
        /// </summary>
        public String FailureReport
            => $"{ErrorType}: {Error} (stage '{Stage}')" +
               (DebugLog is { Length: > 0 }
                    ? $"\n--- peer trace ---\n{String.Join("\n", DebugLog.TakeLast(60))}"
                    : "");

        /// <summary>A one-line rendering of the negotiated algorithms for the test log.</summary>
        public String AlgorithmSummary
            => Algorithms is null or { Count: 0 }
                   ? "(peer exposes no negotiated-algorithm information)"
                   : String.Join(", ", Algorithms.Where(kv => kv.Value is not null).Select(kv => $"{kv.Key}={kv.Value}"));

    }


    /// <summary>
    /// Collects our own server's audit events during an interop test.
    ///
    /// <para>
    /// A peer can only ever report that the conversation stopped, never why — when the reason is on our
    /// side, this is where it shows up. It earned its place finding exactly that: an AsyncSSH login died
    /// with a bare timeout, and our own audit trail named the cause.
    /// </para>
    /// </summary>
    public sealed class RecordingAuditSink : ISshAuditSink
    {

        private readonly List<SshAuditEvent> events = [];

        public ValueTask WriteAsync(SshAuditEvent Event, CancellationToken CancellationToken = default)
        {
            lock (events)
                events.Add(Event);
            return ValueTask.CompletedTask;
        }

        /// <summary>Everything our server logged, for a failure message.</summary>
        public String Report
        {
            get
            {
                lock (events)
                    return events.Count == 0
                               ? "(our server logged no audit events)"
                               : String.Join("\n", events.Select(e => "   " + e));
            }
        }

    }


    /// <summary>
    /// Drives the interop peers that only exist inside Linux — AsyncSSH, Paramiko, Dropbear, TinySSH —
    /// from the Windows-hosted NUnit process through <c>wsl.exe</c>.
    ///
    /// <para>
    /// Two environment facts shape everything here. First, the peers are provisioned by
    /// <c>interop/setup-wsl.sh</c>, so anything missing is a *setup* problem and must
    /// <see cref="Assert.Ignore(String)"/> with a precise message rather than fail — an unprovisioned
    /// machine has produced no evidence either way. Second, under WSL's default NAT networking a server
    /// hosted on Windows is <b>not</b> reachable at <c>127.0.0.1</c> from inside WSL: the host answers on
    /// the default gateway address. Tests therefore bind to <c>IPv4Address.Any</c> and ask
    /// <see cref="ResolveWindowsHostAsync"/> which address the peer must dial — it probes rather than
    /// assumes, so mirrored networking (where <c>localhost</c> does work) is handled too.
    /// </para>
    /// </summary>
    public static class WslInterop
    {

        #region Data

        private static readonly JsonSerializerOptions jsonOptions = new () { PropertyNameCaseInsensitive = true };

        private static readonly Lazy<(String? InteropDir, String? VenvPython, String? Reason)> harness = new (Locate);

        // The reachable address does not change during a run, so probe once.
        private static String? resolvedHost;
        private static readonly SemaphoreSlim resolveLock = new (1, 1);

        #endregion

        #region Properties

        /// <summary>The source-tree <c>SSH/interop</c> directory holding the venv and the peer drivers.</summary>
        public static String? InteropDirectory
            => harness.Value.InteropDir;

        /// <summary>Why the WSL harness cannot be used on this machine, or <c>null</c> when it can.</summary>
        public static String? UnavailableReason
            => harness.Value.Reason;

        #endregion


        #region (private, static) Locate()

        /// <summary>
        /// Find the interop directory and the Python virtual environment, reporting precisely what is
        /// missing so a skipped test says something actionable.
        /// </summary>
        private static (String?, String?, String?) Locate()
        {

            if (!OperatingSystem.IsWindows())
                return (null, null, "The WSL harness only applies on Windows.");

            // Walk up from the test binaries to the project directory that owns SSH/interop.
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "SSH", "interop")))
                directory = directory.Parent;

            if (directory is null)
                return (null, null, "Could not locate the 'SSH/interop' source directory from the test output directory.");

            var interopDirectory = Path.Combine(directory.FullName, "SSH", "interop");
            var venvPython       = Path.Combine(interopDirectory, ".venv-interop", "bin", "python3");

            if (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe")))
                return (interopDirectory, null, "wsl.exe not found — the Linux-only interop peers need WSL2.");

            if (!File.Exists(venvPython))
                return (interopDirectory, null,
                        $"The Python interop peers are not provisioned: '{venvPython}' is missing. " +
                         "Run interop/setup-wsl.sh from a WSL2 shell.");

            return (interopDirectory, venvPython, null);

        }

        #endregion

        #region SkipIfUnavailable()

        /// <summary>Ignore the calling test when the WSL harness is not provisioned on this machine.</summary>
        public static void SkipIfUnavailable()
        {
            if (UnavailableReason is not null)
                Assert.Ignore(UnavailableReason);
        }

        #endregion


        #region ToWslPath(WindowsPath)

        /// <summary>Translate <c>C:\dir\file</c> into the <c>/mnt/c/dir/file</c> WSL sees.</summary>
        public static String ToWslPath(String WindowsPath)
        {

            var full = Path.GetFullPath(WindowsPath);

            if (full.Length < 2 || full[1] != ':')
                throw new ArgumentException($"'{WindowsPath}' is not an absolute Windows path.", nameof(WindowsPath));

            return $"/mnt/{Char.ToLowerInvariant(full[0])}{full[2..].Replace('\\', '/')}";

        }

        #endregion

        #region RunAsync(Arguments, CancellationToken)

        /// <summary>
        /// Run a command inside WSL. Arguments are passed through <see cref="ProcessStartInfo.ArgumentList"/>,
        /// so nothing has to be shell-escaped by the caller.
        /// </summary>
        public static async Task<(Int32 ExitCode, String StdOut, String StdErr)> RunAsync(IEnumerable<String>  Arguments,
                                                                                          CancellationToken    CancellationToken)
        {

            var startInfo = new ProcessStartInfo("wsl.exe") {
                                RedirectStandardOutput  = true,
                                RedirectStandardError   = true,
                                UseShellExecute         = false,
                                CreateNoWindow          = true,
                                StandardOutputEncoding  = Encoding.UTF8,
                                StandardErrorEncoding   = Encoding.UTF8
                            };

            foreach (var argument in Arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo)
                                    ?? throw new InvalidOperationException("Could not start wsl.exe.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken);
            var stderrTask = process.StandardError. ReadToEndAsync(CancellationToken);

            try
            {
                await process.WaitForExitAsync(CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }

            return (process.ExitCode, await stdoutTask, await stderrTask);

        }

        #endregion

        #region ResolveWindowsHostAsync(ProbePort, CancellationToken)

        /// <summary>
        /// The address a WSL peer must dial to reach a listener bound on the Windows host.
        ///
        /// <para>
        /// Probes <c>127.0.0.1</c> first (correct under mirrored networking) and falls back to the default
        /// gateway (correct under the default NAT networking). Returns <c>null</c> when neither answers —
        /// which on an otherwise healthy machine means the Windows firewall is blocking the test listener,
        /// an environment problem that must not be reported as an interop failure.
        /// </para>
        /// </summary>
        /// <param name="ProbePort">A port that is currently being listened on, used as the probe target.</param>
        public static async Task<String?> ResolveWindowsHostAsync(Int32 ProbePort, CancellationToken CancellationToken)
        {

            if (resolvedHost is not null)
                return resolvedHost;

            await resolveLock.WaitAsync(CancellationToken).ConfigureAwait(false);

            try
            {

                if (resolvedHost is not null)
                    return resolvedHost;

                var probe = $$"""
                              for host in 127.0.0.1 $(ip route show default 2>/dev/null | awk '{print $3; exit}'); do
                                  if timeout 2 bash -c "exec 3<>/dev/tcp/${host}/{{ProbePort}}" 2>/dev/null; then
                                      echo "${host}"
                                      exit 0
                                  fi
                              done
                              exit 1
                              """;

                var (exitCode, stdout, _) = await RunAsync(["-e", "bash", "-c", probe], CancellationToken).ConfigureAwait(false);

                if (exitCode == 0)
                    resolvedHost = stdout.Trim();

                return resolvedHost;

            }
            finally
            {
                resolveLock.Release();
            }

        }

        #endregion

        #region RunPeerDriverAsync(Driver, Configuration, CancellationToken)

        /// <summary>
        /// Run one of the Python peer drivers and parse its JSON verdict.
        ///
        /// <para>
        /// The configuration travels as a JSON file rather than command-line arguments: it carries paths,
        /// algorithm lists and commands, and a file sidesteps every layer of quoting between .NET,
        /// <c>wsl.exe</c> and the shell.
        /// </para>
        /// </summary>
        /// <param name="Driver">Driver file name inside <c>interop/python/</c>, e.g. <c>asyncssh_driver.py</c>.</param>
        /// <param name="Configuration">The driver configuration, serialised to JSON.</param>
        public static async Task<PeerRunResult> RunPeerDriverAsync(String                       Driver,
                                                                   IReadOnlyDictionary<String, Object?>  Configuration,
                                                                   CancellationToken            CancellationToken)
        {

            var (interopDirectory, venvPython, reason) = harness.Value;

            if (reason is not null || interopDirectory is null || venvPython is null)
                throw new InvalidOperationException(reason ?? "The WSL interop harness is unavailable.");

            var driverPath     = Path.Combine(interopDirectory, "python", Driver);
            if (!File.Exists(driverPath))
                throw new FileNotFoundException($"Peer driver '{Driver}' not found.", driverPath);

            var configurationPath = Path.Combine(Path.GetTempPath(), "hermod_peer_" + Guid.NewGuid().ToString("N") + ".json");

            try
            {

                await File.WriteAllTextAsync(configurationPath,
                                             JsonSerializer.Serialize(Configuration),
                                             CancellationToken).ConfigureAwait(false);

                var (exitCode, stdout, stderr) = await RunAsync([
                                                     "-e",
                                                     ToWslPath(venvPython),
                                                     ToWslPath(driverPath),
                                                     ToWslPath(configurationPath)
                                                 ], CancellationToken).ConfigureAwait(false);

                // The driver reports a failed *SSH operation* as ok=false with exit code 0; a non-zero exit
                // means the driver itself broke, which is a harness bug and must be loud.
                if (exitCode != 0)
                    throw new InvalidOperationException(
                              $"The '{Driver}' peer driver failed (exit {exitCode}).\nstdout:\n{stdout}\nstderr:\n{stderr}");

                var result = JsonSerializer.Deserialize<PeerRunResult>(stdout, jsonOptions)
                                 ?? throw new InvalidOperationException($"The '{Driver}' peer driver produced no JSON.\nstdout:\n{stdout}");

                return result;

            }
            finally
            {
                try { File.Delete(configurationPath); } catch { }
            }

        }

        #endregion

    }

}
