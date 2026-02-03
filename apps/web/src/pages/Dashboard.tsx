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

  return (
    <div className="glass-panel rounded-3xl p-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 className="font-display text-2xl font-semibold">Summary dashboard</h2>
          <p className="text-sm text-slate-600">Search and manage your summaries.</p>
        </div>
        <Link
          to="/app"
          className="rounded-full bg-ink px-4 py-2 text-xs font-semibold text-fog"
        >
          New summary
        </Link>
      </div>

      <div className="mt-6 grid gap-3 md:grid-cols-4">
        <input
          className="rounded-xl border border-clay bg-white/70 px-3 py-2 text-sm"
          placeholder="Search by title or filename"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <select
          className="rounded-xl border border-clay bg-white/70 px-3 py-2 text-sm"
          value={lane}
          onChange={(event) => setLane(event.target.value as SummaryLane | "")}
        >
          <option value="">All lanes</option>
          <option value="LaneA">Lane A</option>
          <option value="LaneB">Lane B</option>
          <option value="LaneC">Lane C</option>
          <option value="LaneD">Lane D</option>
        </select>
        <select
          className="rounded-xl border border-clay bg-white/70 px-3 py-2 text-sm"
          value={status}
          onChange={(event) => setStatus(event.target.value as JobStatus | "")}
        >
          <option value="">All status</option>
          <option value="Queued">Queued</option>
          <option value="Processing">Processing</option>
          <option value="Completed">Completed</option>
          <option value="Failed">Failed</option>
        </select>
        <button className="rounded-xl border border-sand px-3 py-2 text-sm font-semibold">Export</button>
      </div>

      {isLoading && <p className="mt-6 text-sm text-slate-500">Loading summaries...</p>}
      {error && <p className="mt-6 text-sm text-ember">Unable to load summaries.</p>}
      {!isLoading && summaries.length === 0 && (
        <div className="mt-6 rounded-2xl border border-dashed border-sand p-6 text-center text-sm text-slate-500">
          No summaries yet. Upload a document to get started.
        </div>
      )}

      {summaries.length > 0 && (
        <div className="mt-6 overflow-x-auto rounded-2xl border border-sand bg-white/70">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-slate-100 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Document</th>
                <th className="px-4 py-3">Lane</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Created</th>
                <th className="px-4 py-3">Actions</th>
              </tr>
            </thead>
            <tbody>
              {summaries.map((summary: any) => {
                const summaryId = summary.id ?? summary.summaryId ?? summary.summary_id;
                return (
                <tr key={summaryId ?? summary.documentId} className="border-t border-sand">
                  <td className="px-4 py-3 font-semibold">{summary.fileName ?? summary.documentName ?? summary.title ?? "Untitled"}</td>
                  <td className="px-4 py-3">{summary.lane}</td>
                  <td className="px-4 py-3">
                    <StatusBadge status={summary.status ?? "Queued"} />
                  </td>
                  <td className="px-4 py-3 text-xs text-slate-500">{summary.createdAt ?? "-"}</td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-2">
                      <Link
                        className="text-xs font-semibold text-moss"
                        to={summaryId ? `/app/summaries/${summaryId}` : "#"}
                      >
                        View
                      </Link>
                      <span className="text-xs text-slate-400">|</span>
                      <button className="text-xs font-semibold text-slate-600" type="button">
                        Download
                      </button>
                      <span className="text-xs text-slate-400">|</span>
                      <button className="text-xs font-semibold text-slate-600" type="button">
                        Re-run
                      </button>
                    </div>
                  </td>
                </tr>
              );})}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
