FROM node:22-alpine AS build
WORKDIR /app

COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci

COPY src/frontend .
RUN npm run build

FROM nginx:1.28-alpine AS runtime
ENV COMPARISON_MAX_REQUEST_BYTES=33554432
COPY src/frontend/nginx.conf /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
