-- Frostbound Frontier - Hito 13: PVP, saqueo, incendio y escudos.
alter table public.frostbound_players add column if not exists peace_shield_until timestamptz,
 add column if not exists city_health integer not null default 100 check(city_health between 0 and 100),
 add column if not exists burning_until timestamptz;
alter table public.frostbound_world_tiles add column if not exists peace_shield_until timestamptz,
 add column if not exists city_health integer not null default 100 check(city_health between 0 and 100),
 add column if not exists burning_until timestamptz;

create or replace function private.frostbound_use_item_impl(p_item_id text,p_target_type text,p_target_key text)
returns jsonb language plpgsql security definer set search_path='' as $$
declare v_uid uuid:=(select auth.uid());v_seconds integer:=0;v_qty integer;v_applied integer:=0;v_until timestamptz;
begin
 if v_uid is null then raise exception 'Authentication required';end if;
 select quantity into v_qty from public.frostbound_inventory where user_id=v_uid and item_id=p_item_id for update;if coalesce(v_qty,0)<1 then raise exception 'Item unavailable';end if;
 if p_item_id='rss_wood_1k' then update public.frostbound_players set wood=wood+1000 where user_id=v_uid;
 elsif p_item_id='rss_food_1k' then update public.frostbound_players set food=food+1000 where user_id=v_uid;
 elsif p_item_id='alliance_chest' then update public.frostbound_players set wood=wood+500,food=food+500,crystals=crystals+2 where user_id=v_uid;
 elsif p_item_id in('speedup_1m','speedup_5m') then v_seconds:=case p_item_id when 'speedup_1m' then 60 else 300 end;
  if p_target_type='Building' then update public.frostbound_buildings set finishes_at=greatest(now(),finishes_at-make_interval(secs=>v_seconds)),updated_at=now() where user_id=v_uid and finishes_at>now() and slot_id=p_target_key;
  elsif p_target_type='Research' then update public.frostbound_research set finishes_at=greatest(now(),finishes_at-make_interval(secs=>v_seconds)),updated_at=now() where user_id=v_uid and status='Researching' and tech_id=p_target_key;
  elsif p_target_type='Training' then v_applied:=v_seconds;else raise exception 'Select an active timer';end if;
  if p_target_type<>'Training' then if not found then raise exception 'Active timer not found';else v_applied:=v_seconds;end if;end if;
 elsif p_item_id='shield_8h' then v_until:=greatest(now(),coalesce((select peace_shield_until from public.frostbound_players where user_id=v_uid),now()))+interval '8 hours';
  update public.frostbound_players set peace_shield_until=v_until where user_id=v_uid;
  update public.frostbound_world_tiles set peace_shield_until=v_until where tile_type='PlayerCity' and occupant_id=v_uid;v_applied:=28800;
 elsif p_item_id='teleport_advanced' then v_applied:=1;else raise exception 'Item cannot be used';end if;
 update public.frostbound_inventory set quantity=quantity-1,updated_at=now() where user_id=v_uid and item_id=p_item_id returning quantity into v_qty;
 return jsonb_build_object('item_id',p_item_id,'quantity',v_qty,'seconds_applied',v_applied,'target_type',p_target_type,'peace_shield_until',v_until);
end $$;

create or replace function private.frostbound_process_city_attack_impl(p_march_id uuid)
returns jsonb language plpgsql security definer set search_path='' as $$
declare v_uid uuid:=(select auth.uid());v_m public.frostbound_marches%rowtype;v_tile public.frostbound_world_tiles%rowtype;v_a public.frostbound_players%rowtype;v_d public.frostbound_players%rowtype;
 v_attack int;v_defense int;v_win bool;v_a_dead int;v_d_dead int;v_d_wounded int;v_wood bigint:=0;v_food bigint:=0;v_coal bigint:=0;v_newx int;v_newy int;v_relocated bool:=false;v_health int;
