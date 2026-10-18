FROM ubuntu:24.04
RUN apt-get update && apt-get install -y --no-install-recommends libatomic1 libgomp1 ca-certificates \
    && rm -rf /var/lib/apt/lists/* && useradd --uid 10001 --create-home tidebite
WORKDIR /app
COPY --chown=tidebite:tidebite Builds/LinuxServer/ /app/
RUN chmod +x /app/TidebiteServer
USER tidebite
EXPOSE 7777/udp
ENTRYPOINT ["/app/TidebiteServer", "-batchmode", "-nographics", "--server", "-logFile", "/dev/stdout"]
CMD ["--port", "7777"]
