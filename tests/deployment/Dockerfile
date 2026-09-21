FROM docker:29.8.0-cli AS docker-cli
FROM node:22.22.2-bookworm-slim@sha256:9f6d5975c7dca860947d3915877f85607946403fc55349f39b4bc3688448bb6e
RUN apt-get update && apt-get install --yes --no-install-recommends bash ca-certificates curl jq tar util-linux shellcheck \
    && rm -rf /var/lib/apt/lists/*
COPY --from=docker-cli /usr/local/bin/docker /usr/local/bin/docker
COPY --from=docker-cli /usr/local/libexec/docker/cli-plugins/docker-compose /usr/local/lib/docker/cli-plugins/docker-compose
WORKDIR /work
ENV LANG=C.UTF-8