begin
 if v_uid is null then raise exception 'Authentication required';end if;
 select * into v_m from public.frostbound_marches where id=p_march_id and user_id=v_uid for update;if not found or v_m.march_type<>'Attack' or v_m.status<>'Battle' or v_m.arrival_time>now() then raise exception 'City attack march not ready';end if;
 select * into v_tile from public.frostbound_world_tiles where x=v_m.target_x and y=v_m.target_y and tile_type='PlayerCity' for update;if not found or v_tile.occupant_id is null or v_tile.occupant_id=v_uid then raise exception 'Enemy city not found';end if;
 select * into v_a from public.frostbound_players where user_id=v_uid for update;select * into v_d from public.frostbound_players where user_id=v_tile.occupant_id for update;
 if greatest(v_tile.peace_shield_until,v_d.peace_shield_until)>now() then raise exception 'PEACE_SHIELD_ACTIVE';end if;
 if v_a.alliance_id is not null and v_a.alliance_id=v_d.alliance_id then raise exception 'Cannot attack an ally';end if;
 if v_m.troop_count<1 or v_a.snow_infantry<v_m.troop_count then raise exception 'Not enough troops';end if;
 v_attack:=round(v_m.troop_count*20*(1+coalesce(v_m.hero_power_bonus,0)+coalesce((select level*.05 from public.frostbound_research where user_id=v_uid and tech_id='InfanteriaBlindada'),0)));
 v_defense:=v_d.snow_infantry*20+v_d.generator_level*55+v_d.city_health*2;v_win:=v_attack>v_defense;
 v_a_dead:=least(v_m.troop_count,case when v_win then greatest(1,v_m.troop_count/10) else greatest(1,v_m.troop_count/3) end);
 v_d_dead:=least(v_d.snow_infantry,case when v_win then greatest(1,v_d.snow_infantry/4) else greatest(0,v_d.snow_infantry/20) end);v_d_wounded:=least(v_d.snow_infantry-v_d_dead,case when v_win then v_d.snow_infantry/5 else v_d.snow_infantry/20 end);
 if v_win then v_wood:=greatest(0,v_d.wood-1000)/10;v_food:=greatest(0,v_d.food-1000)/10;v_coal:=greatest(0,v_d.coal-500)/10;end if;
 update public.frostbound_players set snow_infantry=snow_infantry-v_a_dead,wood=wood+v_wood,food=food+v_food,coal=coal+v_coal where user_id=v_uid;
 v_health:=case when v_win then greatest(0,v_d.city_health-25) else v_d.city_health end;
 update public.frostbound_players set snow_infantry=greatest(0,snow_infantry-v_d_dead-v_d_wounded),wood=wood-v_wood,food=food-v_food,coal=coal-v_coal,city_health=v_health,burning_until=case when v_win then now()+interval '1 hour' else burning_until end where user_id=v_d.user_id;
 insert into public.frostbound_hospital(user_id,wounded_infantry) values(v_d.user_id,v_d_wounded) on conflict(user_id) do update set wounded_infantry=public.frostbound_hospital.wounded_infantry+excluded.wounded_infantry;
 update public.frostbound_world_tiles set city_health=v_health,burning_until=case when v_win then now()+interval '1 hour' else burning_until end where id=v_tile.id;
 if v_health=0 then select x,y into v_newx,v_newy from (select floor(random()*1200)::int x,floor(random()*1200)::int y from generate_series(1,200))q where abs(x-v_tile.x)+abs(y-v_tile.y)>250 and not exists(select 1 from public.frostbound_world_tiles w where w.x=q.x and w.y=q.y and w.tile_type<>'Empty') limit 1;
  if v_newx is null then raise exception 'No relocation tile available';end if;update public.frostbound_world_tiles set tile_type='Empty',occupant_id=null,peace_shield_until=null,city_health=100,burning_until=null where id=v_tile.id;
  insert into public.frostbound_world_tiles(x,y,tile_type,occupant_id,level,city_health,updated_at) values(v_newx,v_newy,'PlayerCity',v_d.user_id,greatest(1,v_d.generator_level),100,now()) on conflict(x,y) do update set tile_type='PlayerCity',occupant_id=v_d.user_id,level=excluded.level,city_health=100,updated_at=now();update public.frostbound_players set city_health=100,burning_until=null where user_id=v_d.user_id;v_relocated:=true;end if;
 update public.frostbound_marches set status='Completed',battle_result=case when v_win then 'Victory' else 'Defeat' end,casualties=v_a_dead,loot_type=case when v_win then 'MixedResources' end,loot_amount=(v_wood+v_food+v_coal)::int where id=v_m.id;
 insert into public.frostbound_mail(user_id,category,subject,body,source_key) values
 (v_uid,'Battle',case when v_win then 'Victoria PVP' else 'Derrota PVP' end,'Poder '+v_attack+'. Bajas '+v_a_dead+'. Saqueo: '+v_wood+' madera, '+v_food+' comida, '+v_coal+' carbón.','city_attack_attacker_'+p_march_id),
 (v_d.user_id,'Battle',case when v_win then 'Tu ciudad fue derrotada' else 'Defensa exitosa' end,'Defensa '+v_defense+'. Bajas '+v_d_dead+', heridos '+v_d_wounded+'. Recursos perdidos: '+v_wood+' madera, '+v_food+' comida, '+v_coal+' carbón.','city_attack_defender_'+p_march_id) on conflict(user_id,source_key) do nothing;
 return jsonb_build_object('victory',v_win,'attacker_power',v_attack,'defender_power',v_defense,'attacker_casualties',v_a_dead,'defender_casualties',v_d_dead,'defender_wounded',v_d_wounded,'loot_wood',v_wood,'loot_food',v_food,'loot_coal',v_coal,'city_health',v_health,'burning',v_win,'relocated',v_relocated,'new_x',v_newx,'new_y',v_newy);
end $$;
create or replace function public.frostbound_process_city_attack(p_march_id uuid) returns jsonb language sql security invoker set search_path='' as $$select private.frostbound_process_city_attack_impl(p_march_id);$$;
revoke all on function private.frostbound_process_city_attack_impl(uuid) from public,anon,authenticated;grant execute on function private.frostbound_process_city_attack_impl(uuid) to authenticated;
revoke all on function public.frostbound_process_city_attack(uuid) from public,anon;grant execute on function public.frostbound_process_city_attack(uuid) to authenticated;
