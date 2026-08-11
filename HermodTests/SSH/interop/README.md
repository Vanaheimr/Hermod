# Interop harness assets

Supporting files for the interoperability test program. The plan it implements (`PLAN.md`, section 11)
lives in the **SSHConformanceTests** repository, which consumes this one as a submodule and provides the
demo CLI, the benchmarks and the interop-matrix generator.
The NUnit interop tests (`[Category("Interop")]`) drive our client and server against real
third-party SSH/SFTP implementations and probe the environment first — missing prerequisites make a
test `Assert.Ignore(...)` with a precise message, never a red failure.

## Layout

| Path             | Purpose                                                                      |
|------------------|------------------------------------------------------------------------------|
| `setup-wsl.sh`   | Provision the peers inside a WSL2 Debian/Ubuntu (idempotent; sudo for apt).   |
| `docker/`        | Dockerfiles per peer + version for the CI/version-matrix runs (added in M9).  |
| `go/`            | A small `golang.org/x/crypto/ssh` harness (added when that peer is wired in). |

## Local setup (WSL2)

```bash
# From a WSL2 Debian/Ubuntu shell, in this directory:
./setup-wsl.sh            # install OpenSSH, Dropbear, TinySSH, PuTTY tools, curl + Python peers
./setup-wsl.sh --check    # report what is present, install nothing
```

The tests reach this shell from Windows via `wsl.exe -e`. Note the two WSL gotchas the harness
handles automatically (the plan's §11.2): private keys are copied off `/mnt/c` into the WSL home and
`chmod 600`-ed (OpenSSH refuses world-readable keys), and `localhost` reachability differs between
NAT and mirrored networking modes.

### Which address a WSL peer must dial (measured 2026-08-11, NAT mode)

Peers that only exist inside Linux — Dropbear, TinySSH, AsyncSSH, Paramiko, the Go harness — run in
WSL and connect *back* to a server hosted on Windows. Under WSL's default **NAT** networking that
server is **not reachable at `127.0.0.1`**: from inside WSL, the Windows host answers on the
**default gateway** address (`ip route show default | awk '{print $3}'`, e.g. `172.23.32.1`).

So a test driving a WSL peer must bind our listener to **`IPv4Address.Any`**, not `Localhost`, and
hand the peer the gateway address. The existing interop tests bind to `Localhost` and are unaffected
only because their peer (`ssh.exe`, `ssh-keygen`, SSH.NET) runs on Windows alongside the server.
Under **mirrored** networking (`networkingMode=mirrored` in `.wslconfig`) `localhost` does work — so
detect rather than assume: try `127.0.0.1` first, fall back to the gateway.

## CI

Hosted CI runners have no WSL; the same peers are provided there via the `docker/` images. The full
matrix strategy (per-commit smoke vs. nightly matrix) lives in the plan's §11.6.
