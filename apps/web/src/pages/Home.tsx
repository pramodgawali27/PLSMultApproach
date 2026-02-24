import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  fetchEntitlements,
  fetchJobStage,
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

  // Poll real Lane D stage every 2s while processing
  const { data: stageData } = useQuery({
    queryKey: ["job-stage", jobId],
    queryFn: () => fetchJobStage(jobId!),
    enabled: Boolean(jobId) && selectedLane === "LaneD" && jobStatus === "Processing",
    refetchInterval: 2000
  });

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
      description: "Prompt-based generation — fast turnaround for clinical documents",
      bestFor: "Early-stage drafts & quick reviews",
      badges: ["Fast", "Low cost"]
    },
    {
      lane: "LaneB" as SummaryLane,
      title: "Lane B",
      description: "Fine-tuned model for consistent patient-accessible language",
      bestFor: "Repeatable patient communications",
      badges: ["Consistent", "Patient-friendly"]
    },
    {
      lane: "LaneC" as SummaryLane,
      title: "Lane C",
      description: "RAG-grounded summary with traceable evidence citations",
      bestFor: "Regulatory submissions & evidence review",
      badges: ["Citations", "Evidence", "RAG"]
    },
    {
      lane: "LaneD" as SummaryLane,
      title: "Lane D",
      description: "Multi-agent pipeline with claim verification and PLSP-compliant visuals",
      bestFor: "Taylor & Francis PLSP format, complex trials",
      badges: ["Agentic", "Verified", "Diagrams"]
    },
    {
      lane: "LaneE" as SummaryLane,
      title: "Lane E",
      description: "Enterprise batch processing for portfolio-scale submissions",
      bestFor: "High-volume regulatory pipelines",
      badges: ["Batch", "Automation", "Enterprise"]
    }
  ];

  const selectedLaneMeta = laneCards.find((card) => card.lane === selectedLane);
  const lanePricing: Record<SummaryLane, string> = {
    LaneA: "Low cost · Fastest turnaround · No retrieval",
    LaneB: "Medium cost · Consistent tone · Template-driven",
    LaneC: "Higher cost · Evidence-grounded · Citation trail",
    LaneD: "Premium · 10-stage agentic pipeline · PLSP-compliant",
    LaneE: "Enterprise · Parallel batch · Portfolio-scale"
  };
  const laneTips: Record<SummaryLane, string[]> = {
    LaneA: [
      "Ideal for documents under 10 pages or when speed matters.",
      "Use prompt override to apply your organisation's plain-language style guide.",
      "Results are generated in seconds — best for iterating on tone and structure."
    ],
    LaneB: [
      "Uses a model fine-tuned on life sciences plain-language conventions.",
      "Delivers repeatable output suitable for patient-facing communications.",
      "Consistent section structure across large document portfolios."
    ],
    LaneC: [
      "Retrieves context from the source document using semantic search.",
      "Every claim in the summary is anchored to a source chunk with citation.",
      "Ideal for long clinical trial reports and documents with complex evidence structures."
    ],
    LaneD: [
      "Runs 10 agentic stages: planner → facts → evidence → writer → review → finalize → visuals.",
      "Every numeric claim is cross-checked against retrieved evidence — no hallucinated numbers.",
      "Generates PLSP-compliant SVG diagrams and bar charts from structured facts.",
      "Outputs follow Taylor & Francis Plain Language Summary Publication (PLSP) format.",
      "Expect 3–8 minutes processing time for typical clinical trial documents."
    ],
    LaneE: [
      "Designed for automated, high-volume document processing pipelines.",
      "Process entire study portfolios in parallel across tenant accounts.",
      "Requires enterprise entitlement — contact your account manager."
    ]
  };

  // Lane D agentic stages (D0–D9) with estimated durations for animated progress
  const laneDStages = [
    { key: "D0", label: "D0 · Canonicalize & index", estimatedSec: 20 },
    { key: "D1", label: "D1 · Planner — outline & questions", estimatedSec: 25 },
    { key: "D2", label: "D2 · Facts — structured extraction", estimatedSec: 40 },
    { key: "D3", label: "D3 · Evidence — claim mapping", estimatedSec: 30 },
    { key: "D4", label: "D4 · Writer — plain-language draft", estimatedSec: 50 },
    { key: "D5", label: "D5 · Review — numeric verification", estimatedSec: 30 },
    { key: "D6", label: "D6 · Finalize — apply corrections", estimatedSec: 35 },
    { key: "D7", label: "D7 · Visual planner — spec generation", estimatedSec: 25 },
    { key: "D8", label: "D8 · Render — SVG & charts", estimatedSec: 10 },
    { key: "D9", label: "D9 · Images — concept illustrations", estimatedSec: 20 }
  ];

  // Derive current Lane D phase from real stage data; fall back to time-estimation if not yet available
  const laneDPhase = useMemo(() => {
    if (selectedLane !== "LaneD" || jobStatus !== "Processing") return -1;
    if (stageData?.stageId) {
      const idx = laneDStages.findIndex((s) => stageData.stageId.startsWith(s.key));
      return idx >= 0 ? idx : 0;
    }
    // Fallback: estimate from elapsed time until first stage report arrives
    if (!jobStartedAt) return 0;
    const elapsedSec = (Date.now() - jobStartedAt) / 1000;
    let cumulative = 0;
    for (let i = 0; i < laneDStages.length; i++) {
      cumulative += laneDStages[i].estimatedSec;
      if (elapsedSec < cumulative) return i;
    }
    return laneDStages.length - 1;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedLane, jobStatus, stageData, jobStartedAt, jobElapsedMinutes]);

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
      <section className="glass-panel rounded-3xl p-6">
        <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-violet">Document Processing</p>
        <h2 className="mt-1 font-display text-2xl font-bold text-ink">Upload & generate</h2>
        <p className="mt-1 text-sm text-slate-500">
          Upload a clinical or regulatory document and generate a plain-language summary.
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
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Summarization Lane</p>
          <p className="mt-0.5 text-xs text-slate-400">Choose the pipeline that matches your document and regulatory requirements.</p>
          {loadingEntitlements && <p className="mt-2 text-xs text-slate-400">Loading entitlements…</p>}
          {!loadingEntitlements && (entitlements?.enabledLanes?.length ?? 0) === 0 && (
            <p className="mt-2 text-sm text-ember">Unable to load entitlements.</p>
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
            className="rounded-full bg-ink px-6 py-3 text-sm font-semibold text-fog transition-colors hover:bg-ink/80 disabled:cursor-not-allowed disabled:opacity-50"
            onClick={handleUpload}
            disabled={!file || isSubmitting}
          >
            {isSubmitting ? "Starting…" : "Generate summary"}
          </button>
          <button className="rounded-full border border-sand px-6 py-3 text-sm font-semibold text-slate-600 transition-colors hover:bg-fog" type="button">
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
            <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-violet">Pipeline</p>
            <h2 className="mt-1 font-display text-2xl font-bold text-ink">Job status</h2>
            <p className="mt-1 text-sm text-slate-500">Live updates for your latest processing job.</p>

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

              {/* Lane D: show agentic stage pipeline */}
              {selectedLane === "LaneD" && (jobStatus === "Processing" || jobStatus === "Completed") ? (
                <div className="mt-3 space-y-1.5">
                  {laneDStages.map((stage, idx) => {
                    const isDone = jobStatus === "Completed" || idx < laneDPhase;
                    const isActive = jobStatus === "Processing" && idx === laneDPhase;
                    return (
                      <div key={stage.key} className="flex items-center gap-3">
                        <span
                          className={`h-2 w-2 flex-shrink-0 rounded-full transition-colors ${
                            isDone
                              ? "bg-moss"
                              : isActive
                                ? "animate-pulse bg-ember"
                                : "bg-slate-300"
                          }`}
                        />
                        <span
                          className={`text-xs ${
                            isDone
                              ? "text-slate-500 line-through"
                              : isActive
                                ? "font-semibold text-slate-800"
                                : "text-slate-400"
                          }`}
                        >
                          {stage.label}
                        </span>
                      </div>
                    );
                  })}
                  {jobStatus === "Processing" && (
                    <div className="mt-2 space-y-1">
                      {stageData?.message && (
                        <p className="flex items-center gap-1.5 text-xs font-medium text-slate-700">
                          <span className="inline-block h-1.5 w-1.5 animate-pulse rounded-full bg-amber-500" />
                          {stageData.message}
                        </p>
                      )}
                      <p className="text-xs text-slate-400">
                        Multi-agent pipeline running — Lane D typically takes 3–8 minutes.
                      </p>
                    </div>
                  )}
                </div>
              ) : (
                /* Lanes A / B / C / E: generic 4-step progress */
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
              )}

              {jobElapsedMinutes > 2 && selectedLane !== "LaneD" && (jobStatus === "Queued" || jobStatus === "Processing") && (
                <p className="mt-3 text-xs text-ember">
                  This step is taking longer than usual. Check service logs for stuck jobs.
                </p>
              )}
              {jobElapsedMinutes > 10 && selectedLane === "LaneD" && jobStatus === "Processing" && (
                <p className="mt-3 text-xs text-ember">
                  Lane D is taking longer than usual. Check service logs for timeouts.
                </p>
              )}
            </div>

            {jobData?.summaryId && jobData.status === "Completed" && (
              <div className="mt-6 rounded-2xl border border-teal/30 bg-teal/5 p-4">
                <div className="flex items-center gap-2">
                  <span className="h-2 w-2 rounded-full bg-teal" />
                  <p className="text-[10px] font-semibold uppercase tracking-wide text-teal">Summary ready</p>
                </div>
                <h3 className="mt-2 font-display text-base font-bold text-ink">Plain Language Summary</h3>
                <p className="mt-0.5 text-xs text-slate-500">Your document has been successfully summarized.</p>
                <div className="mt-4 flex flex-wrap gap-3">
                  <a
                    className="rounded-full bg-moss px-4 py-2 text-xs font-semibold text-white transition-colors hover:bg-moss/90"
                    href={`/app/summaries/${jobData.summaryId}`}
                  >
                    View Summary
                  </a>
                  <button
                    className="rounded-full border border-sand px-4 py-2 text-xs font-semibold text-slate-600 transition-colors hover:bg-fog"
                    type="button"
                    onClick={() => setJobId(null)}
                  >
                    New document
                  </button>
                </div>
              </div>
            )}
          </>
        ) : (
          <>
            <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-violet">Lane Guide</p>
            <h2 className="mt-1 font-display text-2xl font-bold text-ink">Lane details</h2>
            <p className="mt-1 text-sm text-slate-500">
              Review capabilities and guidance for the selected lane.
            </p>

            <div className="mt-6 rounded-2xl border border-sand bg-white p-4">
              <div className="flex items-center justify-between">
                <span className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Selected</span>
                <span className="rounded-full bg-violet/10 px-2.5 py-1 text-xs font-semibold text-violet">
                  {selectedLaneMeta?.title}
                </span>
              </div>
              <p className="mt-2 text-sm font-semibold text-ink">{selectedLaneMeta?.description}</p>
              <p className="mt-1 text-xs text-slate-400">{lanePricing[selectedLane]}</p>
              <div className="mt-3 flex flex-wrap gap-1.5">
                {selectedLaneMeta?.badges.map((badge) => (
                  <span key={badge} className="rounded-full bg-violet/10 px-2.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-violet">
                    {badge}
                  </span>
                ))}
              </div>
            </div>

            <div className="mt-4 rounded-2xl border border-sand bg-white p-4">
              <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Guidance</p>
              <ul className="mt-2 space-y-2">
                {laneTips[selectedLane].map((tip) => (
                  <li key={tip} className="flex gap-2 text-xs text-slate-600">
                    <span className="mt-1 h-1.5 w-1.5 flex-shrink-0 rounded-full bg-violet/40" />
                    {tip}
                  </li>
                ))}
              </ul>
            </div>
          </>
        )}
      </section>
    </div>
  );
}
