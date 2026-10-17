# Etapa 1: Construcción y Compilación (SDK de .NET 9)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copiar archivos de proyectos (.csproj)
COPY ["Presentation.WebApi/Presentation.WebApi.csproj", "Presentation.WebApi/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Core.Application/Core.Application.csproj", "Core.Application/"]

# Restaurar dependencias
RUN dotnet restore "Presentation.WebApi/Presentation.WebApi.csproj"

# Copiar el resto del código fuente
COPY . .

# Publicar la aplicación
WORKDIR "/src/Presentation.WebApi"
RUN dotnet publish "Presentation.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Runtime (.NET 9)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

# Puerto utilizado por Render
ENV ASPNETCORE_URLS=http://+:10000

EXPOSE 10000

# Ejecutar la API
ENTRYPOINT ["dotnet", "Presentation.WebApi.dll"]