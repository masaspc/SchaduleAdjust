# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY ScheduleAdjust.sln ./
COPY src/ScheduleAdjust/ScheduleAdjust.csproj src/ScheduleAdjust/
COPY tests/ScheduleAdjust.Tests/ScheduleAdjust.Tests.csproj tests/ScheduleAdjust.Tests/
RUN dotnet restore

# Copy everything and publish
COPY . .
RUN dotnet publish src/ScheduleAdjust/ScheduleAdjust.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user
RUN adduser --disabled-password --gecos "" appuser

COPY --from=build /app/publish .

# App Service Linux uses port 8080 by default
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER appuser
ENTRYPOINT ["dotnet", "ScheduleAdjust.dll"]
