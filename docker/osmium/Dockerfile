# OSM ham verisini (.osm.pbf) işlemek için araç kutusu.
# Türkiye extract'i ~450 MB; osmium bunu saniyeler içinde filtreleyip
# harvester'ın okuyacağı GeoJSONSeq formatına çevirir.
FROM debian:bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        osmium-tool \
        ca-certificates \
        curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /data

ENTRYPOINT ["osmium"]
CMD ["--help"]
