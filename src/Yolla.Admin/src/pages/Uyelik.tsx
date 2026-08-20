import type { Uyelik as UyelikVeri } from '../api/tipler';
import { YatayCubuk } from '../charts/grafikler';
import { sayi } from '../charts/palet';
import { Bolum, Sayac, SayfaBasligi, Tablo } from '../components/parcalar';
import { Durumlu, useVeri } from '../components/veri';

export function Uyelik() {
  const durum = useVeri<UyelikVeri>('/admin/uyelik');

  return (
    <>
      <SayfaBasligi
        ad="Üyelik"
        aciklama="Premium dağılımı ve coin ekonomisi. Premium sayısı geçerli hakları sayıyor; süresi dolmuş hak premium sayılmıyor."
      />

      <Durumlu durum={durum}>
        {(u) => {
          const oran = u.totalUsers
            ? Math.round((u.premiumUsers / u.totalUsers) * 100)
            : 0;

          return (
            <>
              <div className="izgara dortlu">
                <Sayac baslik="Toplam kullanıcı" deger={u.totalUsers} />
                <Sayac
                  baslik="Premium"
                  deger={u.premiumUsers}
                  alt={`kullanıcıların %${oran}'i`}
                  vurgu
                />
                <Sayac baslik="Ücretsiz" deger={u.freeUsers} />
                <Sayac
                  baslik="Elde kalan coin"
                  deger={u.coinsOutstanding}
                  alt={`${sayi(u.coinsEarned)} kazanıldı, ${sayi(u.coinsSpent)} harcandı`}
                />
              </div>

              <div className="izgara ikili" style={{ marginTop: 16 }}>
                <Bolum
                  baslik="Premium nereden geldi"
                  aciklama="Coinle kazanılan ile satın alınanı ayırmak, ekonominin kendi kendine dönüp dönmediğini gösteriyor."
                >
                  <YatayCubuk
                    birim="kullanıcı"
                    yukseklik={u.premiumBySource.length * 44 + 30}
                    veri={u.premiumBySource.map((k) => ({
                      ad: k.name,
                      deger: k.count,
                    }))}
                  />
                </Bolum>

                <Bolum
                  baslik="Coin nereden kazanıldı"
                  aciklama="Kullanıcıların katkı karşılığı aldığı toplam coin."
                >
                  <YatayCubuk
                    birim="coin"
                    yukseklik={u.coinsByReason.length * 44 + 30}
                    veri={u.coinsByReason.map((k) => ({
                      ad: k.name,
                      deger: k.count,
                    }))}
                  />
                </Bolum>
              </div>

              <div style={{ marginTop: 16 }}>
                <Bolum baslik="Coin defteri özeti">
                  <Tablo
                    satirlar={[
                      { ad: 'Dağıtılan', deger: u.coinsEarned },
                      { ad: 'Premium’a çevrilen', deger: u.coinsSpent },
                      { ad: 'Kullanıcıların elinde', deger: u.coinsOutstanding },
                    ]}
                    anahtar={(s) => s.ad}
                    sutunlar={[
                      { baslik: 'Ölçü', ciz: (s) => s.ad },
                      { baslik: 'Coin', sagda: true, ciz: (s) => sayi(s.deger) },
                    ]}
                  />
                </Bolum>
              </div>
            </>
          );
        }}
      </Durumlu>
    </>
  );
}
