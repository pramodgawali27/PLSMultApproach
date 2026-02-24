import { useEffect, useState } from "react";
import { useLocation, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { downloadSummary, fetchSummary } from "../api";
import StatusBadge from "../components/StatusBadge";
import SummaryRenderer from "../components/SummaryRenderer";
import EvidencePanel from "../components/EvidencePanel";

const tabs = ["Summary", "Evidence", "Metadata", "Audit"] as const;

export default function SummaryDetail() {
  const { summaryId } = useParams();
  const location = useLocation();
  const [activeTab, setActiveTab] = useState<(typeof tabs)[number]>("Summary");

  const { data, isLoading, error } = useQuery({
    queryKey: ["summary", summaryId],
    queryFn: () => fetchSummary(summaryId!)
  });

  useEffect(() => {
    if (location.hash.startsWith("#evidence-")) {
      setActiveTab("Evidence");
    }
  }, [location.hash]);

  if (isLoading) {
    return (
      <div className="flex h-48 items-center justify-center">
        <p className="text-sm text-slate-400">Loading summary…</p>
      </div>
    );
  }

  if (error || !data) {
    return <p className="text-sm text-ember">Unable to load summary.</p>;
  }

  const evidenceSections = data.citations ?? [];
  const modelInfo = data.modelInfoJson ?? {};
  const guardrails = modelInfo.guardrails ?? {};

  return (
    <div className="space-y-6">
      {/* ── Header card ── */}
      <div className="glass-panel rounded-3xl p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex-1 min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-violet">Summary Detail</p>
              <StatusBadge status={data.status ?? "Completed"} />
              <span className="rounded-full bg-violet/10 px-2.5 py-1 text-xs font-semibold text-violet">
                {data.lane}
              </span>
            </div>
            <h2 className="mt-2 font-display text-2xl font-bold text-ink">
              {data.title ?? "Plain Language Summary"}
            </h2>
          </div>
          <div className="flex flex-wrap gap-2">
            <button
              className="rounded-full bg-moss px-4 py-2 text-xs font-semibold text-white transition-colors hover:bg-moss/90"
              onClick={() => downloadSummary(summaryId!, "pdf")}
            >
              ↓ Download PDF
            </button>
            <button className="rounded-full border border-sand px-4 py-2 text-xs font-semibold text-slate-600 transition-colors hover:bg-fog">
              Copy text
            </button>
          </div>
        </div>

        {/* Meta grid */}
        <div className="mt-4 grid grid-cols-2 gap-3 rounded-2xl border border-sand bg-white/60 p-4 md:grid-cols-4">
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Therapeutic area</p>
            <p className="mt-0.5 text-sm font-medium text-ink">{data.therapeuticArea ?? "—"}</p>
          </div>
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Audience</p>
            <p className="mt-0.5 text-sm font-medium text-ink">{data.audience ?? "—"}</p>
          </div>
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Generated</p>
            <p className="mt-0.5 text-sm font-medium text-ink">
              {new Date(data.createdAt).toLocaleDateString(undefined, {
                year: "numeric",
                month: "short",
                day: "numeric"
              })}
            </p>
          </div>
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Images</p>
            <p className="mt-0.5 text-sm font-medium text-ink">{data.images?.length ?? 0}</p>
          </div>
        </div>
      </div>

      {/* ── Tab panel ── */}
      <div className="glass-panel rounded-3xl p-6">
        {/* Underline tabs */}
        <div className="border-b border-sand">
          <div className="flex gap-1">
            {tabs.map((tab) => (
              <button
                key={tab}
                className={`tab-underline ${activeTab === tab ? "tab-underline-active" : "tab-underline-inactive"}`}
                onClick={() => setActiveTab(tab)}
              >
                {tab}
                {tab === "Evidence" && evidenceSections.length > 0 && (
                  <span className="ml-1.5 rounded-full bg-violet/10 px-1.5 py-0.5 text-[10px] font-bold text-violet">
                    {evidenceSections.length}
                  </span>
                )}
              </button>
            ))}
          </div>
        </div>

        <div className="mt-6">
          {/* Summary tab */}
          {activeTab === "Summary" && (
            <SummaryRenderer content={data.summaryText ?? ""} />
          )}

          {/* Evidence tab */}
          {activeTab === "Evidence" && (
            <EvidencePanel sections={evidenceSections} />
          )}

          {/* Metadata tab */}
          {activeTab === "Metadata" && (
            <div className="grid gap-4 md:grid-cols-2">
              <div className="rounded-2xl border border-sand bg-white p-5">
                <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Model deployment</p>
                <p className="mt-1 font-semibold text-ink">{modelInfo.modelDeployment ?? "—"}</p>
              </div>
              <div className="rounded-2xl border border-sand bg-white p-5">
                <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Estimated tokens</p>
                <p className="mt-1 font-semibold text-ink">
                  {modelInfo.tokensEstimated?.toLocaleString() ?? "—"}
                </p>
              </div>
              <div className="rounded-2xl border border-sand bg-white p-5">
                <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Lane</p>
                <p className="mt-1 font-semibold text-ink">{data.lane}</p>
              </div>
              <div className="rounded-2xl border border-sand bg-white p-5">
                <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Images generated</p>
                <p className="mt-1 font-semibold text-ink">{data.images?.length ?? 0}</p>
              </div>
            </div>
          )}

          {/* Audit tab */}
          {activeTab === "Audit" && (
            <div className="space-y-4">
              {/* Compliance & guardrails */}
              <div className="rounded-2xl border border-sand bg-white p-5">
                <div className="flex items-center justify-between">
                  <h3 className="text-sm font-semibold text-ink">Compliance & Guardrails</h3>
                  <span className={Object.values(guardrails).some(Boolean)
                    ? "compliance-warn"
                    : "compliance-pass"}>
                    {Object.values(guardrails).some(Boolean)
                      ? "⚠ Review required"
                      : "✓ All checks passed"}
                  </span>
                </div>
                {Object.keys(guardrails).length > 0 ? (
                  <div className="mt-3 flex flex-wrap gap-2">
                    {Object.entries(guardrails).map(([key, value]) => (
                      <span key={key} className={value ? "compliance-fail" : "compliance-pass"}>
                        <span>{value ? "✗" : "✓"}</span>
                        {key.replace(/_/g, " ")}
                      </span>
                    ))}
                  </div>
                ) : (
                  <p className="mt-2 text-xs text-slate-400">No guardrail checks recorded for this summary.</p>
                )}
              </div>

              {/* Processing record */}
              <div className="rounded-2xl border border-sand bg-white p-5">
                <h3 className="text-sm font-semibold text-ink">Processing Record</h3>
                <dl className="mt-3 grid gap-3 text-sm md:grid-cols-2">
                  <div>
                    <dt className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Document</dt>
                    <dd className="mt-0.5 font-medium text-ink">{data.title ?? "—"}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Lane strategy</dt>
                    <dd className="mt-0.5 font-medium text-ink">{data.lane}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Model</dt>
                    <dd className="mt-0.5 font-medium text-ink">{modelInfo.modelDeployment ?? "—"}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Tokens used</dt>
                    <dd className="mt-0.5 font-medium text-ink">
                      {modelInfo.tokensEstimated?.toLocaleString() ?? "—"}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Generated at</dt>
                    <dd className="mt-0.5 font-medium text-ink">
                      {new Date(data.createdAt).toLocaleString()}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Status</dt>
                    <dd className="mt-0.5">
                      <StatusBadge status={data.status ?? "Completed"} />
                    </dd>
                  </div>
                </dl>
              </div>

              {/* Raw data (collapsible) */}
              <details className="rounded-2xl border border-sand bg-white p-5">
                <summary className="cursor-pointer select-none text-sm font-semibold text-slate-600 hover:text-ink">
                  Raw audit data
                </summary>
                <pre className="mt-3 whitespace-pre-wrap break-all text-xs text-slate-500">
                  {JSON.stringify(modelInfo, null, 2)}
                </pre>
              </details>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
