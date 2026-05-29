# syntax=docker/dockerfile:1

# Stufe 1: WASM-SPA bauen (Avalonia.Browser, dotnet publish).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Emscripten (Native-AOT-Schritt im Release-Publish) ruft "python" auf — schlankes SDK-Image hat keins.
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && ln -sf /usr/bin/python3 /usr/bin/python \
    && rm -rf /var/lib/apt/lists/*

RUN dotnet workload install wasm-tools

# Erst nur die Projektdateien für gecachtes Restore.
COPY src/FlexFamilyCalendar.csproj src/
COPY browser/FlexFamilyCalendar.Browser.csproj browser/
RUN dotnet restore browser/FlexFamilyCalendar.Browser.csproj

# Dann die Quellen.
COPY src/ src/
COPY browser/ browser/

RUN dotnet publish browser/FlexFamilyCalendar.Browser.csproj -c Release -o /tmp/publish

# Stufe 2: Caddy mit eingebetteter SPA. TLS terminiert Caddy, /api/* geht an die API.
FROM caddy:2
COPY --from=build /src/browser/bin/Release/net10.0-browser/browser-wasm/AppBundle/ /srv/spa/
