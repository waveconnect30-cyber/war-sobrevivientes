-- Frostbound Frontier - Hito 7: alianzas y ayuda de temporizadores.
-- Ejecutable varias veces. Las operaciones sensibles se realizan mediante RPCs
-- SECURITY INVOKER; el unico SECURITY DEFINER vive en private y solo es un trigger.

create extension if not exists pgcrypto;

create table if not exists public.frostbound_alliances (
  id uuid primary key default gen_random_uuid(),
  name text not null check (char_length(trim(name)) between 3 and 32),
  tag text not null check (tag ~ '^[A-Z]{3}$'),
  leader_id uuid not null references auth.users(id) on delete restrict,
  power_total bigint not null default 0 check (power_total >= 0),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (tag)
);

create table if not exists public.frostbound_alliance_members (
  alliance_id uuid not null references public.frostbound_alliances(id) on delete cascade,
  user_id uuid not null references auth.users(id) on delete cascade,
  member_role text not null default 'Member' check (member_role in ('Leader','Officer','Member')),
  contributed_power bigint not null default 0 check (contributed_power >= 0),
  joined_at timestamptz not null default now(),
  primary key (alliance_id, user_id),
  unique (user_id)
);

create table if not exists public.frostbound_alliance_help (
  id uuid primary key default gen_random_uuid(),
  alliance_id uuid not null references public.frostbound_alliances(id) on delete cascade,
  requester_id uuid not null references auth.users(id) on delete cascade,
  target_type text not null check (target_type in ('BuildingUpgrade','HospitalHealing')),
  target_key text not null,
  status text not null default 'Open' check (status in ('Open','Completed','Cancelled')),
  help_count integer not null default 0 check (help_count >= 0),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (requester_id, target_type, target_key, status)
);

create table if not exists public.frostbound_alliance_help_actions (
  help_id uuid not null references public.frostbound_alliance_help(id) on delete cascade,
  helper_id uuid not null references auth.users(id) on delete cascade,
  seconds_reduced integer not null default 0 check (seconds_reduced >= 0),
  created_at timestamptz not null default now(),
  primary key (help_id, helper_id)
);

alter table public.frostbound_players
  add column if not exists alliance_id uuid references public.frostbound_alliances(id) on delete set null;

create index if not exists idx_frostbound_alliance_members_user on public.frostbound_alliance_members(user_id);
create index if not exists idx_frostbound_alliance_members_alliance on public.frostbound_alliance_members(alliance_id);
create index if not exists idx_frostbound_alliance_help_open on public.frostbound_alliance_help(alliance_id, status, created_at desc);
create index if not exists idx_frostbound_alliances_leader on public.frostbound_alliances(leader_id);
create index if not exists idx_frostbound_players_alliance on public.frostbound_players(alliance_id);
create index if not exists idx_frostbound_help_actions_helper on public.frostbound_alliance_help_actions(helper_id);
create unique index if not exists idx_frostbound_alliances_name_unique on public.frostbound_alliances(lower(name));

alter table public.frostbound_alliances enable row level security;
alter table public.frostbound_alliance_members enable row level security;
alter table public.frostbound_alliance_help enable row level security;
alter table public.frostbound_alliance_help_actions enable row level security;

revoke all on public.frostbound_alliances, public.frostbound_alliance_members,
  public.frostbound_alliance_help, public.frostbound_alliance_help_actions from anon, authenticated;
grant select, insert, update on public.frostbound_alliances to authenticated;
grant select, insert, update, delete on public.frostbound_alliance_members to authenticated;
grant select, insert, update on public.frostbound_alliance_help to authenticated;
grant select, insert on public.frostbound_alliance_help_actions to authenticated;

drop policy if exists frostbound_alliances_authenticated_read on public.frostbound_alliances;
create policy frostbound_alliances_authenticated_read on public.frostbound_alliances
  for select to authenticated using ((select auth.uid()) is not null);
drop policy if exists frostbound_alliances_leader_insert on public.frostbound_alliances;
create policy frostbound_alliances_leader_insert on public.frostbound_alliances
  for insert to authenticated with check (leader_id = (select auth.uid()));
drop policy if exists frostbound_alliances_leader_update on public.frostbound_alliances;
create policy frostbound_alliances_leader_update on public.frostbound_alliances
  for update to authenticated using (leader_id = (select auth.uid())) with check (leader_id = (select auth.uid()));

