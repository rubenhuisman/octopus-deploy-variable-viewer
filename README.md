# octopus-deploy-variable-viewer
Simple tool for fast lookup of Octopus Deploy variable sets.

## Problem
Quick lookups isn't possible with Octopus Deploy, with this simple GUI it is.

## Setup
Make sure a HTTP server is serving this script.
Pass API key and host as URL params.

```localhost:8080/?apiKey=API-KEY-PLACEHOLDER&host=https://octopus.host.com```

See: https://octopus.com/docs/octopus-rest-api/how-to-create-an-api-key for obtaining an API Key

Note: this is just a quick and dirty implementation, usage at your own risk
