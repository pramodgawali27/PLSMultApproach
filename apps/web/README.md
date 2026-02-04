# Web App (React + Vite)

Frontend for the PLS platform.

## Features
- Dev login
- Upload + generate summary
- Job status polling
- Dashboard with search/filter
- Summary detail with PDF/DOCX download
- Prompt customization

## Config
- `VITE_API_BASE_URL` (default: `http://localhost:5072`)
- Runtime override: `apps/web/public/config.json` with `apiBaseUrl`.
  - This lets you change API URL in production without rebuilding the UI.

## Local Dev
```bash
npm install
npm run dev
```

## Notes
- Auth token is stored in `localStorage` under `pls_token`.
