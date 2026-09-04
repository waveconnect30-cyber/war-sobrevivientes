-- Frostbound Frontier - Hito 8: Centro de Investigacion y tecnologias.

alter table public.frostbound_buildings drop constraint if exists frostbound_buildings_building_type_check;
alter table public.frostbound_buildings add constraint frostbound_buildings_building_type_check
  check (building_type in ('generator','sawmill','kitchen','shelter','barracks','hospital','research'));

create table if not exists public.frostbound_research (
  user_id uuid not null references auth.users(id) on delete cascade,
  tech_id text not null check (tech_id in ('CortaEficaz','RacionesOptimizadas','InfanteriaBlindada','MarchaForzada')),
  branch text not null check (branch in ('Economy','Military')),
  level integer not null default 0 check (level between 0 and 20),
  target_level integer not null default 0 check (target_level between 0 and 20),
  status text not null default 'Idle' check (status in ('Idle','Researching')),
  research_started_at timestamptz,
  finishes_at timestamptz,
  wood_cost integer not null default 0 check (wood_cost >= 0),
  food_cost integer not null default 0 check (food_cost >= 0),
  crystal_cost integer not null default 0 check (crystal_cost >= 0),
  updated_at timestamptz not null default now(),
  primary key (user_id, tech_id)
);

create unique index if not exists idx_frostbound_research_one_active
  on public.frostbound_research(user_id) where status='Researching';
create index if not exists idx_frostbound_research_finish
  on public.frostbound_research(finishes_at) where status='Researching';

alter table public.frostbound_research enable row level security;
revoke all on public.frostbound_research from anon, authenticated;
grant select, insert, update on public.frostbound_research to authenticated;

drop policy if exists frostbound_research_select_own on public.frostbound_research;
create policy frostbound_research_select_own on public.frostbound_research for select to authenticated
  using ((select auth.uid()) = user_id);
drop policy if exists frostbound_research_insert_own on public.frostbound_research;
create policy frostbound_research_insert_own on public.frostbound_research for insert to authenticated
  with check ((select auth.uid()) = user_id);
drop policy if exists frostbound_research_update_own on public.frostbound_research;
create policy frostbound_research_update_own on public.frostbound_research for update to authenticated
  using ((select auth.uid()) = user_id) with check ((select auth.uid()) = user_id);

create or replace function public.frostbound_start_research(p_tech_id text)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare
  v_uid uuid := (select auth.uid()); v_level int; v_branch text; v_wood int; v_food int; v_crystals int; v_seconds int;
  v_row public.frostbound_research%rowtype;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if p_tech_id not in ('CortaEficaz','RacionesOptimizadas','InfanteriaBlindada','MarchaForzada') then raise exception 'Unknown technology'; end if;
  if exists(select 1 from public.frostbound_research where user_id=v_uid and status='Researching') then raise exception 'Research queue busy'; end if;
  select coalesce(level,0) into v_level from public.frostbound_research where user_id=v_uid and tech_id=p_tech_id;
  v_level := coalesce(v_level,0);
  if v_level >= 20 then raise exception 'Maximum technology level'; end if;
  v_branch := case when p_tech_id in ('CortaEficaz','RacionesOptimizadas') then 'Economy' else 'Military' end;
  v_wood := 60 * (v_level + 1); v_food := 45 * (v_level + 1);
  v_crystals := case when v_level >= 4 then 5 * (v_level - 3) else 0 end;
  v_seconds := 10 + 8 * v_level;
  update public.frostbound_players set wood=wood-v_wood,food=food-v_food,crystals=crystals-v_crystals
    where user_id=v_uid and wood>=v_wood and food>=v_food and crystals>=v_crystals;
  if not found then raise exception 'Not enough research resources'; end if;
  insert into public.frostbound_research(user_id,tech_id,branch,level,target_level,status,research_started_at,finishes_at,wood_cost,food_cost,crystal_cost)
    values(v_uid,p_tech_id,v_branch,v_level,v_level+1,'Researching',now(),now()+make_interval(secs=>v_seconds),v_wood,v_food,v_crystals)
    on conflict(user_id,tech_id) do update set branch=excluded.branch,target_level=excluded.target_level,status='Researching',
      research_started_at=excluded.research_started_at,finishes_at=excluded.finishes_at,wood_cost=excluded.wood_cost,
      food_cost=excluded.food_cost,crystal_cost=excluded.crystal_cost,updated_at=now()
    returning * into v_row;
  return jsonb_build_object('tech_id',v_row.tech_id,'branch',v_row.branch,'level',v_row.level,'target_level',v_row.target_level,
    'status',v_row.status,'research_started_at',v_row.research_started_at,'finishes_at',v_row.finishes_at,
    'wood_cost',v_row.wood_cost,'food_cost',v_row.food_cost,'crystal_cost',v_row.crystal_cost);
