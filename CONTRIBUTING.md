# Contributing

Open an issue for a bug or proposed feature before a large change. Keep contributions focused and preserve the rule that a download must not silently downgrade or report success before verification.

1. Fork and clone the repository on Windows.
2. Run `./scripts/Build.ps1` and `./scripts/Test.ps1` in PowerShell 7.
3. Make the change and add meaningful regression coverage where needed.
4. Run those scripts again. Inspect updated UI renders for interface changes.
5. Open a pull request describing the user-visible behavior, validation and limitations.

Do not commit generated executables, dependencies, cookies, account tokens, download history or downloaded media. Use synthetic fixtures for automated tests. Network tests should be opt-in and use content you have permission to save.

Update CHANGELOG.md for user-visible changes. Version bumps and release tags are handled by the maintainer. Changes to dependency versions must include a reviewed upstream URL and SHA-256 digest in `config/tools.lock.json`.

By contributing, you agree that your contributions are licensed under the repository's MIT license. Third-party files must retain their own attribution and license.
