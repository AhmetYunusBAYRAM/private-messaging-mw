FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PRIVATE.MESSAGING.MW/PRIVATE.MESSAGING.MW.csproj", "PRIVATE.MESSAGING.MW/"]
RUN dotnet restore "PRIVATE.MESSAGING.MW/PRIVATE.MESSAGING.MW.csproj"
COPY . .
WORKDIR "/src/PRIVATE.MESSAGING.MW"
RUN dotnet build "PRIVATE.MESSAGING.MW.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PRIVATE.MESSAGING.MW.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PRIVATE.MESSAGING.MW.dll"]
