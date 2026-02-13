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
    return <p className="text-sm text-slate-500">Loading summary...</p>;
  }

  if (error || !data) {
    return <p className="text-sm text-ember">Unable to load summary.</p>;
  }

  const evidenceSections = data.citations ?? [];
  const modelInfo = data.modelInfoJson ?? {};

  return (
    <div className="space-y-6">
      <div className="glass-panel rounded-3xl p-6">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <p className="text-xs uppercase tracking-wide text-slate-500">Summary detail</p>
            <h2 className="font-display text-2xl font-semibold">{data.title ?? "Plain Language Summary"}</h2>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={data.status ?? "Completed"} />
            <span className="badge bg-slate-100 text-slate-600">{data.lane}</span>
          </div>
        </div>
        <div className="mt-4 flex flex-wrap gap-3">
          <button
            className="rounded-full bg-moss px-4 py-2 text-xs font-semibold text-white"
            onClick={() => downloadSummary(summaryId!, "pdf")}
          >
            Download PDF
          </button>
          <button className="rounded-full border border-sand px-4 py-2 text-xs font-semibold">
            Download HTML
          </button>
          <button className="rounded-full border border-sand px-4 py-2 text-xs font-semibold">
            Copy summary
          </button>
        </div>
      </div>

      <div className="glass-panel rounded-3xl p-6">
        <div className="flex flex-wrap gap-3">
          {tabs.map((tab) => (
            <button
              key={tab}
              className={`rounded-full px-4 py-2 text-xs font-semibold ${
                activeTab === tab ? "bg-ink text-fog" : "border border-sand"
              }`}
              onClick={() => setActiveTab(tab)}
            >
              {tab}
            </button>
          ))}
        </div>

        <div className="mt-6">
          {activeTab === "Summary" && (
            <SummaryRenderer content={data.summaryText ?? ""} />
          )}
          {activeTab === "Evidence" && (
            <EvidencePanel sections={evidenceSections} />
          )}
          {activeTab === "Metadata" && (
            <div className="space-y-3 text-sm">
              <div className="rounded-xl bg-white/70 p-4">
                <p className="text-xs uppercase text-slate-500">Model</p>
                <p className="font-semibold">{modelInfo.modelDeployment ?? "-"}</p>
              </div>
              <div className="rounded-xl bg-white/70 p-4">
                <p className="text-xs uppercase text-slate-500">Tokens</p>
                <p className="font-semibold">{modelInfo.tokensEstimated ?? "-"}</p>
              </div>
              <div className="rounded-xl bg-white/70 p-4">
                <p className="text-xs uppercase text-slate-500">Images generated</p>
                <p className="font-semibold">{data.images?.length ?? 0}</p>
              </div>
            </div>
          )}
          {activeTab === "Audit" && (
            <div className="rounded-2xl bg-white/70 p-4 text-sm">
              <p className="text-xs uppercase text-slate-500">Guardrails</p>
              <pre className="mt-2 whitespace-pre-wrap text-xs text-slate-600">
                {JSON.stringify(modelInfo.guardrails ?? {}, null, 2)}
              </pre>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
