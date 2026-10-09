# Kayit, giris ve JWT akisi

Bu proje kullanici kaydi ve giris icin Supabase Auth kullanir. Kullanici yalniz
e-posta ve sifre gonderir. API kendi parola tablosunu olusturmaz, parolayi
hashlemez ve JWT uretmez.

Supabase Auth:

- kullanici icin bir UUID olusturur;
- parolayi duz metin yerine salt eklenmis bcrypt hash olarak kendi auth.users
  semasinda saklar;
- basarili giriste access token (JWT) ve refresh token uretir.

API parolayi yalniz TLS uzerinden Supabase Auth kayit/giris uclarina iletir. Parola,
token ve API anahtari loglanmaz veya Git'e yazilmaz.

Kayit, login, token yenileme ve onay e-postasi istekleri IP + endpoint basina
10 istek/dakika ile sinirlidir. Bir endpointin kotasi diger auth endpointini
kilitlemez.

Yerel VS Code profili HTTPS icin `https://localhost:7000` adresini kullanir;
`http://localhost:5000` istekleri HTTPS'e yonlendirilir. Uretimde TLS ya dogrudan
API'de ya da guvenilir bir reverse proxy'de sonlandirilmalidir. Proxy kullanilirsa
forwarded header ve guvenilir proxy listesi deployment ortaminda ayrica
yapilandirilmalidir.

## Istek sirasi

    Istemci
      |
      +-- POST /auth/kayit (e-posta + sifre)
      |      +-- API -> Supabase Auth /signup
      |
      +-- POST /auth/login (e-posta + sifre)
      |      +-- API -> Supabase parola kontrolu -> Brevo HTTPS API ile OTP
      |             +-- 202 Accepted + islem_id
      |
      +-- POST /auth/otp/dogrula (islem_id + kod)
      |      +-- tek kullanimlik dogrulama_token
      |
      +-- POST /auth/login (e-posta + sifre + dogrulama_token)
      |      +-- API -> OTP kanitini tuket -> Supabase Auth /token?grant_type=password
      |             +-- access_token + refresh_token
      |
      +-- POST /auth/token-yenile (refresh_token)
      |      +-- API -> Supabase Auth /token?grant_type=refresh_token
      |             +-- yeni access_token + yeni refresh_token
      |
      +-- POST /auth/onay-mailini-yeniden-gonder (e-posta)
      |      +-- API -> Supabase Auth /resend (type=signup)
      |
      +-- Korumali endpoint + Authorization: Bearer access_token
             +-- API -> Supabase JWKS ile JWT kontrolu
                    +-- Business -> DataAccess -> PostgreSQL
      |
      +-- POST /auth/cikis + Authorization: Bearer access_token
             +-- API -> Supabase Auth /logout?scope=local

## Endpointler

| Yontem | Adres | Kimlik gerekli mi? | Aciklama |
|---|---|---:|---|
| POST | /auth/kayit | Hayir | E-posta ve sifre ile Supabase kullanicisi olusturur |
| POST | /auth/login | Hayir | Ilk istekte parolayi dogrulayip OTP baslatir; ikinci istekte tek kullanimlik OTP kanitini tuketip access ve refresh token dondurur |
| POST | /auth/otp/dogrula | Hayir | Islem ID ve kodu dogrulayip kisa omurlu, tek kullanimlik login kaniti dondurur |
| POST | /auth/token-yenile | Hayir | Refresh tokeni dondurerek yeni access/refresh cifti dondurur |
| POST | /auth/onay-mailini-yeniden-gonder | Hayir | Kayit onay e-postasini yeniden ister; hesap varligini aciklamaz |
| POST | /auth/cikis | Evet | Yalniz mevcut oturumu Supabase tarafinda sonlandirir |
| GET | /auth/me | Evet | JWT'deki kullanici ID ve e-postayi dondurur |
| GET/POST | Mevcut dort is endpointi | Evet | Bearer JWT olmadan 401 doner |

Kayit ornegi:

    {
      "email": "ornek@example.com",
      "sifre": "Ornek-Sifre-123!"
    }

Ilk login istegi:

    {
      "email": "ornek@example.com",
      "sifre": "Ornek-Sifre-123!"
    }

Basarili parola kontrolu `202 Accepted` ile `islem_id` dondurur ve OTP'yi
e-postaya gonderir. Yanlis parola girilirse OTP uretilmez.

OTP dogrulama istegi:

    {
      "islem_id": "LOGIN_CEVABINDAKI_ISLEM_ID",
      "kod": "123456"
    }

Ikinci login istegi:

    {
      "email": "ornek@example.com",
      "sifre": "Ornek-Sifre-123!",
      "dogrulama_token": "OTP_DOGRULAMA_CEVABINDAKI_TOKEN"
    }

