FROM rust:1-slim-bookworm AS build
RUN apt-get update && apt-get install -y --no-install-recommends gcc libc6-dev && rm -rf /var/lib/apt/lists/*
WORKDIR /source
COPY Server/tidebite-lobby/ Server/tidebite-lobby/
RUN cargo build --locked --release --manifest-path Server/tidebite-lobby/Cargo.toml
FROM debian:bookworm-slim
RUN groupadd --gid 10001 tidebite && useradd --uid 10001 --gid tidebite --create-home tidebite && mkdir /data && chown tidebite:tidebite /data
WORKDIR /app
COPY --from=build /source/Server/tidebite-lobby/target/release/tidebite-lobby /app/tidebite-lobby
USER 10001:10001
ENV TIDEBITE_BIND=0.0.0.0:8080 TIDEBITE_DB=/data/accounts.sqlite3
EXPOSE 8080
ENTRYPOINT ["/app/tidebite-lobby"]
