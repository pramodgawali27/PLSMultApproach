import { JobStatus } from "../api";

const statusStyles: Record<JobStatus, string> = {
  Queued: "bg-slate-200 text-slate-700",
  Processing: "bg-amber-100 text-amber-800",
  Completed: "bg-emerald-100 text-emerald-800",
  Failed: "bg-rose-100 text-rose-800"
};

export default function StatusBadge({ status }: { status: JobStatus }) {
  return (
    <span className={`badge ${statusStyles[status]}`}>{status}</span>
  );
}