Ilk login isteginde `dogrulama_token` gonderilmez. Ikinci istekte OTP dogrulama
cevabindaki token eklenir. Gecersiz, suresi dolmus, baska e-postaya ait veya daha
once kullanilmis token `401` dondurur. Kanitin yalniz SHA-256 hash'i Redis'te TTL
ile tutulur ve basarili tuketme atomiktir; ayni token ikinci kez kullanilamaz.
Redis kullanilamiyorsa gelistirme ortaminda surec ici bellek deposu kullanilir.

E-posta dogrulamasi Supabase Dashboard'da aciksa kayit cevabinda
email_onayi_gerekli true ve oturum null gelir. Kullanici e-postasini
dogruladiktan sonra login yapar. Dogrulama kapaliysa kayit cevabi oturum
bilgilerini de icerebilir.

Login cevabindaki access_token Swagger'da Authorize dugmesine yazilir. Swagger
Authorization: Bearer basligini ekler. Tokeni README'ye, Git'e, ekran goruntusune
veya sohbete koyma.

Access token suresi dolmadan kisa sure once ya da tokenin suresi nedeniyle korumali
bir istek ilk kez `401` dondurdugunde istemci `token-yenile` ucunu bir kez cagirir.
Issuer, imza veya claim hatalarinda yenilemeyi sonsuz tekrarlamaz. Supabase refresh token
rotasyonu uygular: cevapta gelen yeni refresh_token eski degerin yerine atomik
olarak kaydedilmelidir. Ayni oturum icin ayni anda birden fazla yenileme istegi
gonderilmemelidir. Yenileme `401 OTURUM_GECERSIZ` dondururse istemci yerel tokenlari
siler ve kullaniciyi login ekranina yonlendirir.

`cikis` yalniz mevcut oturumun refresh tokenini iptal eder (`scope=local`). Istemci
204 cevabindan bagimsiz olarak kendi access ve refresh tokenlarini silmelidir.
JWT stateless oldugu icin mevcut access token `exp` zamanina kadar teknik olarak
gecerli kalabilir; bu Supabase'in resmi oturum davranisidir.

Tarayici tabanli gercek uygulamada refresh tokeni `localStorage` gibi JavaScript'in
okuyabildigi kalici depolara koymak XSS riskini buyutur. Bu ilk surum Swagger/API
testi icin JSON token dondurur; production istemci depolama modeli frontend ve
deployment guvenlik tasariminda ayrica kesinlestirilmelidir.

## Yerel konfigürasyon

Gercek degerler yalniz .NET User Secrets icinde tutulur:

    {
      "Database": {
        "ConnectionString": "<yerel PostgreSQL baglantisi>"
      },
      "SupabaseAuth": {
        "ProjectUrl": "https://<PROJECT_REF>.supabase.co",
        "PublishableKey": "<SB_PUBLISHABLE_KEY>",
        "Audience": "authenticated",
        "HttpTimeoutSeconds": 15
      }
    }

PublishableKey icin Dashboard'daki sb_publishable_ ile baslayan anahtari kullan.
sb_secret, service_role, JWT signing secret veya DB parolasi kullanma. JWT
dogrulamasi private secret ile degil Supabase'in acik JWKS anahtarlariyla yapilir.

Uygulama su guvenlik degerlerini baslangicta dogrular:

- proje URL'si HTTPS ve .supabase.co alaninda olmali;
- anahtar sb_publishable_ ile baslamali;
- audience authenticated olmali;
- HTTP timeout 1-60 saniye araliginda olmali.

## JWT kontrolu

Mevcut is endpointleri varsayilan authorization policy ile korunur. API:

- imzayi yalniz ES256 veya RS256 acik anahtariyla;
- issuer degerini https://PROJECT_REF.supabase.co/auth/v1 olarak;
- audience degerini authenticated olarak;
- token omrunu;
- UUID bicimindeki sub ve session_id alanlarini;
- role = authenticated kosulunu;
- is_anonymous degerinin true olmadigini

dogrular. Eksik veya gecersiz token 401, gecerli kimligin yetkisi yetersizse 403
doner.

JWT imza anahtarlari Supabase JWKS ucundan alinir; uygulama metadata yenilemeyi
10 dakikalik aralikla dener ve bilinmeyen yeni `kid` yenilemeyi tetikler. Son iyi
anahtar seti, Supabase gecici olarak kullanilamazsa daha uzun tutulabilir. Eski
anahtarin acil iptal senaryosu Dashboard key-rotation smoke testiyle ayrica
dogrulanmalidir.

