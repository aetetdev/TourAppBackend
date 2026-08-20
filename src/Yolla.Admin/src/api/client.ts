/** API zarfı: sunucu her yanıtı `data` alanında döndürüyor. */
interface Envelope<T> {
  data: T;
}

/** Sunucunun RFC 7807 hata gövdesi. */
interface Problem {
  title?: string;
  detail?: string;
}

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

const KOK = '/api/v1';

/**
 * Jeton yalnızca sekme ömrü boyunca tutuluyor.
 *
 * Panel ortak bir bilgisayardan da açılabiliyor; `localStorage` sekme
 * kapandıktan sonra da oturumu açık bırakırdı.
 */
const JETON_ANAHTARI = 'yolla.admin.jeton';

export const jeton = {
  oku: () => sessionStorage.getItem(JETON_ANAHTARI),
  yaz: (deger: string) => sessionStorage.setItem(JETON_ANAHTARI, deger),
  sil: () => sessionStorage.removeItem(JETON_ANAHTARI),
};

/** Yetkisiz yanıt gelince tetikleniyor; kabuk oturumu kapatıyor. */
type YetkiDinleyici = () => void;
let yetkiDusenler: YetkiDinleyici[] = [];

export function yetkiDusunce(dinleyici: YetkiDinleyici) {
  yetkiDusenler.push(dinleyici);
  return () => {
    yetkiDusenler = yetkiDusenler.filter((d) => d !== dinleyici);
  };
}

async function istek<T>(yol: string, secenekler: RequestInit = {}): Promise<T> {
  const anahtar = jeton.oku();

  const yanit = await fetch(KOK + yol, {
    ...secenekler,
    headers: {
      ...(secenekler.headers ?? {}),
      ...(anahtar ? { Authorization: `Bearer ${anahtar}` } : {}),
    },
  });

  // 401 jetonun süresi dolmuş, 403 rol yetmiyor. İkisinde de panelde
  // kalmanın anlamı yok: kullanıcı girişe düşüyor.
  if (yanit.status === 401 || yanit.status === 403) {
    jeton.sil();
    yetkiDusenler.forEach((d) => d());
    throw new ApiError('Bu hesabın yetkisi yok ya da oturum düştü.', yanit.status);
  }

  if (!yanit.ok) {
    const govde = (await yanit.json().catch(() => ({}))) as Problem;
    throw new ApiError(
      govde.detail ?? govde.title ?? 'İstek başarısız oldu.',
      yanit.status,
    );
  }

  // 204 gövdesiz döner; `json()` çağırmak patlar.
  return yanit.status === 204 ? (undefined as T) : ((await yanit.json()) as T);
}

export const api = {
  async get<T>(yol: string): Promise<T> {
    const zarf = await istek<Envelope<T>>(yol);
    return zarf.data;
  },

  async post<T>(yol: string, govde?: unknown): Promise<T> {
    const zarf = await istek<Envelope<T>>(yol, {
      method: 'POST',
      ...(govde === undefined
        ? { headers: { 'Content-Length': '0' } }
        : {
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(govde),
          }),
    });
    return zarf?.data;
  },

  /** Dosya yükleyen uçlar; `Content-Type`'ı tarayıcı sınırla birlikte kuruyor. */
  async postForm<T>(yol: string, form: FormData): Promise<T> {
    const zarf = await istek<Envelope<T>>(yol, { method: 'POST', body: form });
    return zarf?.data;
  },
};

/** Giriş: panelin kendi ucu yok, uygulamanın hesap ucunu kullanıyor. */
export async function girisYap(email: string, password: string) {
  const yanit = await fetch(`${KOK}/account/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });

  const govde = await yanit.json();

  if (!yanit.ok) {
    throw new ApiError(
      govde.detail ?? govde.title ?? 'Giriş başarısız.',
      yanit.status,
    );
  }

  jeton.yaz(govde.data.accessToken);
}
