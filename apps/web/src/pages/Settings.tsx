import { useQuery } from "@tanstack/react-query";
import { fetchEntitlements } from "../api";

export default function Settings() {
  const { data, isLoading, error } = useQuery({
    queryKey: ["entitlements"],
    queryFn: fetchEntitlements
  });

  return (
    <div className="glass-panel rounded-3xl p-6">
      <h2 className="font-display text-2xl font-semibold">Tenant plan & usage</h2>
      <p className="mt-2 text-sm text-slate-600">Read-only view of your subscription.</p>

      {isLoading && <p className="mt-6 text-sm text-slate-500">Loading settings...</p>}
      {error && <p className="mt-6 text-sm text-ember">Unable to load settings.</p>}

      {data && (
        <div className="mt-6 grid gap-4 md:grid-cols-2">
          <div className="rounded-2xl bg-white/70 p-4">
            <p className="text-xs uppercase text-slate-500">Plan</p>
            <p className="mt-2 text-lg font-semibold">Starter</p>
          </div>
          <div className="rounded-2xl bg-white/70 p-4">
            <p className="text-xs uppercase text-slate-500">Allowed lanes</p>
            <div className="mt-2 flex flex-wrap gap-2">
              {data.enabledLanes.map((lane) => (
                <span key={lane} className="badge bg-slate-100 text-slate-600">
                  {lane}
                </span>
              ))}
            </div>
          </div>
          <div className="rounded-2xl bg-white/70 p-4">
            <p className="text-xs uppercase text-slate-500">Monthly quota</p>
            <p className="mt-2 text-lg font-semibold">10,000 tokens</p>
          </div>
          <div className="rounded-2xl bg-white/70 p-4">
            <p className="text-xs uppercase text-slate-500">Usage this month</p>
            <p className="mt-2 text-lg font-semibold">—</p>
          </div>
        </div>
      )}

      <button className="mt-6 rounded-full border border-sand px-4 py-2 text-xs font-semibold" disabled>
        Manage subscription
      </button>
    </div>
  );
}
