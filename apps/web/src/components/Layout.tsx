import { NavLink } from "react-router-dom";
import { useAuth } from "./AuthProvider";

export default function Layout({ children }: { children: React.ReactNode }) {
  const { setToken } = useAuth();

  return (
    <div className="min-h-screen px-6 py-8 md:px-12">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-[0.3em] text-moss">PLS Platform</p>
          <h1 className="font-display text-3xl font-semibold">Plain Language Summaries</h1>
        </div>
        <nav className="flex gap-4 text-sm font-semibold">
          <NavLink className="hover:text-moss" to="/app">Home</NavLink>
          <NavLink className="hover:text-moss" to="/app/dashboard">Dashboard</NavLink>
          <NavLink className="hover:text-moss" to="/app/settings">Settings</NavLink>
          <button className="text-ember" onClick={() => setToken(null)}>Log out</button>
        </nav>
      </header>
      <main className="mt-8 fade-in">{children}</main>
    </div>
  );
}
