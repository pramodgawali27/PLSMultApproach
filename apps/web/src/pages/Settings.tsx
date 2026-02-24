import { useQuery } from "@tanstack/react-query";
import { fetchEntitlements } from "../api";

export default function Settings() {
  const { data, isLoading, error } = useQuery({
    queryKey: ["entitlements"],
    queryFn: fetchEntitlements
  });

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div>
        <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-violet">Account</p>
        <h2 className="font-display text-2xl font-bold text-ink">Settings</h2>
      </div>

      <div className="glass-panel rounded-3xl p-6">
        <h3 className="font-display text-base font-bold text-ink">Tenant plan & usage</h3>
        <p className="mt-1 text-xs text-slate-400">Read-only view of your subscription entitlements.</p>

        {isLoading && <p className="mt-6 text-sm text-slate-400">Loading settings…</p>}
        {error && <p className="mt-6 text-sm text-ember">Unable to load settings.</p>}

        {data && (
          <div className="mt-6 grid gap-4 md:grid-cols-2">
            <div className="rounded-2xl border border-sand bg-white p-5">
              <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Plan</p>
              <p className="mt-2 font-display text-xl font-bold text-ink">Starter</p>
              <p className="mt-0.5 text-xs text-slate-400">Standard regulatory PLS plan</p>
            </div>

            <div className="rounded-2xl border border-sand bg-white p-5">
              <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Enabled lanes</p>
              <div className="mt-3 flex flex-wrap gap-2">
                {data.enabledLanes.map((lane) => (
                  <span key={lane} className="rounded-full bg-violet/10 px-2.5 py-1 text-xs font-semibold text-violet">
                    {lane}
                  </span>
                ))}
              </div>
            </div>

            <div className="rounded-2xl border border-sand bg-white p-5">
              <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Monthly quota</p>
              <p className="mt-2 font-display text-xl font-bold text-ink">10,000</p>
              <p className="mt-0.5 text-xs text-slate-400">tokens / month</p>
            </div>

            <div className="rounded-2xl border border-sand bg-white p-5">
              <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Usage this month</p>
              <p className="mt-2 font-display text-xl font-bold text-slate-400">—</p>
              <p className="mt-0.5 text-xs text-slate-400">Usage tracking coming soon</p>
            </div>
          </div>
        )}

        <button
          className="mt-6 rounded-full border border-sand px-4 py-2 text-xs font-semibold text-slate-400"
          disabled
        >
          Manage subscription
        </button>
      </div>
    </div>
  );
}