drop policy if exists frostbound_members_read on public.frostbound_alliance_members;
create policy frostbound_members_read on public.frostbound_alliance_members
  for select to authenticated using (
    user_id = (select auth.uid()) or alliance_id = (
      select p.alliance_id from public.frostbound_players p where p.user_id = (select auth.uid())
    )
  );
drop policy if exists frostbound_members_self_insert on public.frostbound_alliance_members;
create policy frostbound_members_self_insert on public.frostbound_alliance_members
  for insert to authenticated with check (user_id = (select auth.uid()));
drop policy if exists frostbound_members_self_delete on public.frostbound_alliance_members;
create policy frostbound_members_self_delete on public.frostbound_alliance_members
  for delete to authenticated using (user_id = (select auth.uid()));

drop policy if exists frostbound_help_members_read on public.frostbound_alliance_help;
create policy frostbound_help_members_read on public.frostbound_alliance_help
  for select to authenticated using (alliance_id = (
    select p.alliance_id from public.frostbound_players p where p.user_id = (select auth.uid())
  ));
drop policy if exists frostbound_help_requester_insert on public.frostbound_alliance_help;
create policy frostbound_help_requester_insert on public.frostbound_alliance_help
  for insert to authenticated with check (
    requester_id = (select auth.uid()) and alliance_id = (
      select p.alliance_id from public.frostbound_players p where p.user_id = (select auth.uid())
    )
  );
drop policy if exists frostbound_help_requester_update on public.frostbound_alliance_help;
create policy frostbound_help_requester_update on public.frostbound_alliance_help
  for update to authenticated using (requester_id = (select auth.uid())) with check (requester_id = (select auth.uid()));

drop policy if exists frostbound_help_actions_members_read on public.frostbound_alliance_help_actions;
create policy frostbound_help_actions_members_read on public.frostbound_alliance_help_actions
  for select to authenticated using (exists (
    select 1 from public.frostbound_alliance_help h
    where h.id = help_id and h.alliance_id = (
      select p.alliance_id from public.frostbound_players p where p.user_id = (select auth.uid())
    )
  ));
drop policy if exists frostbound_help_actions_self_insert on public.frostbound_alliance_help_actions;
create policy frostbound_help_actions_self_insert on public.frostbound_alliance_help_actions
  for insert to authenticated with check (helper_id = (select auth.uid()));

create or replace function public.frostbound_create_alliance(p_name text, p_tag text)
returns jsonb language plpgsql security invoker set search_path = '' as $$
declare v_uid uuid := (select auth.uid()); v_alliance public.frostbound_alliances%rowtype; v_cost int := 500;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if exists(select 1 from public.frostbound_alliance_members where user_id=v_uid) then raise exception 'Already in an alliance'; end if;
  update public.frostbound_players set crystals=crystals-v_cost where user_id=v_uid and crystals>=v_cost;
  if not found then raise exception 'Not enough crystals'; end if;
  insert into public.frostbound_alliances(name,tag,leader_id)
    values(trim(p_name),upper(trim(p_tag)),v_uid) returning * into v_alliance;
  insert into public.frostbound_alliance_members(alliance_id,user_id,member_role)
    values(v_alliance.id,v_uid,'Leader');
  update public.frostbound_players set alliance_id=v_alliance.id where user_id=v_uid;
  return jsonb_build_object('alliance_id',v_alliance.id,'name',v_alliance.name,'tag',v_alliance.tag,
    'member_role','Leader','member_count',1,'power_total',0,'crystal_cost',v_cost);
end $$;

create or replace function public.frostbound_join_alliance(p_alliance_id uuid)
returns jsonb language plpgsql security invoker set search_path = '' as $$
declare v_uid uuid := (select auth.uid()); v_alliance public.frostbound_alliances%rowtype;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if exists(select 1 from public.frostbound_alliance_members where user_id=v_uid) then raise exception 'Already in an alliance'; end if;
  select * into v_alliance from public.frostbound_alliances where id=p_alliance_id;
  if not found then raise exception 'Alliance not found'; end if;
  insert into public.frostbound_alliance_members(alliance_id,user_id) values(v_alliance.id,v_uid);
  update public.frostbound_players set alliance_id=v_alliance.id where user_id=v_uid;
  return jsonb_build_object('alliance_id',v_alliance.id,'name',v_alliance.name,'tag',v_alliance.tag,
    'member_role','Member','member_count',(select count(*) from public.frostbound_alliance_members where alliance_id=v_alliance.id),
    'power_total',v_alliance.power_total);
