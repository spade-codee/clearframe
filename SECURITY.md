# Security

Security fixes target the latest release. This project does not promise support for older releases.

Report a vulnerability using [GitHub's private reporting form](https://github.com/spade-codee/clearframe/security/advisories/new). If private reporting is unavailable, open an issue asking the maintainer for a private contact route without including exploit details or personal data.

Do not post browser cookies, tokens, account credentials, full local paths or private video URLs. Review diagnostic logs before sharing them.

ClearFrame accepts individual YouTube links, passes process arguments without a command shell, and verifies tool downloads with pinned checksums. These safeguards are not a security audit. Third-party download engines and media parsers also need maintenance. Executables are currently unsigned.
