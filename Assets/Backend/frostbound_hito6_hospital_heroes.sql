-- Frostbound Frontier · Hito 6: Enfermería y héroes.
create table if not exists public.frostbound_hospital (
  user_id uuid primary key references auth.users(id) on delete cascade,
  wounded_infantry integer not null default 0 check (wounded_infantry >= 0),
  healing_amount integer not null default 0 check (healing_amount >= 0),
  healing_started_at timestamptz,
  healing_finishes_at timestamptz,
  updated_at timestamptz not null default now()
);

create table if not exists public.frostbound_heroes (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  hero_key text not null,
  level integer not null default 1 check (level >= 1),
  star_level integer not null default 1 check (star_level between 1 and 5),
  power_bonus numeric(5,2) not null default 0.15 check (power_bonus >= 0),
  march_speed_bonus numeric(5,2) not null default 0.20 check (march_speed_bonus >= 0),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (user_id, hero_key)
);

alter table public.frostbound_marches
  add column if not exists hero_id uuid references public.frostbound_heroes(id) on delete set null,
  add column if not exists hero_key text,
  add column if not exists hero_power_bonus numeric(5,2) not null default 0,
  add column if not exists hero_speed_bonus numeric(5,2) not null default 0;

alter table public.frostbound_buildings drop constraint if exists frostbound_buildings_building_type_check;
alter table public.frostbound_buildings add constraint frostbound_buildings_building_type_check
  check (building_type in ('generator','sawmill','kitchen','shelter','barracks','hospital'));

alter table public.frostbound_hospital enable row level security;
alter table public.frostbound_heroes enable row level security;

drop policy if exists frostbound_hospital_owner on public.frostbound_hospital;
create policy frostbound_hospital_owner on public.frostbound_hospital for all to authenticated
  using ((select auth.uid()) = user_id)
  with check ((select auth.uid()) = user_id);

drop policy if exists frostbound_heroes_owner on public.frostbound_heroes;
create policy frostbound_heroes_owner on public.frostbound_heroes for all to authenticated
  using ((select auth.uid()) = user_id)
  with check ((select auth.uid()) = user_id);

revoke all on public.frostbound_hospital, public.frostbound_heroes from anon;
grant select, insert, update on public.frostbound_hospital, public.frostbound_heroes to authenticated;

create index if not exists idx_frostbound_heroes_user on public.frostbound_heroes(user_id);
create index if not exists idx_frostbound_marches_hero on public.frostbound_marches(hero_id) where hero_id is not null;

-- Backfill every existing Frostbound player; new sessions also call the
-- idempotent initializer below.
insert into public.frostbound_hospital(user_id)
select user_id from public.frostbound_players
on conflict(user_id) do nothing;
insert into public.frostbound_heroes(user_id,hero_key,level,star_level,power_bonus,march_speed_bonus)
select user_id,'elena_ice_huntress',1,1,0.15,0.20 from public.frostbound_players
on conflict(user_id,hero_key) do nothing;

create or replace function public.frostbound_initialize_hito6()
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid := (select auth.uid()); v_hero public.frostbound_heroes%rowtype;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  insert into public.frostbound_hospital(user_id) values(v_uid) on conflict(user_id) do nothing;
  insert into public.frostbound_heroes(user_id,hero_key,level,star_level,power_bonus,march_speed_bonus)
  values(v_uid,'elena_ice_huntress',1,1,0.15,0.20)
  on conflict(user_id,hero_key) do nothing;
  select * into v_hero from public.frostbound_heroes where user_id=v_uid and hero_key='elena_ice_huntress';
  return jsonb_build_object('hero_id',v_hero.id,'hero_key',v_hero.hero_key,'level',v_hero.level,
    'star_level',v_hero.star_level,'power_bonus',v_hero.power_bonus,'march_speed_bonus',v_hero.march_speed_bonus);
end $$;

create or replace function public.frostbound_start_healing(p_amount integer)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid := (select auth.uid()); v_h public.frostbound_hospital%rowtype; v_cost integer;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if p_amount <= 0 then raise exception 'Invalid healing amount'; end if;
  select * into v_h from public.frostbound_hospital where user_id=v_uid for update;
  if not found then raise exception 'Hospital not initialized'; end if;
  if v_h.healing_amount > 0 then raise exception 'Healing already active'; end if;
  if p_amount > v_h.wounded_infantry then raise exception 'Not enough wounded troops'; end if;
  v_cost := p_amount * 2;
  update public.frostbound_players set food=food-v_cost
    where user_id=v_uid and food>=v_cost;
  if not found then raise exception 'Not enough food'; end if;
  update public.frostbound_hospital set wounded_infantry=wounded_infantry-p_amount,
    healing_amount=p_amount, healing_started_at=now(),
    healing_finishes_at=now()+make_interval(secs => greatest(5,p_amount*2)), updated_at=now()
    where user_id=v_uid returning * into v_h;
  return jsonb_build_object('wounded',v_h.wounded_infantry,'healing_amount',v_h.healing_amount,
    'healing_started_at',v_h.healing_started_at,'healing_finishes_at',v_h.healing_finishes_at,'food_cost',v_cost);
