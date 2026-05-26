# LocalGo SIT Deploy (Hybrid ฟรี)

Deploy สภาพแวดล้อม **SIT** ด้วย:

| ชั้น | แพลตฟอร์ม | URL ตัวอย่าง |
|------|-----------|--------------|
| Frontend | Cloudflare Pages | `https://localgo-sit.pages.dev` |
| API | Render (Docker, Singapore) | `https://be-localgo.onrender.com` |
| Database | Neon PostgreSQL + PostGIS (Singapore) | connection string |
| Redis (optional) | Upstash Redis (Singapore) | `rediss://...` |

Repo นี้เตรียม Dockerfile, `render.yaml`, GitHub Actions และสคริปต์ build ไว้แล้ว — ส่วนที่ต้องสมัครบัญชีและใส่ secret ทำตามลำดับด้านล่าง

---

## สิ่งที่ repo เตรียมไว้แล้ว

- `be-localgo/Dockerfile` — .NET 10 API container (port 8080)
- `be-localgo/src/LocalGo.Api/appsettings.Staging.json.example` — ตัวอย่าง config
- `be-localgo/src/LocalGo.Api/Program.cs` — รองรับ `Staging`, env vars, migrate อัตโนมัติ
- `render.yaml` — Blueprint สำหรับ Render
- `scripts/generate-fe-environment.mjs` — สร้าง `environment.sit.ts` ตอน build
- `scripts/sit-neon-init.sql` — เปิด PostGIS บน Neon
- `scripts/sit-verify.sh` — ตรวจ health หลัง deploy
- `.github/workflows/build-sit-api.yml` — build Docker ใน CI
- `.github/workflows/deploy-sit-fe.yml` — deploy FE ไป Cloudflare Pages
- `fe-localgo/public/_redirects` — SPA routing บน Pages

---

## ลำดับที่แนะนำ (คุณทำเอง)

### 1) Neon — PostgreSQL + PostGIS

