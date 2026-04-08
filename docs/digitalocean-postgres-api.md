# DigitalOcean Setup

## Goal

Run a central PostgreSQL-backed API so two MuaythaiApp desktop clients can work against the same live data.

## Recommended first server

- Provider: DigitalOcean
- Image: Ubuntu 24.04 LTS
- Size: Basic droplet
- Region: choose the one closest to your venue

## What to install on the droplet

1. ASP.NET 8 runtime
2. PostgreSQL 16
3. Nginx
4. Your published `MuaythaiApp.Api`

## Environment variable

Set this on the server before starting the API:

`ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=muaythaiapp;Username=muaythaiapp;Password=YOUR_PASSWORD`

## First deployment flow

1. Create the droplet.
2. Open firewall ports `22`, `80`, and `443`.
3. Install PostgreSQL.
4. Create database `muaythaiapp`.
5. Create user `muaythaiapp`.
6. Give that user ownership of the database.
7. Publish the API from this repo.
8. Copy publish output to the server.
9. Run the API behind `systemd`.
10. Put Nginx in front of it.

## Next step after server is ready

When the droplet is created, send back:

- droplet public IP
- PostgreSQL username
- PostgreSQL database name
- whether you want HTTP only first, or HTTPS with a domain

Then the desktop app can be switched from SQLite calls to API calls.
