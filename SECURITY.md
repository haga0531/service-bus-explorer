# Security Policy

## Supported Versions

We release patches for security vulnerabilities. Which versions are eligible for receiving such patches depends on the CVSS v3.0 Rating:

| Version | Supported          |
| ------- | ------------------ |
| latest  | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability within Service Bus Explorer, please follow these steps:

1. **DO NOT** create a public GitHub issue for security vulnerabilities
2. Send a detailed report to [security@your-email.com]
3. Include the following information:
   - Type of issue (e.g., buffer overflow, SQL injection, cross-site scripting, etc.)
   - Full paths of source file(s) related to the manifestation of the issue
   - The location of the affected source code (tag/branch/commit or direct URL)
   - Any special configuration required to reproduce the issue
   - Step-by-step instructions to reproduce the issue
   - Proof-of-concept or exploit code (if possible)
   - Impact of the issue, including how an attacker might exploit it

## Response Timeline

- **Initial Response**: Within 48 hours
- **Status Update**: Within 7 days
- **Resolution**: Varies based on complexity, typically within 30 days

## Security Best Practices for Users

1. **Connection Strings**: 
   - Never commit connection strings to source control
   - Use environment variables or secure vaults
   - Regularly rotate your Service Bus keys

2. **Application Updates**:
   - Always use the latest version
   - Enable automatic updates if available
   - Monitor our releases page for security updates

3. **Network Security**:
   - Use Service Bus with Private Endpoints when possible
   - Enable firewall rules on your Service Bus namespace
   - Use managed identities instead of connection strings when possible

## Security Features

Service Bus Explorer implements several security measures:

- Connection strings are not persisted by default
- No automatic telemetry or data collection
- All Service Bus operations use the official Azure SDK
- Support for managed identity authentication

## Acknowledgments

We appreciate responsible disclosure of security vulnerabilities. Contributors who report valid security issues will be acknowledged in our Hall of Fame (unless they prefer to remain anonymous).

## Contact

For any security-related questions, contact:
- Email: [security@your-email.com]
- PGP Key: [Link to PGP key if available]