end $$;

create or replace function public.frostbound_complete_healing()
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid := (select auth.uid()); v_h public.frostbound_hospital%rowtype; v_done integer;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  select * into v_h from public.frostbound_hospital where user_id=v_uid for update;
  if not found then raise exception 'Hospital not initialized'; end if;
  if v_h.healing_amount <= 0 then
    return jsonb_build_object('completed',0,'wounded',v_h.wounded_infantry);
  end if;
  if v_h.healing_finishes_at > now() then raise exception 'Healing is not ready'; end if;
  v_done := v_h.healing_amount;
  update public.frostbound_players set snow_infantry=snow_infantry+v_done where user_id=v_uid;
  update public.frostbound_hospital set healing_amount=0,healing_started_at=null,healing_finishes_at=null,
    updated_at=now() where user_id=v_uid returning * into v_h;
  return jsonb_build_object('completed',v_done,'wounded',v_h.wounded_infantry);
end $$;

-- Add Hito 6 effects to the atomic PVE transaction.
create or replace function private.frostbound_process_beast_battle_impl(p_march_id uuid)
returns jsonb language plpgsql security definer set search_path='' as $$
declare
 v_uid uuid := (select auth.uid()); v_m public.frostbound_marches%rowtype;
 v_b public.frostbound_world_tiles%rowtype; v_power int; v_win boolean;
 v_dead int; v_wounded int; v_loot int := 0; v_bonus numeric := 0;
begin
 if v_uid is null then raise exception 'Authentication required'; end if;
 select * into v_m from public.frostbound_marches where id=p_march_id and user_id=v_uid for update;
 if not found then raise exception 'March not found'; end if;
 if v_m.status='Completed' then return jsonb_build_object('victory',v_m.battle_result='Victory','casualties',v_m.casualties,'wounded',v_m.wounded,'loot_type',v_m.loot_type,'loot_amount',v_m.loot_amount,'power_used',ceil(v_m.troop_count*20*(1+v_m.hero_power_bonus))::int); end if;
 if v_m.march_type <> 'Attack' or v_m.status not in ('Marching','Battle') then raise exception 'Invalid battle march'; end if;
 select * into v_b from public.frostbound_world_tiles where x=v_m.target_x and y=v_m.target_y and tile_type='Beast' for update;
 if not found then raise exception 'Beast not found'; end if;
 perform 1 from public.frostbound_players where user_id=v_uid for update;
 insert into public.frostbound_hospital(user_id) values(v_uid) on conflict(user_id) do nothing;
 perform 1 from public.frostbound_hospital where user_id=v_uid for update;
 if v_m.hero_id is not null and exists(select 1 from public.frostbound_heroes where id=v_m.hero_id and user_id=v_uid) then v_bonus:=least(0.15,v_m.hero_power_bonus); end if;
 if v_m.troop_count<=0 then raise exception 'No troops assigned'; end if;
 v_power:=ceil(v_m.troop_count*20*(1+v_bonus))::int; v_win:=v_power>=v_b.beast_power;
 v_dead:=least(v_m.troop_count,greatest(1,ceil(v_m.troop_count*(case when v_win then .10 else .35 end))::int));
 v_wounded:=least(v_m.troop_count-v_dead,ceil(v_m.troop_count*(case when v_win then .10 else .25 end))::int);
 if v_win then v_loot:=v_b.reward_amount; end if;
 update public.frostbound_players set snow_infantry=greatest(0,snow_infantry-v_dead-v_wounded),
  coal=coal+case when v_win and v_b.reward_type='Coal' then v_loot else 0 end,
  crystals=crystals+case when v_win and v_b.reward_type='Crystals' then v_loot else 0 end,
  speedups=speedups+case when v_win and v_b.reward_type='Speedups' then v_loot else 0 end where user_id=v_uid;
 update public.frostbound_hospital set wounded_infantry=wounded_infantry+v_wounded,updated_at=now() where user_id=v_uid;
 if v_win then update public.frostbound_world_tiles set tile_type='Empty',level=1,beast_kind=null,beast_power=0,beast_hp=0,beast_max_hp=0,reward_type=null,reward_amount=0 where id=v_b.id; end if;
 update public.frostbound_marches set status='Completed',battle_result=case when v_win then 'Victory' else 'Defeat' end,
  casualties=v_dead,wounded=v_wounded,loot_type=case when v_win then v_b.reward_type else null end,
  loot_amount=v_loot,arrival_time=now() where id=p_march_id;
 return jsonb_build_object('victory',v_win,'casualties',v_dead,'wounded',v_wounded,'loot_type',case when v_win then v_b.reward_type else null end,'loot_amount',v_loot,'power_used',v_power);
end $$;

revoke all on function public.frostbound_initialize_hito6() from public,anon;
revoke all on function public.frostbound_start_healing(integer) from public,anon;
revoke all on function public.frostbound_complete_healing() from public,anon;
grant execute on function public.frostbound_initialize_hito6() to authenticated;
grant execute on function public.frostbound_start_healing(integer) to authenticated;
grant execute on function public.frostbound_complete_healing() to authenticated;
