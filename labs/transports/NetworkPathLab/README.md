# NetworkPathLab

Spectre.Console dogfood for `Novolis.Transports.NetworkPath`. It lists local
adapters, resolves the operating system's source address for a destination,
and compares TCP connection probes bound to selected adapters.

The lab does not change routes or call a public IP/reputation service. A
tunnel candidate is a heuristic based on adapter type and descriptive hints;
it is not proof that the adapter is a VPN.

## Batch usage

The target is explicit because probing creates outbound connection attempts.
The default probes only the adapter selected for the target. Add
`--all-adapters` to compare every operational adapter with a local address.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\transports\NetworkPathLab\NetworkPathLab.csproj -- --target example.com:443
dotnet run --project d:\novolis\novolis-lab\labs\transports\NetworkPathLab\NetworkPathLab.csproj -- --target example.com:443 --all-adapters --count 5 --timeout 5 --format json
```

The TCP probe measures connection establishment, not ICMP echo latency. The
result includes attempts, successes, loss, minimum, median, maximum, and the
adapter selected for the best healthy median. JSON and redirected output do
not prompt.

## Interactive usage

Use `--interactive` from a real terminal for panels, status output, adapter
classification, and a rounded probe table. A missing target is prompted only
when standard input and output are not redirected.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\transports\NetworkPathLab\NetworkPathLab.csproj -- --interactive --all-adapters
```

Full addresses are hidden by default in displayed and JSON output. Add
`--show-addresses` when the local diagnostic requires them.