end $$;

create or replace function public.frostbound_request_alliance_help(p_target_type text, p_target_key text)
returns uuid language plpgsql security invoker set search_path = '' as $$
declare v_uid uuid := (select auth.uid()); v_alliance uuid; v_help uuid;
begin
  select alliance_id into v_alliance from public.frostbound_players where user_id=v_uid;
  if v_alliance is null then raise exception 'Alliance required'; end if;
  if p_target_type='BuildingUpgrade' and not exists(
    select 1 from public.frostbound_buildings where user_id=v_uid and slot_id=p_target_key and finishes_at>now()
  ) then raise exception 'No active building timer'; end if;
  if p_target_type='HospitalHealing' and not exists(
    select 1 from public.frostbound_hospital where user_id=v_uid and healing_finishes_at>now()
  ) then raise exception 'No active healing timer'; end if;
  insert into public.frostbound_alliance_help(alliance_id,requester_id,target_type,target_key)
    values(v_alliance,v_uid,p_target_type,p_target_key)
    on conflict(requester_id,target_type,target_key,status) do update set updated_at=now()
    returning id into v_help;
  return v_help;
end $$;

create schema if not exists private;
create or replace function private.frostbound_apply_alliance_help()
returns trigger language plpgsql security definer set search_path = '' as $$
declare v_help public.frostbound_alliance_help%rowtype; v_seconds int; v_finish timestamptz; v_start timestamptz;
begin
  select * into v_help from public.frostbound_alliance_help where id=new.help_id for update;
  if v_help.status <> 'Open' or v_help.requester_id = new.helper_id then raise exception 'Help not allowed'; end if;
  if not exists(select 1 from public.frostbound_alliance_members where alliance_id=v_help.alliance_id and user_id=new.helper_id) then
    raise exception 'Helper is not an alliance member';
  end if;
  if v_help.target_type='BuildingUpgrade' then
    select upgrade_started_at,finishes_at into v_start,v_finish from public.frostbound_buildings
      where user_id=v_help.requester_id and slot_id=v_help.target_key for update;
    v_seconds:=least(60,greatest(1,ceil(extract(epoch from (v_finish-v_start))*.01)::int));
    update public.frostbound_buildings set finishes_at=greatest(now(),finishes_at-make_interval(secs=>v_seconds))
      where user_id=v_help.requester_id and slot_id=v_help.target_key and finishes_at>now();
  else
    select healing_started_at,healing_finishes_at into v_start,v_finish from public.frostbound_hospital
      where user_id=v_help.requester_id for update;
    v_seconds:=least(60,greatest(1,ceil(extract(epoch from (v_finish-v_start))*.01)::int));
    update public.frostbound_hospital set healing_finishes_at=greatest(now(),healing_finishes_at-make_interval(secs=>v_seconds)),updated_at=now()
      where user_id=v_help.requester_id and healing_finishes_at>now();
  end if;
  if not found then raise exception 'Timer is no longer active'; end if;
  new.seconds_reduced:=v_seconds;
  update public.frostbound_alliance_help set help_count=help_count+1,updated_at=now() where id=new.help_id;
  return new;
end $$;

drop trigger if exists trg_frostbound_apply_alliance_help on public.frostbound_alliance_help_actions;
create trigger trg_frostbound_apply_alliance_help before insert on public.frostbound_alliance_help_actions
for each row execute function private.frostbound_apply_alliance_help();

revoke all on function private.frostbound_apply_alliance_help() from public,anon,authenticated;
revoke all on function public.frostbound_create_alliance(text,text) from public,anon;
revoke all on function public.frostbound_join_alliance(uuid) from public,anon;
revoke all on function public.frostbound_request_alliance_help(text,text) from public,anon;
grant execute on function public.frostbound_create_alliance(text,text) to authenticated;
grant execute on function public.frostbound_join_alliance(uuid) to authenticated;
grant execute on function public.frostbound_request_alliance_help(text,text) to authenticated;
