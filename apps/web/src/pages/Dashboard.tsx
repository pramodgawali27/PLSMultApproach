import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { fetchSummaries, JobStatus, SummaryLane } from "../api";
import StatusBadge from "../components/StatusBadge";
import { Link } from "react-router-dom";

export default function Dashboard() {
  const [search, setSearch] = useState("");
  const [lane, setLane] = useState<SummaryLane | "">("");
  const [status, setStatus] = useState<JobStatus | "">("");

  const { data, isLoading, error } = useQuery({
    queryKey: ["summaries", search, lane, status],
    queryFn: () => fetchSummaries({ search, lane: lane || undefined, status: status || undefined })
  });

  const summaries = useMemo(() => (Array.isArray(data) ? data : data?.items ?? []), [data]);

  const stats = useMemo(() => ({
    total: summaries.length,
    completed: summaries.filter((s: any) => s.status === "Completed").length,
    processing: summaries.filter((s: any) => s.status === "Processing").length,
    failed: summaries.filter((s: any) => s.status === "Failed").length
  }), [summaries]);

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-violet">Summary Management</p>
          <h2 className="font-display text-2xl font-bold text-ink">Dashboard</h2>
        </div>
        <Link
          to="/app"
          className="rounded-full bg-ink px-5 py-2.5 text-sm font-semibold text-fog transition-colors hover:bg-ink/80"
        >
          + New Summary
        </Link>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        <div className="stat-card">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Total</p>
          <p className="mt-1 font-display text-3xl font-bold text-ink">{stats.total}</p>
          <p className="mt-0.5 text-xs text-slate-400">All summaries</p>
        </div>
        <div className="stat-card">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Completed</p>
          <p className="mt-1 font-display text-3xl font-bold text-moss">{stats.completed}</p>
          <p className="mt-0.5 text-xs text-slate-400">Ready to review</p>
        </div>
        <div className="stat-card">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Processing</p>
          <p className="mt-1 font-display text-3xl font-bold text-amber-500">{stats.processing}</p>
          <p className="mt-0.5 text-xs text-slate-400">In pipeline</p>
        </div>
        <div className="stat-card">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Failed</p>
          <p className="mt-1 font-display text-3xl font-bold text-ember">{stats.failed}</p>
          <p className="mt-0.5 text-xs text-slate-400">Need attention</p>
        </div>
      </div>

      {/* Filters + table */}
      <div className="glass-panel rounded-3xl p-6">
        <div className="flex flex-wrap gap-3">
          <input
            className="min-w-[200px] flex-1 rounded-xl border border-clay bg-white/70 px-3 py-2 text-sm outline-none focus:border-violet/50 focus:ring-2 focus:ring-violet/10"
            placeholder="Search by title or filename…"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          <select
            className="rounded-xl border border-clay bg-white/70 px-3 py-2 text-sm outline-none focus:border-violet/50"
            value={lane}
            onChange={(event) => setLane(event.target.value as SummaryLane | "")}
          >
            <option value="">All lanes</option>
            <option value="LaneA">Lane A — Prompt</option>
            <option value="LaneB">Lane B — Fine-tuned</option>
            <option value="LaneC">Lane C — RAG + Citations</option>
            <option value="LaneD">Lane D — Agentic</option>
          </select>
          <select
            className="rounded-xl border border-clay bg-white/70 px-3 py-2 text-sm outline-none focus:border-violet/50"
            value={status}
            onChange={(event) => setStatus(event.target.value as JobStatus | "")}
          >
            <option value="">All status</option>
            <option value="Queued">Queued</option>
            <option value="Processing">Processing</option>
            <option value="Completed">Completed</option>
            <option value="Failed">Failed</option>
          </select>
        </div>

        {isLoading && (
          <div className="mt-8 flex justify-center">
            <p className="text-sm text-slate-400">Loading summaries…</p>
          </div>
        )}
        {error && <p className="mt-6 text-sm text-ember">Unable to load summaries.</p>}
        {!isLoading && summaries.length === 0 && (
          <div className="mt-6 rounded-2xl border border-dashed border-sand p-10 text-center">
            <p className="text-sm font-semibold text-slate-500">No summaries found</p>
            <p className="mt-1 text-xs text-slate-400">
              Upload a document on the New Summary page to generate your first PLS.
            </p>
            <Link
              to="/app"
              className="mt-4 inline-block rounded-full bg-violet px-5 py-2 text-xs font-semibold text-white"
            >
              Get started
            </Link>
          </div>
        )}

        {summaries.length > 0 && (
          <div className="mt-5 overflow-x-auto rounded-2xl border border-sand">
            <table className="min-w-full text-left text-sm">
              <thead className="border-b border-sand bg-fog/60">
                <tr>
                  <th className="px-5 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Document</th>
                  <th className="px-5 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Lane</th>
                  <th className="px-5 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Status</th>
                  <th className="px-5 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Generated</th>
                  <th className="px-5 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-sand/60 bg-white">
                {summaries.map((summary: any) => {
                  const summaryId = summary.id ?? summary.summaryId ?? summary.summary_id;
                  return (
                    <tr
                      key={summaryId ?? summary.documentId}
                      className="transition-colors hover:bg-fog/40"
                    >
                      <td className="px-5 py-3.5">
                        <p className="font-semibold text-ink">
                          {summary.fileName ?? summary.documentName ?? summary.title ?? "Untitled"}
                        </p>
                        {summary.therapeuticArea && (
                          <p className="mt-0.5 text-xs text-slate-400">{summary.therapeuticArea}</p>
                        )}
                      </td>
                      <td className="px-5 py-3.5">
                        <span className="rounded-full bg-violet/10 px-2.5 py-1 text-xs font-semibold text-violet">
                          {summary.lane}
                        </span>
                      </td>
                      <td className="px-5 py-3.5">
                        <StatusBadge status={summary.status ?? "Queued"} />
                      </td>
                      <td className="px-5 py-3.5 text-xs text-slate-400">
                        {summary.createdAt
                          ? new Date(summary.createdAt).toLocaleDateString(undefined, {
                              year: "numeric",
                              month: "short",
                              day: "numeric"
                            })
                          : "—"}
                      </td>
                      <td className="px-5 py-3.5">
                        <div className="flex items-center gap-3">
                          <Link
                            className="text-xs font-semibold text-violet hover:underline"
                            to={summaryId ? `/app/summaries/${summaryId}` : "#"}
                          >
                            View
                          </Link>
                          <span className="text-slate-300">|</span>
                          <button
                            className="text-xs font-semibold text-slate-500 hover:text-ink"
                            type="button"
                          >
                            Download
                          </button>
                          <span className="text-slate-300">|</span>
                          <button
                            className="text-xs font-semibold text-slate-500 hover:text-ink"
                            type="button"
                          >
                            Re-run
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
