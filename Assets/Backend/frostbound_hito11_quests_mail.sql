-- Frostbound Frontier - Hito 11: misiones, logros y correo.
-- Todas las RPC publicas son SECURITY INVOKER y trabajan solo con auth.uid().

create table if not exists public.frostbound_quests (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  quest_date date not null default current_date,
  quest_key text not null,
  title text not null,
  objective_type text not null check (objective_type in ('GatherWood','DefeatBeast','TrainTroops')),
  target_amount integer not null check (target_amount > 0),
  progress integer not null default 0 check (progress >= 0),
  points integer not null default 0 check (points >= 0),
  reward_wood integer not null default 0 check (reward_wood >= 0),
  reward_food integer not null default 0 check (reward_food >= 0),
  reward_crystals integer not null default 0 check (reward_crystals >= 0),
  reward_speedups integer not null default 0 check (reward_speedups >= 0),
  claimed_at timestamptz,
  updated_at timestamptz not null default now(),
  unique(user_id,quest_date,quest_key)
);

create table if not exists public.frostbound_achievements (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  achievement_key text not null,
  title text not null,
  objective_type text not null check (objective_type in ('GeneratorLevel','FacilityConquest')),
  target_amount integer not null check (target_amount > 0),
  progress integer not null default 0 check (progress >= 0),
  reward_wood integer not null default 0 check (reward_wood >= 0),
  reward_food integer not null default 0 check (reward_food >= 0),
  reward_crystals integer not null default 0 check (reward_crystals >= 0),
  reward_speedups integer not null default 0 check (reward_speedups >= 0),
  claimed_at timestamptz,
  updated_at timestamptz not null default now(),
  unique(user_id,achievement_key)
);

create table if not exists public.frostbound_mail (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  category text not null check (category in ('Battle','Alliance','System')),
  subject text not null,
  body text not null default '',
  source_key text,
  reward_wood integer not null default 0 check (reward_wood >= 0),
  reward_food integer not null default 0 check (reward_food >= 0),
  reward_crystals integer not null default 0 check (reward_crystals >= 0),
  reward_speedups integer not null default 0 check (reward_speedups >= 0),
  read_at timestamptz,
  claimed_at timestamptz,
  created_at timestamptz not null default now(),
  unique(user_id,source_key)
);

create table if not exists public.frostbound_daily_chests (
  user_id uuid not null references auth.users(id) on delete cascade,
  chest_date date not null default current_date,
  milestone integer not null check (milestone in (20,50,100)),
  claimed_at timestamptz not null default now(),
  primary key(user_id,chest_date,milestone)
);

create index if not exists idx_frostbound_quests_user_date on public.frostbound_quests(user_id,quest_date);
create index if not exists idx_frostbound_achievements_user on public.frostbound_achievements(user_id);
create index if not exists idx_frostbound_mail_user_created on public.frostbound_mail(user_id,created_at desc);

alter table public.frostbound_quests enable row level security;
alter table public.frostbound_achievements enable row level security;
alter table public.frostbound_mail enable row level security;
alter table public.frostbound_daily_chests enable row level security;

drop policy if exists frostbound_quests_own on public.frostbound_quests;
create policy frostbound_quests_own on public.frostbound_quests for all to authenticated
  using ((select auth.uid())=user_id) with check ((select auth.uid())=user_id);
drop policy if exists frostbound_achievements_own on public.frostbound_achievements;
create policy frostbound_achievements_own on public.frostbound_achievements for all to authenticated
  using ((select auth.uid())=user_id) with check ((select auth.uid())=user_id);
drop policy if exists frostbound_mail_own on public.frostbound_mail;
create policy frostbound_mail_own on public.frostbound_mail for all to authenticated
  using ((select auth.uid())=user_id) with check ((select auth.uid())=user_id);
drop policy if exists frostbound_daily_chests_own on public.frostbound_daily_chests;
create policy frostbound_daily_chests_own on public.frostbound_daily_chests for all to authenticated
  using ((select auth.uid())=user_id) with check ((select auth.uid())=user_id);

revoke all on public.frostbound_quests,public.frostbound_achievements,public.frostbound_mail,public.frostbound_daily_chests from anon,authenticated;
grant select,insert,update on public.frostbound_quests,public.frostbound_achievements,public.frostbound_mail to authenticated;
grant select,insert on public.frostbound_daily_chests to authenticated;

