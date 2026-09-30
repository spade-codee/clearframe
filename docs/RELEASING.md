# Releasing ClearFrame

`VERSION` contains `MAJOR.MINOR.PATCH`, without a leading `v`. The build generates the assembly version, file version, informational version, and manifest version from it. The UI reads the assembly version. Do not edit generated `.build/` files.

1. Update `VERSION` and add an entry to `CHANGELOG.md`.
2. Run `./scripts/Build.ps1`, `./scripts/Test.ps1`, and `./scripts/Package.ps1` on Windows.
3. Review the ZIP: it should contain the app, configuration, license, guide, installer and tool lock file; no third-party executables, media, history or credentials.
4. Commit the changes and push `main`. Wait for CI to pass.
5. Create an annotated tag matching the version, for example:

   ```powershell
   git tag -a v0.2.1 -m "ClearFrame 0.2.1"
   git push origin v0.2.1
   ```

6. The release workflow verifies the tag, builds and tests, packages the app, and publishes the ZIP and SHA-256 checksum on GitHub Releases. It uses only the workflow's short-lived GitHub token.

If a tag's release fails, fix the cause and inspect the run before retrying. Do not silently move a published tag. Tool lock updates should use versioned upstream URLs, never a mutable `/latest/` asset; if an asset is removed, setup fails rather than substituting an unchecked version.

The ZIP's version is the application version. External tool versions are independent and tracked in `config/tools.lock.json`. The in-app engine-update button can change the installed yt-dlp version after setup.
