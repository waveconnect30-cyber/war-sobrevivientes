-- Frostbound Frontier - Hito 10: instalaciones neutrales, conquista y banderas.
-- Idempotente. RPC publicas SECURITY INVOKER; toda escritura valida auth.uid().

alter table public.frostbound_world_tiles
  add column if not exists facility_key text,
  add column if not exists facility_power integer not null default 0,
  add column if not exists owner_alliance_id uuid references public.frostbound_alliances(id) on delete set null,
  add column if not exists buff_type text,
  add column if not exists buff_percent numeric(5,2) not null default 0;

alter table public.frostbound_world_tiles drop constraint if exists frostbound_world_tiles_facility_power_check;
alter table public.frostbound_world_tiles add constraint frostbound_world_tiles_facility_power_check check (facility_power >= 0);
alter table public.frostbound_world_tiles drop constraint if exists frostbound_world_tiles_buff_type_check;
alter table public.frostbound_world_tiles add constraint frostbound_world_tiles_buff_type_check
  check (buff_type is null or buff_type in ('ResourceProduction','AllianceAttack'));
create index if not exists idx_frostbound_facilities_owner
  on public.frostbound_world_tiles(owner_alliance_id) where tile_type='Fortress';

-- Instalaciones iniciales cercanas a la zona de prueba. No reemplazan tiles ocupadas.
insert into public.frostbound_world_tiles
  (id,x,y,tile_type,level,facility_key,facility_power,buff_type,buff_percent,updated_at)
overriding system value
values
  (10001,588,590,'Fortress',2,'RegionalThermalStation',80,'ResourceProduction',10,now()),
  (10002,606,598,'Fortress',2,'GlacialHuntingPost',90,'AllianceAttack',10,now()),
  (10003,620,610,'Fortress',3,'RegionalThermalStation',140,'ResourceProduction',10,now()),
  (10004,560,610,'Fortress',3,'GlacialHuntingPost',150,'AllianceAttack',10,now())
on conflict do nothing;

create or replace function public.frostbound_place_alliance_structure(p_structure_type text,p_x integer,p_y integer)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_member public.frostbound_alliance_members%rowtype; v_row public.frostbound_alliance_structures%rowtype; v_radius integer;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if p_structure_type not in ('HQ','Flag') then raise exception 'Invalid structure type'; end if;
  if p_x not between 0 and 1199 or p_y not between 0 and 1199 then raise exception 'Outside world bounds'; end if;
  select * into v_member from public.frostbound_alliance_members where user_id=v_uid and member_role in ('Leader','Officer');
  if not found then raise exception 'Leader or Officer role required'; end if;
  if exists(select 1 from public.frostbound_world_tiles where x=p_x and y=p_y and (tile_type<>'Empty' or occupant_id is not null)) then raise exception 'Target tile must be Empty'; end if;
  if p_structure_type='HQ' and exists(select 1 from public.frostbound_alliance_structures where alliance_id=v_member.alliance_id and structure_type='HQ') then raise exception 'Alliance HQ already exists'; end if;
  if p_structure_type='Flag' then
    if not exists(select 1 from public.frostbound_alliance_structures s where s.alliance_id=v_member.alliance_id and s.status='Active'
      and greatest(abs(s.x-p_x),abs(s.y-p_y)) <= s.territory_radius + 1) then
      raise exception 'Flag must connect to existing alliance territory';
    end if;
    v_radius:=3;
  else v_radius:=5;
  end if;
  insert into public.frostbound_alliance_structures(alliance_id,structure_type,x,y,status,territory_radius,created_by)
    values(v_member.alliance_id,p_structure_type,p_x,p_y,'Active',v_radius,v_uid) returning * into v_row;
  return to_jsonb(v_row);
end $$;

create or replace function public.frostbound_get_my_alliance_buffs()
returns jsonb language sql stable security invoker set search_path='' as $$
  with mine as (select alliance_id from public.frostbound_alliance_members where user_id=(select auth.uid()) limit 1)
  select jsonb_build_object(
    'resource_bonus',coalesce(max(w.buff_percent) filter(where w.buff_type='ResourceProduction'),0),
    'attack_bonus',coalesce(max(w.buff_percent) filter(where w.buff_type='AllianceAttack'),0),
    'facility_count',count(*) filter(where w.owner_alliance_id is not null))
  from public.frostbound_world_tiles w join mine on mine.alliance_id=w.owner_alliance_id
  where w.tile_type='Fortress';
