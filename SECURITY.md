# Security Policy

## Reporting a vulnerability

Do not include credentials, connection strings, tokens, or customer data in issues, pull requests, or public channels. Report suspected exposures through the project owner's private channel and include only the affected path, revision, and the minimum evidence needed to investigate.

## Immediate response

1. Revoke or rotate the affected credential outside the repository.
2. Remove the value from active configuration and documentation.
3. Record the incident through the private project channel.
4. Review repository history and deployed environments before declaring the incident closed.

Removing a value from a repository file does not revoke it. Rotation or revocation must be completed in the provider or affected environment.

## Repository rules

- Version only placeholders and configuration templates.
- Keep local configuration files, certificates, publish profiles, and secret files out of Git.
- Use the repository secret scan before publishing changes.
- Run destructive or historical database scripts only with an approved backup and explicit authorization.
- Follow [the credential configuration guide](Trilogia-Cursos/docs/credenciales-configuracion-segura.md) for local development and deployment configuration.

## Affected environments

Local development, shared development resources, Azure-hosted environments, and CI configuration must be reviewed whenever an exposure is suspected. This repository policy does not authorize changing any Azure resource directly.

## Known historic risk

Historical SMTP examples may have existed in prior revisions. Treat any related value as compromised: rotate it, verify current Azure configuration, and do not reuse it.
