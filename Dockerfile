FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/core/runtime:3.0
WORKDIR /app
COPY --from=build /out .

ENV PoGoEnvironment=

ENTRYPOINT ["dotnet", "PoGo.DiscordBot.dll"]