end $$;

create or replace function public.frostbound_complete_research(p_tech_id text)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid := (select auth.uid()); v_row public.frostbound_research%rowtype;
begin
  select * into v_row from public.frostbound_research where user_id=v_uid and tech_id=p_tech_id for update;
  if not found or v_row.status <> 'Researching' then raise exception 'Research is not active'; end if;
  if v_row.finishes_at > now() then raise exception 'Research timer is active'; end if;
  update public.frostbound_research set level=target_level,status='Idle',research_started_at=null,finishes_at=null,updated_at=now()
    where user_id=v_uid and tech_id=p_tech_id returning * into v_row;
  return jsonb_build_object('tech_id',v_row.tech_id,'branch',v_row.branch,'level',v_row.level,'target_level',v_row.target_level,
    'status',v_row.status,'wood_cost',v_row.wood_cost,'food_cost',v_row.food_cost,'crystal_cost',v_row.crystal_cost);
end $$;

-- Amplia la ayuda de alianza para investigaciones.
alter table public.frostbound_alliance_help drop constraint if exists frostbound_alliance_help_target_type_check;
alter table public.frostbound_alliance_help add constraint frostbound_alliance_help_target_type_check
  check (target_type in ('BuildingUpgrade','HospitalHealing','Research'));

create or replace function public.frostbound_request_alliance_help(p_target_type text, p_target_key text)
returns uuid language plpgsql security invoker set search_path = '' as $$
declare v_uid uuid := (select auth.uid()); v_alliance uuid; v_help uuid;
begin
  select alliance_id into v_alliance from public.frostbound_players where user_id=v_uid;
  if v_alliance is null then raise exception 'Alliance required'; end if;
  if p_target_type='BuildingUpgrade' and not exists(select 1 from public.frostbound_buildings where user_id=v_uid and slot_id=p_target_key and finishes_at>now()) then raise exception 'No active building timer'; end if;
  if p_target_type='HospitalHealing' and not exists(select 1 from public.frostbound_hospital where user_id=v_uid and healing_finishes_at>now()) then raise exception 'No active healing timer'; end if;
  if p_target_type='Research' and not exists(select 1 from public.frostbound_research where user_id=v_uid and tech_id=p_target_key and status='Researching' and finishes_at>now()) then raise exception 'No active research timer'; end if;
  insert into public.frostbound_alliance_help(alliance_id,requester_id,target_type,target_key)
    values(v_alliance,v_uid,p_target_type,p_target_key)
    on conflict(requester_id,target_type,target_key,status) do update set updated_at=now() returning id into v_help;
  return v_help;
end $$;

create or replace function private.frostbound_apply_alliance_help()
returns trigger language plpgsql security definer set search_path = '' as $$
declare v_help public.frostbound_alliance_help%rowtype; v_seconds int; v_finish timestamptz; v_start timestamptz;
begin
  select * into v_help from public.frostbound_alliance_help where id=new.help_id for update;
  if v_help.status <> 'Open' or v_help.requester_id = new.helper_id then raise exception 'Help not allowed'; end if;
  if not exists(select 1 from public.frostbound_alliance_members where alliance_id=v_help.alliance_id and user_id=new.helper_id) then raise exception 'Helper is not an alliance member'; end if;
  if v_help.target_type='BuildingUpgrade' then
    select upgrade_started_at,finishes_at into v_start,v_finish from public.frostbound_buildings where user_id=v_help.requester_id and slot_id=v_help.target_key for update;
    v_seconds:=least(60,greatest(1,ceil(extract(epoch from (v_finish-v_start))*.01)::int));
    update public.frostbound_buildings set finishes_at=greatest(now(),finishes_at-make_interval(secs=>v_seconds)) where user_id=v_help.requester_id and slot_id=v_help.target_key and finishes_at>now();
  elsif v_help.target_type='HospitalHealing' then
    select healing_started_at,healing_finishes_at into v_start,v_finish from public.frostbound_hospital where user_id=v_help.requester_id for update;
    v_seconds:=least(60,greatest(1,ceil(extract(epoch from (v_finish-v_start))*.01)::int));
    update public.frostbound_hospital set healing_finishes_at=greatest(now(),healing_finishes_at-make_interval(secs=>v_seconds)),updated_at=now() where user_id=v_help.requester_id and healing_finishes_at>now();
  else
    select research_started_at,finishes_at into v_start,v_finish from public.frostbound_research where user_id=v_help.requester_id and tech_id=v_help.target_key for update;
    v_seconds:=least(60,greatest(1,ceil(extract(epoch from (v_finish-v_start))*.01)::int));
    update public.frostbound_research set finishes_at=greatest(now(),finishes_at-make_interval(secs=>v_seconds)),updated_at=now() where user_id=v_help.requester_id and tech_id=v_help.target_key and status='Researching' and finishes_at>now();
  end if;
  if not found then raise exception 'Timer is no longer active'; end if;
  new.seconds_reduced:=v_seconds;
  update public.frostbound_alliance_help set help_count=help_count+1,updated_at=now() where id=new.help_id;
  return new;