1. สมัคร [neon.tech](https://neon.tech)
2. สร้าง Project ชื่อ `localgo-sit`
3. **Region:** `AWS Asia Pacific (Singapore)` — `aws-ap-southeast-1`
4. เปิด **SQL Editor** รัน:

```sql
-- หรือใช้ไฟล์ scripts/sit-neon-init.sql
CREATE EXTENSION IF NOT EXISTS postgis;
```

5. คัดลอก **connection string** (แบบ pooled หรือ direct ก็ได้ — ต้องมี `SSL Mode=Require`)

เก็บไว้เป็น `ConnectionStrings__Default` บน Render

---

### 2) Upstash Redis (optional แต่แนะนำ)

1. สมัคร [upstash.com](https://upstash.com)
2. สร้าง Redis database region **Singapore**
3. คัดลอก URL แบบ `rediss://default:...@....upstash.io:6379`

ถ้ายังไม่ใส่ API จะขึ้น health **Degraded** ได้ (ยังใช้งานหลักได้)

---

### 3) Render — API

**วิธี A: Blueprint (ง่าย)**

1. Push repo ขึ้น GitHub (ถ้ายังไม่ได้ push)
2. [Render Dashboard](https://dashboard.render.com) → **New** → **Blueprint**
3. เชื่อม repo → เลือก `render.yaml`
4. ใส่ค่า env ที่ Render ถาม (sync: false):

| Env var (Render) | ค่า |
|------------------|-----|
| `ConnectionStrings__Default` | Neon connection string |
| `ConnectionStrings__Redis` | Upstash URL หรือว่าง |
| `Jwt__SigningKey` | สุ่มอย่างน้อย 32 ตัวอักษร |
| `Line__ChannelId` | `2010177240` |
| `Line__ChannelSecret` | จาก LINE Console |
| `Line__LiffId` | `2010177240-Bu98ZCLT` |
| `Line__MessagingAccessToken` | จาก Messaging API tab |
| `Cors__AllowedOrigins` | URL Cloudflare Pages (ขั้น 4) — ใส่ทีหลังได้ |

5. Deploy รอ build Docker เสร็จ
6. ทดสอบ: `https://<service-name>.onrender.com/api/health`

**หมายเหตุ free tier:** service หลับหลัง idle ~15 นาที → cold start 30–60 วิ

**วิธี B: Manual**

- New Web Service → Docker
- Root directory: `be-localgo`
- Region: **Singapore**
- Health check path: `/api/health`

---

### 4) Cloudflare Pages — Frontend

**วิธี A: GitHub Actions (แนะนำถ้าใช้ CI)**

1. สมัคร [Cloudflare](https://dash.cloudflare.com)
2. สร้าง Pages project ชื่อ **`localgo-sit`** (ว่างไว้ก่อน deploy ครั้งแรกก็ได้)
3. สร้าง API Token: My Profile → API Tokens → Custom token
   - Permission: **Account → Cloudflare Pages → Edit**
4. ใส่ **GitHub Secrets** ใน repo:

| Secret | ตัวอย่าง |
|--------|----------|
| `CLOUDFLARE_API_TOKEN` | token จากข้อ 3 |
| `CLOUDFLARE_ACCOUNT_ID` | จาก Cloudflare Dashboard URL |
| `SIT_API_BASE_URL` | `https://be-localgo.onrender.com/api` |
| `SIT_LIFF_ID` | `2010177240-Bu98ZCLT` |

5. Push ขึ้น `main` หรือรัน workflow **Deploy SIT Frontend** แบบ manual

**วิธี B: Dashboard โดยตรง**

Build settings:

| Field | Value |
|-------|-------|
| Root directory | `fe-localgo` |
| Build command | `node ../scripts/generate-fe-environment.mjs && npm ci && npm run build:sit` |
| Build output | `dist/localgo/browser` |
| Environment variables | `NG_APP_API_BASE_URL`, `NG_APP_LIFF_ID` (เหมือน `.env.sit.example`) |

---

### 5) อัปเดต CORS + LINE Console

หลังได้ URL FE จริง (เช่น `https://localgo-sit.pages.dev`):

1. **Render** → แก้ `Cors__AllowedOrigins` = `https://localgo-sit.pages.dev` (ไม่มี `/` ท้าย)
2. **LINE Developers** → Channel `2010177240`:
   - **LIFF Endpoint URL** = `https://localgo-sit.pages.dev/` (ต้องมี `/` ท้าย)
   - **LINE Login Callback URL** = URL เดียวกัน
3. Redeploy API ถ้าเปลี่ยน CORS

---

### 6) ตรวจสอบ

```bash
chmod +x scripts/sit-verify.sh

# หลัง API deploy
./scripts/sit-verify.sh https://be-localgo.onrender.com

# หลัง FE deploy
./scripts/sit-verify.sh https://be-localgo.onrender.com https://localgo-sit.pages.dev
```

เปิด LIFF:

```text
https://liff.line.me/2010177240-Bu98ZCLT/
```

Swagger SIT (เปิดบน Staging):

```text
https://be-localgo.onrender.com/swagger
```

---

## Build ทดสอบบนเครื่อง

```bash
# FE
export NG_APP_API_BASE_URL=https://be-localgo.onrender.com/api
node scripts/generate-fe-environment.mjs
cd fe-localgo && npm run build:sit

# API Docker
docker build -t localgo-api-sit ./be-localgo
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Default='Host=...' \
  -e Jwt__SigningKey='your-32-char-minimum-signing-key-here' \
  -e Cors__AllowedOrigins='http://localhost:4200' \
  localgo-api-sit
```

---

## Env vars สรุป (API)

Render ใช้ `__` คั่น nested config:

```
ASPNETCORE_ENVIRONMENT=Staging
ConnectionStrings__Default=<Neon>
ConnectionStrings__Redis=<Upstash or empty>
Jwt__SigningKey=<32+ chars>
Jwt__Issuer=LocalGo-SIT
Jwt__Audience=LocalGo-SIT
Line__ChannelId=2010177240
Line__ChannelSecret=<secret>
Line__LiffId=2010177240-Bu98ZCLT
Line__MessagingAccessToken=<token>
Cors__AllowedOrigins=https://localgo-sit.pages.dev
```

---

## Troubleshooting

| อาการ | แก้ |
|-------|-----|
| `/api/health` database Unhealthy | ตรวจ Neon connection string + รัน `CREATE EXTENSION postgis` |
| LINE login 401 | LIFF channel ต้องตรงกับ `Line__ChannelId` / Endpoint URL |
| CORS error จาก FE | `Cors__AllowedOrigins` ต้องตรง origin (https, ไม่มี path) |
| Render cold start ช้า | ปกติของ free tier — upgrade Starter $7/mo ถ้าต้อง always-on |
| Neon cold start | request แรกหลัง idle อาจช้า ~1–2 วิ |

---

## เอกสารที่เกี่ยวข้อง

- [`doc/design-spec/19-environments.md`](design-spec/19-environments.md)
- [`doc/line-dev-urls.md`](line-dev-urls.md)
- [`fe-localgo/.env.sit.example`](../fe-localgo/.env.sit.example)
- [`be-localgo/src/LocalGo.Api/appsettings.Staging.json.example`](../be-localgo/src/LocalGo.Api/appsettings.Staging.json.example)
