import {
  Bar,
  BarChart,
  CartesianGrid,
  LabelList,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { EKSEN_YAZI, IZGARA, SERI_1, sayi } from './palet';

const YAZI = { fill: EKSEN_YAZI, fontSize: 12 };

/** Rakamları binlik ayraçlı gösteren ortak ipucu kutusu. */
function Ipucu({
  active,
  payload,
  label,
  birim,
}: {
  active?: boolean;
  payload?: { value: number }[];
  label?: string | number;
  birim: string;
}) {
  if (!active || !payload?.length) return null;

  return (
    <div className="ipucu">
      <div className="ipucu-baslik">{label}</div>
      <div>
        {sayi(payload[0].value)} {birim}
      </div>
    </div>
  );
}

/**
 * Yatay sütun.
 *
 * Şehir ve yer adları uzun; dikey sütunda etiketler eğik yazılmak zorunda
 * kalıyor ve okunmuyor. Yatayda ad doğal yönünde duruyor.
 *
 * Tek seri olduğu için gösterge yok — başlık zaten neyi ölçtüğünü söylüyor.
 * Değerler sütunların ucuna yazılıyor: sayıyı okumak için eksene bakmak
 * gerekmiyor.
 */
export function YatayCubuk({
  veri,
  birim,
  yukseklik,
}: {
  veri: { ad: string; deger: number }[];
  birim: string;
  yukseklik?: number;
}) {
  return (
    <ResponsiveContainer width="100%" height={yukseklik ?? veri.length * 34 + 30}>
      <BarChart
        data={veri}
        layout="vertical"
        margin={{ top: 4, right: 56, bottom: 4, left: 4 }}
      >
        <CartesianGrid stroke={IZGARA} horizontal={false} />
        <XAxis type="number" tick={YAZI} stroke={IZGARA} tickFormatter={sayi} />
        <YAxis
          type="category"
          dataKey="ad"
          tick={YAZI}
          stroke={IZGARA}
          width={140}
        />
        <Tooltip
          content={<Ipucu birim={birim} />}
          cursor={{ fill: 'rgb(23 17 14 / 4%)' }}
        />
        <Bar dataKey="deger" fill={SERI_1} radius={[0, 4, 4, 0]} barSize={16}>
          <LabelList
            dataKey="deger"
            position="right"
            formatter={(deger) => sayi(Number(deger ?? 0))}
            style={YAZI}
          />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

/**
 * Zaman eğrisi.
 *
 * Noktaların hepsine değer yazılmıyor: otuz günün otuz etiketi grafiği
 * okunmaz hale getirir. Kesin sayı için üstüne gelmek yeterli.
 */
export function Egri({
  veri,
  birim,
}: {
  veri: { ad: string; deger: number }[];
  birim: string;
}) {
  return (
    <ResponsiveContainer width="100%" height={220}>
      <LineChart data={veri} margin={{ top: 8, right: 12, bottom: 4, left: 4 }}>
        <CartesianGrid stroke={IZGARA} vertical={false} />
        <XAxis dataKey="ad" tick={YAZI} stroke={IZGARA} interval="preserveStartEnd" />
        <YAxis tick={YAZI} stroke={IZGARA} allowDecimals={false} width={36} />
        <Tooltip content={<Ipucu birim={birim} />} />
        <Line
          type="monotone"
          dataKey="deger"
          stroke={SERI_1}
          strokeWidth={2}
          dot={false}
          activeDot={{ r: 4, strokeWidth: 2, stroke: '#fff' }}
        />
      </LineChart>
    </ResponsiveContainer>
  );
}
