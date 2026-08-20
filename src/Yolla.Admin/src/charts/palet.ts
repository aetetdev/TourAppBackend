/**
 * Grafiklerin renk yuvaları.
 *
 * Arayüz renklerinden ayrı tutuluyor: düğme ve menü rengi markanın işi,
 * seri rengi okunabilirliğin. Bu üçlü renk körlüğü ayrımı, açıklık bandı ve
 * doygunluk tabanı için doğrulandı (en kötü komşu çift ΔE 23,1 protan /
 * 9,6 tritan — eşik 8).
 *
 * Sıra sabit: bir seri her zaman aynı yuvayı kullanıyor. Filtre serileri
 * azaltınca kalanların rengi değişmemeli, yoksa aynı şey iki ekranda iki
 * renkte görünür.
 *
 * `SERI_3` açık zeminde 3:1 kontrastın altında kalıyor; yalnızca yanında
 * görünür etiket ya da tablo varken kullanılıyor.
 */
export const SERI_1 = '#d9542b';
export const SERI_2 = '#2a78d6';
export const SERI_3 = '#1baf7a';

export const SERILER = [SERI_1, SERI_2, SERI_3];

/** Eksen ve ızgara geri planda kalmalı; veriyle yarışmamalı. */
export const IZGARA = '#e2dcd5';
export const EKSEN_YAZI = '#6b615b';

/** Sayıları binlik ayraçlı yazar. */
export const sayi = (deger: number) => deger.toLocaleString('tr-TR');
