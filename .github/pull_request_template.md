<!--
Title: <type>(<scope>)!: <summary>, at most 72 characters, no trailing period (spec 0008 GIT-2).
Delete the checklist blocks this PR doesn't touch. The Security block always stays (spec 0008 PRT-1).
-->

## Summary

<!-- What changes, and why. -->

- **Feature and spec:** <!-- for example: feature 7, spec 0008 -->
- **Rule IDs touched:** <!-- for example: FMT-1, LNT-3 -->

## Server

- [ ] Log content has no secrets or personal data (LOG-2, LOG-3)
- [ ] Any cache has a measured need or a spec, and goes through `HybridCache` (PRF-2)
- [ ] Reads project to records or use `AsNoTracking()`, with no N+1 queries (DAT-4)
- [ ] Tests are named `<Behavior>_<expected>_when_<condition>`, one behavior each (TST-3)
- [ ] A new package has an allowed license (spec 0009 DEP-1) and recent maintenance (GEN-5)

## Console

- [ ] Loaders feed the query cache; nothing reads `useLoaderData` (RTE-4)
- [ ] Search param writes use the right history mode (URL-3, UX-27)
- [ ] State sits in the first place that fits (STA-1)
- [ ] A new dependency's gzip size and reason are in this description; over 10 KB needs review's agreement (WEB-7, BDL-3)
- [ ] Writes update the cache the way QRY-6 says (QRY-6)
- [ ] The screen follows its spec's UX IDs, including what tooling can't check (UX-6, UX-36, UX-38)

## Security

- [ ] The feature spec's ASVS criteria are met (STD-1)
- [ ] Keys keep to one project and their type's scope ceiling (KEY-4)
- [ ] Nothing here weakens operator sign in or TOTP (CON-4)
- [ ] Every new rule disable says why on the same line (LNT-5)

## Docs

- [ ] A changed behavior or command is reflected in the matching `AGENTS.md` or spec (or `/sync` is planned)
