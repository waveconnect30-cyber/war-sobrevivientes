-- Applied to Supabase project "War Tanks" (qbqfysphnotknygiurnj).
-- Frostbound data remains isolated from Mini War Tanks tables.
create table if not exists public.frostbound_saves (
    user_id uuid primary key references auth.users(id) on delete cascade,
    save_json text not null check (char_length(save_json) between 2 and 100000),
    client_saved_at bigint not null default 0,
    updated_at timestamptz not null default now()
);

alter table public.frostbound_saves enable row level security;

create policy "frostbound players read own save"
on public.frostbound_saves for select
to authenticated
using ((select auth.uid()) = user_id);

create policy "frostbound players insert own save"
on public.frostbound_saves for insert
to authenticated
with check ((select auth.uid()) = user_id);

create policy "frostbound players update own save"
on public.frostbound_saves for update
to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

grant select, insert, update on public.frostbound_saves to authenticated;
