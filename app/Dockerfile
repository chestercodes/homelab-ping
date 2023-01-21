FROM mcr.microsoft.com/dotnet/sdk:6.0 as base

COPY src /src

WORKDIR /src/HomelabPing.Tests

RUN dotnet test

RUN mkdir /publish

WORKDIR /src/HomelabPing

RUN dotnet publish -c Release -o /publish

WORKDIR /src/HomelabPing.FunctionalTests

RUN dotnet build -c Release

ARG REGHOST

FROM ${REGHOST}base-images/pipeline:6.0 as pipeline

COPY --from=base /src /work/src

COPY scripts/functionalTests.ps1 /work/functionalTests.ps1
COPY scripts/migrations.ps1 /work/migrations.ps1

FROM mcr.microsoft.com/dotnet/aspnet:6.0 as runtime

WORKDIR /app

COPY --from=base /publish /app

ENTRYPOINT ["dotnet", "HomelabPing.dll"]
