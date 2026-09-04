begin;

alter table public.frostbound_world_tiles
    add column if not exists res_type text null,
    add column if not exists res_capacity integer not null default 0,
    add column if not exists res_remaining integer not null default 0;

update public.frostbound_world_tiles
set res_type = case abs(x + y) % 3 when 0 then 'Wood' when 1 then 'Food' else 'Coal' end,
    res_capacity = 5000,
    res_remaining = case when res_remaining > 0 then res_remaining else 5000 end
where tile_type = 'ResourceNode';

alter table public.frostbound_world_tiles drop constraint if exists frostbound_world_tiles_resource_type_check;
alter table public.frostbound_world_tiles add constraint frostbound_world_tiles_resource_type_check
check (
    (tile_type = 'ResourceNode' and res_type in ('Wood', 'Food', 'Coal') and res_capacity > 0 and res_remaining between 0 and res_capacity)
    or (tile_type <> 'ResourceNode' and res_type is null and res_capacity = 0 and res_remaining = 0)
);

create table if not exists public.frostbound_marches (
    id uuid primary key,
    user_id uuid not null references auth.users(id) on delete cascade,
    origin_x integer not null check (origin_x between 0 and 1199),
    origin_y integer not null check (origin_y between 0 and 1199),
    target_x integer not null check (target_x between 0 and 1199),
    target_y integer not null check (target_y between 0 and 1199),
    march_type text not null check (march_type in ('Gathering', 'Return')),
    res_type text not null check (res_type in ('Wood', 'Food', 'Coal')),
    payload_amount integer not null default 0 check (payload_amount >= 0),
    departure_time timestamptz not null,
    arrival_time timestamptz not null,
    status text not null check (status in ('Marching', 'Gathering', 'Return', 'Completed')),
    updated_at timestamptz not null default now()
);

create index if not exists frostbound_marches_user_status_idx
    on public.frostbound_marches (user_id, status);
create index if not exists frostbound_marches_arrival_idx
    on public.frostbound_marches (arrival_time)
    where status <> 'Completed';

alter table public.frostbound_marches enable row level security;
revoke all on table public.frostbound_marches from public, anon, authenticated;
grant select, insert, update, delete on table public.frostbound_marches to authenticated;

drop policy if exists frostbound_marches_select_own on public.frostbound_marches;
create policy frostbound_marches_select_own on public.frostbound_marches for select to authenticated
using ((select auth.uid()) = user_id);
drop policy if exists frostbound_marches_insert_own on public.frostbound_marches;
create policy frostbound_marches_insert_own on public.frostbound_marches for insert to authenticated
with check ((select auth.uid()) = user_id);
drop policy if exists frostbound_marches_update_own on public.frostbound_marches;
create policy frostbound_marches_update_own on public.frostbound_marches for update to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);
drop policy if exists frostbound_marches_delete_own on public.frostbound_marches;
create policy frostbound_marches_delete_own on public.frostbound_marches for delete to authenticated
using ((select auth.uid()) = user_id);

drop trigger if exists frostbound_marches_set_updated_at on public.frostbound_marches;
create trigger frostbound_marches_set_updated_at before update on public.frostbound_marches
for each row execute function public.frostbound_set_updated_at();

with generated_nodes as (
    select
        ((73 + n * 47) % 1200)::integer as x,
        ((191 + n * 83) % 1200)::integer as y,
        case n % 3 when 0 then 'Wood' when 1 then 'Food' else 'Coal' end as res_type,
        (1 + n % 5)::integer as level
    from generate_series(0, 179) as n
)
insert into public.frostbound_world_tiles
    (x, y, tile_type, occupant_id, level, res_type, res_capacity, res_remaining)
select x, y, 'ResourceNode', null, level, res_type, 5000, 5000
from generated_nodes
on conflict (x, y) do update
set tile_type = excluded.tile_type,
    occupant_id = null,
    level = excluded.level,
    res_type = excluded.res_type,
    res_capacity = excluded.res_capacity,
    res_remaining = excluded.res_remaining
where public.frostbound_world_tiles.tile_type = 'Empty';

commit;
