-- Frostbound Frontier · Hito 4: finalización atómica y tropas.
alter table public.frostbound_players
    add column if not exists snow_infantry integer not null default 20 check (snow_infantry >= 0);

alter table public.frostbound_buildings drop constraint if exists frostbound_buildings_building_type_check;
alter table public.frostbound_buildings add constraint frostbound_buildings_building_type_check
    check (building_type in ('generator', 'sawmill', 'kitchen', 'shelter', 'barracks'));

-- Nodos cercanos para probar el bucle inmediatamente desde la ciudad inicial.
insert into public.frostbound_world_tiles (x,y,tile_type,level,res_type,res_capacity,res_remaining)
values (577,596,'ResourceNode',4,'Food',5000,5000),
       (581,595,'ResourceNode',2,'Wood',5000,5000),
       (580,593,'ResourceNode',3,'Coal',5000,5000)
on conflict (x,y) do update set tile_type=excluded.tile_type, level=excluded.level,
    occupant_id=null, res_type=excluded.res_type, res_capacity=excluded.res_capacity,
    res_remaining=greatest(public.frostbound_world_tiles.res_remaining, excluded.res_remaining);

create schema if not exists private;

create or replace function private.frostbound_complete_gather_march_impl(p_march_id uuid)
returns integer
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_uid uuid := (select auth.uid());
    v_march public.frostbound_marches%rowtype;
    v_tile public.frostbound_world_tiles%rowtype;
    v_delivered integer;
begin
    if v_uid is null then raise exception 'Authentication required'; end if;
    select * into v_march from public.frostbound_marches
      where id=p_march_id and user_id=v_uid for update;
    if not found then raise exception 'March not found'; end if;
    if v_march.status='Completed' then return v_march.payload_amount; end if;
    if v_march.status <> 'Return' then raise exception 'March is not returning'; end if;

    select * into v_tile from public.frostbound_world_tiles
      where x=v_march.target_x and y=v_march.target_y and tile_type='ResourceNode' for update;
    if not found then raise exception 'Resource node not found'; end if;
    if v_tile.res_type <> v_march.res_type then raise exception 'Resource type mismatch'; end if;

    v_delivered := least(v_march.payload_amount, v_tile.res_remaining);
    update public.frostbound_world_tiles set
      res_remaining=res_remaining-v_delivered,
      tile_type=case when res_remaining-v_delivered <= 0 then 'Empty' else tile_type end,
      res_type=case when res_remaining-v_delivered <= 0 then null else res_type end,
      res_capacity=case when res_remaining-v_delivered <= 0 then 0 else res_capacity end,
      level=case when res_remaining-v_delivered <= 0 then 1 else level end
      where id=v_tile.id;

    update public.frostbound_players set
      wood=wood + case when v_march.res_type='Wood' then v_delivered else 0 end,
      food=food + case when v_march.res_type='Food' then v_delivered else 0 end,
      coal=coal + case when v_march.res_type='Coal' then v_delivered else 0 end
      where user_id=v_uid;

    update public.frostbound_marches set status='Completed', march_type='Return',
      payload_amount=v_delivered, arrival_time=now() where id=p_march_id;
    return v_delivered;
end;
$$;

create or replace function public.frostbound_complete_gather_march(p_march_id uuid)
returns integer language sql security invoker set search_path=''
as $$ select private.frostbound_complete_gather_march_impl(p_march_id); $$;

revoke all on function private.frostbound_complete_gather_march_impl(uuid) from public, anon;
revoke all on function public.frostbound_complete_gather_march(uuid) from public, anon;
grant usage on schema private to authenticated;
grant execute on function private.frostbound_complete_gather_march_impl(uuid) to authenticated;
grant execute on function public.frostbound_complete_gather_march(uuid) to authenticated;
