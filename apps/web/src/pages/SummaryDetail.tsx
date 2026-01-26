import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { downloadSummary, fetchSummary } from "../api";

export default function SummaryDetail() {
  const { summaryId } = useParams();
  const [summary, setSummary] = useState<any | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  useEffect(() => {
    if (!summaryId) return;
    fetchSummary(summaryId)
      .then(setSummary)
      .catch(() => setError("Unable to load summary."));
  }, [summaryId]);

  if (error) {
    return <p className="text-ember">{error}</p>;
  }

  if (!summary) {
    return <p>Loading...</p>;
  }

  return (
    <section className="glass-panel rounded-3xl p-6 md:p-8">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="font-display text-2xl font-semibold">Summary detail</h2>
          <p className="text-sm text-moss">Lane {summary.lane}</p>
        </div>
        <span className="badge bg-clay text-ink">{new Date(summary.createdAt).toLocaleString()}</span>
      </div>

      <div className="mt-4 flex flex-wrap gap-3">
        <button
          className="rounded-full border border-moss px-4 py-2 text-sm font-semibold text-moss"
          onClick={() => {
            if (!summaryId) return;
            setDownloadError(null);
            downloadSummary(summaryId, "pdf").catch(() => setDownloadError("Failed to download PDF."));
          }}
        >
          Download PDF
        </button>
        <button
          className="rounded-full border border-moss px-4 py-2 text-sm font-semibold text-moss"
          onClick={() => {
            if (!summaryId) return;
            setDownloadError(null);
            downloadSummary(summaryId, "docx").catch(() => setDownloadError("Failed to download DOCX."));
          }}
        >
          Download DOCX
        </button>
        {downloadError && <p className="text-sm text-ember">{downloadError}</p>}
      </div>

      <article className="mt-6 whitespace-pre-line rounded-2xl border border-clay/70 bg-white/70 p-6 text-base">
        {summary.summaryText}
      </article>

      <div className="mt-6 text-sm text-moss">
        <p>Model info: {summary.modelInfoJson ?? "--"}</p>
      </div>
    </section>
  );
}
