using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Yolla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedCategoriesAndCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "created_at", "icon", "is_visible", "key", "name_en", "name_tr", "parent_id", "updated_at", "weight" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "landmark", true, "historic", "History", "Tarihi", null, null, (short)0 },
                    { 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "palette", true, "culture", "Culture & Arts", "Kültür & Sanat", null, null, (short)0 },
                    { 3, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "tree", true, "nature", "Nature", "Doğa", null, null, (short)0 },
                    { 4, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "mosque", true, "religious", "Religious", "İnanç", null, null, (short)0 },
                    { 5, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ferris-wheel", true, "entertainment", "Entertainment", "Eğlence", null, null, (short)0 },
                    { 6, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "map-pin", true, "other", "Other", "Diğer", null, null, (short)0 }
                });

            migrationBuilder.InsertData(
                table: "countries",
                columns: new[] { "id", "created_at", "is_active", "iso2", "name_en", "name_tr", "osm_relation_id", "updated_at" },
                values: new object[] { 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "TR", "Türkiye", "Türkiye", 174737L, null });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "created_at", "icon", "is_visible", "key", "name_en", "name_tr", "parent_id", "updated_at", "weight" },
                values: new object[,]
                {
                    { 10, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "castle", true, "castle", "Castle", "Kale", 1, null, (short)10 },
                    { 11, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "columns", true, "ruins", "Ruins", "Ören Yeri", 1, null, (short)9 },
                    { 12, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "columns", true, "archaeological_site", "Archaeological Site", "Antik Kent", 1, null, (short)10 },
                    { 13, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "monument", true, "monument", "Monument", "Anıt", 1, null, (short)7 },
                    { 14, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "building", true, "historic_building", "Historic Building", "Tarihi Yapı", 1, null, (short)8 },
                    { 15, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "bridge", true, "historic_bridge", "Historic Bridge", "Tarihi Köprü", 1, null, (short)7 },
                    { 16, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "landmark", true, "tomb", "Tomb", "Türbe", 1, null, (short)6 },
                    { 17, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "warehouse", true, "caravanserai", "Caravanserai", "Kervansaray", 1, null, (short)9 },
                    { 18, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "tower", true, "tower", "Tower", "Kule", 1, null, (short)7 },
                    { 20, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "museum", true, "museum", "Museum", "Müze", 2, null, (short)10 },
                    { 21, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "image", true, "gallery", "Art Gallery", "Sanat Galerisi", 2, null, (short)7 },
                    { 22, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "drama", true, "theatre", "Theatre", "Tiyatro", 2, null, (short)6 },
                    { 23, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "brush", true, "artwork", "Artwork", "Sanat Eseri", 2, null, (short)4 },
                    { 30, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "umbrella", true, "beach", "Beach", "Plaj", 3, null, (short)9 },
                    { 31, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "waves", true, "waterfall", "Waterfall", "Şelale", 3, null, (short)10 },
                    { 32, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "mountain", true, "cave", "Cave", "Mağara", 3, null, (short)9 },
                    { 33, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "trees", true, "national_park", "National Park", "Milli Park", 3, null, (short)10 },
                    { 34, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "binoculars", true, "viewpoint", "Viewpoint", "Manzara Noktası", 3, null, (short)8 },
                    { 35, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "droplet", true, "lake", "Lake", "Göl", 3, null, (short)8 },
                    { 36, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "hot-tub", true, "hot_spring", "Hot Spring", "Kaplıca", 3, null, (short)8 },
                    { 37, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "mountain-snow", true, "valley", "Valley & Canyon", "Vadi & Kanyon", 3, null, (short)8 },
                    { 38, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "palmtree", true, "island", "Island", "Ada", 3, null, (short)7 },
                    { 39, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "tree-deciduous", true, "park", "Park", "Park", 3, null, (short)4 },
                    { 40, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "mosque", true, "mosque", "Mosque", "Cami", 4, null, (short)6 },
                    { 41, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "church", true, "church", "Church", "Kilise", 4, null, (short)8 },
                    { 42, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "star-of-david", true, "synagogue", "Synagogue", "Sinagog", 4, null, (short)8 },
                    { 43, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "church", true, "monastery", "Monastery", "Manastır", 4, null, (short)9 },
                    { 50, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ferris-wheel", true, "theme_park", "Theme Park", "Tema Parkı", 5, null, (short)7 },
                    { 51, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "paw-print", true, "zoo", "Zoo", "Hayvanat Bahçesi", 5, null, (short)7 },
                    { 52, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "fish", true, "aquarium", "Aquarium", "Akvaryum", 5, null, (short)7 },
                    { 53, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "waves", true, "water_park", "Water Park", "Su Parkı", 5, null, (short)6 },
                    { 60, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "shopping-bag", true, "bazaar", "Bazaar", "Çarşı & Pazar", 6, null, (short)7 },
                    { 61, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "utensils", true, "picnic_site", "Picnic Site", "Piknik Alanı", 6, null, (short)3 },
                    { 62, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "map-pin", true, "other_poi", "Other", "Diğer", 6, null, (short)2 },
                    { 90, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "accommodation", "Accommodation", "Konaklama", 6, null, (short)0 },
                    { 91, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "tourist_information", "Tourist Information", "Turizm Bürosu", 6, null, (short)0 },
                    { 92, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "camp_site", "Camp Site", "Kamp Alanı", 6, null, (short)0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: 6);
        }
    }
}
