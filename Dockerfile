# Build the React/Vite frontend
FROM node:20-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm install
COPY frontend/ ./
RUN npm run build

# Build and run the .NET API, serving the React build from wwwroot
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY backend/AIWorkflow.Api/ ./backend/AIWorkflow.Api/
RUN dotnet restore ./backend/AIWorkflow.Api/AIWorkflow.Api.csproj
RUN dotnet publish ./backend/AIWorkflow.Api/AIWorkflow.Api.csproj -c Release -o /app/publish /p:UseAppHost=false
COPY --from=frontend-build /src/frontend/dist /app/publish/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
COPY --from=backend-build /app/publish ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "AIWorkflow.Api.dll"]
