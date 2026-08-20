/**
 * Henüz yazılmamış ekranların yerini tutuyor.
 *
 * Menü baştan tam kuruluyor ki panelin kapsamı belli olsun; ekranlar sırayla
 * dolduruluyor. Boş bir bağlantıya basınca hiçbir şey olmaması yerine "burası
 * hazırlanıyor" demek daha dürüst.
 */
export function Yapim({ ad }: { ad: string }) {
  return (
    <>
      <div className="sayfa-basligi">
        <h1>{ad}</h1>
        <p>Bu ekran hazırlanıyor.</p>
      </div>
      <div className="kart bos">Yakında burada olacak.</div>
    </>
  );
}
