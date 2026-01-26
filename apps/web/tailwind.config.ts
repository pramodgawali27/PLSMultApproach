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
        ember: "#d4572a"
      },
      fontFamily: {
        display: ["'Space Grotesk'", "sans-serif"],
        body: ["'Crimson Text'", "serif"]
      }
    }
  },
  plugins: []
} satisfies Config;
