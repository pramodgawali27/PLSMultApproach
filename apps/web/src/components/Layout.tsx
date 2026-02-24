import { NavLink } from "react-router-dom";
import { useAuth } from "./AuthProvider";

function IconHome() {
  return (
    <svg className="h-4 w-4 flex-shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
      <polyline points="9 22 9 12 15 12 15 22" />
    </svg>
  );
}

function IconGrid() {
  return (
    <svg className="h-4 w-4 flex-shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="7" height="7" />
      <rect x="14" y="3" width="7" height="7" />
      <rect x="14" y="14" width="7" height="7" />
      <rect x="3" y="14" width="7" height="7" />
    </svg>
  );
}

function IconSettings() {
  return (
    <svg className="h-4 w-4 flex-shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  );
}

function IconLogout() {
  return (
    <svg className="h-4 w-4 flex-shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <polyline points="16 17 21 12 16 7" />
      <line x1="21" y1="12" x2="9" y2="12" />
    </svg>
  );
}

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors ${
    isActive
      ? "bg-violet/10 text-violet"
      : "text-slate-600 hover:bg-fog hover:text-ink"
  }`;

export default function Layout({ children }: { children: React.ReactNode }) {
  const { setToken } = useAuth();

  return (
    <div className="flex min-h-screen bg-fog">
      {/* ── Sidebar ── */}
      <aside className="fixed inset-y-0 left-0 z-20 flex w-60 flex-col border-r border-clay/50 bg-white">
        {/* Brand */}
        <div className="border-b border-clay/50 px-5 py-4">
          <p className="text-[10px] font-bold uppercase tracking-[0.3em] text-violet">PLS Platform</p>
          <h1 className="mt-1 font-display text-sm font-bold leading-tight text-ink">
            Plain Language<br />Summaries
          </h1>
          <p className="mt-0.5 text-[10px] text-slate-400">Regulated document summarization</p>
        </div>

        {/* Navigation links */}
        <nav className="flex flex-1 flex-col gap-1 px-3 py-4">
          <p className="mb-1 px-3 text-[10px] font-bold uppercase tracking-[0.2em] text-slate-400">Workspace</p>
          <NavLink end to="/app" className={navLinkClass}>
            <IconHome />
            New Summary
          </NavLink>
          <NavLink to="/app/dashboard" className={navLinkClass}>
            <IconGrid />
            Dashboard
          </NavLink>

          <p className="mb-1 mt-4 px-3 text-[10px] font-bold uppercase tracking-[0.2em] text-slate-400">Account</p>
          <NavLink to="/app/settings" className={navLinkClass}>
            <IconSettings />
            Settings
          </NavLink>
        </nav>

        {/* Logout */}
        <div className="border-t border-clay/50 px-3 py-4">
          <button
            className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-rose-50 hover:text-ember"
            onClick={() => setToken(null)}
          >
            <IconLogout />
            Log out
          </button>
        </div>
      </aside>

      {/* ── Main content ── */}
      <main className="ml-60 min-h-screen flex-1 px-8 py-8">
        <div className="fade-in">{children}</div>
      </main>
    </div>
  );
}
