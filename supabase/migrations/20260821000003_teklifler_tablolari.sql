-- Teklif API'sinin kalıcı PostgreSQL tabloları.
-- Bu dosya uygulama tarafından otomatik çalıştırılmaz.
-- Veri/Supabase sorumlusu tarafından incelenip ilgili ortamda uygulanmalıdır.

begin;

create table if not exists public.teklifler
(
    teklif_id varchar(40) primary key,
    bayi_kodu varchar(100) not null,
    bayi_adi varchar(250),
    bayi_sehir varchar(100),
    bayi_bolge varchar(100),
    aciklama varchar(1000),
    durum varchar(30) not null,
    toplam_net_tutar numeric(18,2) not null,
    olusturulma_tarihi_utc timestamp with time zone not null,
    degerlendirme_sonucu jsonb,
    constraint ck_teklifler_durum
        check (durum in ('TASLAK', 'DEGERLENDIRILDI', 'ONAYLANDI', 'REDDEDILDI')),
    constraint ck_teklifler_toplam_net_tutar
        check (toplam_net_tutar >= 0)
);

create table if not exists public.teklif_kalemleri
(
    teklif_id varchar(40) not null,
    sira_no integer not null,
    urun_kodu varchar(100) not null,
    urun_adi varchar(250),
    depo_kodu varchar(100) not null,
    depo_adi varchar(250),
    miktar numeric(18,4) not null,
    birim_fiyat numeric(18,4) not null,
    iskonto_orani numeric(9,6) not null,
    net_tutar numeric(18,2) not null,
    primary key (teklif_id, sira_no),
    constraint fk_teklif_kalemleri_teklif
        foreign key (teklif_id)
        references public.teklifler(teklif_id)
        on delete cascade,
    constraint ck_teklif_kalemleri_miktar check (miktar > 0),
    constraint ck_teklif_kalemleri_birim_fiyat check (birim_fiyat > 0),
    constraint ck_teklif_kalemleri_iskonto check (iskonto_orani between 0 and 1),
    constraint ck_teklif_kalemleri_net_tutar check (net_tutar >= 0)
);

create index if not exists ix_teklifler_bayi_kodu
    on public.teklifler (bayi_kodu);
create index if not exists ix_teklifler_bayi_sehir
    on public.teklifler (bayi_sehir);
create index if not exists ix_teklifler_bayi_bolge
    on public.teklifler (bayi_bolge);
create index if not exists ix_teklifler_durum
    on public.teklifler (durum);
create index if not exists ix_teklifler_olusturulma_tarihi
    on public.teklifler (olusturulma_tarihi_utc desc);

commit;