Bu kontrol yalniz kullanicinin giris yaptigini kanitlar. Hangi bayinin hangi
veriyi gorebilecegi gibi bayi bazli yetki gerekiyorsa PO ayrica kural ve
kullanici-bayi eslemesi vermelidir.

Mevcut ilk surumde e-posta onayini tamamlayan her kayitli kullanici dort is
endpointine erisebilir. Erisim yalniz davetli stajyerlerle sinirlanacaksa acik
kayit kapatilmali veya mentor/PO onayli uygulama rolu eklenmelidir; Supabase'in
`authenticated` rolu tek basina uygulama yetkisi degildir.

## Supabase Dashboard ayarlari

Proje yoneticisi sunlari kontrol etmelidir:

1. Authentication > Providers > Email altinda e-posta/parola acik olmali.
2. Acik kayit mi, yalniz davetli kayit mi kullanilacagi kararlastirilmali.
3. E-posta dogrulamasinin acik/kapali olmasi kararlastirilmali.
4. Minimum parola uzunlugu en az 8 yapilmali.
5. JWT signing key ES256 veya RS256 olmali; legacy HS256 kullanilmamali.
6. Authentication > URL Configuration altindaki Site URL gercek uygulama adresi
   olmali; `http://localhost:3000` ancak orada gercekten bir uygulama calisiyorsa
   kullanilmali. Production ve yerel callback adresleri Redirect URLs listesine
   tam olarak eklenmelidir.
7. Supabase kayit/onay e-postalarinin production teslimi icin Supabase tarafinda
   custom SMTP ayrica tanimlanmalidir. Bu, uygulamanin Brevo OTP akisindan farklidir.
   Supabase'in varsayilan e-posta gondericisi yalniz deneme amaclidir; kota ve
   alici kisitlari nedeniyle resend endpointinin basarili olmasi mail teslimini
   garanti etmez.

Onay linki kullanilmis veya suresi gecmisse Supabase `otp_expired` dondurur.
Kullanici yeni bir link icin `/auth/onay-mailini-yeniden-gonder` ucunu kullanir ve
yalniz en son gelen e-postadaki linke bir kez tiklar. E-posta guvenlik tarayicilari
linki onceden aciyorsa mail saglayicisindaki link tracking/prefetch kapatilmalidir.

Bot korumasi/CAPTCHA ileride acilirsa istemci sozlesmesine CAPTCHA tokeni de
eklenmelidir. Mentorun mevcut "yalniz e-posta ve sifre" karari nedeniyle bu ilk
surumde ek alan yoktur.

## Uygulama OTP'si ve e-posta gonderimi

Supabase'in kayit/onay e-postalari ile uygulamanin `/auth/otp/*` akisi farkli
mekanizmalardir. Uygulama OTP'si Redis'te hash + TTL saklanir ve Brevo HTTPS API
ile e-postaya iletilir. Brevo API anahtari yalniz backend User Secrets veya ortam
degiskenlerinde tutulur; Brevo devre disiyse endpoint sahte basari yerine kontrollu
`503` dondurur. Ayrintili kurulum ve kabul adimlari
[Redis ve e-posta OTP teknik notundadir](./OTP_REDIS_EMAIL.md).

## Gorev siniri

Bu kimlik altyapisi Yazilim-1 kapsamindadir: HTTP sozlesmesi, Supabase Auth
istemcisi, JWT middleware, Swagger ve konfigurasyon.

Yazilim-2 parola hashleme, kullanici tablosu veya JWT uretimi yazmaz. Dogrulanmis
kullanicinin eristigi performans, risk, stok ve teklif servislerini uygular.

## Test

BayiPerformansAppWeb.Api.Tests projesi gercek kullanici veya secret kullanmadan,
sahte Supabase HTTP cevaplariyla su davranislari dogrular:

- basarili login cevabinin eslenmesi;
- e-posta onayi bekleyen kayit;
- refresh token rotasyonunun yeni access/refresh cifti dondurmesi;
- gecersiz refresh tokenin kontrollu 401 olmasi;
- local logout ve onay maili resend sozlesmesi;
- yanlis kimlik bilgisinin kontrollu 401 olmasi;
- rate-limit cevabi ve Retry-After;
- gecersiz upstream cevabinin kontrollu 503 olmasi.
- yanlis issuer, audience, imza ve suresi gecmis JWT'nin reddedilmesi.

Test projesi Yazilim-2'nin solution degisikligiyle cakismamasi icin simdilik sln
dosyasina eklenmemistir. VS Code'da **Terminal > Run Task > BayiPerformans: API
auth testleri** ile dogrudan calistirilir. Yalniz solution testini calistirmak bu
testleri kapsamaz; ekip branch'leri birlestirilirken API ve Business test
projeleri solution'a birlikte eklenmelidir.
