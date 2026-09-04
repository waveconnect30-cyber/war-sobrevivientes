-- Frostbound Frontier - Hito 12: inventario, consumibles y tienda de alianza.

create table if not exists public.frostbound_inventory(
 user_id uuid not null references auth.users(id) on delete cascade,
 item_id text not null,
 quantity integer not null default 0 check(quantity>=0),
 updated_at timestamptz not null default now(),
 primary key(user_id,item_id)
);
create table if not exists public.frostbound_alliance_shop(
 item_id text primary key,
 display_name text not null,
 category text not null check(category in('Resources','Speedups','Combat','Special')),
 honor_cost integer not null check(honor_cost>0),
 quantity_per_purchase integer not null default 1 check(quantity_per_purchase>0),
 active boolean not null default true,
 updated_at timestamptz not null default now()
);
alter table public.frostbound_alliance_members add column if not exists honor_points integer not null default 0 check(honor_points>=0);

insert into public.frostbound_alliance_shop(item_id,display_name,category,honor_cost,quantity_per_purchase) values
 ('speedup_1m','Acelerador de 1 min','Speedups',40,1),('speedup_5m','Acelerador de 5 min','Speedups',150,1),
 ('shield_8h','Escudo de protección 8 h','Combat',300,1),('teleport_advanced','Teletransporte avanzado','Special',500,1),
 ('rss_wood_1k','Caja de 1,000 Madera','Resources',80,1),('rss_food_1k','Caja de 1,000 Comida','Resources',80,1),
 ('alliance_chest','Cofre de la Alianza','Special',250,1)
on conflict(item_id) do update set display_name=excluded.display_name,category=excluded.category,honor_cost=excluded.honor_cost,quantity_per_purchase=excluded.quantity_per_purchase,active=true,updated_at=now();

alter table public.frostbound_inventory enable row level security;
alter table public.frostbound_alliance_shop enable row level security;
drop policy if exists frostbound_inventory_own on public.frostbound_inventory;
create policy frostbound_inventory_own on public.frostbound_inventory for select to authenticated using((select auth.uid())=user_id);
drop policy if exists frostbound_shop_read on public.frostbound_alliance_shop;
create policy frostbound_shop_read on public.frostbound_alliance_shop for select to authenticated using(active);
revoke all on public.frostbound_inventory,public.frostbound_alliance_shop from anon,authenticated;
grant select on public.frostbound_inventory,public.frostbound_alliance_shop to authenticated;

create or replace function public.frostbound_get_my_honor() returns jsonb language sql stable security invoker set search_path='' as $$
 select jsonb_build_object('honor_points',coalesce((select honor_points from public.frostbound_alliance_members where user_id=(select auth.uid())),0));
$$;

create or replace function private.frostbound_buy_alliance_item_impl(p_item_id text)
returns jsonb language plpgsql security definer set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_member public.frostbound_alliance_members%rowtype; v_item public.frostbound_alliance_shop%rowtype; v_qty integer;
begin
 if v_uid is null then raise exception 'Authentication required'; end if;
 select * into v_member from public.frostbound_alliance_members where user_id=v_uid for update;
 if not found then raise exception 'Alliance membership required'; end if;
 select * into v_item from public.frostbound_alliance_shop where item_id=p_item_id and active;
 if not found then raise exception 'Shop item unavailable'; end if;
 if v_member.honor_points<v_item.honor_cost then raise exception 'Not enough alliance honor'; end if;
 update public.frostbound_alliance_members set honor_points=honor_points-v_item.honor_cost where user_id=v_uid;
 insert into public.frostbound_inventory(user_id,item_id,quantity) values(v_uid,v_item.item_id,v_item.quantity_per_purchase)
 on conflict(user_id,item_id) do update set quantity=public.frostbound_inventory.quantity+excluded.quantity,updated_at=now() returning quantity into v_qty;
 return jsonb_build_object('item_id',v_item.item_id,'quantity',v_qty,'honor_points',v_member.honor_points-v_item.honor_cost);
end $$;

