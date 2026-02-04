FROM node:20-alpine
WORKDIR /app
COPY apps/web/package.json ./
COPY apps/web/vite.config.ts ./
COPY apps/web/tsconfig.json ./
COPY apps/web/tsconfig.node.json ./
COPY apps/web/postcss.config.cjs ./
COPY apps/web/tailwind.config.ts ./
COPY apps/web/index.html ./
COPY apps/web/public ./public
COPY apps/web/src ./src
RUN npm install
EXPOSE 5173
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
