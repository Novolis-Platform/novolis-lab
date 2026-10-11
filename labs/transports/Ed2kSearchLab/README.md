# Ed2kSearchLab

Console dogfood for `Novolis.Transports.Ed2k`. The lab loads a local text
server list or `server.met`; when the cache path is missing or empty, it
downloads the default `server.met` source over HTTPS, validates it, and
caches it atomically before connecting to the first reachable eD2k server.
It searches by name and displays a paginated result page with connection,
peer, source, and transfer-readiness metadata.

The default source is the eMule Security mirror documented by aMule:
`https://upd.emule-security.org/server.met`. Use `--server-url` to choose
another HTTPS source.

## Batch mode

Batch mode is plain and deterministic, including when output is redirected.
`--skip` is a zero-based result offset and defaults to `0`; `--take` defaults
to `25` and is limited to `1000`.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\transports\Ed2kSearchLab -- --servers d:\data\servers.met --query "example" --skip 0 --take 5
dotnet run --project d:\novolis\novolis-lab\labs\transports\Ed2kSearchLab -- --servers d:\data\servers.met --query "example" --skip 5 --take 5
```

If the server list is absent, the first command downloads and caches it.
Override the source with an HTTPS URL:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\transports\Ed2kSearchLab -- --servers d:\data\servers.met --server-url https://example.invalid/server.met --query "example"
```

## Interactive mode

Pass `--interactive` from a real terminal to use Spectre.Console panels,
status spinners, metadata grids, and result tables. Missing server-list and
query values are prompted for. The flag never prompts when standard input or
output is redirected; redirected execution stays in batch mode and still
requires `--servers` and `--query`.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\transports\Ed2kSearchLab -- --interactive --skip 0 --take 25
```

The interactive view shows:

- server-list cache/source and candidate endpoint metadata;
- connected server, client ID, HighID/LowID, and assigned port;
- query duration, total result count, and the visible result range;
- result name, type, byte size, source count, file ID, peer ID/port, and
  transfer-readiness state.

Transfer readiness is a preview only. HighID one-part results are marked as
eligible for a future direct transfer; LowID results require callback
support; results larger than `Ed2kProtocol.PartSize` require multipart/resume
support. The lab does not call `Ed2kSession.DownloadAsync`, connect to result
peers, or create destination files. The only file it may write is the
validated server-list cache.
