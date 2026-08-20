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
