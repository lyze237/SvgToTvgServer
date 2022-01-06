FROM rust:buster AS rust
WORKDIR /app

RUN git clone -b switch-to-usvg https://github.com/RazrFalcon/sdk.git
WORKDIR /app/sdk/src/tools/svg2tvgt/
RUN cargo build --release


FROM debian:buster AS tvg
WORKDIR /app
RUN apt update && apt install -y curl unzip
RUN curl https://tinyvg.tech/download/tinyvg-x86_64-linux.zip -O
RUN unzip tinyvg-x86_64-linux.zip
RUN rm svg2tvgt tinyvg-x86_64-linux.zip


FROM mcr.microsoft.com/dotnet/sdk:5.0 AS dotnet
WORKDIR /src
COPY ["Server/SvgToTvgServer.Server.csproj", "Server/"]
COPY ["Client/SvgToTvgServer.Client.csproj", "Client/"]
COPY ["Shared/SvgToTvgServer.Shared.csproj", "Shared/"]
RUN dotnet restore "Server/SvgToTvgServer.Server.csproj"
RUN dotnet restore "Shared/SvgToTvgServer.Shared.csproj"
RUN dotnet restore "Client/SvgToTvgServer.Client.csproj"
COPY . .
WORKDIR /src/Server
RUN dotnet publish "SvgToTvgServer.Server.csproj" -c Release --self-contained --runtime linux-x64 -o /app/publish
RUN ls /app/publish

FROM node:buster
WORKDIR /app
RUN npm -g install svgo
EXPOSE 80
COPY Server/Tools/ /tools/
COPY --from=dotnet /app/publish .
COPY --from=tvg /app/ /tools/
COPY --from=rust /app/sdk/src/tools/svg2tvgt/target/release/svg2tvgt /tools/svg2tvgt
RUN ls -la
ENTRYPOINT ["./SvgToTvgServer.Server", "--urls", "http://*:80"]

