FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish NileChain.API/NileChain.API.csproj -c Release -o /app/publish --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
# Heroku/Railway/Render set PORT. Program.cs binds http://*:$PORT when ASPNETCORE_URLS is empty.
ENTRYPOINT ["dotnet", "NileChain.API.dll"]
