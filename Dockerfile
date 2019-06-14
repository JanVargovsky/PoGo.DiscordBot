FROM mcr.microsoft.com/dotnet/core/sdk:2.1 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o ../out

FROM mcr.microsoft.com/dotnet/core/runtime:2.1.11-alpine3.9
WORKDIR /app
COPY --from=build /app/out .

ENV PoGoEnvironment=

ENTRYPOINT ["dotnet", "PoGo.DiscordBot.dll"]