FROM node:20-alpine AS build
WORKDIR /app
COPY apps/web/package*.json ./
RUN npm install
COPY apps/web/ ./
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY apps/web/docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
EXPOSE 80
ENTRYPOINT ["/entrypoint.sh"]
