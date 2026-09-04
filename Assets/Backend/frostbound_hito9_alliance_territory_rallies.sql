-- Frostbound Frontier - Hito 9: territorio y rallies de alianza.
-- Idempotente. Las RPC publicas son SECURITY INVOKER y respetan RLS.

create extension if not exists pgcrypto;

create table if not exists public.frostbound_alliance_structures (
  id uuid primary key default gen_random_uuid(),
  alliance_id uuid not null references public.frostbound_alliances(id) on delete cascade,
  structure_type text not null check (structure_type in ('HQ','Flag')),
  x integer not null check (x between 0 and 1199),
  y integer not null check (y between 0 and 1199),
  status text not null default 'Building' check (status in ('Building','Active')),
  territory_radius integer not null default 5 check (territory_radius between 1 and 12),
  created_by uuid not null references auth.users(id) on delete restrict,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (x,y)
);

create unique index if not exists idx_frostbound_alliance_one_hq
  on public.frostbound_alliance_structures(alliance_id) where structure_type='HQ';
create index if not exists idx_frostbound_alliance_structures_coords
  on public.frostbound_alliance_structures(x,y);
create index if not exists idx_frostbound_alliance_structures_alliance
  on public.frostbound_alliance_structures(alliance_id,status);
create index if not exists idx_frostbound_alliance_structures_created_by
  on public.frostbound_alliance_structures(created_by);

