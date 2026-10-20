# octopus-deploy-variable-viewer
Simple CLI tool for fast lookup of Octopus Deploy project variables.

## Problem
Quick lookups of variable values across environments aren't possible in the Octopus Deploy UI. This tool shows them side by side in your terminal.

## Setup
Requires the .NET 10 SDK.

Create an `appsettings.json` next to `octopus-deploy-variable-viewer.csproj`:

```json
{
  "Octopus": {
    "Host": "https://octopus.host.com",
    "ApiKey": "API-KEY-PLACEHOLDER",
    "Space": "Spaces-1"
  },
  "Display": {
    "MaxValueWidth": 40
  }
}
```

`Display:MaxValueWidth` is optional (default 40). Longer values are cut off with `…`; set it to `0` to always show full values.

See: https://octopus.com/docs/octopus-rest-api/how-to-create-an-api-key for obtaining an API Key

Then run:

```
dotnet run
```

## Usage
Pick a project from the list (type to search). The tool shows:

- a header with the project name, space, and variable and environment counts
- **Differs per environment**: one column per environment, with its deployment targets (hosts) listed under the environment name
- **Same in every environment**: variables with a single value everywhere, in a compact two-column table

Values that come from an unscoped variable are shown in grey. Sensitive values are masked. Scopes other than environment (role, machine, channel, ...) are listed after the value.

For version-controlled (config-as-code) projects, variables are read from the project's default branch, plus the sensitive variables that Octopus keeps in its database. The branch is shown in the header.
To show full values for one run, ignoring `MaxValueWidth`:

```
dotnet run -- --full
```

Note: this is just a quick and dirty implementation, usage at your own risk