$$;

create or replace function private.frostbound_process_facility_rally_impl(p_rally_id uuid)
returns jsonb language plpgsql security definer set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_rally public.frostbound_rallies%rowtype; v_tile public.frostbound_world_tiles%rowtype;
  v_power integer:=0; v_members integer:=0; v_victory boolean:=false;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  select * into v_rally from public.frostbound_rallies where id=p_rally_id for update;
  if not found or v_rally.target_type<>'Facility' then raise exception 'Facility rally not found'; end if;
  if not exists(select 1 from public.frostbound_alliance_members where alliance_id=v_rally.alliance_id and user_id=v_uid) then raise exception 'Alliance mismatch'; end if;
  if v_rally.status='Completed' then raise exception 'Rally already completed'; end if;
  if v_rally.rally_starts_at>now() then raise exception 'Rally countdown is still active'; end if;
  select * into v_tile from public.frostbound_world_tiles where x=v_rally.target_x and y=v_rally.target_y and tile_type='Fortress' for update;
  if not found then raise exception 'Neutral facility not found'; end if;
  select coalesce(sum(round(rm.troop_count*20*(1+coalesce(rt.level,0)*0.05))),0)::integer,count(*)::integer
    into v_power,v_members
  from public.frostbound_rally_members rm
  left join public.frostbound_research rt on rt.user_id=rm.user_id and rt.tech_id='InfanteriaBlindada'
  where rm.rally_id=v_rally.id and rm.status in ('Ready','Joining');
  v_victory:=v_power>=v_tile.facility_power;
  if v_victory then
    update public.frostbound_world_tiles set owner_alliance_id=v_rally.alliance_id,updated_at=now() where id=v_tile.id;
  end if;
  update public.frostbound_rallies set status='Completed',updated_at=now() where id=v_rally.id;
  return jsonb_build_object('rally_id',v_rally.id,'victory',v_victory,'combined_power',v_power,
    'defense_power',v_tile.facility_power,'member_count',v_members,'facility_key',v_tile.facility_key,
    'buff_type',case when v_victory then v_tile.buff_type else null end,
    'buff_percent',case when v_victory then v_tile.buff_percent else 0 end,
    'alliance_id',case when v_victory then v_rally.alliance_id else v_tile.owner_alliance_id end);
end $$;

revoke all on function private.frostbound_process_facility_rally_impl(uuid) from public,anon,authenticated;
grant execute on function private.frostbound_process_facility_rally_impl(uuid) to authenticated;
create or replace function public.frostbound_process_facility_rally(p_rally_id uuid)
returns jsonb language sql security invoker set search_path='' as $$
  select private.frostbound_process_facility_rally_impl(p_rally_id);
$$;

-- Protege nodos y bastiones dentro de territorio enemigo ante escrituras autenticadas.
create or replace function public.frostbound_guard_alliance_territory()
returns trigger language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_owner uuid;
begin
  if v_uid is null then return new; end if;
  select s.alliance_id into v_owner from public.frostbound_alliance_structures s
    where s.status='Active' and greatest(abs(s.x-old.x),abs(s.y-old.y))<=s.territory_radius
    order by case when s.structure_type='HQ' then 0 else 1 end limit 1;
  if v_owner is not null and not exists(select 1 from public.frostbound_alliance_members m where m.alliance_id=v_owner and m.user_id=v_uid) then
    raise exception 'Tile protected by alliance territory';
  end if;
  return new;
end $$;
drop trigger if exists frostbound_guard_alliance_territory on public.frostbound_world_tiles;
create trigger frostbound_guard_alliance_territory before update or delete on public.frostbound_world_tiles
  for each row when (old.tile_type in ('ResourceNode','Beast','Fortress')) execute function public.frostbound_guard_alliance_territory();

revoke all on function public.frostbound_place_alliance_structure(text,integer,integer) from public,anon;
revoke all on function public.frostbound_get_my_alliance_buffs() from public,anon;
revoke all on function public.frostbound_process_facility_rally(uuid) from public,anon;
grant execute on function public.frostbound_place_alliance_structure(text,integer,integer) to authenticated;
grant execute on function public.frostbound_get_my_alliance_buffs() to authenticated;
grant execute on function public.frostbound_process_facility_rally(uuid) to authenticated;
