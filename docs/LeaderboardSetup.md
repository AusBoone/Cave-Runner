<!--
  LeaderboardSetup.md
  --------------------
  Deployment and configuration guide for the custom leaderboard service used
  by Cave Runner. This document outlines how to host the REST API securely and
  connect the Unity client via `LeaderboardClient`.
-->

# Leaderboard Service Setup

The game includes a lightweight HTTP API for storing and retrieving high
scores when Steam leaderboards are unavailable. This guide explains how to
deploy the service and configure the client.

## Deployment Requirements

- **HTTPS only** – The service **must** be hosted on an HTTPS endpoint to
  protect player data. The client refuses to send requests to plain HTTP
  addresses.
- Provide an endpoint responding to:
  - `GET /scores` – returns an array of `{ "name": string, "score": number }`.
  - `POST /scores` – accepts a JSON payload with the same structure.

## Configuring the Unity Client

1. Add the `LeaderboardClient` component to a persistent GameObject.
2. Set the **Service Url** field to the HTTPS base URL of your deployed API.
   - The value is empty by default, preventing accidental requests during
     development.
3. Reference the `LeaderboardClient` from `UIManager` to display remote scores.

If the URL is missing or does not use HTTPS, the client logs an error and
falls back to the local high score.

## Example

```
Service Url: https://leaderboards.example.com/api
```

With this configuration, the client accesses `https://leaderboards.example.com/api/scores`
for both uploads and downloads.

## Troubleshooting

- Ensure your SSL certificate is valid and trusted; otherwise Unity may reject
  the connection.
- Use developer tools like `curl` or browser networking tabs to verify the API
  responds correctly.

