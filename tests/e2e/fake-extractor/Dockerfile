FROM node:22.22.2-bookworm-slim@sha256:9f6d5975c7dca860947d3915877f85607946403fc55349f39b4bc3688448bb6e
WORKDIR /app
COPY tests/e2e/fake-extractor/server.mjs ./server.mjs
USER node
EXPOSE 8080
CMD ["node", "server.mjs"]
