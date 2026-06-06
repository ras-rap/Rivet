FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Rivet.csproj -c Release -o /publish \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=embedded

FROM alpine:3.20
RUN apk add --no-cache libstdc++ libgcc
WORKDIR /app
COPY --from=build /publish/Rivet .
COPY steam_appid.txt .
EXPOSE 25000/udp
EXPOSE 27011/udp
ENV SV_PORT=25000
ENV SV_MAXPLAYERS=8
ENV SV_NAME="Rivet Server (Docker)"
ENV SV_PASSWORD=""
ENV SV_STEAM_QUERY_PORT=27011
ENTRYPOINT ["./Rivet"]
