FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY Server/Tidebite.Matchmaker/ Server/Tidebite.Matchmaker/
COPY Assets/Tidebite/Core/AdmissionTickets.cs Assets/Tidebite/Core/AdmissionTickets.cs
COPY Assets/Tidebite/Core/LobbyContracts.cs Assets/Tidebite/Core/LobbyContracts.cs
RUN dotnet publish Server/Tidebite.Matchmaker/Tidebite.Matchmaker.csproj -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out/ .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tidebite.Matchmaker.dll"]
