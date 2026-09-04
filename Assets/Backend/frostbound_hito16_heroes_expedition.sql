-- Frostbound Frontier · Hito 16: héroes avanzados, reclutamiento y expedición.
alter table public.frostbound_heroes
  add column if not exists hero_type text not null default 'Infantry',
  add column if not exists rarity text not null default 'Rare',
  add column if not exists shards_count integer not null default 0;

alter table public.frostbound_heroes drop constraint if exists frostbound_heroes_hero_type_check;
alter table public.frostbound_heroes add constraint frostbound_heroes_hero_type_check
  check (hero_type in ('Infantry','Lancer','Marksman'));
alter table public.frostbound_heroes drop constraint if exists frostbound_heroes_rarity_check;
alter table public.frostbound_heroes add constraint frostbound_heroes_rarity_check
  check (rarity in ('Rare','Epic','Legendary'));
alter table public.frostbound_heroes drop constraint if exists frostbound_heroes_shards_count_check;
alter table public.frostbound_heroes add constraint frostbound_heroes_shards_count_check check (shards_count >= 0);

update public.frostbound_heroes set hero_type='Marksman',rarity='Epic'
where hero_key='elena_ice_huntress';

alter table public.frostbound_players
  add column if not exists snow_lancers integer not null default 0 check (snow_lancers >= 0),
  add column if not exists snow_marksmen integer not null default 0 check (snow_marksmen >= 0);
alter table public.frostbound_buildings drop constraint if exists frostbound_buildings_building_type_check;
alter table public.frostbound_buildings add constraint frostbound_buildings_building_type_check
  check (building_type in ('generator','sawmill','kitchen','shelter','barracks','hospital','research','lancer_camp','marksman_camp'));

create table if not exists public.frostbound_recruitment_keys (
  user_id uuid primary key references auth.users(id) on delete cascade,
  common_keys integer not null default 5 check (common_keys >= 0),
  epic_keys integer not null default 1 check (epic_keys >= 0),
  updated_at timestamptz not null default now()
);

create table if not exists public.frostbound_expedition_stages (
  user_id uuid not null references auth.users(id) on delete cascade,
  stage_key text not null,
  chapter integer not null check (chapter between 1 and 5),
  stage_number integer not null check (stage_number between 1 and 10),
  completed boolean not null default false,
  best_power integer not null default 0,
  completed_at timestamptz,
  primary key (user_id,stage_key)
);