end $$;

-- El combate calcula el bonus militar directamente desde la investigacion persistida.
create or replace function private.frostbound_process_beast_battle_impl(p_march_id uuid)
returns jsonb language plpgsql security definer set search_path='' as $$
declare
 v_uid uuid:=(select auth.uid()); v_m public.frostbound_marches%rowtype; v_b public.frostbound_world_tiles%rowtype;
 v_power int; v_win bool; v_dead int; v_wounded int; v_loot int:=0; v_bonus numeric:=0; v_research_bonus numeric:=0;
begin
 if v_uid is null then raise exception 'Authentication required'; end if;
 select * into v_m from public.frostbound_marches where id=p_march_id and user_id=v_uid for update;
 if not found or v_m.march_type<>'BeastAttack' then raise exception 'Invalid beast march'; end if;
 if v_m.status='Completed' then return jsonb_build_object('victory',v_m.battle_result='Victory','casualties',v_m.casualties,'wounded',v_m.wounded,'loot_type',v_m.loot_type,'loot_amount',v_m.loot_amount,'power_used',ceil(v_m.troop_count*20*(1+v_m.hero_power_bonus))::int); end if;
 if v_m.arrival_time>now() then raise exception 'March has not arrived'; end if;
 select * into v_b from public.frostbound_world_tiles where x=v_m.target_x and y=v_m.target_y for update;
 if not found or v_b.tile_type<>'Beast' then raise exception 'Beast is no longer available'; end if;
 insert into public.frostbound_hospital(user_id) values(v_uid) on conflict(user_id) do nothing;
 perform 1 from public.frostbound_hospital where user_id=v_uid for update;
 if v_m.hero_id is not null and exists(select 1 from public.frostbound_heroes where id=v_m.hero_id and user_id=v_uid) then v_bonus:=least(.15,v_m.hero_power_bonus); end if;
 select coalesce(level,0)*.05 into v_research_bonus from public.frostbound_research where user_id=v_uid and tech_id='InfanteriaBlindada';
 v_research_bonus:=coalesce(v_research_bonus,0);
 v_power:=ceil(v_m.troop_count*20*(1+v_bonus+v_research_bonus)); v_win:=v_power>=v_b.beast_power;
 v_dead:=least(v_m.troop_count,floor(v_m.troop_count*(case when v_win then .05 else .20 end))::int);
 v_wounded:=least(v_m.troop_count-v_dead,ceil(v_m.troop_count*(case when v_win then .10 else .25 end))::int);
 if v_win then v_loot:=greatest(0,v_b.reward_amount); end if;
 update public.frostbound_players set snow_infantry=greatest(0,snow_infantry-v_dead-v_wounded),coal=coal+(case when v_win and v_b.reward_type='Coal' then v_loot else 0 end),crystals=crystals+(case when v_win and v_b.reward_type='Crystals' then v_loot else 0 end),speedups=speedups+(case when v_win and v_b.reward_type='Speedups' then v_loot else 0 end) where user_id=v_uid;
 update public.frostbound_hospital set wounded_infantry=wounded_infantry+v_wounded,updated_at=now() where user_id=v_uid;
 if v_win then update public.frostbound_world_tiles set tile_type='Empty',occupant_id=null,level=1,beast_kind=null,beast_power=0,beast_hp=0,beast_max_hp=0,reward_type=null,reward_amount=0,updated_at=now() where id=v_b.id; end if;
 update public.frostbound_marches set status='Completed',battle_result=case when v_win then 'Victory' else 'Defeat' end,casualties=v_dead,wounded=v_wounded,loot_type=case when v_win then v_b.reward_type else null end,loot_amount=v_loot where id=v_m.id;
 return jsonb_build_object('victory',v_win,'casualties',v_dead,'wounded',v_wounded,'loot_type',case when v_win then v_b.reward_type else null end,'loot_amount',v_loot,'power_used',v_power);
end $$;

revoke all on function public.frostbound_start_research(text) from public,anon;
revoke all on function public.frostbound_complete_research(text) from public,anon;
grant execute on function public.frostbound_start_research(text) to authenticated;
grant execute on function public.frostbound_complete_research(text) to authenticated;
revoke all on function private.frostbound_process_beast_battle_impl(uuid) from public,anon;
grant execute on function private.frostbound_process_beast_battle_impl(uuid) to authenticated;
