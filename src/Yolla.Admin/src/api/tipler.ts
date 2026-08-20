/** Sunucudaki `Yolla.Application.Admin` DTO'larının istemci karşılıkları. */

export interface AdSayi {
  name: string;
  count: number;
}

export interface GunSayi {
  day: string;
  count: number;
}

export interface Ozet {
  userCount: number;
  premiumUserCount: number;
  deviceCount: number;
  activeDevices7: number;
  activeDevices30: number;
  placeCount: number;
  placesWithoutPhoto: number;
  cityCount: number;
  swipeCount: number;
  likeCount: number;
  tripCount: number;
  tripsLast30Days: number;
  pendingPhotoCount: number;
  pendingSuggestionCount: number;
  platforms: AdSayi[];
  tripsByDay: GunSayi[];
}

export interface SehirKullanim {
  cityId: number;
  cityName: string;
  swipes: number;
  likes: number;
  trips: number;
  devices: number;
  places: number;
}

export interface PopulerYer {
  placeId: number;
  name: string;
  cityName: string;
  categoryName: string;
  likes: number;
  tripAdds: number;
  hasPhoto: boolean;
}

export interface SehirPlani {
  cityName: string;
  trips: number;
  averageStops: number;
  averageDistanceKm: number;
}

export interface Koridor {
  fromCityName: string;
  toCityName: string;
  trips: number;
}

export interface Rotalar {
  cityTrips: SehirPlani[];
  corridors: Koridor[];
  travelModes: AdSayi[];
}

export interface Uyelik {
  totalUsers: number;
  premiumUsers: number;
  freeUsers: number;
  premiumBySource: AdSayi[];
  coinsEarned: number;
  coinsSpent: number;
  coinsOutstanding: number;
  coinsByReason: AdSayi[];
}

/** Moderasyon kuyruğundaki fotoğraf gönderisi. */
export interface FotoGonderi {
  id: number;
  placeId: number;
  placeName: string;
  cityName?: string;
  existingPhotoUrl?: string;
  url: string;
  width: number;
  height: number;
  sizeBytes: number;
  userId: number;
  userApprovedCount: number;
  userRejectedCount: number;
  createdAt: string;
}

/** Öneriye yakın, katalogda kayıtlı yer. */
export interface YakinYer {
  id: number;
  name: string;
  categoryName: string;
  distanceMeters: number;
}

/** Moderasyon kuyruğundaki yer önerisi. */
export interface YerOnerisi {
  id: number;
  name: string;
  categoryName: string;
  cityName: string;
  districtName?: string;
  latitude: number;
  longitude: number;
  description?: string;
  address?: string;
  userId: number;
  userApprovedCount: number;
  nearby: YakinYer[];
  createdAt: string;
}

/** Fotoğraf bekleyen yer. */
export interface FotografsizYer {
  placeId: number;
  name: string;
  cityName: string;
  districtName?: string;
  categoryName: string;
  qualityScore: number;
  hasDescription: boolean;
  latitude: number;
  longitude: number;
  wikidataId?: string;
}

/** Öneri kurulurken seçilebilecek kategori. */
export interface Kategori {
  key: string;
  name: string;
  icon?: string;
}
