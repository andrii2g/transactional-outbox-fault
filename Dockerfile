FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS verify
WORKDIR /src
COPY . .
RUN dotnet restore TransactionalOutboxFaultLab.slnx --locked-mode
RUN dotnet build TransactionalOutboxFaultLab.slnx -c Release --no-restore
RUN dotnet test TransactionalOutboxFaultLab.slnx -c Release --no-build
RUN dotnet run --project src/OutboxFaultLab.Cli -c Release --no-build -- matrix --output /tmp/matrix
RUN dotnet publish src/OutboxFaultLab.Cli -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/runtime:10.0@sha256:b89586dc17781f25531909993658aa8161205ae38b8cec8847df4a8221a403d5 AS runtime
WORKDIR /app
COPY --from=verify /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "OutboxFaultLab.Cli.dll"]
CMD ["demo", "--output", "/tmp/outbox-demo"]
