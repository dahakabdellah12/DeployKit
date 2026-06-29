FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . ./
RUN dotnet publish DeployKit.Cloud.Api/DeployKit.Cloud.Api.csproj -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 80
ENV ASPNETCORE_URLS=http://0.0.0.0:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__Sqlite="Data Source=/data/deploykit.db"
VOLUME /data
ENTRYPOINT ["dotnet", "DeployKit.Cloud.Api.dll"]
