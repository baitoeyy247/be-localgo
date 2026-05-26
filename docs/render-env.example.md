# Render env vars (names only — paste values in Dashboard, never commit secrets)

Set these on Render for `be-localgo`:

```
ASPNETCORE_ENVIRONMENT=Staging
ConnectionStrings__Default=<Neon pooled connection string>
ConnectionStrings__Redis=<Upstash rediss:// URL>
Jwt__SigningKey=<random 32+ characters>
Jwt__Issuer=LocalGo-SIT
Jwt__Audience=LocalGo-SIT
Line__ChannelId=2010177240
Line__ChannelSecret=<from LINE Console>
Line__LiffId=2010177240-Bu98ZCLT
Line__MessagingAccessToken=<from LINE Console>
Cors__AllowedOrigins=https://localgo-sit.pages.dev
```

Neon: run `scripts/sit-neon-init.sql` once (`CREATE EXTENSION postgis`).

Local override (gitignored): copy values to `src/LocalGo.Api/appsettings.Staging.local.json`.