create table if not exists public.frostbound_rallies (
  id uuid primary key default gen_random_uuid(),
  alliance_id uuid not null references public.frostbound_alliances(id) on delete cascade,
  leader_id uuid not null references auth.users(id) on delete restrict,
  target_x integer not null check (target_x between 0 and 1199),
  target_y integer not null check (target_y between 0 and 1199),
  target_type text not null check (target_type in ('EliteBeast','Facility')),
  status text not null default 'Forming' check (status in ('Forming','Marching','Completed','Cancelled')),
  rally_starts_at timestamptz not null default (now() + interval '5 minutes'),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.frostbound_rally_members (
  rally_id uuid not null references public.frostbound_rallies(id) on delete cascade,
  user_id uuid not null references auth.users(id) on delete cascade,
  troop_count integer not null check (troop_count > 0),
  origin_x integer not null check (origin_x between 0 and 1199),
  origin_y integer not null check (origin_y between 0 and 1199),
  destination_x integer not null check (destination_x between 0 and 1199),
  destination_y integer not null check (destination_y between 0 and 1199),
  status text not null default 'Joining' check (status in ('Joining','Ready','Returned')),
  departure_time timestamptz not null default now(),
  arrival_time timestamptz not null,
  created_at timestamptz not null default now(),
  primary key (rally_id,user_id)
);

create index if not exists idx_frostbound_rallies_active
  on public.frostbound_rallies(alliance_id,status,rally_starts_at);
create index if not exists idx_frostbound_rallies_leader
  on public.frostbound_rallies(leader_id);
create index if not exists idx_frostbound_rally_members_user
  on public.frostbound_rally_members(user_id,status);

alter table public.frostbound_alliance_structures enable row level security;
alter table public.frostbound_rallies enable row level security;
alter table public.frostbound_rally_members enable row level security;

revoke all on public.frostbound_alliance_structures, public.frostbound_rallies,
  public.frostbound_rally_members from anon, authenticated;
grant select,insert,update,delete on public.frostbound_alliance_structures to authenticated;
grant select,insert,update on public.frostbound_rallies to authenticated;
grant select,insert,update,delete on public.frostbound_rally_members to authenticated;

drop policy if exists frostbound_structures_authenticated_read on public.frostbound_alliance_structures;
create policy frostbound_structures_authenticated_read on public.frostbound_alliance_structures
  for select to authenticated using ((select auth.uid()) is not null);
drop policy if exists frostbound_structures_officer_insert on public.frostbound_alliance_structures;
create policy frostbound_structures_officer_insert on public.frostbound_alliance_structures
  for insert to authenticated with check (
    created_by=(select auth.uid()) and alliance_id in (
      select m.alliance_id from public.frostbound_alliance_members m
      where m.user_id=(select auth.uid()) and m.member_role in ('Leader','Officer')));
drop policy if exists frostbound_structures_officer_update on public.frostbound_alliance_structures;
create policy frostbound_structures_officer_update on public.frostbound_alliance_structures
  for update to authenticated using (alliance_id in (
      select m.alliance_id from public.frostbound_alliance_members m
      where m.user_id=(select auth.uid()) and m.member_role in ('Leader','Officer')))
  with check (alliance_id in (
      select m.alliance_id from public.frostbound_alliance_members m
      where m.user_id=(select auth.uid()) and m.member_role in ('Leader','Officer')));
drop policy if exists frostbound_structures_officer_delete on public.frostbound_alliance_structures;
create policy frostbound_structures_officer_delete on public.frostbound_alliance_structures
  for delete to authenticated using (alliance_id in (
      select m.alliance_id from public.frostbound_alliance_members m
      where m.user_id=(select auth.uid()) and m.member_role in ('Leader','Officer')));

drop policy if exists frostbound_rallies_member_read on public.frostbound_rallies;
create policy frostbound_rallies_member_read on public.frostbound_rallies
  for select to authenticated using (alliance_id in (
    select m.alliance_id from public.frostbound_alliance_members m where m.user_id=(select auth.uid())));
drop policy if exists frostbound_rallies_member_insert on public.frostbound_rallies;
create policy frostbound_rallies_member_insert on public.frostbound_rallies
  for insert to authenticated with check (leader_id=(select auth.uid()) and alliance_id in (
    select m.alliance_id from public.frostbound_alliance_members m where m.user_id=(select auth.uid())));
drop policy if exists frostbound_rallies_leader_update on public.frostbound_rallies;
create policy frostbound_rallies_leader_update on public.frostbound_rallies
  for update to authenticated using (leader_id=(select auth.uid())) with check (leader_id=(select auth.uid()));

drop policy if exists frostbound_rally_members_alliance_read on public.frostbound_rally_members;
create policy frostbound_rally_members_alliance_read on public.frostbound_rally_members
  for select to authenticated using (rally_id in (
    select r.id from public.frostbound_rallies r where r.alliance_id in (
      select m.alliance_id from public.frostbound_alliance_members m where m.user_id=(select auth.uid()))));
drop policy if exists frostbound_rally_members_self_insert on public.frostbound_rally_members;
create policy frostbound_rally_members_self_insert on public.frostbound_rally_members
  for insert to authenticated with check (user_id=(select auth.uid()));
drop policy if exists frostbound_rally_members_self_update on public.frostbound_rally_members;
create policy frostbound_rally_members_self_update on public.frostbound_rally_members
  for update to authenticated using (user_id=(select auth.uid())) with check (user_id=(select auth.uid()));
drop policy if exists frostbound_rally_members_self_delete on public.frostbound_rally_members;
create policy frostbound_rally_members_self_delete on public.frostbound_rally_members
  for delete to authenticated using (user_id=(select auth.uid()));

create or replace function public.frostbound_place_alliance_structure(p_structure_type text,p_x integer,p_y integer)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_member public.frostbound_alliance_members%rowtype; v_row public.frostbound_alliance_structures%rowtype;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if p_structure_type not in ('HQ','Flag') then raise exception 'Invalid structure type'; end if;
  if p_x not between 0 and 1199 or p_y not between 0 and 1199 then raise exception 'Coordinates out of range'; end if;
  select * into v_member from public.frostbound_alliance_members where user_id=v_uid;
  if not found or v_member.member_role not in ('Leader','Officer') then raise exception 'Leader or Officer required'; end if;
  if exists(select 1 from public.frostbound_world_tiles where x=p_x and y=p_y and tile_type<>'Empty') then raise exception 'World tile is occupied'; end if;
  insert into public.frostbound_alliance_structures(alliance_id,structure_type,x,y,status,territory_radius,created_by)
  values(v_member.alliance_id,p_structure_type,p_x,p_y,'Active',case when p_structure_type='HQ' then 5 else 3 end,v_uid)
  returning * into v_row;
  return to_jsonb(v_row);
end $$;

create or replace function public.frostbound_demolish_alliance_structure(p_structure_id uuid)
returns boolean language plpgsql security invoker set search_path='' as $$
begin
  delete from public.frostbound_alliance_structures where id=p_structure_id;
  if not found then raise exception 'Structure not found or not authorized'; end if;
  return true;
end $$;

create or replace function public.frostbound_create_rally(p_target_x integer,p_target_y integer,p_target_type text,p_troop_count integer)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_alliance uuid; v_rally public.frostbound_rallies%rowtype; v_origin record;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if p_target_type not in ('EliteBeast','Facility') then raise exception 'Invalid rally target'; end if;
  if p_troop_count<1 then raise exception 'Troops required'; end if;
  select alliance_id into v_alliance from public.frostbound_alliance_members where user_id=v_uid;
  if v_alliance is null then raise exception 'Alliance required'; end if;
  if not exists(select 1 from public.frostbound_players where user_id=v_uid and snow_infantry>=p_troop_count) then raise exception 'Not enough troops'; end if;
  if p_target_type='EliteBeast' and not exists(select 1 from public.frostbound_world_tiles where x=p_target_x and y=p_target_y and tile_type='Beast' and level>=2) then raise exception 'Elite beast not found'; end if;
  if p_target_type='Facility' and not exists(select 1 from public.frostbound_world_tiles where x=p_target_x and y=p_target_y and tile_type='Fortress') then raise exception 'Facility not found'; end if;
  select x,y into v_origin from public.frostbound_world_tiles where tile_type='PlayerCity' and occupant_id=v_uid limit 1;
  if not found then raise exception 'Player city not found'; end if;
  insert into public.frostbound_rallies(alliance_id,leader_id,target_x,target_y,target_type,status,rally_starts_at)
    values(v_alliance,v_uid,p_target_x,p_target_y,p_target_type,'Forming',now()+interval '5 minutes') returning * into v_rally;
  insert into public.frostbound_rally_members(rally_id,user_id,troop_count,origin_x,origin_y,destination_x,destination_y,status,arrival_time)
    values(v_rally.id,v_uid,p_troop_count,v_origin.x,v_origin.y,v_origin.x,v_origin.y,'Ready',now());
  return jsonb_build_object('id',v_rally.id,'alliance_id',v_alliance,'leader_id',v_uid,'target_x',p_target_x,'target_y',p_target_y,
    'target_type',p_target_type,'status','Forming','rally_starts_at',v_rally.rally_starts_at,'member_count',1,'troop_total',p_troop_count);
end $$;

create or replace function public.frostbound_join_rally(p_rally_id uuid,p_troop_count integer)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_rally public.frostbound_rallies%rowtype; v_origin record; v_destination record; v_seconds integer;
begin
  if v_uid is null or p_troop_count<1 then raise exception 'Authentication and troops required'; end if;
  select * into v_rally from public.frostbound_rallies where id=p_rally_id for update;
  if not found or v_rally.status<>'Forming' or v_rally.rally_starts_at<=now() then raise exception 'Rally is not accepting members'; end if;
  if not exists(select 1 from public.frostbound_alliance_members where alliance_id=v_rally.alliance_id and user_id=v_uid) then raise exception 'Alliance mismatch'; end if;
  if not exists(select 1 from public.frostbound_players where user_id=v_uid and snow_infantry>=p_troop_count) then raise exception 'Not enough troops'; end if;
  select x,y into v_origin from public.frostbound_world_tiles where tile_type='PlayerCity' and occupant_id=v_uid limit 1;
  select x,y into v_destination from public.frostbound_world_tiles where tile_type='PlayerCity' and occupant_id=v_rally.leader_id limit 1;
  if v_origin is null or v_destination is null then raise exception 'City coordinates missing'; end if;
  v_seconds:=greatest(2,ceil(sqrt(power(v_origin.x-v_destination.x,2)+power(v_origin.y-v_destination.y,2))*.12)::int);
  insert into public.frostbound_rally_members(rally_id,user_id,troop_count,origin_x,origin_y,destination_x,destination_y,status,departure_time,arrival_time)
  values(v_rally.id,v_uid,p_troop_count,v_origin.x,v_origin.y,v_destination.x,v_destination.y,
    case when v_seconds<=2 then 'Ready' else 'Joining' end,now(),now()+make_interval(secs=>v_seconds))
  on conflict(rally_id,user_id) do update set troop_count=excluded.troop_count,origin_x=excluded.origin_x,origin_y=excluded.origin_y,
    destination_x=excluded.destination_x,destination_y=excluded.destination_y,status=excluded.status,departure_time=excluded.departure_time,arrival_time=excluded.arrival_time;
  return jsonb_build_object('rally_id',v_rally.id,'origin_x',v_origin.x,'origin_y',v_origin.y,'destination_x',v_destination.x,
    'destination_y',v_destination.y,'troop_count',p_troop_count,'status',case when v_seconds<=2 then 'Ready' else 'Joining' end,
    'departure_time',now(),'arrival_time',now()+make_interval(secs=>v_seconds));
end $$;

revoke all on function public.frostbound_place_alliance_structure(text,integer,integer) from public,anon;
revoke all on function public.frostbound_demolish_alliance_structure(uuid) from public,anon;
revoke all on function public.frostbound_create_rally(integer,integer,text,integer) from public,anon;
revoke all on function public.frostbound_join_rally(uuid,integer) from public,anon;
grant execute on function public.frostbound_place_alliance_structure(text,integer,integer) to authenticated;
grant execute on function public.frostbound_demolish_alliance_structure(uuid) to authenticated;
grant execute on function public.frostbound_create_rally(integer,integer,text,integer) to authenticated;
grant execute on function public.frostbound_join_rally(uuid,integer) to authenticated;
