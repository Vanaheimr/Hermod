#!/usr/bin/env python3
#
# asyncssh_driver.py — drive HermodSSH's server from AsyncSSH, for the NUnit interop suite.
#
# Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
# This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
# Licensed under the Apache License, Version 2.0.
#
# Contract with the C# side (WslInterop.RunPeerDriverAsync):
#   argv[1] is a JSON configuration file; exactly one JSON object is printed to stdout.
#   A failed SSH operation is reported as {"ok": false, ...} with exit code 0 — a non-zero
#   exit means the driver itself broke, which the test surfaces as a harness bug.
#
# Configuration keys:
#   action        "exec" | "sftp" | "connect"
#   host, port, username, key_path, known_hosts
#   command                        (exec)
#   upload_path, download_path, remote_path   (sftp)
#   kex_algs, encryption_algs      (optional lists constraining what the peer offers)

import asyncio
import json
import logging
import sys
import traceback

import asyncssh


class _RingHandler(logging.Handler):
    """
    Keeps AsyncSSH's own protocol trace so a failed run can hand it to the test.

    An interop failure is usually a disagreement about the wire, and the peer's view of that
    conversation is the fastest way to see who stopped talking first — but it is noise when the
    run succeeds, so it is only reported on failure.
    """

    def __init__(self, capacity=400):
        super().__init__(level=logging.DEBUG)
        self.records = []
        self.capacity = capacity

    def emit(self, record):
        self.records.append(self.format(record))
        if len(self.records) > self.capacity:
            del self.records[0]


def _text(value):
    """asyncssh reports algorithm names as bytes; the JSON contract wants strings."""
    if value is None:
        return None
    if isinstance(value, (bytes, bytearray)):
        return value.decode("ascii", "replace")
    return str(value)


def _negotiated(conn):
    """
    Best-effort view of what AsyncSSH thinks it negotiated.

    AsyncSSH exposes no public API for this, so the private attributes are read defensively and
    purely as diagnostic evidence for the test log: the tests prove negotiation by constraining
    what the peer is allowed to offer, never by trusting what it reports here.
    """
    algorithms = {}
    for label, attribute in (("kex",      "_kex_alg"),
                             ("cipher",   "_enc_alg_cs"),
                             ("mac",      "_mac_alg_cs"),
                             ("host_key", "_server_host_key_alg")):
        algorithms[label] = _text(getattr(conn, attribute, None))

    # Newer releases keep the key exchange object rather than the name.
    if algorithms.get("kex") is None:
        kex = getattr(conn, "_kex", None)
        algorithms["kex"] = _text(getattr(kex, "algorithm", None)) if kex is not None else None

    return algorithms


async def _run(config, result):

    options = {
        "username":     config["username"],
        "client_keys":  [config["key_path"]],
        "known_hosts":  config["known_hosts"],
    }

    # Constraining the offer is how the tests prove a specific algorithm was used: if the peer may
    # only offer one key exchange, a completed handshake is proof that both sides agreed on it.
    if config.get("kex_algs"):
        options["kex_algs"] = config["kex_algs"]
    if config.get("encryption_algs"):
        options["encryption_algs"] = config["encryption_algs"]

    result["stage"] = "connecting"

    async with asyncssh.connect(config["host"], config["port"], **options) as conn:

        result["stage"]          = "connected"
        result["server_version"] = _text(conn.get_extra_info("server_version"))
        result["algorithms"]     = _negotiated(conn)

        action = config["action"]

        if action == "connect":
            pass

        elif action == "exec":
            result["stage"]       = "exec"
            completed             = await conn.run(config["command"], check=False)
            result["stdout"]      = completed.stdout
            result["stderr"]      = completed.stderr
            result["exit_status"] = completed.exit_status

        elif action == "sftp":
            result["stage"] = "sftp-open"
            async with conn.start_sftp_client() as sftp:
                result["stage"] = "sftp-put"
                await sftp.put(config["upload_path"], config["remote_path"])
                result["stage"] = "sftp-get"
                await sftp.get(config["remote_path"], config["download_path"])
                result["stage"] = "sftp-list"
                result["listing"] = [str(name) for name in await sftp.listdir("/")]
            result["stage"] = "sftp-closed"

        else:
            raise ValueError(f"unknown action '{action}'")

        result["ok"]    = True
        result["stage"] = "closing"

    result["stage"] = "closed"


async def _run_guarded(config, result):
    """
    Run the requested action under our own timeout.

    Without this a peer that blocks — waiting for a channel close we never send, say — would burn the
    whole NUnit CancelAfter budget and the test would report a bare timeout. Failing here instead
    keeps the 'stage' field, which says exactly how far the session got.
    """
    try:
        await asyncio.wait_for(_run(config, result), timeout=config.get("timeout_seconds", 30))

    except asyncio.TimeoutError:
        result["error"]      = f"timed out after {config.get('timeout_seconds', 30)}s in stage '{result.get('stage')}'"
        result["error_type"] = "TimeoutError"

    except Exception as exception:                                     # noqa: BLE001 — reported, not swallowed
        result["error"]      = str(exception) or repr(exception)
        result["error_type"] = type(exception).__name__

    return result


def main():

    if len(sys.argv) != 2:
        print("usage: asyncssh_driver.py <configuration.json>", file=sys.stderr)
        return 2

    result = {
        "ok":             False,
        "error":          None,
        "error_type":     None,
        "stage":          "starting",
        "stdout":         None,
        "stderr":         None,
        "exit_status":    None,
        "listing":        None,
        "algorithms":     None,
        "peer_version":   asyncssh.__version__,
        "server_version": None,
    }

    trace = _RingHandler()
    trace.setFormatter(logging.Formatter("%(message)s"))
    logging.getLogger("asyncssh").addHandler(trace)
    asyncssh.set_log_level(logging.DEBUG)
    asyncssh.set_debug_level(2)

    try:
        with open(sys.argv[1], "r", encoding="utf-8") as stream:
            config = json.load(stream)
        asyncio.run(_run_guarded(config, result))
    except Exception:                                                  # noqa: BLE001 — a harness bug
        traceback.print_exc(file=sys.stderr)
        return 2

    if not result["ok"]:
        result["debug_log"] = trace.records

    print(json.dumps(result))
    return 0


if __name__ == "__main__":
    sys.exit(main())
