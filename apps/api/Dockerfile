FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Financas.Api/Financas.Api.csproj src/Financas.Api/
RUN dotnet restore src/Financas.Api/Financas.Api.csproj
COPY src/Financas.Api/ src/Financas.Api/
# O openapi.json é gerado no build local (fonte do cliente do front); na imagem não é necessário.
RUN dotnet publish src/Financas.Api/Financas.Api.csproj -c Release -o /out --no-restore \
    -p:OpenApiGenerateDocuments=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
# /keys: volume persistente das chaves do Data Protection (protegem o cookie de sessão).
RUN mkdir /keys && chown $APP_UID /keys
ENV ASPNETCORE_URLS=http://+:8080 \
    DataProtection__KeysPath=/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Financas.Api.dll"]
