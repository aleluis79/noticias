# syntax=docker/dockerfile:1

# Build stage
FROM node:22-alpine AS build
WORKDIR /app

RUN corepack enable

COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

COPY . ./

# Empty by default so the bundle calls the API through the nginx reverse proxy
# on the same origin. Set it to an absolute URL to build against another API.
ARG VITE_API_BASE=""
ENV VITE_API_BASE=$VITE_API_BASE

RUN pnpm build

# Runtime stage
FROM nginx:1.27-alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
