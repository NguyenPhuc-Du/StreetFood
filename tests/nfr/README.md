# StreetFood NFR Load Test Guide

This folder contains k6 load-test scenarios for StreetFood MVP operations.

## 1) Prerequisites

- Install [k6](https://k6.io/docs/get-started/installation/)
- Start `StreetFoodAPI`
- Ensure a valid POI exists (default `POI_ID=1`)

## 2) Scenarios

- `streetfood-read-load.js`: read-heavy (`/api/health`, `/api/Poi`)
- `streetfood-write-load.js`: write-heavy (`/api/analytics/poi-audio-listen`)
- `streetfood-mixed-load.js`: combined read + write
- `streetfood-smoke.js`: lightweight smoke check (existing script)

## 3) Load Profiles

Use `LOAD_LEVEL` to switch concurrency profile:

- `50`: ramp to 50 VUs, hold 90s
- `100`: ramp to 100 VUs, hold 2m
- `200`: ramp to 200 VUs, hold 3m

## 4) Run Commands

### Read-heavy

```bash
k6 run -e BASE_URL=https://localhost:7236 -e LOAD_LEVEL=50 tests/nfr/streetfood-read-load.js
```

### Write-heavy

```bash
k6 run -e BASE_URL=https://localhost:7236 -e LOAD_LEVEL=100 -e POI_ID=1 tests/nfr/streetfood-write-load.js
```

### Mixed

```bash
k6 run -e BASE_URL=https://localhost:7236 -e LOAD_LEVEL=200 -e POI_ID=1 tests/nfr/streetfood-mixed-load.js
```

### Against ngrok URL

```bash
k6 run -e BASE_URL=https://<your-ngrok-domain> -e LOAD_LEVEL=50 tests/nfr/streetfood-mixed-load.js
```

## 5) Pass/Fail Baseline

All scripts use baseline thresholds:

- `http_req_failed < 2%`
- `p95(http_req_duration) < 800ms` for successful responses

If thresholds fail at a load level, treat that level as unstable for MVP.

## 6) Capacity Test Flow (Recommended)

Run in order:

1. `LOAD_LEVEL=50` for mixed scenario
2. `LOAD_LEVEL=100` for mixed scenario
3. `LOAD_LEVEL=200` for mixed scenario

Record each run:

- error rate
- p95 latency
- request rate
- failed checks

The highest level that still meets thresholds is your current safe MVP capacity.

## 7) Notes

- `online-now` is now near real-time with a 5-second window and 5-second cache.
- For consistent results, run tests against local API first, then verify through ngrok.

## 8) One-command Capacity Run (PowerShell)

You can run all levels `50 -> 100 -> 200` in one command:

```powershell
powershell -ExecutionPolicy Bypass -File tests/nfr/run-capacity.ps1 -BaseUrl https://localhost:7236 -Scenario mixed -PoiId 1
```

Optional:

- `-Scenario read|write|mixed` (default `mixed`)
- `-OutputDir tests/nfr/results`
