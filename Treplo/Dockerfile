FROM ubuntu AS cpp_compile
RUN apt-get update && apt-get install wget gcc make -y

FROM cpp_compile AS opus
WORKDIR /opus
RUN \
    DIR=/opus/src && \
    mkdir -p ${DIR} && \
    cd ${DIR} && \
    wget https://downloads.xiph.org/releases/opus/opus-1.4.tar.gz -O - | tar -xz -C ${DIR} --strip-components 1
RUN \
    cd src && \
    ./configure && \
    make && make install && \
    cd .. && \
    cp /usr/local/lib/libopus.so opus

FROM cpp_compile AS libsodium
WORKDIR /libsodium
RUN \
    DIR=/libsodium/src && \
    mkdir -p ${DIR} && \
    cd ${DIR} && \
    wget https://download.libsodium.org/libsodium/releases/LATEST.tar.gz -O - | tar -xz -C ${DIR} --strip-components 1
RUN \
    cd src && \
    ./configure && \
    make && make check && \
    make install && \
    cd .. && \
    cp /usr/local/lib/libsodium.so libsodium

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Treplo/Treplo.csproj", "Treplo/"]
COPY ["Treplo.Common.OrleansGrpcConnector/Treplo.Common.OrleansGrpcConnector.csproj", "Treplo.Common.OrleansGrpcConnector/"]
COPY ["Treplo.Infrastructure/Treplo.Infrastructure.csproj", "Treplo.Infrastructure/"]
RUN dotnet restore "Treplo/Treplo.csproj"
COPY . .
WORKDIR "/src/Treplo"
RUN dotnet build "Treplo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Treplo.csproj" -c Release -o /app/publish /p:UseAppHost=false



FROM base AS final
WORKDIR /app
RUN apt-get clean && apt-get update && apt-get install ffmpeg -y
ENV FfmpegSettings__Path /usr/bin/ffmpeg
COPY --from=opus /opus/opus ./
COPY --from=libsodium /libsodium/libsodium ./
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Treplo.dll"]
