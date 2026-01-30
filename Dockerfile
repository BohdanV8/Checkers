# Використовуємо офіційний образ .NET SDK для збірки
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копіюємо файл проекту і відновлюємо залежності
# УВАГА: Якщо твій .csproj лежить в папці Checkers, шлях буде Checkers/Checkers.csproj
COPY ["Checkers/Checkers.csproj", "Checkers/"]
RUN dotnet restore "Checkers/Checkers.csproj"

# Копіюємо решту файлів і збираємо проект
COPY . .
WORKDIR "/src/Checkers"
RUN dotnet build "Checkers.csproj" -c Release -o /app/build

# Публікуємо проект
FROM build AS publish
RUN dotnet publish "Checkers.csproj" -c Release -o /app/publish

# Фінальний образ для запуску
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Checkers.dll"]