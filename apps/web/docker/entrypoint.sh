#!/usr/bin/env sh
set -e

API_BASE_URL_VALUE="${API_BASE_URL:-http://localhost:5072}"

cat > /usr/share/nginx/html/config.json <<EOC
{"apiBaseUrl":"${API_BASE_URL_VALUE}"}
EOC

exec nginx -g "daemon off;"
