# Etapa 1: Construcción y Compilación (SDK de .NET 9)
FROM ://microsoft.com AS build
WORKDIR /src

# Copiar archivos de proyectos (.csproj) respetando las mayúsculas exactas
COPY ["Presentation.WebApi/Presentation.WebApi.csproj", "Presentation.WebApi/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Core.Application/Core.Application.csproj", "Core.Application/"]

# Restaurar dependencias de la API principal
RUN dotnet restore "Presentation.WebApi/Presentation.WebApi.csproj"

# Copiar todo el resto del código fuente al contenedor
COPY . .

# Compilar el proyecto de presentación en modo Release
WORKDIR "/src/Presentation.WebApi"
RUN dotnet publish "Presentation.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Entorno de ejecución (Runtime de .NET 9)
FROM ://microsoft.com AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render expone los contenedores en el puerto 10000 por defecto
ENV ASPNETCORE_URLS=http://+:10000

# Ejecutar la API apuntando a la DLL con su nombre exacto
ENTRYPOINT ["dotnet", "Presentation.WebApi.dll"]