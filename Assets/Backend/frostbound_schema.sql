-- Frostbound Frontier relational schema for Supabase.
-- Independent from Mini War Tanks tables. Safe to expose through the Data API with RLS.

create table if not exists public.frostbound_players (
    user_id uuid primary key references auth.users(id) on delete cascade,
    display_name text not null default 'SUPERVIVIENTE'
        check (char_length(display_name) between 1 and 24),
    temperature real not null default 12
        check (temperature between -100 and 100),
    population integer not null default 6 check (population >= 0),
    wood bigint not null default 180 check (wood >= 0),
    food bigint not null default 140 check (food >= 0),
    coal bigint not null default 50 check (coal >= 0),
    generator_level integer not null default 1 check (generator_level >= 1),
    health real not null default 100 check (health between 0 and 100),
    happiness real not null default 100 check (happiness between 0 and 100),
    power bigint not null default 0 check (power >= 0),
    client_saved_at bigint not null default 0,
    updated_at timestamptz not null default now()
);

create table if not exists public.frostbound_buildings (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users(id) on delete cascade,
    slot_id text not null check (slot_id ~ '^[a-z0-9_]{1,40}$'),
    building_type text not null
        check (building_type in ('generator', 'sawmill', 'kitchen', 'shelter')),
    level integer not null default 1 check (level between 1 and 100),
    assigned_workers integer not null default 0 check (assigned_workers between 0 and 1000),
    upgrade_started_at timestamptz,
    finishes_at timestamptz,
    pos_x real not null default 0,
    pos_z real not null default 0,
    updated_at timestamptz not null default now(),
    constraint frostbound_buildings_user_slot_unique unique (user_id, slot_id),
    constraint frostbound_buildings_timer_order check (
        upgrade_started_at is null
        or finishes_at is null
        or finishes_at >= upgrade_started_at
    )
);

create table if not exists public.frostbound_leaderboard (
    user_id uuid primary key references auth.users(id) on delete cascade,
    display_name text not null default 'SUPERVIVIENTE'
        check (char_length(display_name) between 1 and 24),
    generator_level integer not null default 1 check (generator_level >= 1),
    power bigint not null default 0 check (power >= 0),
    updated_at timestamptz not null default now()
);

create index if not exists frostbound_buildings_user_id_idx
    on public.frostbound_buildings (user_id);
create index if not exists frostbound_buildings_user_type_idx
    on public.frostbound_buildings (user_id, building_type);
create index if not exists frostbound_buildings_finishes_at_idx
    on public.frostbound_buildings (finishes_at)
    where finishes_at is not null;
create index if not exists frostbound_leaderboard_rank_idx
    on public.frostbound_leaderboard (power desc, generator_level desc);

alter table public.frostbound_players enable row level security;
alter table public.frostbound_buildings enable row level security;
alter table public.frostbound_leaderboard enable row level security;

create policy "frostbound_players_select_own"
on public.frostbound_players for select to authenticated
using ((select auth.uid()) = user_id);
create policy "frostbound_players_insert_own"
on public.frostbound_players for insert to authenticated
with check ((select auth.uid()) = user_id);
create policy "frostbound_players_update_own"
on public.frostbound_players for update to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

create policy "frostbound_buildings_select_own"
on public.frostbound_buildings for select to authenticated
using ((select auth.uid()) = user_id);
create policy "frostbound_buildings_insert_own"
on public.frostbound_buildings for insert to authenticated
with check ((select auth.uid()) = user_id);
create policy "frostbound_buildings_update_own"
on public.frostbound_buildings for update to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);
create policy "frostbound_buildings_delete_own"
on public.frostbound_buildings for delete to authenticated
using ((select auth.uid()) = user_id);

create policy "frostbound_leaderboard_read_rankings"
on public.frostbound_leaderboard for select to authenticated
using (true);
create policy "frostbound_leaderboard_insert_own"
on public.frostbound_leaderboard for insert to authenticated
with check ((select auth.uid()) = user_id);
create policy "frostbound_leaderboard_update_own"
on public.frostbound_leaderboard for update to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

-- Supabase projects can have broad default grants. Reduce them explicitly.
revoke all on table public.frostbound_players from anon, authenticated;
revoke all on table public.frostbound_buildings from anon, authenticated;
revoke all on table public.frostbound_leaderboard from anon, authenticated;
grant select, insert, update on table public.frostbound_players to authenticated;
grant select, insert, update, delete on table public.frostbound_buildings to authenticated;
grant select, insert, update on table public.frostbound_leaderboard to authenticated;

create or replace function public.frostbound_set_updated_at()
returns trigger
language plpgsql
security invoker
set search_path = ''
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

revoke all on function public.frostbound_set_updated_at() from public, anon, authenticated;

drop trigger if exists frostbound_players_set_updated_at on public.frostbound_players;
create trigger frostbound_players_set_updated_at
before update on public.frostbound_players
for each row execute function public.frostbound_set_updated_at();

drop trigger if exists frostbound_buildings_set_updated_at on public.frostbound_buildings;
create trigger frostbound_buildings_set_updated_at
before update on public.frostbound_buildings
for each row execute function public.frostbound_set_updated_at();

drop trigger if exists frostbound_leaderboard_set_updated_at on public.frostbound_leaderboard;
create trigger frostbound_leaderboard_set_updated_at
before update on public.frostbound_leaderboard
for each row execute function public.frostbound_set_updated_at();
