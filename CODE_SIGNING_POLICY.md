# Code Signing Policy

This project signs official Windows release binaries built from this public repository so Windows users can install the **English (Bangladesh) [`en-BD`]** regional locale and **US with Taka sign** keyboard layout safely without tampering risks.

> Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/). *(Pending approval - interim preview builds are unsigned and clearly labelled on GitHub Releases.)*

## Scope of Signed Binaries

Only `en-BD-Setup.exe` (and its embedded `kbdusbd.dll` layout binaries compiled from source in this repository) built by GitHub Actions are submitted for signing. No third-party or precompiled binaries are signed with this project's certificate.

## Team Roles & Build Provenance

* **Maintainers / Committers:** Members of the `ekushey-org` GitHub organisation with write access to this repository.
* **Approvers:** Designated `ekushey.org` maintainers who verify release tags and approve signing requests in SignPath.
* **Build Environment:** Release binaries are compiled exclusively on GitHub-hosted Windows runners (`windows-2022`) from tagged commits on `main`. Private signing keys are stored strictly on SignPath's Hardware Security Module (HSM).

## Privacy & End-User Safety

* **Zero telemetry or background services:** `en-BD-Setup.exe` collects no user data and exits completely once setup finishes.
* **Network usage:** Connects to no third-party servers.
* **Clean uninstall:** Backs up prior Windows regional and language settings automatically and registers a standard uninstaller in Windows *Installed Apps* to remove `en-BD.nlp`, `kbdusbd.dll`, and restore previous settings.
