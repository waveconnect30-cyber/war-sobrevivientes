create table if not exists public.frostbound_tutorial_progress (
  user_id uuid primary key references auth.users(id) on delete cascade,
  step integer not null default 0 check (step between 0 and 5),
  completed boolean not null default false,
  updated_at timestamptz not null default now()
);

alter table public.frostbound_tutorial_progress enable row level security;
revoke all on table public.frostbound_tutorial_progress from public, anon;
grant select, insert, update on table public.frostbound_tutorial_progress to authenticated;

drop policy if exists frostbound_tutorial_select_own on public.frostbound_tutorial_progress;
create policy frostbound_tutorial_select_own on public.frostbound_tutorial_progress
for select to authenticated using ((select auth.uid()) = user_id);

drop policy if exists frostbound_tutorial_insert_own on public.frostbound_tutorial_progress;
create policy frostbound_tutorial_insert_own on public.frostbound_tutorial_progress
for insert to authenticated with check ((select auth.uid()) = user_id);

drop policy if exists frostbound_tutorial_update_own on public.frostbound_tutorial_progress;
create policy frostbound_tutorial_update_own on public.frostbound_tutorial_progress
for update to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

create index if not exists frostbound_tutorial_updated_idx
on public.frostbound_tutorial_progress (updated_at desc);
