# Set up this repo

You made this repo from `orvanohq/sdk-template-dotnet` (spec 0010 TPL-4). Work through this list, then delete this file.

## Placeholders to fill in

- `{{PRODUCT_NAME}}` in `NOTICE` and `README.md`: the repo's name from spec 0009 LIC-2's table
- `{{PACKAGE_NAME}}` in `README.md`: the package name from spec 0007 REP-3
- The package name and description in the language manifest: the packable project's `.csproj` (`PackageId`, `Description`), and the repo name in `Directory.Build.props` `RepositoryUrl` (it says `sdk-dotnet`)

## Steps

1. Make the repo **public** from the start ("Use this template" with visibility public). On GitHub Free, its
   ruleset, CodeQL, and secret scanning need that.
2. Fill in the placeholders above.
3. From a clone of `orvanohq/orvano`, apply the settings (a template doesn't copy rulesets or settings):

   ```bash
   node tooling/scripts/github-settings.mjs orvanohq/<this-repo> --profile sdk-dotnet --apply
   ```

4. Open a first PR. Its green CI is the proof these starter files work (spec 0008 KIT-3).
5. Delete this file.

CI calls the core's shared workflows at `cb12f1df33e00f892de7040a05e3ab17cf9410d4` (`main` as of 2026-10-18). Move that pin forward with the
core.

## Common files

Every SDK repo has these at its root:

- `.editorconfig`, `.gitattributes`: the same basics as the core repo (spec 0008 FMT-4, GIT-4)
- `LICENSE`, `NOTICE`: Apache 2.0 (spec 0009 LIC-1, LIC-2). Replace `{{PRODUCT_NAME}}` in `NOTICE` with the repo's
  name from spec 0009 LIC-2's table (for example `Orvano .NET Server SDK`); leave `LICENSE` exactly as it is
- `.github/dependency-review-config.yml`: the SDK license lists (spec 0009 DEP-8); keep only your repo's exceptions
- `typos.toml`: keep only the excludes that exist in your repo
- `.github/pull_request_template.md`: keep the blocks that fit the SDK (Security always stays, PRT-1)
- `.github/dependabot.yml`: the ecosystems the repo uses (the export keeps the language's and `github-actions`)
- `.github/zizmor.yml`: the same zizmor settings as the core repo
- `.github/CODEOWNERS`: added by the export (spec 0010 OWN-1)
- `.github/workflows/ci.yml`: calls the shared workflows at a pinned core SHA (KIT-1). The export sets the language,
  the SHA, and its date; move the pin forward with the core
- `.github/workflows/codeql.yml` (not for Dart) and `.github/workflows/dependency-review.yml`: the security checks
  the repo's `main` ruleset requires (spec 0010 TPL-5, RUL-3). Both need a public repo on GitHub Free

Community files (security policy, code of conduct, support, contributing, and issue forms) come from the org's
`orvanohq/.github` repo, so an SDK repo doesn't carry its own (spec 0010 COM-4, ISS-3).

## .NET SDK starter

For the `sdk-dotnet` repo (spec 0002 LIB-1 to LIB-12, name from spec 0007 REP-3). At the repo root:

- `global.json`, `Directory.Build.props`, `Directory.Packages.props`: the server's settings plus LIB-2's package
  settings, including `Apache-2.0` and packing `LICENSE` and `NOTICE` (spec 0009 LIC-6).
- `.editorconfig`: the common basics plus the server's C# rules (FMT-6), in one file that replaces
  `common/.editorconfig`.
- `BannedSymbols.txt`: the server's list without the EF Core and Npgsql rows (the SDK has neither) and without
  the `HttpClient` constructor (LIB-4 builds one on a shared `SocketsHttpHandler` when the app passes none).

Then, in the packable project: set `<CentralPackageTransitivePinningEnabled>false</CentralPackageTransitivePinningEnabled>`
(LIB-3) and reference `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PublicAPI.Shipped.txt` and
`PublicAPI.Unshipped.txt` (LIB-10). Tests get `XUNIT_GENERATED_DISABLE_WARNINGS` in their `DefineConstants`, as in the
core repo's `server/tests/Directory.Build.props`.

CI: `sdk-dotnet.yml` (KIT-1) restores in locked mode, checks `dotnet format`, builds with `-p:CI=true`, tests, and
packs.
