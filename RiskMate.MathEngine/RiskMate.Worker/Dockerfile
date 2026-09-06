FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY RiskMate.MathEngine.sln ./
COPY RiskMate.Api/RiskMate.Api.csproj RiskMate.Api/
COPY RiskMate.MathEngine/RiskMate.MathEngine.csproj RiskMate.MathEngine/
COPY RiskMate.Shared/RiskMate.Shared.csproj RiskMate.Shared/
COPY RiskMate.Worker/RiskMate.Worker.csproj RiskMate.Worker/
COPY RiskMate.MathEngine.Tests/RiskMate.MathEngine.Tests.csproj RiskMate.MathEngine.Tests/

RUN dotnet restore RiskMate.MathEngine.sln

COPY . .

WORKDIR /app/RiskMate.Worker
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "RiskMate.Worker.dll"]