create or replace function public.frostbound_initialize_hito11()
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid());
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  insert into public.frostbound_quests(user_id,quest_key,title,objective_type,target_amount,points,reward_wood,reward_food,reward_crystals,reward_speedups)
  values
    (v_uid,'daily_wood','Recolecta 1,000 de Madera','GatherWood',1000,30,250,0,0,0),
    (v_uid,'daily_beasts','Derrota 2 Bestias','DefeatBeast',2,40,0,200,2,0),
    (v_uid,'daily_troops','Entrena 20 Tropas','TrainTroops',20,30,0,150,0,1)
  on conflict(user_id,quest_date,quest_key) do nothing;
  insert into public.frostbound_achievements(user_id,achievement_key,title,objective_type,target_amount,reward_wood,reward_food,reward_crystals,reward_speedups)
  values
    (v_uid,'generator_10','Generador Térmico Nivel 10','GeneratorLevel',10,1000,1000,20,3),
    (v_uid,'facility_1','Conquista 1 Instalación','FacilityConquest',1,500,500,10,2)
  on conflict(user_id,achievement_key) do nothing;
  insert into public.frostbound_mail(user_id,category,subject,body,source_key,reward_wood,reward_food,reward_crystals)
  values(v_uid,'System','Suministros del Comandante','Bienvenido al centro de operaciones. Reclama estos suministros para continuar la supervivencia.','hito11_welcome',200,200,5)
  on conflict(user_id,source_key) do nothing;
  return jsonb_build_object('initialized',true,'date',current_date);
end $$;

create or replace function public.frostbound_record_progress(p_objective_type text,p_amount integer)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_amount integer:=greatest(0,p_amount);
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if p_objective_type not in ('GatherWood','DefeatBeast','TrainTroops','GeneratorLevel','FacilityConquest') then raise exception 'Invalid objective'; end if;
  if p_objective_type in ('GeneratorLevel','FacilityConquest') then
    update public.frostbound_achievements set progress=least(target_amount,greatest(progress,v_amount)),updated_at=now()
      where user_id=v_uid and objective_type=p_objective_type and claimed_at is null;
  else
    update public.frostbound_quests set progress=least(target_amount,progress+v_amount),updated_at=now()
      where user_id=v_uid and quest_date=current_date and objective_type=p_objective_type and claimed_at is null;
  end if;
  return jsonb_build_object('recorded',v_amount,'objective_type',p_objective_type);
end $$;

create or replace function public.frostbound_claim_quest(p_quest_id uuid)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_q public.frostbound_quests%rowtype;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  select * into v_q from public.frostbound_quests where id=p_quest_id and user_id=v_uid for update;
  if not found or v_q.quest_date<>current_date then raise exception 'Quest not found'; end if;
  if v_q.progress<v_q.target_amount then raise exception 'Quest incomplete'; end if;
  if v_q.claimed_at is not null then raise exception 'Quest already claimed'; end if;
  update public.frostbound_players set wood=wood+v_q.reward_wood,food=food+v_q.reward_food,
    crystals=crystals+v_q.reward_crystals,speedups=speedups+v_q.reward_speedups where user_id=v_uid;
  update public.frostbound_quests set claimed_at=now(),updated_at=now() where id=v_q.id;
  return jsonb_build_object('wood',v_q.reward_wood,'food',v_q.reward_food,'crystals',v_q.reward_crystals,'speedups',v_q.reward_speedups);
end $$;

create or replace function public.frostbound_claim_achievement(p_achievement_id uuid)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_a public.frostbound_achievements%rowtype;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  select * into v_a from public.frostbound_achievements where id=p_achievement_id and user_id=v_uid for update;
  if not found or v_a.progress<v_a.target_amount then raise exception 'Achievement incomplete'; end if;
  if v_a.claimed_at is not null then raise exception 'Achievement already claimed'; end if;
  update public.frostbound_players set wood=wood+v_a.reward_wood,food=food+v_a.reward_food,
    crystals=crystals+v_a.reward_crystals,speedups=speedups+v_a.reward_speedups where user_id=v_uid;
  update public.frostbound_achievements set claimed_at=now(),updated_at=now() where id=v_a.id;
  return jsonb_build_object('wood',v_a.reward_wood,'food',v_a.reward_food,'crystals',v_a.reward_crystals,'speedups',v_a.reward_speedups);
end $$;

