-- Run in midian-game's SQL editor. Preserves all existing rows.
BEGIN;
CREATE TABLE IF NOT EXISTS public.game_saves (
  app_id text NOT NULL,
  openid text NOT NULL,
  profile jsonb NOT NULL DEFAULT '{}'::jsonb,
  revision bigint NOT NULL DEFAULT 1,
  updated_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (app_id, openid)
);
ALTER TABLE public.game_saves ADD COLUMN IF NOT EXISTS last_operation_id text;
ALTER TABLE public.game_saves ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.game_saves FROM PUBLIC, anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON TABLE public.game_saves TO service_role;
COMMIT;
-- No public RLS policy is needed. Only the server's API Key (service_role)
-- accesses the table; every HTTP request is authenticated by the cloud function.
