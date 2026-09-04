-- Frostbound Frontier · Hito 5: Bestias de nieve y combate PVE.
alter table public.frostbound_world_tiles
  add column if not exists beast_kind text,
  add column if not exists beast_power integer not null default 0,
  add column if not exists beast_hp integer not null default 0,
  add column if not exists beast_max_hp integer not null default 0,
  add column if not exists reward_type text,
  add column if not exists reward_amount integer not null default 0;

alter table public.frostbound_players
  add column if not exists crystals bigint not null default 0 check (crystals >= 0),
  add column if not exists speedups integer not null default 0 check (speedups >= 0);

alter table public.frostbound_marches alter column res_type drop not null;
alter table public.frostbound_marches
  add column if not exists troop_count integer not null default 0,
  add column if not exists battle_result text,
  add column if not exists casualties integer not null default 0,
  add column if not exists wounded integer not null default 0,
  add column if not exists loot_type text,
  add column if not exists loot_amount integer not null default 0;
alter table public.frostbound_marches drop constraint if exists frostbound_marches_march_type_check;
alter table public.frostbound_marches add constraint frostbound_marches_march_type_check check (march_type in ('Gathering','Attack','Return'));
alter table public.frostbound_marches drop constraint if exists frostbound_marches_res_type_check;
alter table public.frostbound_marches add constraint frostbound_marches_res_type_check check (res_type is null or res_type in ('Wood','Food','Coal'));
alter table public.frostbound_marches drop constraint if exists frostbound_marches_status_check;
alter table public.frostbound_marches add constraint frostbound_marches_status_check check (status in ('Marching','Gathering','Battle','Return','Completed'));

with beasts as (
 select ((311+n*67)%1200)::int x, ((509+n*101)%1200)::int y,
   case when n%2=0 then 'MistWolf' else 'GlacialBear' end kind,
   case when n%2=0 then 1 else 2 end lvl
 from generate_series(0,119) n
)
insert into public.frostbound_world_tiles
 (x,y,tile_type,level,beast_kind,beast_power,beast_hp,beast_max_hp,reward_type,reward_amount)
select x,y,'Beast',lvl,kind,case when lvl=1 then 80 else 180 end,
 case when lvl=1 then 120 else 260 end,case when lvl=1 then 120 else 260 end,
 case when n%3=0 then 'Coal' when n%3=1 then 'Crystals' else 'Speedups' end,
 case when lvl=1 then 40 else 90 end
from (select beasts.*,row_number() over() n from beasts) seeded
on conflict (x,y) do update set tile_type=excluded.tile_type,occupant_id=null,level=excluded.level,
 beast_kind=excluded.beast_kind,beast_power=excluded.beast_power,beast_hp=excluded.beast_hp,
 beast_max_hp=excluded.beast_max_hp,reward_type=excluded.reward_type,reward_amount=excluded.reward_amount
where public.frostbound_world_tiles.tile_type='Empty';

insert into public.frostbound_world_tiles
 (x,y,tile_type,level,beast_kind,beast_power,beast_hp,beast_max_hp,reward_type,reward_amount)
values (582,596,'Beast',1,'MistWolf',80,120,120,'Coal',40),
       (576,594,'Beast',2,'GlacialBear',180,260,260,'Crystals',90)
on conflict (x,y) do update set tile_type=excluded.tile_type,occupant_id=null,level=excluded.level,
 beast_kind=excluded.beast_kind,beast_power=excluded.beast_power,beast_hp=excluded.beast_hp,
 beast_max_hp=excluded.beast_max_hp,reward_type=excluded.reward_type,reward_amount=excluded.reward_amount;

create or replace function private.frostbound_process_beast_battle_impl(p_march_id uuid)
returns jsonb language plpgsql security definer set search_path='' as $$
declare
 v_uid uuid := (select auth.uid()); v_m public.frostbound_marches%rowtype;
 v_b public.frostbound_world_tiles%rowtype; v_power int; v_win boolean;
 v_dead int; v_wounded int; v_loot int := 0;
begin
 if v_uid is null then raise exception 'Authentication required'; end if;
 select * into v_m from public.frostbound_marches where id=p_march_id and user_id=v_uid for update;
 if not found then raise exception 'March not found'; end if;
 if v_m.status='Completed' then return jsonb_build_object('victory',v_m.battle_result='Victory','casualties',v_m.casualties,'wounded',v_m.wounded,'loot_type',v_m.loot_type,'loot_amount',v_m.loot_amount); end if;
 if v_m.march_type <> 'Attack' or v_m.status not in ('Marching','Battle') then raise exception 'Invalid battle march'; end if;
 select * into v_b from public.frostbound_world_tiles where x=v_m.target_x and y=v_m.target_y and tile_type='Beast' for update;
 if not found then raise exception 'Beast not found'; end if;
 perform 1 from public.frostbound_players where user_id=v_uid for update;
 if v_m.troop_count<=0 then raise exception 'No troops assigned'; end if;
 v_power:=v_m.troop_count*20; v_win:=v_power>=v_b.beast_power;
 v_dead:=least(v_m.troop_count, greatest(1,ceil(v_m.troop_count*(case when v_win then .10 else .35 end))::int));
 v_wounded:=least(v_m.troop_count-v_dead,ceil(v_m.troop_count*(case when v_win then .10 else .25 end))::int);
 if v_win then v_loot:=v_b.reward_amount; end if;
 update public.frostbound_players set snow_infantry=greatest(0,snow_infantry-v_dead),
  coal=coal+case when v_win and v_b.reward_type='Coal' then v_loot else 0 end,
  crystals=crystals+case when v_win and v_b.reward_type='Crystals' then v_loot else 0 end,
  speedups=speedups+case when v_win and v_b.reward_type='Speedups' then v_loot else 0 end where user_id=v_uid;
 if v_win then update public.frostbound_world_tiles set tile_type='Empty',level=1,beast_kind=null,beast_power=0,beast_hp=0,beast_max_hp=0,reward_type=null,reward_amount=0 where id=v_b.id; end if;
 update public.frostbound_marches set status='Completed',battle_result=case when v_win then 'Victory' else 'Defeat' end,
  casualties=v_dead,wounded=v_wounded,loot_type=case when v_win then v_b.reward_type else null end,
  loot_amount=v_loot,arrival_time=now() where id=p_march_id;
 return jsonb_build_object('victory',v_win,'casualties',v_dead,'wounded',v_wounded,'loot_type',case when v_win then v_b.reward_type else null end,'loot_amount',v_loot);
end $$;

create or replace function public.frostbound_process_beast_battle(p_march_id uuid)
returns jsonb language sql security invoker set search_path='' as $$ select private.frostbound_process_beast_battle_impl(p_march_id); $$;
revoke all on function private.frostbound_process_beast_battle_impl(uuid) from public,anon;
revoke all on function public.frostbound_process_beast_battle(uuid) from public,anon;
grant execute on function private.frostbound_process_beast_battle_impl(uuid) to authenticated;
grant execute on function public.frostbound_process_beast_battle(uuid) to authenticated;
