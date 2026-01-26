import { useEffect, useMemo, useState } from "react";
import { fetchEntitlements, fetchJob, fetchPromptTemplate, uploadDocument, type SummaryLane } from "../api";

const laneLabels: Record<string, string> = {
  LaneA: "Lane A — Prompt",
  LaneB: "Lane B — Fine-tuned",
  LaneC: "Lane C — RAG",
  LaneD: "Lane D — Agentic"
};

export default function Home() {
  const [file, setFile] = useState<File | null>(null);
  const [lanes, setLanes] = useState<SummaryLane[]>([]);
  const [selectedLane, setSelectedLane] = useState<SummaryLane>("LaneA");
  const [jobId, setJobId] = useState<string | null>(null);
  const [jobStatus, setJobStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [showPromptEditor, setShowPromptEditor] = useState(false);
  const [promptOverride, setPromptOverride] = useState("");
  const [promptLoading, setPromptLoading] = useState(false);

  useEffect(() => {
    fetchEntitlements()
      .then((enabled) => {
        setLanes(enabled);
        if (enabled.length > 0) {
          setSelectedLane(enabled[0]);
        }
      })
      .catch(() => setError("Unable to load entitlements."));
  }, []);

  useEffect(() => {
    if (!jobId) return;
    let active = true;
    const interval = setInterval(async () => {
      try {
        const data = await fetchJob(jobId);
        if (!active) return;
        setJobStatus(data.status);
        if (data.status === "Completed" || data.status === "Failed") {
          clearInterval(interval);
        }
      } catch {
        if (active) {
          setError("Failed to fetch job status.");
        }
      }
    }, 2000);

    return () => {
      active = false;
      clearInterval(interval);
    };
  }, [jobId]);

  const laneOptions = useMemo<SummaryLane[]>(() => {
    if (lanes.length === 0) {
      return ["LaneA"];
    }
    return lanes;
  }, [lanes]);

  const handleUpload = async () => {
    if (!file) {
      setError("Select a document to upload.");
      return;
    }
    setError(null);
    setLoading(true);
    try {
      const upload = await uploadDocument(
        file,
        selectedLane,
        showPromptEditor && promptOverride.trim() ? promptOverride : undefined
      );
      setJobId(upload.jobId);
      setJobStatus("Queued");
    } catch {
      setError("Upload or generation failed.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="grid gap-6 lg:grid-cols-[2fr_1fr]">
      <section className="glass-panel rounded-3xl p-6 md:p-8">
        <h2 className="font-display text-2xl font-semibold">Generate a plain-language summary</h2>
        <p className="mt-2 text-sm text-moss">
          Upload a medical document and pick an enabled lane. Lane A is available in this starter.
        </p>

        <div className="mt-6 grid gap-4">
          <label className="grid gap-2 text-sm font-semibold">
            Document
            <input
              className="rounded-xl border border-clay bg-white/70 px-4 py-2"
              type="file"
              accept=".pdf,.docx,.txt"
              onChange={(event) => setFile(event.target.files?.[0] ?? null)}
            />
          </label>

          <label className="grid gap-2 text-sm font-semibold">
            Lane
            <select
              className="rounded-xl border border-clay bg-white/70 px-4 py-2"
              value={selectedLane}
              onChange={(event) => {
                setSelectedLane(event.target.value as SummaryLane);
                setPromptOverride("");
              }}
            >
              {laneOptions.map((lane) => (
                <option key={lane} value={lane}>
                  {laneLabels[lane] ?? lane}
                </option>
              ))}
            </select>
          </label>

          <label className="flex items-center gap-2 text-sm font-semibold">
            <input
              type="checkbox"
              checked={showPromptEditor}
              onChange={(event) => setShowPromptEditor(event.target.checked)}
            />
            Customize prompt
          </label>

          {showPromptEditor && (
            <div className="grid gap-3">
              <div className="flex flex-wrap gap-2">
                <button
                  className="rounded-full border border-moss px-4 py-2 text-sm font-semibold text-moss"
                  onClick={async () => {
                    setPromptLoading(true);
                    try {
                      const template = await fetchPromptTemplate(selectedLane);
                      setPromptOverride(template);
                    } catch {
                      setError("Unable to load prompt template.");
                    } finally {
                      setPromptLoading(false);
                    }
                  }}
                  disabled={promptLoading}
                >
                  {promptLoading ? "Loading..." : "Load default prompt"}
                </button>
              </div>
              <textarea
                className="min-h-[160px] rounded-xl border border-clay bg-white/70 px-4 py-3 text-sm"
                value={promptOverride}
                onChange={(event) => setPromptOverride(event.target.value)}
                placeholder="Edit the prompt template for this run."
              />
            </div>
          )}

          {error && <p className="text-sm text-ember">{error}</p>}

          <button
            className="rounded-full bg-moss px-6 py-3 font-semibold text-fog"
            onClick={handleUpload}
            disabled={loading}
          >
            {loading ? "Generating..." : "Generate Summary"}
          </button>
        </div>
      </section>

      <aside className="glass-panel rounded-3xl p-6">
        <h3 className="font-display text-xl font-semibold">Job status</h3>
        <p className="mt-2 text-sm text-moss">Watch the job progress across the pipeline.</p>

        <div className="mt-6 grid gap-3">
          <div className="flex items-center justify-between text-sm">
            <span>Current job</span>
            <span className="font-semibold">{jobId ?? "--"}</span>
          </div>
          <div className="flex items-center justify-between text-sm">
            <span>Status</span>
            <span className="badge bg-clay text-ink">{jobStatus ?? "Idle"}</span>
          </div>
        </div>
      </aside>
    </div>
  );
}
