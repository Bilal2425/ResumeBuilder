FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for caching restore
COPY ["BaseApi.WebApi/BaseApi.WebApi.csproj", "BaseApi.WebApi/"]
COPY ["BaseApi.Business/BaseApi.Business.csproj", "BaseApi.Business/"]
COPY ["BaseApi.Data/BaseApi.Data.csproj", "BaseApi.Data/"]
COPY ["BaseApi.Model/BaseApi.Model.csproj", "BaseApi.Model/"]
COPY ["BaseApi.Service/BaseApi.Service.csproj", "BaseApi.Service/"]
COPY ["BaseApi.ServiceInterface/BaseApi.ServiceInterface.csproj", "BaseApi.ServiceInterface/"]
COPY ["BaseApi.Shared/BaseApi.Shared.csproj", "BaseApi.Shared/"]
COPY ["BaseApi.sln", "./"]

RUN dotnet restore BaseApi.sln

# Copy the remaining files and build
COPY . .
RUN dotnet publish BaseApi.WebApi/BaseApi.WebApi.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "BaseApi.WebApi.dll"]
