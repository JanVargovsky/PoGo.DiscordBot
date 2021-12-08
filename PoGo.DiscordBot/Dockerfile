FROM mcr.microsoft.com/dotnet/runtime:6.0-bullseye-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye AS build
WORKDIR /src
COPY ["PoGo.DiscordBot/PoGo.DiscordBot.csproj", "PoGo.DiscordBot/"]
RUN dotnet restore "PoGo.DiscordBot/PoGo.DiscordBot.csproj"
COPY . .
WORKDIR "/src/PoGo.DiscordBot"
RUN dotnet build "PoGo.DiscordBot.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "PoGo.DiscordBot.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "PoGo.DiscordBot.dll"]
