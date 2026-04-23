-- Premium + MoMo payment schema
-- Keep source-of-truth flags on POIs and detailed order/subscription tables.

ALTER TABLE pois
    ADD COLUMN IF NOT EXISTS is_premium BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS premium_end_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS last_premium_order_id TEXT;

CREATE INDEX IF NOT EXISTS idx_pois_is_premium
    ON pois (is_premium, premium_end_at DESC NULLS LAST);

CREATE TABLE IF NOT EXISTS poi_premium_subscriptions (
    poi_id          INT PRIMARY KEY REFERENCES pois(id) ON DELETE CASCADE,
    plan_name       TEXT        NOT NULL DEFAULT 'premium',
    status          TEXT        NOT NULL DEFAULT 'active',
    price_vnd       INT         NOT NULL DEFAULT 199000,
    started_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    end_at          TIMESTAMPTZ NOT NULL,
    last_order_id   TEXT,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS vendor_payment_orders (
    order_id        TEXT PRIMARY KEY,
    request_id      TEXT        NOT NULL,
    vendor_user_id  INT         NOT NULL REFERENCES users(id),
    poi_id          INT         NOT NULL REFERENCES pois(id) ON DELETE CASCADE,
    provider        TEXT        NOT NULL,
    amount_vnd      INT         NOT NULL,
    status          TEXT        NOT NULL,
    trans_id        TEXT,
    raw_response    JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    paid_at         TIMESTAMPTZ,
    expires_at      TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS vendor_payment_orders_status_idx
    ON vendor_payment_orders (status, created_at DESC);

CREATE INDEX IF NOT EXISTS vendor_payment_orders_poi_idx
    ON vendor_payment_orders (poi_id, created_at DESC);

