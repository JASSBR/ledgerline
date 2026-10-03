# syntax=docker/dockerfile:1
# One Dockerfile for every deployable: the gateway and the three services share the same build and runtime.
#   docker build --build-arg PROJECT=src/Services/Ledger/Ledgerline.Ledger/Ledgerline.Ledger.csproj -t ledgerline-ledger .
# Add --platform linux/amd64 on Apple Silicon for cloud targets.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src

# Restore from project files only: this layer stays cached until a dependency changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
RUN dotnet restore "$PROJECT"
# Services pre-generate their Wolverine handler code, compiled into the image: no Roslyn at startup.
# The connection strings are placeholders: `codegen write` builds the host but never connects.
RUN if grep -q RunJasperFxCommands "$(dirname "$PROJECT")/Program.cs"; then \
      cd "$(dirname "$PROJECT")" \
      && ConnectionStrings__ledgerdb=Host=codegen ConnectionStrings__paymentsdb=Host=codegen \
         ConnectionStrings__frauddb=Host=codegen ConnectionStrings__messaging=amqp://codegen \
         dotnet run -c Release --no-restore -- codegen write; \
    fi
RUN dotnet publish "$PROJECT" -c Release -o /app --no-restore \
    # The native launcher embeds the name of its assembly: a fixed file name keeps one ENTRYPOINT for all images.
    && mv "/app/$(basename "$PROJECT" .csproj)" /app/service

# Chiseled runtime: no shell, no package manager, non-root by default.
# "-extra" adds ICU: amounts and dates are formatted for fr-FR, which globalization-invariant mode cannot do.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["/app/service"]