create or replace function private.frostbound_use_item_impl(p_item_id text,p_target_type text,p_target_key text)
returns jsonb language plpgsql security definer set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_seconds integer:=0; v_qty integer; v_applied integer:=0;
begin
 if v_uid is null then raise exception 'Authentication required'; end if;
 select quantity into v_qty from public.frostbound_inventory where user_id=v_uid and item_id=p_item_id for update;
 if coalesce(v_qty,0)<1 then raise exception 'Item unavailable'; end if;
 if p_item_id='rss_wood_1k' then update public.frostbound_players set wood=wood+1000 where user_id=v_uid;
 elsif p_item_id='rss_food_1k' then update public.frostbound_players set food=food+1000 where user_id=v_uid;
 elsif p_item_id='alliance_chest' then update public.frostbound_players set wood=wood+500,food=food+500,crystals=crystals+2 where user_id=v_uid;
 elsif p_item_id in('speedup_1m','speedup_5m') then
   v_seconds:=case p_item_id when 'speedup_1m' then 60 else 300 end;
   if p_target_type='Building' then
     update public.frostbound_buildings set finishes_at=greatest(now(),finishes_at-make_interval(secs=>v_seconds)),updated_at=now()
       where user_id=v_uid and finishes_at>now() and slot_id=p_target_key;
   elsif p_target_type='Research' then
     update public.frostbound_research set finishes_at=greatest(now(),finishes_at-make_interval(secs=>v_seconds)),updated_at=now()
       where user_id=v_uid and status='Researching' and tech_id=p_target_key;
   elsif p_target_type='Training' then v_applied:=v_seconds;
   else raise exception 'Select an active timer'; end if;
   if p_target_type<>'Training' then if not found then raise exception 'Active timer not found'; else v_applied:=v_seconds; end if; end if;
 elsif p_item_id in('shield_8h','teleport_advanced') then v_applied:=1;
 else raise exception 'Item cannot be used'; end if;
 update public.frostbound_inventory set quantity=quantity-1,updated_at=now() where user_id=v_uid and item_id=p_item_id returning quantity into v_qty;
 return jsonb_build_object('item_id',p_item_id,'quantity',v_qty,'seconds_applied',v_applied,'target_type',p_target_type);
end $$;

create or replace function private.frostbound_donate_alliance_technology_impl(p_resource text,p_amount integer)
returns jsonb language plpgsql security definer set search_path='' as $$
declare v_uid uuid:=(select auth.uid()); v_honor integer;
begin
 if v_uid is null or p_amount<10 or p_resource not in('Wood','Food') then raise exception 'Invalid donation'; end if;
 if not exists(select 1 from public.frostbound_alliance_members where user_id=v_uid) then raise exception 'Alliance membership required'; end if;
 if p_resource='Wood' then update public.frostbound_players set wood=wood-p_amount where user_id=v_uid and wood>=p_amount;
 else update public.frostbound_players set food=food-p_amount where user_id=v_uid and food>=p_amount; end if;
 if not found then raise exception 'Not enough resources'; end if;
 update public.frostbound_alliance_members set honor_points=honor_points+greatest(1,p_amount/10) where user_id=v_uid returning honor_points into v_honor;
 return jsonb_build_object('honor_points',v_honor,'donated',p_amount,'resource',p_resource);
end $$;

create or replace function public.frostbound_buy_alliance_item(p_item_id text) returns jsonb language sql security invoker set search_path='' as $$ select private.frostbound_buy_alliance_item_impl(p_item_id); $$;
create or replace function public.frostbound_use_item(p_item_id text,p_target_type text default null,p_target_key text default null) returns jsonb language sql security invoker set search_path='' as $$ select private.frostbound_use_item_impl(p_item_id,p_target_type,p_target_key); $$;
create or replace function public.frostbound_donate_alliance_technology(p_resource text,p_amount integer) returns jsonb language sql security invoker set search_path='' as $$ select private.frostbound_donate_alliance_technology_impl(p_resource,p_amount); $$;

create or replace function public.frostbound_award_help_honor() returns trigger language plpgsql security invoker set search_path='' as $$
begin update public.frostbound_alliance_members set honor_points=honor_points+10 where user_id=new.helper_id; return new; end $$;
drop trigger if exists frostbound_help_award_honor on public.frostbound_alliance_help_actions;
create trigger frostbound_help_award_honor after insert on public.frostbound_alliance_help_actions for each row execute function public.frostbound_award_help_honor();

revoke all on function private.frostbound_buy_alliance_item_impl(text),private.frostbound_use_item_impl(text,text,text),private.frostbound_donate_alliance_technology_impl(text,integer) from public,anon,authenticated;
grant execute on function private.frostbound_buy_alliance_item_impl(text),private.frostbound_use_item_impl(text,text,text),private.frostbound_donate_alliance_technology_impl(text,integer) to authenticated;
revoke all on function public.frostbound_get_my_honor(),public.frostbound_buy_alliance_item(text),public.frostbound_use_item(text,text,text),public.frostbound_donate_alliance_technology(text,integer) from public,anon;
grant execute on function public.frostbound_get_my_honor(),public.frostbound_buy_alliance_item(text),public.frostbound_use_item(text,text,text),public.frostbound_donate_alliance_technology(text,integer) to authenticated;
