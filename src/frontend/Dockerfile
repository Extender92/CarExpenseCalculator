FROM node:22.22.2-bookworm-slim@sha256:9f6d5975c7dca860947d3915877f85607946403fc55349f39b4bc3688448bb6e AS build
WORKDIR /app
RUN node --version | grep --fixed-strings "v22.22.2"

COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci

COPY src/frontend .
RUN npm run build

FROM nginx:1.28-alpine AS runtime
ENV COMPARISON_MAX_REQUEST_BYTES=33554432
COPY src/frontend/nginx.conf /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
