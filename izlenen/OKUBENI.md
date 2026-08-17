# İzlenen klasör

`docker compose up` ile çalıştırıldığında bu klasör container'ın içine
`/data/watch` olarak salt okunur biçimde bağlanır ve tarayıcı burayı tarar
(varsayılan aralık: 10 saniye).

Klasörde hazır üç örnek dosya var, demo ilk açılışta çalışsın diye:

| Dosya | Ne gösteriyor |
|---|---|
| `rapor.txt` ve `rapor-kopya.txt` | Aynı içerik, farklı isim → `duplicates` bunları tek grupta bulur, çünkü hash isimden değil İÇERİKTEN hesaplanır |
| `BELGE.TXT` | Uzantısı büyük harfli → `search?extension=.txt` bunu da bulur, arama büyük/küçük harfe duyarsız |

Bakılacak yerler:

- <http://localhost:8080/swagger> — arayüz
- `GET /api/files` — kayıtlar
- `GET /api/files/search?extension=.txt` — uzantıya göre arama
- `GET /api/files/duplicates` — aynı içerikli dosyalar

Kendi dosyalarını da kopyalayabilirsin; tarayıcı bir sonraki turda alır.

Bu dosyanın kendisi de taranan dosyalardan biri olarak listede görünür —
tarayıcı dosya tipine bakmaz, klasördeki her şeyi kaydeder.
