# Security Policy

> **Important:** Azure Event Grid Simulator is intended for **local development and testing only**. It should **never** be used in production environments. For production workloads, use the official [Azure Event Grid](https://azure.microsoft.com/en-au/services/event-grid/) service.

## Supported Versions

The following versions of Azure Event Grid Simulator are currently supported with security updates:

| Version | Supported          |
| ------- | ------------------ |
| 4.x     | :white_check_mark: |
| < 4.0   | :x:                |

## Reporting a Vulnerability

We take security vulnerabilities seriously. If you discover a security issue, please report it responsibly.

**Please do NOT report security vulnerabilities through public GitHub issues.**

Instead, please report them using GitHub's private vulnerability reporting feature:

1. Go to the [Security tab](https://github.com/pmcilreavy/AzureEventGridSimulator/security) of this repository
2. Click "Report a vulnerability"
3. Fill in the details of the vulnerability

### What to include in your report

- A description of the vulnerability
- Steps to reproduce the issue
- Potential impact of the vulnerability
- Any suggested fixes (optional)

### What to expect

- You will receive an acknowledgment within 48 hours
- We will investigate and provide updates on the progress
- Once the issue is resolved, we will publicly acknowledge your contribution (unless you prefer to remain anonymous)

## Security Best Practices

When using Azure Event Grid Simulator for local development:

- Keep your `aeg-sas-key` values secure and do not commit them to source control
- Use environment variables for sensitive configuration
- Do not expose the simulator to the public internet
- Use the official Azure Event Grid service for any production or internet-facing scenarios
