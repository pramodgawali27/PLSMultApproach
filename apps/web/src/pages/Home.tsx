import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  fetchEntitlements,
  fetchPromptTemplate,
  SummaryLane,
  uploadDocument
} from "../api";
import LaneCard from "../components/LaneCard";
import StatusBadge from "../components/StatusBadge";
import UploadDropzone from "../components/UploadDropzone";
import { useJobPolling } from "../hooks/useJobPolling";

export default function Home() {
  const [selectedLane, setSelectedLane] = useState<SummaryLane>("LaneA");
  const [file, setFile] = useState<File | null>(null);
  const [promptOverride, setPromptOverride] = useState<string | null>(null);
  const [jobId, setJobId] = useState<string | null>(null);
  const [title, setTitle] = useState("");
  const [therapeuticArea, setTherapeuticArea] = useState("");
  const [audience, setAudience] = useState("Patients");
  const [includeImages, setIncludeImages] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [pendingMessage, setPendingMessage] = useState<string | null>(null);
  const [jobStartedAt, setJobStartedAt] = useState<number | null>(null);

  const { data: entitlements, isLoading: loadingEntitlements } = useQuery({
    queryKey: ["entitlements"],
    queryFn: fetchEntitlements
  });

  const { data: jobData } = useJobPolling(jobId ?? undefined);
  const jobStatus = jobData?.status ?? "Queued";
  const jobError = jobData?.errorMessage ?? null;
  const jobElapsedMinutes = jobStartedAt ? (Date.now() - jobStartedAt) / 60000 : 0;

  const allowedLanes = useMemo(
    () => new Set(entitlements?.enabledLanes ?? []),
    [entitlements?.enabledLanes]
  );

  const features = entitlements?.features ?? { images: true, citations: true };

  const handleUpload = async () => {
    if (!file) {
      setError("Please select a document.");
      return;
    }

    try {
      setIsSubmitting(true);
      setPendingMessage("Submitting your document. Starting the summary job...");
      setError(null);
      const uploadResult = await uploadDocument(file, selectedLane, promptOverride ?? undefined, {
        title: title || undefined,
        therapeuticArea: therapeuticArea || undefined,
        audience: audience as "Patients" | "General public" | "Healthcare professionals"
      });
      setJobId(uploadResult.jobId);
      setJobStartedAt(Date.now());
      setPendingMessage(null);
    } catch (err) {
      setError("Upload or generation failed.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handlePromptLoad = async () => {
    try {
      const template = await fetchPromptTemplate(selectedLane);
      setPromptOverride(template);
    } catch {
      setPromptOverride("Unable to load prompt template.");
    }
  };

  const laneCards = [
    {
      lane: "LaneA" as SummaryLane,
      title: "Lane A",
      description: "Prompt-based summary (fast, low cost)",
      bestFor: "Quick summaries",
      badges: ["Fast"]
    },
    {
      lane: "LaneB" as SummaryLane,
      title: "Lane B",
      description: "Fine-tuned model (consistent style)",
      bestFor: "Consistent tone",
      badges: ["Consistent"]
    },
    {
      lane: "LaneC" as SummaryLane,
      title: "Lane C",
      description: "RAG grounded summary with citations",
      bestFor: "Evidence-based",
      badges: ["Citations", "Evidence"]
    },
    {
      lane: "LaneD" as SummaryLane,
      title: "Lane D",
      description: "Agentic multi-step orchestration",
      bestFor: "Premium workflows",
      badges: ["Premium", "Diagrams"]
    },
    {
      lane: "LaneE" as SummaryLane,
      title: "Lane E",
      description: "Batch processing for large portfolios",
      bestFor: "High-volume runs",
      badges: ["Batch", "Automation"]
    }
  ];

  const selectedLaneMeta = laneCards.find((card) => card.lane === selectedLane);
  const lanePricing: Record<SummaryLane, string> = {
    LaneA: "Low cost · Fast turnaround",
    LaneB: "Medium cost · Consistent tone",
    LaneC: "Higher cost · Evidence-based",
    LaneD: "Premium · Multi-step + diagrams",
    LaneE: "Enterprise · Batch throughput"
  };
  const laneTips: Record<SummaryLane, string[]> = {
    LaneA: [
      "Best for quick summaries under 10 pages.",
      "Use prompt override for tone control."
    ],
    LaneB: [
      "Great for repeatable template output.",
      "Use for consistent patient-friendly language."
    ],
    LaneC: [
      "Ideal for long documents with citations.",
      "Make sure the source document text is extractable."
    ],
    LaneD: [
      "Use for compliance-heavy workflows.",
      "Expect longer processing times."
    ],
    LaneE: [
      "Best for bulk document sets and automated reviews.",
      "Designed for high-volume tenant pipelines."
    ]
  };

  const stages = [
    {
      key: "upload",
      label: "Upload document",
      state: isSubmitting ? "active" : jobId ? "done" : "pending"
    },
    {
      key: "index",
      label: selectedLane === "LaneC" ? "Index & retrieve context" : "Prepare summary input",
      state: jobId
        ? jobStatus === "Queued"
          ? "active"
          : jobStatus === "Processing" || jobStatus === "Completed"
            ? "done"
            : "pending"
        : "pending"
    },
    {
      key: "summarize",
      label: "Generate summary",
      state: jobStatus === "Processing"
        ? "active"
        : jobStatus === "Completed"
          ? "done"
          : jobStatus === "Failed"
            ? "failed"
            : "pending"
    },
    {
      key: "finalize",
      label: "Finalize & save",
      state: jobStatus === "Completed"
        ? "done"
        : jobStatus === "Failed"
          ? "failed"
          : "pending"
    }
  ];

  return (
    <div className="grid gap-8 lg:grid-cols-[1.1fr_0.9fr]">
      <section className="glass-panel rounded-3xl p-6 shadow-xl">
        <h2 className="font-display text-2xl font-semibold">Upload & generate</h2>
        <p className="mt-2 text-sm text-slate-600">
          Upload a medical document and generate a plain-language summary.
        </p>

        <div className="mt-6">
          <UploadDropzone onFileSelected={setFile} />
          {file && <p className="mt-2 text-xs text-slate-500">Selected: {file.name}</p>}
        </div>

        <div className="mt-6 grid gap-4 md:grid-cols-2">
          <label className="grid gap-1 text-sm font-semibold">
            Title (optional)
            <input
              className="rounded-xl border border-clay bg-white/70 px-3 py-2 text-sm"
              value={title}
              onChange={(event) => setTitle(event.target.value)}
            />
          </label>
          <label className="grid gap-1 text-sm font-semibold">
            Therapeutic area
            <input
              className="rounded-xl border border-clay bg-white/70 px-3 py-2 text-sm"
              value={therapeuticArea}
              onChange={(event) => setTherapeuticArea(event.target.value)}
            />
          </label>
          <label className="grid gap-1 text-sm font-semibold">
            Audience
            <select
              className="rounded-xl border border-clay bg-white/70 px-3 py-2 text-sm"
              value={audience}
              onChange={(event) => setAudience(event.target.value)}
            >
              <option>Patients</option>
              <option>General public</option>
              <option>Healthcare professionals</option>
            </select>
          </label>
        </div>

        <div className="mt-6">
          <label className="text-sm font-semibold">Lane selection</label>
          {loadingEntitlements && <p className="text-sm text-slate-500">Loading entitlements...</p>}
          {!loadingEntitlements && (entitlements?.enabledLanes?.length ?? 0) === 0 && (
            <p className="text-sm text-ember">Unable to load entitlements.</p>
          )}
          <div className="mt-3 grid gap-3 md:grid-cols-2">
            {laneCards.map((card) => (
              <LaneCard
                key={card.lane}
                lane={card.lane}
                title={card.title}
                description={card.description}
                bestFor={card.bestFor}
                badges={card.badges}
                selected={selectedLane === card.lane}
                locked={!allowedLanes.has(card.lane)}
                onSelect={setSelectedLane}
              />
            ))}
          </div>
        </div>

        <div className="mt-6 grid gap-3 md:grid-cols-2">
          <label className="flex items-center justify-between rounded-xl border border-sand bg-white/70 px-3 py-3 text-sm">
            Include diagrams/images
            <input
              type="checkbox"
              checked={includeImages}
              onChange={(event) => setIncludeImages(event.target.checked)}
              disabled={!features.images}
            />
          </label>
        </div>

        <div className="mt-6">
          <label className="text-sm font-semibold">Prompt override</label>
          <div className="mt-2 flex flex-wrap gap-3">
            <button
              className="rounded-full border border-sand px-4 py-2 text-xs font-semibold"
              type="button"
              onClick={handlePromptLoad}
            >
              Load template
            </button>
            <button
              className="rounded-full border border-sand px-4 py-2 text-xs font-semibold"
              type="button"
              onClick={() => setPromptOverride("")}
            >
              Clear override
            </button>
          </div>
          {promptOverride !== null && (
            <textarea
              className="mt-3 w-full rounded-xl border border-sand bg-white/70 p-3 text-xs"
              rows={6}
              value={promptOverride}
              onChange={(event) => setPromptOverride(event.target.value)}
            />
          )}
        </div>

        {error && <p className="mt-4 text-sm text-ember">{error}</p>}

        <div className="mt-6 flex flex-wrap gap-3">
          <button
            className="rounded-full bg-ink px-6 py-3 text-sm font-semibold text-fog disabled:cursor-not-allowed disabled:opacity-60"
            onClick={handleUpload}
            disabled={!file || isSubmitting}
          >
            {isSubmitting ? "Starting..." : "Generate summary"}
          </button>
          <button className="rounded-full border border-sand px-6 py-3 text-sm font-semibold" type="button">
            Save as draft
          </button>
        </div>
        {pendingMessage && (
          <p className="mt-3 text-xs text-slate-500">{pendingMessage}</p>
        )}
      </section>

      <section className="glass-panel rounded-3xl p-6 lg:sticky lg:top-8 self-start">
        {jobId || isSubmitting ? (
          <>
            <h2 className="font-display text-2xl font-semibold">Job status</h2>
            <p className="mt-2 text-sm text-slate-600">Live status updates for your latest job.</p>

            <div className="mt-6 space-y-3 rounded-2xl bg-white/70 p-4 text-sm">
              <div className="flex items-center justify-between">
                <span className="text-xs uppercase tracking-wide text-slate-500">Current job</span>
                <span className="text-xs font-semibold break-all text-right">
                  {jobId ?? "Pending..."}
                </span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-slate-600">Status</span>
                <StatusBadge status={jobId ? jobStatus : "Queued"} />
              </div>
              {pendingMessage && <p className="text-xs text-slate-500">{pendingMessage}</p>}
              {jobError && <p className="text-xs text-ember">Failed. {jobError}</p>}
            </div>

            <div className="mt-4 rounded-2xl border border-sand bg-white/70 p-4 text-sm">
              <p className="text-xs uppercase tracking-wide text-slate-500">Progress</p>
              <div className="mt-3 space-y-2">
                {stages.map((stage) => (
                  <div key={stage.key} className="flex items-center gap-3">
                    <span
                      className={`h-2.5 w-2.5 rounded-full ${
                        stage.state === "done"
                          ? "bg-moss"
                          : stage.state === "active"
                            ? "bg-ember"
                            : stage.state === "failed"
                              ? "bg-ink"
                              : "bg-slate-300"
                      }`}
                    />
                    <span className="text-slate-700">{stage.label}</span>
                  </div>
                ))}
              </div>
              {jobElapsedMinutes > 2 && (jobStatus === "Queued" || jobStatus === "Processing") && (
                <p className="mt-3 text-xs text-ember">
                  This step is taking longer than usual. Check service logs for stuck jobs.
                </p>
              )}
            </div>

            {jobData?.summaryId && jobData.status === "Completed" && (
              <div className="mt-6 rounded-2xl border border-sand bg-white/70 p-4">
                <p className="text-xs uppercase tracking-wide text-slate-500">Summary ready</p>
                <h3 className="mt-2 font-semibold">Summary in plain language</h3>
                <div className="mt-4 flex flex-wrap gap-3">
                  <a
                    className="rounded-full bg-moss px-4 py-2 text-xs font-semibold text-white"
                    href={`/app/summaries/${jobData.summaryId}`}
                  >
                    View Summary
                  </a>
                  <button
                    className="rounded-full border border-sand px-4 py-2 text-xs font-semibold"
                    type="button"
                    onClick={() => setJobId(null)}
                  >
                    Start new
                  </button>
                </div>
              </div>
            )}
          </>
        ) : (
          <>
            <h2 className="font-display text-2xl font-semibold">Lane details</h2>
            <p className="mt-2 text-sm text-slate-600">
              Compare lanes, pricing, and tips before generating.
            </p>

            <div className="mt-6 rounded-2xl bg-white/70 p-4">
              <div className="flex items-center justify-between">
                <span className="text-xs uppercase tracking-wide text-slate-500">Selected lane</span>
                <span className="badge bg-slate-100 text-slate-700">{selectedLaneMeta?.title}</span>
              </div>
              <p className="mt-3 text-sm font-semibold">{selectedLaneMeta?.description}</p>
              <p className="mt-1 text-xs text-slate-500">{lanePricing[selectedLane]}</p>
              <div className="mt-4 flex flex-wrap gap-2">
                {selectedLaneMeta?.badges.map((badge) => (
                  <span key={badge} className="badge bg-slate-100 text-slate-600">
                    {badge}
                  </span>
                ))}
              </div>
            </div>

            <div className="mt-4 rounded-2xl border border-sand bg-white/70 p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500">Tips</p>
              <ul className="mt-2 space-y-2 text-sm text-slate-600">
                {laneTips[selectedLane].map((tip) => (
                  <li key={tip}>• {tip}</li>
                ))}
              </ul>
            </div>
          </>
        )}
      </section>
    </div>
  );
}
