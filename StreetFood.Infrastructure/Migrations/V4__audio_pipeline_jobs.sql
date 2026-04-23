-- Hàng đợi tác vụ dịch/TTS/regenerate (PRD §10.2)
CREATE TABLE IF NOT EXISTS audio_pipeline_jobs (
    id              BIGSERIAL PRIMARY KEY,
    idempotency_key TEXT,
    job_type        TEXT        NOT NULL DEFAULT 'tts_poi',
    payload         JSONB       NOT NULL,
    status          TEXT        NOT NULL DEFAULT 'pending',
    attempt_count   INT         NOT NULL DEFAULT 0,
    last_error      TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    next_run_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS audio_pipeline_jobs_idem_uq
    ON audio_pipeline_jobs (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS audio_pipeline_jobs_status_next_idx
    ON audio_pipeline_jobs (status, next_run_at)
    WHERE status IN ('pending', 'processing', 'retrying');
