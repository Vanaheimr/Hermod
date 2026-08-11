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

## CI

Hosted CI runners have no WSL; the same peers are provided there via the `docker/` images. The full
matrix strategy (per-commit smoke vs. nightly matrix) lives in the plan's §11.6.
