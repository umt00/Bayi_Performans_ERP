# Redis ve Brevo e-posta OTP teknik notu

Bu belge mentorun istediği yerel Redis + e-posta OTP denemesinin mimarisini,
kurulumunu ve kabul sınırlarını açıklar.

## Seçilen yaklaşım

```text
POST /auth/login (ilk adım)
        |
        v
OtpService -- hash + TTL/cooldown --> Redis (Docker, localhost:6379)
        |
        +-- ham 6 haneli kod --> IOtpNotificationSender
                                  |
                                  v
                           Brevo HTTPS API
                                  |
                                  v
                         Kullanıcının e-postası
```

- Ham OTP yalnız e-posta gövdesine girer; Redis'te SHA-256 hash + salt saklanır.
- OTP API yanıtına, loga, `appsettings` dosyasına veya Git'e yazılmaz.
- Redis kaydı 180 saniye, yeniden gönderim kilidi 60 saniye geçerlidir.
- Üç hatalı denemeden sonra OTP silinir.
- Brevo gönderimi başarısızsa kullanılmayan OTP ve cooldown temizlenir, API
  `503 OTP_EPOSTA_GONDERILEMEDI` döndürür.
- Redis geçici olarak yoksa geliştirme ortamında process-içi fallback vardır;
  çok instance'lı/production çalışmada Redis zorunlu kabul edilmelidir.

## Neden Brevo HTTPS API?

Proje domain/DNS kurulumu olmadan farklı test kullanıcılarına OTP gönderebilmek
istemektedir. Brevo'da gönderici e-posta adresi, o adrese gönderilen 6 haneli
kodla doğrulanabilir. Transactional e-posta isteği doğrudan
`POST https://api.brevo.com/v3/smtp/email` HTTPS endpointine gider; SMTP portu,
Gmail uygulama parolası veya MailKit kullanılmaz.

Brevo'nun ücretsiz planında günlük gönderim kotası vardır. Domainsiz veya
ücretsiz bir gönderici adresinde Brevo gönderen adresini kendi uyumlu adresiyle
değiştirebilir ve teslim edilebilirlik production seviyesinde garanti edilmez.
Bu seçim staj/demonstrasyon ortamı içindir; gerçek production kullanımında
kurumsal domain doğrulaması ayrıca planlanmalıdır.

Resmî kaynaklar:

