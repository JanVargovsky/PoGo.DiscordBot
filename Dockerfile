FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build /out .

ENV PoGoEnvironment=

ENTRYPOINT ["dotnet", "PoGo.DiscordBot.dll"]
