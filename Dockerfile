# Portal web EYESOFTER (ASP.NET Core 10) para Railway. Dos etapas:
#  1. "build": el SDK de .NET compila y publica.
#  2. Imagen final con solo el runtime de ASP.NET, sin código fuente.

# ---------- Etapa 1: compilar ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Primero solo el proyecto: si no cambian los paquetes, Docker reusa la restauración.
COPY EyesofterWeb/EyesofterWeb.csproj EyesofterWeb/
RUN dotnet restore EyesofterWeb/EyesofterWeb.csproj
COPY EyesofterWeb/ EyesofterWeb/
RUN dotnet publish EyesofterWeb/EyesofterWeb.csproj -c Release -o /app --no-restore

# ---------- Etapa 2: ejecutar ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# Fuentes y fontconfig: los PDF (QuestPDF) y los Excel (ClosedXML) las usan para medir el texto.
# libgssapi-krb5-2: la librería de PostgreSQL (Npgsql) la busca al arrancar; sin ella sale un aviso en el log.
RUN apt-get update \
 && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
# La imagen de .NET trae el usuario sin privilegios "app": no se ejecuta como root.
USER app
# Railway define PORT; Program.cs lo lee.
EXPOSE 8080
ENTRYPOINT ["dotnet", "EyesofterWeb.dll"]
