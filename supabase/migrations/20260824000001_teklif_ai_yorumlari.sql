-- AI Yorumlarını ana teklifler tablosundan ayırmak için (Database Normalization)
begin;

create table if not exists public.teklif_ai_yorumlari
(
    id uuid primary key default gen_random_uuid(),
    teklif_id varchar(40) not null,
    ai_comment text,
    alert_message_draft text,
    is_fallback boolean default false,
    created_at timestamp with time zone default now(),
    
    constraint fk_ai_yorum_teklif
        foreign key (teklif_id)
        references public.teklifler(teklif_id)
        on delete cascade,
        
    -- Bir teklifin sadece bir güncel AI yorumu olabilir
    constraint uq_ai_yorum_teklif
        unique (teklif_id)
);

-- RLS (Row Level Security) ayarları
alter table public.teklif_ai_yorumlari enable row level security;

-- Örnek RLS Politikası: Yalnızca yetkili kullanıcılar veya teklif sahibi okuyabilir.
-- Not: Projede özel bir auth şeması varsa policy ona göre ayarlanmalıdır. 
-- Şimdilik anonim (public) okumaya açık bırakıp demo ortamını kırmıyoruz, 
-- ancak gerçekte auth.uid() veya role check eklenmelidir.
create policy "Enable read access for all users"
    on public.teklif_ai_yorumlari
    for select
    using (true);

commit;
