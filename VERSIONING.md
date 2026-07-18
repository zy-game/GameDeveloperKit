# Versioning Policy

GameDeveloperKit follows Semantic Versioning for the UPM package.

- `MAJOR`: removes or changes a public/protected Runtime API, assembly identity, serialized contract, resource/network/data protocol, or required consumer behavior.
- `MINOR`: adds backward-compatible public capability.
- `PATCH`: fixes behavior without changing the supported public contract.
- Preview releases append `-preview.N`. Every preview release still records breaking changes and migration steps; a preview label is not permission to make silent changes.

## Public API baseline

`Tools/CodeAnalysis/public-api/GameDeveloperKit.Runtime.txt` is the committed Runtime public/protected API baseline. Normal CodeAnalysis verification is read-only and fails on additions, removals, or signature changes.

After intentionally changing the public API:

1. Choose the next SemVer before updating the baseline.
2. Add the exact version and migration instructions to `CHANGELOG.md`.
3. Regenerate the baseline explicitly:

   ```powershell
   $env:GDK_UPDATE_PUBLIC_API = '1'
   dotnet test Tools/CodeAnalysis/tests/GameDeveloperKit.Analyzers.Tests/GameDeveloperKit.Analyzers.Tests.csproj `
     --filter RuntimePublicApiMatchesBaseline
   Remove-Item Env:GDK_UPDATE_PUBLIC_API
   ```

4. Run `pwsh Tools/Quality/quality-gate.ps1`.

Do not restore removed APIs or assemblies with compatibility wrappers. Consumers migrate on the declared breaking version line.
