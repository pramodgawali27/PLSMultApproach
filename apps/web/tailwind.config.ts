import type { Config } from "tailwindcss";

export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: "#121212",
        fog: "#f5f1ea",
        moss: "#2b5d4a",
        clay: "#d9c7b2",
        ember: "#d4572a",
        sand: "#e8ddd0",
        violet: "#6b4fa0",
        teal: "#47b5a0"
      },
      fontFamily: {
        display: ["'Space Grotesk'", "sans-serif"],
        body: ["'Crimson Text'", "serif"]
      }
    }
  },
  plugins: []
} satisfies Config;
