import { useEffect, useState } from "react";
import { fetchSummaries } from "../api";
import { Link } from "react-router-dom";

const laneOptions = ["LaneA", "LaneB", "LaneC", "LaneD"] as const;
const statusOptions = ["Queued", "Processing", "Completed", "Failed"] as const;

export default function Dashboard() {
  const [summaries, setSummaries] = useState<any[]>([]);
  const [search, setSearch] = useState("");
  const [lane, setLane] = useState("");
  const [status, setStatus] = useState("");
  const [error, setError] = useState<string | null>(null);

  const loadSummaries = async () => {
    try {
      const data = await fetchSummaries({
        search: search || undefined,
        lane: lane ? (lane as any) : undefined,
        status: status ? (status as any) : undefined
      });
      setSummaries(data);
    } catch {
      setError("Unable to load summaries.");
    }
  };

  useEffect(() => {
    loadSummaries();
  }, []);

  return (
    <section className="glass-panel rounded-3xl p-6 md:p-8">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="font-display text-2xl font-semibold">Summary dashboard</h2>
          <p className="text-sm text-moss">Search by filename, lane, or status.</p>
        </div>
        <button
          className="rounded-full border border-ink px-4 py-2 text-sm font-semibold"
          onClick={loadSummaries}
        >
          Refresh
        </button>
      </div>

      <div className="mt-6 grid gap-3 md:grid-cols-[2fr_1fr_1fr_auto]">
        <input
          className="rounded-xl border border-clay bg-white/70 px-4 py-2"
          placeholder="Search filename"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <select
          className="rounded-xl border border-clay bg-white/70 px-4 py-2"
          value={lane}
          onChange={(event) => setLane(event.target.value)}
        >
          <option value="">All lanes</option>
          {laneOptions.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
        <select
          className="rounded-xl border border-clay bg-white/70 px-4 py-2"
          value={status}
          onChange={(event) => setStatus(event.target.value)}
        >
          <option value="">All statuses</option>
          {statusOptions.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
        <button
          className="rounded-full bg-ink px-4 py-2 text-sm font-semibold text-fog"
          onClick={loadSummaries}
        >
          Apply
        </button>
      </div>

      {error && <p className="mt-4 text-sm text-ember">{error}</p>}

      <div className="mt-6 overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead>
            <tr className="text-left text-xs uppercase tracking-wide text-moss">
              <th className="py-2">File</th>
              <th className="py-2">Lane</th>
              <th className="py-2">Status</th>
              <th className="py-2">Created</th>
              <th className="py-2"></th>
            </tr>
          </thead>
          <tbody>
            {summaries.length === 0 && (
              <tr>
                <td className="py-4" colSpan={5}>
                  No summaries yet.
                </td>
              </tr>
            )}
            {summaries.map((summary) => (
              <tr key={summary.summaryId} className="border-t border-clay/50">
                <td className="py-3">{summary.fileName}</td>
                <td className="py-3">{summary.lane}</td>
                <td className="py-3">
                  <span className="badge bg-clay text-ink">{summary.status}</span>
                </td>
                <td className="py-3">{new Date(summary.createdAt).toLocaleString()}</td>
                <td className="py-3 text-right">
                  <Link className="text-moss underline" to={`/summaries/${summary.summaryId}`}>
                    View
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
