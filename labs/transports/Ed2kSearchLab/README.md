# Ed2kSearchLab

Console dogfood for `Novolis.Transports.Ed2k`. It loads a local text server
list or `server.met`; when the cache path is missing or empty, it downloads
the documented default `server.met` source over HTTPS, validates it, and
caches it atomically before connecting to the first reachable eD2k server.
It prints HighID or LowID and runs one name search.

The lab never ships a server list and never downloads a search result. The
default source is the eMule Security mirror documented by aMule:
`https://upd.emule-security.org/server.met`. Use `--server-url` to choose
another HTTPS source.

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\transports\Ed2kSearchLab -- --servers d:\data\servers.met --query "example"
```
