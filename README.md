# octopus-deploy-variable-viewer
Simple tool for fast lookup of Octopus Deploy variable sets.

## Problem
Quick lookups isn't possible with Octopus Deploy, with this simple GUI it is ;)

## Setup
Just fill the config.js file with your own Octopus Deploy host and Octopus Deploy API key.
```
window.config = {
  host: "https://octopus.company.com",
  apiKey: "API-JUST-A-PLACE-HOLDER"
}
```
See: https://octopus.com/docs/octopus-rest-api/how-to-create-an-api-key for obtaining an API Key

Note: this is just a quick and dirty script, not intended to use in production environments. 
