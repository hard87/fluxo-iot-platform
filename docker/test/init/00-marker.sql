-- Marks this server as a disposable test environment.
--
-- DisposableTestDatabase refuses to CREATE or DROP anything on a server where this table
-- is absent. That is the guard that stops FLUXO_TESTS_POSTGRES_ADMIN_CONNECTION from being
-- pointed at the pilot database by accident: asking the server itself is the only check
-- that cannot be defeated by a plausible-looking connection string.
--
-- Never create this table on a real environment.

CREATE TABLE IF NOT EXISTS public.fluxo_disposable_test_marker (
    id              integer PRIMARY KEY DEFAULT 1,
    purpose         text        NOT NULL,
    created_at_utc  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fluxo_disposable_test_marker_single_row CHECK (id = 1)
);

INSERT INTO public.fluxo_disposable_test_marker (id, purpose)
VALUES (1, 'Disposable PostgreSQL for Fluxo automated tests. Storage depends on the selected test profile.')
ON CONFLICT (id) DO NOTHING;

COMMENT ON TABLE public.fluxo_disposable_test_marker IS
    'Presence of this table authorises destructive test DDL. Absent on real environments.';