- [Brevo'da gönderici oluşturma ve doğrulama](https://help.brevo.com/hc/en-us/articles/208836149-Create-a-new-sender-From-name-and-From-email)
- [Brevo ücretsiz plan sınırları](https://help.brevo.com/hc/en-us/articles/208580669-FAQs-What-are-the-limits-of-the-Free-plan)
- [Brevo transactional e-posta API'si](https://developers.brevo.com/reference/send-transac-email)
- [Redis'i Docker ile çalıştırma](https://redis.io/docs/latest/operate/oss_and_stack/install/install-stack/)

## Brevo kurulumu

1. Brevo hesabında **Settings > Senders, Domains, IPs > Senders** bölümünü aç.
2. **Add a sender** ile uygulamanın gönderen adını ve e-posta adresini ekle.
3. Gönderici adresine gelen 6 haneli kodla bu adresi doğrula.
4. Brevo panelinde transactional e-posta gönderebilen bir API anahtarı üret.
5. API anahtarını yalnız backend User Secrets veya deployment ortam değişkenine
   yaz. Sohbete, ekran görüntüsüne, UI'a veya Git'e koyma.

Yerel User Secrets içindeki bölüm:

```json
{
  "BrevoEmail": {
    "Enabled": true,
    "ApiKey": "<BREVO_API_KEY>",
    "FromEmail": "<VERIFIED_BREVO_SENDER_EMAIL>",
    "FromName": "Bayi Performans",
    "TimeoutSeconds": 15
  }
}
```

Bu bölüm mevcut `Database` ve `SupabaseAuth` bölümlerinin yanına eklenir; ikinci
bir kök JSON nesnesi oluşturulmaz. `FromEmail`, Brevo panelinde doğrulanan adresle
aynı olmalıdır.

Render gibi bir deployment ortamındaki karşılıkları:

```text
BrevoEmail__Enabled=true
BrevoEmail__ApiKey=<BREVO_API_KEY>
BrevoEmail__FromEmail=<VERIFIED_BREVO_SENDER_EMAIL>
BrevoEmail__FromName=Bayi Performans
BrevoEmail__TimeoutSeconds=15
```

Değerler yalnız backend servisine tanımlanır. Vercel frontend değişkenine veya
`VITE_` önekli herhangi bir alana API anahtarı yazılmaz.

## Docker ve VS Code

Docker Desktop çalışırken VS Code'da:

1. **Terminal > Run Task > BayiPerformans: API icin hazirla** seç. Bu görev
   Redis'i başlatıp solution'ı derler.
2. Görev tamamlanınca `F5` ile API'yi aç.
3. API durduğunda Redis kalıcı olarak açık kalabilir. Durdurmak için
   **BayiPerformans: Redis durdur** görevini seç.

Redis verisi `redis_data` Docker volume'ünde tutulur. Redis Commander yalnız
isteğe bağlı inceleme aracıdır; OTP'nin çalışması için gerekli değildir.

## API testi

Login OTP'si doğrudan OTP gönder endpointinden başlatılmaz. Sıra şöyledir:

1. `POST /auth/login` çağrısına e-posta ve şifre gönder. Başarılı parola
   kontrolü `202 Accepted` ve `islem_id` döndürür; kod e-postaya gider.
2. `POST /auth/otp/dogrula` çağrısına işlem ID ve e-postadaki kodu gönder:

```json
{
  "islem_id": "LOGIN_CEVABINDAKI_ID",
  "kod": "EPOSTADAKI_6_HANELI_KOD"
}
```

3. Cevaptaki tek kullanımlık `dogrulama_token` değerini aynı e-posta ve şifreyle
   yapılan ikinci `POST /auth/login` isteğine ekle.
4. Başarılı login cevabındaki `access_token` değerini Swagger **Authorize**
   penceresine yaz.

Brevo ayarı kullanıcı hesabına ait değildir. User Secrets'taki `ApiKey` ve
`FromEmail`, çalışan backend'in tek gönderici kimliğidir ve her yeni kullanıcı
için değiştirilmez. Kullanıcının login formunda yazdığı e-posta alıcıdır.

## Güvenlik sınırları

- API anahtarı, OTP kodu ve Brevo cevap gövdesi loglanmaz.
- Hata cevabı sağlayıcı ayrıntısını dışarı sızdırmaz; kontrollü `503` döner.
- API anahtarı sabit C# değeri olarak tutulmaz.
- Gerçek değerler `appsettings.json` ve `appsettings.Example.json` içine yazılmaz.
- Açığa çıktığı düşünülen API anahtarı Brevo panelinden iptal edilip yenilenir.
- UI yalnız backend'i çağırır; tarayıcı Brevo API'sine doğrudan bağlanmaz.

## Tamamlanma ölçütü

- Docker Redis `healthy` görünür.
- Gönderimden sonra Redis'te TTL'li `otp:<islem_id>` anahtarı oluşur.
- Farklı bir test kullanıcısının e-postasına 6 haneli OTP gelir.
- Kod API'de başarıyla doğrulanır ve OTP anahtarı silinir.
- Yanlış kodda deneme sayısı azalır; üçüncü hatada kayıt silinir.
- Brevo kapalı/yanlış ayarlıysa `200` değil kontrollü `503` döner.
- Login kanıtı aynı kullanıcı için yalnız bir kez tüketilebilir.
- UI canlı modda sabit `123456` veya mock başarı kullanmaz.
