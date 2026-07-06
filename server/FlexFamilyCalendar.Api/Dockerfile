# syntax=docker/dockerfile:1
#
# Build-Context ist das Repo-Root — die Dockerfile-Referenz aus release.yml setzt file+context
# entsprechend. Grund: Directory.Build.props (Nullable/ImplicitUsings/MinVer/…) liegt im Root
# und MUSS mit in den Build, sonst kompiliert der Server nicht (fehlende System-Usings).
# MinVer wird per UseMinVer=false ausgeschaltet, weil .dockerignore die .git-Historie ausschließt
# (sonst würde MinVer wegen fehlender Tags werfen). Die Version kommt als Build-Arg.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG VERSION=0.0.0

# Nur die relevanten Projekt-Files ins Build-Image ziehen — Repo-Root-Context bringt sonst zu viel.
COPY Directory.Build.props .
COPY server/FlexFamilyCalendar.Api/FlexFamilyCalendar.Api.csproj server/FlexFamilyCalendar.Api/
RUN dotnet restore server/FlexFamilyCalendar.Api/FlexFamilyCalendar.Api.csproj \
    -p:UseMinVer=false

COPY server/FlexFamilyCalendar.Api/ server/FlexFamilyCalendar.Api/
RUN dotnet publish server/FlexFamilyCalendar.Api/FlexFamilyCalendar.Api.csproj \
    -c Release -o /app \
    -p:UseMinVer=false \
    -p:Version=${VERSION}

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FlexFamilyCalendar.Api.dll"]
