import { JobStatus } from "../api";

type StatusConfig = {
  bg: string;
  text: string;
  dot: string;
};

const statusConfig: Record<JobStatus, StatusConfig> = {
  Queued: { bg: "bg-slate-100", text: "text-slate-600", dot: "bg-slate-400" },
  Processing: { bg: "bg-amber-50", text: "text-amber-700", dot: "bg-amber-400 animate-pulse" },
  Completed: { bg: "bg-emerald-50", text: "text-emerald-700", dot: "bg-emerald-500" },
  Failed: { bg: "bg-rose-50", text: "text-rose-700", dot: "bg-rose-500" }
};

export default function StatusBadge({ status }: { status: JobStatus }) {
  const config = statusConfig[status] ?? statusConfig.Queued;
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ${config.bg} ${config.text}`}
    >
      <span className={`h-1.5 w-1.5 flex-shrink-0 rounded-full ${config.dot}`} />
      {status}
    </span>
  );
}
