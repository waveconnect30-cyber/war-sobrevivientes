begin;

create table if not exists public.frostbound_world_tiles (
    id bigint generated always as identity primary key,
    x integer not null check (x between 0 and 1199),
    y integer not null check (y between 0 and 1199),
    tile_type text not null default 'Empty'
        check (tile_type in ('Empty', 'PlayerCity', 'ResourceNode', 'Beast', 'Fortress')),
    occupant_id uuid null references auth.users(id) on delete set null,
    level integer not null default 1 check (level between 1 and 100),
    updated_at timestamptz not null default now(),
    constraint frostbound_world_tiles_coordinates_key unique (x, y),
    constraint frostbound_world_tiles_occupancy_check check (
        (tile_type = 'PlayerCity' and occupant_id is not null)
        or (tile_type <> 'PlayerCity' and occupant_id is null)
    )
);

create index if not exists idx_world_coords
    on public.frostbound_world_tiles (x, y);
create index if not exists frostbound_world_tiles_occupant_idx
    on public.frostbound_world_tiles (occupant_id)
    where occupant_id is not null;

alter table public.frostbound_world_tiles enable row level security;

revoke all on table public.frostbound_world_tiles from public, anon, authenticated;
grant select, insert, update, delete on table public.frostbound_world_tiles to authenticated;
grant usage, select on sequence public.frostbound_world_tiles_id_seq to authenticated;

drop policy if exists frostbound_world_tiles_read_world on public.frostbound_world_tiles;
create policy frostbound_world_tiles_read_world
on public.frostbound_world_tiles for select
to authenticated
using (true);

drop policy if exists frostbound_world_tiles_insert_own_city on public.frostbound_world_tiles;
create policy frostbound_world_tiles_insert_own_city
on public.frostbound_world_tiles for insert
to authenticated
with check (
    (select auth.uid()) = occupant_id
    and tile_type = 'PlayerCity'
);

drop policy if exists frostbound_world_tiles_update_own_city on public.frostbound_world_tiles;
create policy frostbound_world_tiles_update_own_city
on public.frostbound_world_tiles for update
to authenticated
using (
    ((select auth.uid()) = occupant_id and tile_type = 'PlayerCity')
    or (occupant_id is null and tile_type = 'Empty')
)
with check (
    ((select auth.uid()) = occupant_id and tile_type = 'PlayerCity')
    or (occupant_id is null and tile_type = 'Empty')
);

drop policy if exists frostbound_world_tiles_delete_own_city on public.frostbound_world_tiles;
create policy frostbound_world_tiles_delete_own_city
on public.frostbound_world_tiles for delete
to authenticated
using ((select auth.uid()) = occupant_id and tile_type = 'PlayerCity');

-- Atomic relocation under the caller's RLS permissions. SECURITY INVOKER is
-- intentional: the function never bypasses the ownership policies above.
create or replace function public.frostbound_relocate_city(p_target_x integer, p_target_y integer)
returns void
language plpgsql
security invoker
set search_path = ''
as $$
declare
    v_user_id uuid := (select auth.uid());
    v_target public.frostbound_world_tiles%rowtype;
begin
    if v_user_id is null then
        raise exception 'Authentication required';
    end if;
    if p_target_x not between 0 and 1199 or p_target_y not between 0 and 1199 then
        raise exception 'Destination outside world bounds';
    end if;

    select * into v_target
    from public.frostbound_world_tiles
    where x = p_target_x and y = p_target_y
    for update;

    if found and v_target.tile_type = 'PlayerCity' and v_target.occupant_id = v_user_id then
        return;
    end if;
    if found and (v_target.tile_type <> 'Empty' or v_target.occupant_id is not null) then
        raise exception 'Destination tile is occupied';
    end if;

    update public.frostbound_world_tiles
    set tile_type = 'Empty', occupant_id = null, level = 1
    where occupant_id = v_user_id and tile_type = 'PlayerCity';

    if v_target.id is null then
        insert into public.frostbound_world_tiles (x, y, tile_type, occupant_id, level)
        values (p_target_x, p_target_y, 'PlayerCity', v_user_id, 1);
    else
        update public.frostbound_world_tiles
        set tile_type = 'PlayerCity', occupant_id = v_user_id, level = 1
        where id = v_target.id and tile_type = 'Empty' and occupant_id is null;

        if not found then
            raise exception 'Destination changed before relocation';
        end if;
    end if;
end;
$$;

revoke all on function public.frostbound_relocate_city(integer, integer) from public, anon;
grant execute on function public.frostbound_relocate_city(integer, integer) to authenticated;

drop trigger if exists frostbound_world_tiles_set_updated_at on public.frostbound_world_tiles;
create trigger frostbound_world_tiles_set_updated_at
before update on public.frostbound_world_tiles
for each row execute function public.frostbound_set_updated_at();

insert into public.frostbound_world_tiles (x, y, tile_type, level)
values
    (596, 602, 'ResourceNode', 2),
    (604, 597, 'ResourceNode', 3),
    (607, 604, 'Beast', 4),
    (592, 595, 'Beast', 2),
    (600, 608, 'Fortress', 5)
on conflict (x, y) do nothing;

commit;
