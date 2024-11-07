#!/bin/sh
# https://developers.cloudflare.com/pages/framework-guides/deploy-a-blazor-site/
curl -sSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh -c 8.0 -InstallDir ./dotnet
./dotnet/dotnet --version
./dotnet/dotnet publish Client -c Release -o output