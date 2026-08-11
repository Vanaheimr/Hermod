#!/usr/bin/env python3
#
# paramiko_driver.py — drive HermodSSH's server from Paramiko, for the NUnit interop suite.
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
#   command                                   (exec)
#   upload_path, download_path, remote_path   (sftp)
#   kex_algs                                  (optional; forces a Transport-level connect)
#   host_key_type, host_key_b64               (required when kex_algs is given)

import base64
import json
import logging
import sys
import threading
import traceback

import paramiko


class _RingHandler(logging.Handler):
    """
    Keeps Paramiko's own protocol trace so a failed run can hand it to the test.

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


def _negotiated(transport):
    """
    What Paramiko ended up using. Cipher, MAC and host-key type are public attributes; the key
    exchange is only reachable through the engine object, so it is read defensively and used as
    diagnostic evidence only — the tests prove negotiation by constraining the offer instead.
    """
    kex_engine = getattr(transport, "kex_engine", None)

    return {
        "kex":      getattr(kex_engine, "name", None) or (type(kex_engine).__name__ if kex_engine else None),
        "cipher":   getattr(transport, "remote_cipher", None),
        "mac":      getattr(transport, "remote_mac",    None),
        "host_key": getattr(transport, "host_key_type", None),
    }


def _host_key(config):
    """Rebuild the host key we expect, so a Transport-level connect can verify it."""
    blob     = base64.b64decode(config["host_key_b64"])
    key_type = config["host_key_type"]

    if key_type == "ssh-ed25519":
        return paramiko.Ed25519Key(data=blob)
    if key_type.startswith("ecdsa-"):
        return paramiko.ECDSAKey(data=blob)
    if key_type.startswith("ssh-rsa") or key_type.startswith("rsa-"):
        return paramiko.RSAKey(data=blob)

    raise ValueError(f"unsupported host key type '{key_type}'")


def _run_over_transport(config, result):
    """
    The constrained path: drive a raw Transport so the offered key exchanges can be restricted.
    SSHClient offers no hook for that.
    """
    result["stage"] = "connecting"
    transport = paramiko.Transport((config["host"], config["port"]))

    try:

        transport.get_security_options().kex = tuple(config["kex_algs"])

        transport.connect(hostkey  = _host_key(config),
                          username = config["username"],
                          pkey     = paramiko.Ed25519Key.from_private_key_file(config["key_path"]))

        result["stage"]          = "connected"
        result["server_version"] = transport.remote_version
        result["algorithms"]     = _negotiated(transport)

        if config["action"] == "exec":
            result["stage"] = "exec"
            channel = transport.open_session()
            channel.exec_command(config["command"])
            result["stdout"]      = channel.makefile("r").read().decode("utf-8", "replace")
            result["stderr"]      = channel.makefile_stderr("r").read().decode("utf-8", "replace")
            result["exit_status"] = channel.recv_exit_status()
            channel.close()

        result["ok"] = True

    finally:
        transport.close()


def _run_over_client(config, result):
    """The ordinary path: SSHClient, as an application would use it."""
    result["stage"] = "connecting"
    client = paramiko.SSHClient()

    try:

        client.load_host_keys(config["known_hosts"])
        client.set_missing_host_key_policy(paramiko.RejectPolicy())

        client.connect(config["host"],
                       port            = config["port"],
                       username        = config["username"],
                       key_filename    = config["key_path"],
                       look_for_keys   = False,
                       allow_agent     = False,
                       timeout         = 30)

        result["stage"]          = "connected"
        transport = client.get_transport()
        result["server_version"] = transport.remote_version
        result["algorithms"]     = _negotiated(transport)

        action = config["action"]

        if action == "connect":
            pass

        elif action == "exec":
            result["stage"]       = "exec"
            _, stdout, stderr     = client.exec_command(config["command"])
            result["stdout"]      = stdout.read().decode("utf-8", "replace")
            result["stderr"]      = stderr.read().decode("utf-8", "replace")
            result["exit_status"] = stdout.channel.recv_exit_status()

        elif action == "sftp":
            result["stage"] = "sftp-open"
            sftp = client.open_sftp()
            try:
                result["stage"] = "sftp-put"
                sftp.put(config["upload_path"], config["remote_path"])
                result["stage"] = "sftp-get"
                sftp.get(config["remote_path"], config["download_path"])
                result["stage"] = "sftp-list"
                result["listing"] = list(sftp.listdir("/"))
            finally:
                sftp.close()

        else:
            raise ValueError(f"unknown action '{action}'")

        result["ok"] = True

    finally:
        client.close()


def _run(config, result):
    if config.get("kex_algs"):
        _run_over_transport(config, result)
    else:
        _run_over_client(config, result)


def _run_guarded(config, result):
    """
    Run the requested action under our own timeout.

    Paramiko is synchronous, so the work happens on a worker thread that we stop waiting for once the
    budget is spent. Without this a peer that blocks — waiting for a channel close we never send, say —
    would burn the whole NUnit CancelAfter budget and the test would report a bare timeout. Failing
    here instead keeps the 'stage' field, which says exactly how far the session got.
    """
    timeout = config.get("timeout_seconds", 30)
    failure = {}

    def work():
        try:
            _run(config, result)
        except Exception as exception:                                 # noqa: BLE001 — reported, not swallowed
            failure["error"] = str(exception) or repr(exception)
            failure["type"]  = type(exception).__name__

    worker = threading.Thread(target=work, daemon=True)
    worker.start()
    worker.join(timeout)

    if worker.is_alive():
        result["ok"]         = False
        result["error"]      = f"timed out after {timeout}s in stage '{result.get('stage')}'"
        result["error_type"] = "TimeoutError"
    elif failure:
        result["error"]      = failure["error"]
        result["error_type"] = failure["type"]


def main():

    if len(sys.argv) != 2:
        print("usage: paramiko_driver.py <configuration.json>", file=sys.stderr)
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
        "peer_version":   paramiko.__version__,
        "server_version": None,
    }

    trace = _RingHandler()
    trace.setFormatter(logging.Formatter("%(name)s: %(message)s"))
    logging.getLogger("paramiko").addHandler(trace)
    logging.getLogger("paramiko").setLevel(logging.DEBUG)

    try:
        with open(sys.argv[1], "r", encoding="utf-8") as stream:
            config = json.load(stream)
        _run_guarded(config, result)
    except Exception:                                                  # noqa: BLE001 — a harness bug
        traceback.print_exc(file=sys.stderr)
        return 2

    if not result["ok"]:
        result["debug_log"] = trace.records

    print(json.dumps(result))
    return 0


if __name__ == "__main__":
    sys.exit(main())