create table if not exists public.frostbound_expedition_progress (
  user_id uuid primary key references auth.users(id) on delete cascade,
  highest_stage integer not null default 0 check (highest_stage between 0 and 50),
  hero_xp integer not null default 0 check (hero_xp >= 0),
  idle_claimed_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

alter table public.frostbound_recruitment_keys enable row level security;
alter table public.frostbound_expedition_stages enable row level security;
alter table public.frostbound_expedition_progress enable row level security;

drop policy if exists frostbound_recruitment_keys_own on public.frostbound_recruitment_keys;
create policy frostbound_recruitment_keys_own on public.frostbound_recruitment_keys for select to authenticated
  using ((select auth.uid())=user_id);
drop policy if exists frostbound_recruitment_keys_insert_own on public.frostbound_recruitment_keys;
create policy frostbound_recruitment_keys_insert_own on public.frostbound_recruitment_keys for insert to authenticated
  with check ((select auth.uid())=user_id);
drop policy if exists frostbound_recruitment_keys_update_own on public.frostbound_recruitment_keys;
create policy frostbound_recruitment_keys_update_own on public.frostbound_recruitment_keys for update to authenticated
  using ((select auth.uid())=user_id) with check ((select auth.uid())=user_id);
drop policy if exists frostbound_expedition_stages_own on public.frostbound_expedition_stages;
create policy frostbound_expedition_stages_own on public.frostbound_expedition_stages for select to authenticated
  using ((select auth.uid())=user_id);
drop policy if exists frostbound_expedition_stages_insert_own on public.frostbound_expedition_stages;
create policy frostbound_expedition_stages_insert_own on public.frostbound_expedition_stages for insert to authenticated
  with check ((select auth.uid())=user_id);
drop policy if exists frostbound_expedition_stages_update_own on public.frostbound_expedition_stages;
create policy frostbound_expedition_stages_update_own on public.frostbound_expedition_stages for update to authenticated
  using ((select auth.uid())=user_id) with check ((select auth.uid())=user_id);
drop policy if exists frostbound_expedition_progress_own on public.frostbound_expedition_progress;
create policy frostbound_expedition_progress_own on public.frostbound_expedition_progress for select to authenticated
  using ((select auth.uid())=user_id);
drop policy if exists frostbound_expedition_progress_insert_own on public.frostbound_expedition_progress;
create policy frostbound_expedition_progress_insert_own on public.frostbound_expedition_progress for insert to authenticated
  with check ((select auth.uid())=user_id);
drop policy if exists frostbound_expedition_progress_update_own on public.frostbound_expedition_progress;
create policy frostbound_expedition_progress_update_own on public.frostbound_expedition_progress for update to authenticated
  using ((select auth.uid())=user_id) with check ((select auth.uid())=user_id);

revoke all on public.frostbound_recruitment_keys,public.frostbound_expedition_stages,public.frostbound_expedition_progress from public,anon;
grant select,insert,update on public.frostbound_recruitment_keys,public.frostbound_expedition_stages,public.frostbound_expedition_progress to authenticated;
create index if not exists frostbound_expedition_user_completed_idx on public.frostbound_expedition_stages(user_id,completed,chapter,stage_number);

create or replace function public.frostbound_initialize_hito16()
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid := (select auth.uid()); v_keys public.frostbound_recruitment_keys%rowtype; v_progress public.frostbound_expedition_progress%rowtype;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  insert into public.frostbound_recruitment_keys(user_id) values(v_uid) on conflict(user_id) do nothing;
  insert into public.frostbound_expedition_progress(user_id) values(v_uid) on conflict(user_id) do nothing;
  update public.frostbound_heroes set hero_type='Marksman',rarity='Epic' where user_id=v_uid and hero_key='elena_ice_huntress';
  select * into v_keys from public.frostbound_recruitment_keys where user_id=v_uid;
  select * into v_progress from public.frostbound_expedition_progress where user_id=v_uid;
  return jsonb_build_object('common_keys',v_keys.common_keys,'epic_keys',v_keys.epic_keys,'highest_stage',v_progress.highest_stage,'hero_xp',v_progress.hero_xp,'idle_claimed_at',v_progress.idle_claimed_at);
end $$;

create or replace function public.frostbound_recruit_hero(p_key_type text)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid := (select auth.uid()); v_roll numeric := random(); v_rarity text; v_key text; v_type text; v_shards int; v_new boolean; v_keys public.frostbound_recruitment_keys%rowtype; v_hero public.frostbound_heroes%rowtype;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if p_key_type not in ('Common','Epic') then raise exception 'Invalid key type'; end if;
  insert into public.frostbound_recruitment_keys(user_id) values(v_uid) on conflict(user_id) do nothing;
  select * into v_keys from public.frostbound_recruitment_keys where user_id=v_uid for update;
  if p_key_type='Common' then
    if v_keys.common_keys<1 then raise exception 'No common keys'; end if;
    update public.frostbound_recruitment_keys set common_keys=common_keys-1,updated_at=now() where user_id=v_uid;
    v_rarity:=case when v_roll<.02 then 'Legendary' when v_roll<.20 then 'Epic' else 'Rare' end;
  else
    if v_keys.epic_keys<1 then raise exception 'No epic keys'; end if;
    update public.frostbound_recruitment_keys set epic_keys=epic_keys-1,updated_at=now() where user_id=v_uid;
    v_rarity:=case when v_roll<.10 then 'Legendary' when v_roll<.70 then 'Epic' else 'Rare' end;
  end if;
  if v_rarity='Legendary' then
    if random()<.5 then v_key:='kael_frost_guardian';v_type:='Infantry'; else v_key:='nyra_aurora_spear';v_type:='Lancer'; end if; v_shards:=40;
  elsif v_rarity='Epic' then
    if random()<.5 then v_key:='elena_ice_huntress';v_type:='Marksman'; else v_key:='boris_snow_lancer';v_type:='Lancer'; end if; v_shards:=20;
  else
    if random()<.5 then v_key:='mira_winter_shield';v_type:='Infantry'; else v_key:='orin_frost_marksman';v_type:='Marksman'; end if; v_shards:=10;
  end if;
  select * into v_hero from public.frostbound_heroes where user_id=v_uid and hero_key=v_key for update;
  v_new:=not found;
  if v_new then
    insert into public.frostbound_heroes(user_id,hero_key,hero_type,rarity,level,star_level,shards_count,power_bonus,march_speed_bonus)
    values(v_uid,v_key,v_type,v_rarity,1,1,0,case when v_rarity='Legendary' then .25 when v_rarity='Epic' then .15 else .08 end,case when v_type='Lancer' then .15 else .05 end)
    returning * into v_hero;
  else
    update public.frostbound_heroes set shards_count=shards_count+v_shards,updated_at=now() where id=v_hero.id returning * into v_hero;
  end if;
  select * into v_keys from public.frostbound_recruitment_keys where user_id=v_uid;
  return jsonb_build_object('hero_id',v_hero.id,'hero_key',v_hero.hero_key,'hero_type',v_hero.hero_type,'rarity',v_hero.rarity,'level',v_hero.level,'star_level',v_hero.star_level,'shards_count',v_hero.shards_count,'is_new',v_new,'shards_awarded',case when v_new then 0 else v_shards end,'common_keys',v_keys.common_keys,'epic_keys',v_keys.epic_keys);
end $$;

create or replace function public.frostbound_process_expedition(p_stage integer,p_team_keys text[])
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid := (select auth.uid()); v_count int; v_power int; v_enemy int; v_win boolean; v_chapter int; v_stage int; v_wood int:=0; v_food int:=0; v_xp int:=0; v_enemy_type text; v_advantage int:=0; v_disadvantage int:=0;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if p_stage not between 1 and 50 then raise exception 'Invalid stage'; end if;
  if coalesce(array_length(p_team_keys,1),0) not between 1 and 3 then raise exception 'Select 1 to 3 heroes'; end if;
  v_enemy_type:=case p_stage%3 when 1 then 'Marksman' when 2 then 'Lancer' else 'Infantry' end;
  select count(*),coalesce(sum(100+level*25+star_level*75+case rarity when 'Legendary' then 180 when 'Epic' then 90 else 30 end),0)::int,
    count(*) filter(where (hero_type='Infantry' and v_enemy_type='Marksman') or (hero_type='Marksman' and v_enemy_type='Lancer') or (hero_type='Lancer' and v_enemy_type='Infantry')),
    count(*) filter(where (hero_type='Marksman' and v_enemy_type='Infantry') or (hero_type='Lancer' and v_enemy_type='Marksman') or (hero_type='Infantry' and v_enemy_type='Lancer'))
    into v_count,v_power,v_advantage,v_disadvantage from public.frostbound_heroes where user_id=v_uid and hero_key=any(p_team_keys);
  if v_count<>array_length(p_team_keys,1) then raise exception 'Invalid hero team'; end if;
  v_power:=round(v_power*(1.0+v_advantage*.15-v_disadvantage*.10));
  v_enemy:=140+p_stage*105; v_win:=v_power>=v_enemy; v_chapter:=((p_stage-1)/10)+1; v_stage:=((p_stage-1)%10)+1;
  insert into public.frostbound_expedition_progress(user_id) values(v_uid) on conflict(user_id) do nothing;
  if v_win then
    v_wood:=100+p_stage*20;v_food:=80+p_stage*18;v_xp:=25+p_stage*5;
    insert into public.frostbound_expedition_stages(user_id,stage_key,chapter,stage_number,completed,best_power,completed_at)
    values(v_uid,v_chapter||'-'||v_stage,v_chapter,v_stage,true,v_power,now())
    on conflict(user_id,stage_key) do update set completed=true,best_power=greatest(public.frostbound_expedition_stages.best_power,excluded.best_power),completed_at=coalesce(public.frostbound_expedition_stages.completed_at,now());
    update public.frostbound_expedition_progress set highest_stage=greatest(highest_stage,p_stage),hero_xp=hero_xp+v_xp,updated_at=now() where user_id=v_uid;
    update public.frostbound_players set wood=wood+v_wood,food=food+v_food where user_id=v_uid;
  end if;
  return jsonb_build_object('victory',v_win,'stage',p_stage,'team_power',v_power,'enemy_power',v_enemy,'enemy_type',v_enemy_type,'advantage_count',v_advantage,'disadvantage_count',v_disadvantage,'wood',v_wood,'food',v_food,'hero_xp',v_xp);
end $$;

create or replace function public.frostbound_claim_expedition_idle()
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid := (select auth.uid()); v_p public.frostbound_expedition_progress%rowtype; v_seconds int; v_wood int; v_food int; v_xp int;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  insert into public.frostbound_expedition_progress(user_id) values(v_uid) on conflict(user_id) do nothing;
  select * into v_p from public.frostbound_expedition_progress where user_id=v_uid for update;
  v_seconds:=least(43200,greatest(0,extract(epoch from now()-v_p.idle_claimed_at)::int));
  v_wood:=floor(v_seconds/60.0*(2+v_p.highest_stage*.25));v_food:=floor(v_seconds/60.0*(1.5+v_p.highest_stage*.20));v_xp:=floor(v_seconds/60.0*(.5+v_p.highest_stage*.08));
  update public.frostbound_expedition_progress set hero_xp=hero_xp+v_xp,idle_claimed_at=now(),updated_at=now() where user_id=v_uid;
  update public.frostbound_players set wood=wood+v_wood,food=food+v_food where user_id=v_uid;
  return jsonb_build_object('wood',v_wood,'food',v_food,'hero_xp',v_xp,'seconds',v_seconds,'highest_stage',v_p.highest_stage);
end $$;

revoke all on function public.frostbound_initialize_hito16() from public,anon;
revoke all on function public.frostbound_recruit_hero(text) from public,anon;
revoke all on function public.frostbound_process_expedition(integer,text[]) from public,anon;
revoke all on function public.frostbound_claim_expedition_idle() from public,anon;
grant execute on function public.frostbound_initialize_hito16() to authenticated;
grant execute on function public.frostbound_recruit_hero(text) to authenticated;
grant execute on function public.frostbound_process_expedition(integer,text[]) to authenticated;
grant execute on function public.frostbound_claim_expedition_idle() to authenticated;