create or replace function public.frostbound_claim_daily_chest(p_milestone integer)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_points integer; v_wood integer; v_food integer; v_crystals integer; v_speedups integer;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if p_milestone not in (20,50,100) then raise exception 'Invalid milestone'; end if;
  select coalesce(sum(points),0)::integer into v_points from public.frostbound_quests
    where user_id=v_uid and quest_date=current_date and progress>=target_amount;
  if v_points<p_milestone then raise exception 'Not enough daily points'; end if;
  insert into public.frostbound_daily_chests(user_id,chest_date,milestone) values(v_uid,current_date,p_milestone)
    on conflict do nothing;
  if not found then raise exception 'Chest already claimed'; end if;
  v_wood:=case p_milestone when 20 then 100 when 50 then 250 else 500 end;
  v_food:=case p_milestone when 20 then 100 when 50 then 250 else 500 end;
  v_crystals:=case p_milestone when 100 then 10 else 0 end;
  v_speedups:=case p_milestone when 50 then 1 when 100 then 2 else 0 end;
  update public.frostbound_players set wood=wood+v_wood,food=food+v_food,crystals=crystals+v_crystals,speedups=speedups+v_speedups where user_id=v_uid;
  return jsonb_build_object('wood',v_wood,'food',v_food,'crystals',v_crystals,'speedups',v_speedups);
end $$;

create or replace function public.frostbound_add_battle_mail(p_source_key text,p_subject text,p_body text)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_id uuid;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  if length(trim(p_source_key))<3 or length(p_source_key)>100 then raise exception 'Invalid source'; end if;
  insert into public.frostbound_mail(user_id,category,subject,body,source_key)
    values(v_uid,'Battle',left(p_subject,100),left(p_body,1000),left(p_source_key,100))
    on conflict(user_id,source_key) do update set subject=excluded.subject,body=excluded.body
    returning id into v_id;
  return jsonb_build_object('id',v_id);
end $$;

create or replace function public.frostbound_mark_mail_read(p_mail_id uuid)
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid());
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  update public.frostbound_mail set read_at=coalesce(read_at,now()) where id=p_mail_id and user_id=v_uid;
  if not found then raise exception 'Mail not found'; end if;
  return jsonb_build_object('read',true);
end $$;

create or replace function public.frostbound_claim_all_mail()
returns jsonb language plpgsql security invoker set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_wood integer; v_food integer; v_crystals integer; v_speedups integer; v_count integer;
begin
  if v_uid is null then raise exception 'Authentication required'; end if;
  with claimable as (
    select * from public.frostbound_mail
    where user_id=v_uid and claimed_at is null and (reward_wood+reward_food+reward_crystals+reward_speedups)>0
    for update
  )
  select coalesce(sum(reward_wood),0)::integer,coalesce(sum(reward_food),0)::integer,
    coalesce(sum(reward_crystals),0)::integer,coalesce(sum(reward_speedups),0)::integer,count(*)::integer
    into v_wood,v_food,v_crystals,v_speedups,v_count from claimable;
  update public.frostbound_players set wood=wood+v_wood,food=food+v_food,crystals=crystals+v_crystals,speedups=speedups+v_speedups where user_id=v_uid;
  update public.frostbound_mail set claimed_at=case when (reward_wood+reward_food+reward_crystals+reward_speedups)>0 then now() else claimed_at end,
    read_at=coalesce(read_at,now()) where user_id=v_uid;
  return jsonb_build_object('claimed_count',v_count,'wood',v_wood,'food',v_food,'crystals',v_crystals,'speedups',v_speedups);
end $$;

revoke all on function public.frostbound_initialize_hito11() from public,anon;
revoke all on function public.frostbound_record_progress(text,integer) from public,anon;
revoke all on function public.frostbound_claim_quest(uuid) from public,anon;
revoke all on function public.frostbound_claim_achievement(uuid) from public,anon;
revoke all on function public.frostbound_claim_daily_chest(integer) from public,anon;
revoke all on function public.frostbound_add_battle_mail(text,text,text) from public,anon;
revoke all on function public.frostbound_mark_mail_read(uuid) from public,anon;
revoke all on function public.frostbound_claim_all_mail() from public,anon;
grant execute on function public.frostbound_initialize_hito11() to authenticated;
grant execute on function public.frostbound_record_progress(text,integer) to authenticated;
grant execute on function public.frostbound_claim_quest(uuid) to authenticated;
grant execute on function public.frostbound_claim_achievement(uuid) to authenticated;
grant execute on function public.frostbound_claim_daily_chest(integer) to authenticated;
grant execute on function public.frostbound_add_battle_mail(text,text,text) to authenticated;
grant execute on function public.frostbound_mark_mail_read(uuid) to authenticated;
grant execute on function public.frostbound_claim_all_mail() to authenticated;
