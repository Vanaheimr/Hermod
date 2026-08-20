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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// Creates, tracks and closes all rendezvous of this service.
    /// </summary>
    public sealed class RendezvousManager : IAsyncDisposable
    {

        #region Data

        /// <summary>
        /// How often a free TCP port is looked for, when the operating system
        /// hands out ports outside of the configured port range.
        /// </summary>
        private const Int32 RandomPortAttempts = 64;

        private readonly RendezvousOptions                                options;
        private readonly TimeProvider                                     timeProvider;
        private readonly ILoggerFactory                                   loggerFactory;
        private readonly ILogger<RendezvousManager>                       logger;

        private readonly ConcurrentDictionary<Guid,   RendezvousSession>  sessions    = [];
        private readonly ConcurrentDictionary<IPPort, RendezvousSession>  portOwners  = [];
        private readonly Lock                                             createLock  = new();
        private readonly SessionJanitor?                                  janitor;

        #endregion

        #region Properties

        /// <summary>
        /// All currently known rendezvous.
        /// </summary>
        public IReadOnlyCollection<RendezvousSession> Sessions
            => [.. sessions.Values];

        /// <summary>
        /// The number of currently known rendezvous.
        /// </summary>
        public Int32 Count
            => sessions.Count;

        /// <summary>
        /// The janitor looking after the timeouts, or null when the caller
        /// disabled the automatic maintenance.
        /// </summary>
        internal SessionJanitor? Janitor
            => janitor;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new rendezvous manager.
        ///
        /// Everything is optional, so that a plain "new RendezvousManager()" is
        /// enough to open rendezvous from any application - no hosting, no
        /// dependency injection and no control endpoint required.
        /// </summary>
        /// <param name="Options">An optional configuration, defaults are used otherwise.</param>
        /// <param name="TimeProvider">An optional time provider, e.g. for tests.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        /// <exception cref="ArgumentException">When the given configuration is invalid.</exception>
        public RendezvousManager(RendezvousOptions?  Options         = null,
                                 TimeProvider?       TimeProvider    = null,
                                 ILoggerFactory?     LoggerFactory   = null)
        {

            this.options        = Options        ?? new RendezvousOptions();
            this.timeProvider   = TimeProvider   ?? System.TimeProvider.System;
            this.loggerFactory  = LoggerFactory  ?? NullLoggerFactory.Instance;
            this.logger         = this.loggerFactory.CreateLogger<RendezvousManager>();

            #region Fail fast on an invalid configuration

            var errors = this.options.Validate();

            if (errors.Count > 0)
                throw new ArgumentException(
                          $"Invalid rendezvous configuration: {String.Join(" ", errors)}",
                          nameof(Options)
                      );

            #endregion

            #region Look after the timeouts, unless the caller wants to do it

            if (this.options.AutoMaintenance)
            {

                janitor = new SessionJanitor(
                              this,
                              this.options.MaintenanceInterval,
                              this.timeProvider,
                              this.loggerFactory.CreateLogger<SessionJanitor>()
                          );

                janitor.Start();

            }

            #endregion

        }

        #endregion


        #region Execute(CommandLine,      Authorization = null)

        /// <summary>
        /// Parse and execute the given control command.
        /// </summary>
        /// <param name="CommandLine">A text representation of a control command.</param>
        /// <param name="Authorization">Who is asking, a trusted caller within the same process otherwise.</param>
        public CommandResponse Execute(String                 CommandLine,
                                       ControlAuthorization?  Authorization   = null)

            => CommandParser.TryParse(CommandLine, out var command, out var error)
                   ? Execute(command, Authorization)
                   : CommandResponse.Error(error);

        #endregion

        #region Execute(Command,          Authorization = null)

        /// <summary>
        /// Execute the given control command.
        /// </summary>
        /// <param name="Command">A control command.</param>
        /// <param name="Authorization">Who is asking, a trusted caller within the same process otherwise.</param>
        public CommandResponse Execute(RendezvousCommand      Command,
                                       ControlAuthorization?  Authorization   = null)
        {
            try
            {

                return Command switch {
                           ConnectPortsCommand     connectPorts     => ConnectPorts   (connectPorts,    Authorization),
                           DisconnectPortsCommand  disconnectPorts  => DisconnectPorts(disconnectPorts, Authorization),
                           _                                        => CommandResponse.Error(ResponseCode.UnknownCommand,
                                                                                             $"Unknown command '{Command.CommandName}'!")
                       };

            }
            catch (Exception e)
            {

                logger.LogError(e, "The command {Command} failed!", Command);

                return CommandResponse.Error(ResponseCode.InternalError,
                                             "The command could not be executed!");

            }
        }

        #endregion

        #region ConnectPorts(Command,     Authorization = null)

        /// <summary>
        /// Open a new rendezvous and return the protocol response.
        /// Use <see cref="TryConnectPorts(ConnectPortsCommand, out RendezvousSession?, out CommandResponse)"/>
        /// when the rendezvous itself is needed.
        /// </summary>
        /// <param name="Command">A ConnectPorts command.</param>
        /// <param name="Authorization">Who is asking, a trusted caller within the same process otherwise.</param>
        public CommandResponse ConnectPorts(ConnectPortsCommand    Command,
                                            ControlAuthorization?  Authorization   = null)

            => OpenSession(Command, Authorization ?? ControlAuthorization.Trusted).Response;

        #endregion

        #region TryConnectPorts(Command, [Authorization], out Session, out Response)

        /// <summary>
        /// Open a new rendezvous and return it.
        ///
        /// This is the typed variant for callers within the same process: it hands
        /// out the rendezvous itself, so that the chosen TCP ports of a '?' do not
        /// have to be parsed out of the response text.
        /// </summary>
        /// <param name="Command">A ConnectPorts command.</param>
        /// <param name="Session">The new rendezvous, when successful.</param>
        /// <param name="Response">The protocol response, successful or not.</param>
        public Boolean TryConnectPorts(ConnectPortsCommand                        Command,
                                       [NotNullWhen(true)] out RendezvousSession?  Session,
                                                           out CommandResponse     Response)

            => TryConnectPorts(Command, null, out Session, out Response);

        /// <summary>
        /// Open a new rendezvous on behalf of the given caller and return it.
        /// The keys of the caller become the owners of the new rendezvous.
        /// </summary>
        /// <param name="Command">A ConnectPorts command.</param>
        /// <param name="Authorization">Who is asking, a trusted caller within the same process otherwise.</param>
        /// <param name="Session">The new rendezvous, when successful.</param>
        /// <param name="Response">The protocol response, successful or not.</param>
        public Boolean TryConnectPorts(ConnectPortsCommand                        Command,
                                       ControlAuthorization?                      Authorization,
                                       [NotNullWhen(true)] out RendezvousSession?  Session,
                                                           out CommandResponse     Response)
        {

            (Response, Session) = OpenSession(Command, Authorization ?? ControlAuthorization.Trusted);

            return Response.IsSuccess;

        }

        #endregion

        #region DisconnectPorts(Command,  Authorization = null)

        /// <summary>
        /// Close an existing rendezvous and return the protocol response.
        /// Use <see cref="TryDisconnectPorts(DisconnectPortsCommand, out RendezvousSession?, out CommandResponse)"/>
        /// when the rendezvous itself is needed.
        /// </summary>
        /// <param name="Command">A DisconnectPorts command.</param>
        /// <param name="Authorization">Who is asking, a trusted caller within the same process otherwise.</param>
        public CommandResponse DisconnectPorts(DisconnectPortsCommand  Command,
                                               ControlAuthorization?   Authorization   = null)

            => CloseSession(Command, Authorization ?? ControlAuthorization.Trusted).Response;

        #endregion

        #region TryDisconnectPorts(Command, [Authorization], out Session, out Response)

        /// <summary>
        /// Close an existing rendezvous and return it.
        ///
        /// Closing is asynchronous: await <see cref="RendezvousSession.Completion"/>
        /// of the returned rendezvous to know when its TCP ports are free again.
        /// Looking the rendezvous up afterwards would be a race, as a closed
        /// rendezvous is unregistered.
        /// </summary>
        /// <param name="Command">A DisconnectPorts command.</param>
        /// <param name="Session">The closed rendezvous, when successful.</param>
        /// <param name="Response">The protocol response, successful or not.</param>
        public Boolean TryDisconnectPorts(DisconnectPortsCommand                     Command,
                                          [NotNullWhen(true)] out RendezvousSession?  Session,
                                                              out CommandResponse     Response)

            => TryDisconnectPorts(Command, null, out Session, out Response);

        /// <summary>
        /// Close an existing rendezvous on behalf of the given caller and return it.
        /// Only the keys that opened a rendezvous, and administrator keys, may close it.
        /// </summary>
        /// <param name="Command">A DisconnectPorts command.</param>
        /// <param name="Authorization">Who is asking, a trusted caller within the same process otherwise.</param>
        /// <param name="Session">The closed rendezvous, when successful.</param>
        /// <param name="Response">The protocol response, successful or not.</param>
        public Boolean TryDisconnectPorts(DisconnectPortsCommand                     Command,
                                          ControlAuthorization?                      Authorization,
                                          [NotNullWhen(true)] out RendezvousSession?  Session,
                                                              out CommandResponse     Response)
        {

            (Response, Session) = CloseSession(Command, Authorization ?? ControlAuthorization.Trusted);

            return Response.IsSuccess;

        }

        #endregion


        #region (private) OpenSession(Command, Authorization)

        /// <summary>
        /// Open a new rendezvous: validate the request, bind all TCP ports and
        /// start waiting for the clients.
        /// </summary>
        /// <param name="Command">A ConnectPorts command.</param>
        /// <param name="Authorization">Who asked for this rendezvous.</param>
        private (CommandResponse Response, RendezvousSession? Session)

            OpenSession(ConnectPortsCommand   Command,
                        ControlAuthorization  Authorization)

        {

            #region Validate the request

            // The parser rejects all of this as well, but a caller using the
            // typed API directly never went through the parser.

            if (Command.Ports.Count < CommandParser.MinPortsPerSession)
                return (CommandResponse.Error(ResponseCode.InvalidSyntax,
                                              $"A rendezvous requires at least {CommandParser.MinPortsPerSession} ports!"), null);

            if (Command.Ports.Count > options.MaxPortsPerSession)
                return (CommandResponse.Error(ResponseCode.TooManyPorts,
                                              $"A rendezvous must not have more than {options.MaxPortsPerSession} ports!"), null);

            if (Command.Description?.Length > RendezvousCommand.MaxDescriptionLength)
                return (CommandResponse.Error(ResponseCode.InvalidSyntax,
                                              $"The description must not be longer than {RendezvousCommand.MaxDescriptionLength} characters!"), null);

            var requestedPorts = new HashSet<IPPort>();

            foreach (var port in Command.Ports)
            {

                if (port.Port is not IPPort fixedPort)
                    continue;

                if (!options.IsAllowedDataPort(fixedPort))
                    return (CommandResponse.Error(ResponseCode.PortNotAllowed,
                                                  $"TCP port {fixedPort} is not within the allowed port range {options.MinDataPort} - {options.MaxDataPort}!"), null);

                if (!requestedPorts.Add(fixedPort))
                    return (CommandResponse.Error(ResponseCode.InvalidSyntax,
                                                  $"Duplicate TCP port {fixedPort}!"), null);

            }

            #endregion

            var profile   = Command.Profile ?? options.DefaultProfile;
            var settings  = options.Profiles[profile];
            var address   = options.DataIPAddress;

            lock (createLock)
            {

                if (sessions.Count >= options.MaxSessions)
                    return (CommandResponse.Error(ResponseCode.TooManySessions,
                                                  $"The service is already running {options.MaxSessions} rendezvous!"), null);

                foreach (var port in Command.Ports)
                {
                    if (port.Port is IPPort fixedPort && portOwners.ContainsKey(fixedPort))
                        return (CommandResponse.Error(ResponseCode.PortInUse,
                                                      $"TCP port {fixedPort} is already used by another rendezvous!"), null);
                }

                var endpoints  = new SessionEndpoint?[Command.Ports.Count];
                var usedPorts  = new HashSet<IPPort>();

                try
                {

                    #region Bind all fixed ports first...

                    for (var i = 0; i < Command.Ports.Count; i++)
                    {

                        if (Command.Ports[i].Port is not IPPort fixedPort)
                            continue;

                        endpoints[i] = SessionEndpoint.Bind(address,
                                                            fixedPort,
                                                            Command.Ports[i],
                                                            settings.ListenBacklog);

                        usedPorts.Add(fixedPort);

                    }

                    #endregion

                    #region ...and afterwards all random ports

                    for (var i = 0; i < Command.Ports.Count; i++)
                    {

                        if (!Command.Ports[i].IsRandom)
                            continue;

                        endpoints[i] = BindRandomPort(address,
                                                      settings.ListenBacklog,
                                                      usedPorts);

                    }

                    #endregion

                }
                catch (SocketException e)
                {

                    foreach (var endpoint in endpoints)
                        endpoint?.Dispose();

                    logger.LogWarning("Could not open the requested TCP ports [{Ports}]: {Message}",
                                      String.Join(", ", Command.Ports), e.Message);

                    return (CommandResponse.Error(ResponseCode.PortInUse,
                                                  $"Could not open the requested TCP ports: {e.SocketErrorCode}!"), null);

                }

                #region Register and start the new rendezvous

                var session = new RendezvousSession(
                                  Guid.CreateVersion7(timeProvider.GetUtcNow()),
                                  [.. endpoints.Select(endpoint => endpoint!)],
                                  profile,
                                  settings,
                                  Authorization.KeyIds,
                                  Command.Description,
                                  timeProvider,
                                  loggerFactory.CreateLogger<RendezvousSession>()
                              );

                sessions.TryAdd(session.Id, session);

                foreach (var port in session.Ports)
                    portOwners[port] = session;

                session.OnClosed += SessionClosed;

                session.Start();

                logger.LogInformation("Rendezvous {SessionId} opened on TCP/[{Ports}] using the {Profile} profile, by {CreatedBy}{Description}.",
                                      session.Id, String.Join(", ", session.Ports), profile.AsText(), Authorization,
                                      session.Description is not null ? $": {session.Description}" : "");

                #endregion

                return (CommandResponse.Ok($"{ConnectPortsCommand.Name}([{String.Join(", ", session.Ports)}], {profile.AsText()})"), session);

            }

        }

        #endregion

        #region (private) CloseSession(Command, Authorization)

        /// <summary>
        /// Close an existing rendezvous: all given TCP ports must belong to the
        /// same rendezvous, and the caller must own it.
        /// </summary>
        /// <param name="Command">A DisconnectPorts command.</param>
        /// <param name="Authorization">Who wants to close this rendezvous.</param>
        private (CommandResponse Response, RendezvousSession? Session)

            CloseSession(DisconnectPortsCommand  Command,
                         ControlAuthorization    Authorization)

        {

            if (Command.Ports.Count == 0)
                return (CommandResponse.Error(ResponseCode.InvalidSyntax,
                                              "At least one TCP port is required!"), null);

            if (!portOwners.TryGetValue(Command.Ports[0], out var session))
                return (CommandResponse.Error(ResponseCode.UnknownSession,
                                              $"There is no rendezvous on TCP port {Command.Ports[0]}!"), null);

            foreach (var port in Command.Ports)
            {
                if (!portOwners.TryGetValue(port, out var owner) ||
                    !ReferenceEquals(owner, session))
                {
                    return (CommandResponse.Error(ResponseCode.UnknownSession,
                                                  $"TCP port {port} does not belong to the same rendezvous!"), null);
                }
            }

            if (!session.Authorize(Authorization))
            {

                logger.LogWarning("Rejected an unauthorized {Command} from {Caller} for rendezvous {SessionId}, which was opened by {Owners}!",
                                  DisconnectPortsCommand.Name, Authorization, session.Id, String.Join(", ", session.CreatedBy));

                // Not even the rendezvous itself is handed out here.
                return (CommandResponse.Error(ResponseCode.Unauthorized,
                                              "This rendezvous belongs to somebody else!"), null);

            }

            logger.LogInformation("Closing rendezvous {SessionId} on TCP/[{Ports}], asked by {Caller}{Description}.",
                                  session.Id, String.Join(", ", session.Ports), Authorization,
                                  Command.Description is not null ? $": {Command.Description}" : "");

            session.Close(SessionCloseReason.DisconnectRequested);

            return (CommandResponse.Ok($"{DisconnectPortsCommand.Name}([{String.Join(", ", session.Ports)}])"), session);

        }

        #endregion


        #region Sweep()

        /// <summary>
        /// Close all rendezvous that ran into one of their timeouts.
        /// </summary>
        public void Sweep()
        {

            var now = timeProvider.GetUtcNow();

            foreach (var session in sessions.Values)
            {

                if (session.State == SessionState.Pending &&
                    now - session.CreatedUtc >= options.RendezvousTimeout)
                {

                    logger.LogInformation("Rendezvous {SessionId} on TCP/[{Ports}] timed out: only {Connected} of {Expected} clients arrived within {Timeout}.",
                                          session.Id, String.Join(", ", session.Ports), session.ConnectedClients, session.Ports.Count, options.RendezvousTimeout);

                    session.Close(SessionCloseReason.RendezvousTimeout);

                }

                else if (session.State == SessionState.Established &&
                         now - session.LastActivityUtc >= options.IdleTimeout)
                {

                    logger.LogInformation("Rendezvous {SessionId} on TCP/[{Ports}] was idle for more than {Timeout}.",
                                          session.Id, String.Join(", ", session.Ports), options.IdleTimeout);

                    session.Close(SessionCloseReason.IdleTimeout);

                }

            }

        }

        #endregion

        #region TryGetSessionByPort(Port, out Session)

        /// <summary>
        /// Try to get the rendezvous owning the given TCP port.
        /// </summary>
        /// <param name="Port">A TCP port.</param>
        /// <param name="Session">The rendezvous owning the given TCP port.</param>
        public Boolean TryGetSessionByPort(IPPort Port, out RendezvousSession? Session)

            => portOwners.TryGetValue(Port, out Session);

        #endregion


        #region (private) BindRandomPort(Address, Backlog, UsedPorts)

        /// <summary>
        /// Bind a free TCP port.
        /// </summary>
        /// <param name="Address">The IP address to bind to.</param>
        /// <param name="Backlog">The TCP listen backlog.</param>
        /// <param name="UsedPorts">The TCP ports already used by this rendezvous.</param>
        /// <exception cref="SocketException">When no free TCP port could be found.</exception>
        private SessionEndpoint BindRandomPort(IIPAddress      Address,
                                               Int32           Backlog,
                                               HashSet<IPPort> UsedPorts)
        {

            #region Let the operating system pick a free port...

            var endpoint = SessionEndpoint.Bind(Address, IPPort.Parse(0), PortSpecification.Random, Backlog);

            if ( options.IsAllowedDataPort(endpoint.Port) &&
                !portOwners.ContainsKey(endpoint.Port)    &&
                 UsedPorts.Add(endpoint.Port))
            {
                return endpoint;
            }

            // The operating system handed out a port outside of the configured
            // port range, or a port that is about to be released by another rendezvous.
            endpoint.Dispose();

            #endregion

            #region ...or look for one within the configured port range

            for (var attempt = 0; attempt < RandomPortAttempts; attempt++)
            {

                var candidate = IPPort.Parse(Random.Shared.Next(options.MinDataPort.ToInt32(),
                                                               options.MaxDataPort.ToInt32() + 1));

                if (portOwners.ContainsKey(candidate) ||
                    UsedPorts.Contains(candidate)     ||
                   !options.IsAllowedDataPort(candidate))
                {
                    continue;
                }

                try
                {

                    var randomEndpoint = SessionEndpoint.Bind(Address, candidate, PortSpecification.Random, Backlog);
                    UsedPorts.Add(candidate);

                    return randomEndpoint;

                }
                catch (SocketException)
                {
                    // Somebody else is using this port: try the next one.
                }

            }

            #endregion

            throw new SocketException((Int32) SocketError.AddressAlreadyInUse);

        }

        #endregion

        #region (private) SessionClosed(Session)

        /// <summary>
        /// Remove a closed rendezvous and release its TCP ports.
        /// </summary>
        private void SessionClosed(RendezvousSession Session)
        {

            sessions.TryRemove(new KeyValuePair<Guid, RendezvousSession>(Session.Id, Session));

            foreach (var port in Session.Ports)
                portOwners.TryRemove(new KeyValuePair<IPPort, RendezvousSession>(port, Session));

        }

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Stop the maintenance, close all rendezvous and wait until all TCP
        /// ports are free again.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            if (janitor is not null)
                await janitor.DisposeAsync().ConfigureAwait(false);

            var openSessions = sessions.Values.ToArray();

            foreach (var session in openSessions)
                session.Close(SessionCloseReason.ServiceShutdown);

            foreach (var session in openSessions)
            {
                try
                {
                    await session.Completion.ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogDebug(e, "Rendezvous {SessionId}: shutdown error.", session.Id);
                }
            }

            sessions.Clear();
            portOwners.Clear();

        }

        #endregion

    }

}
