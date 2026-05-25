FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
# Install tzdata to support TZ environment variable
RUN apt-get update && apt-get install -y tzdata && rm -rf /var/lib/apt/lists/*
ARG VERSION=unknown
ENV WETTER_VERSION=$VERSION

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["wetter.Web/wetter.Web.csproj", "wetter.Web/"]
RUN dotnet restore "wetter.Web/wetter.Web.csproj"
COPY . .
WORKDIR "/src/wetter.Web"
RUN dotnet build "wetter.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "wetter.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "wetter.Web.dll"]
