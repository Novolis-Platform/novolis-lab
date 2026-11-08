# novolis-lab — agent contract

Integration labs, demos, and product-adjacent hosts. Libraries live in sibling `novolis-*` package repos.

## Calypso CAD (freighter)

When working under `labs/cad/CalypsoCad/`, follow that host’s contract first:

- [labs/cad/CalypsoCad/AGENTS.md](labs/cad/CalypsoCad/AGENTS.md)
- Terminology: [labs/cad/CalypsoCad/docs/internals/CAL-STRUCT-GLOSSARY.md](labs/cad/CalypsoCad/docs/internals/CAL-STRUCT-GLOSSARY.md)

## Platform rules

- NuGet-only cross-repo deps (`PackageReference` / `LibraryReference`); no local folder feeds
- Absolute paths in agent chat commands
- Push Novolis-Platform work on `main` (no maintainer PRs)
- C# file-based apps + PowerShell for tooling — never Node/Python

Governance sources: `d:\novolis\novolis-governance\AGENTS.md`, `docs/library-boundaries.md`, `docs/nuget-only-policy.md`.
