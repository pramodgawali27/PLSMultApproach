import { SummaryLane } from "../api";

type LaneCardProps = {
  lane: SummaryLane;
  title: string;
  description: string;
  bestFor: string;
  badges: string[];
  selected: boolean;
  locked: boolean;
  onSelect: (lane: SummaryLane) => void;
};

export default function LaneCard({
  lane,
  title,
  description,
  bestFor,
  badges,
  selected,
  locked,
  onSelect
}: LaneCardProps) {
  return (
    <button
      type="button"
      className={`w-full rounded-2xl border px-4 py-4 text-left transition-all ${
        selected
          ? "border-violet bg-violet/5 shadow-md ring-1 ring-violet/20"
          : "border-sand bg-white/70 hover:border-violet/40 hover:bg-white"
      } ${locked ? "cursor-not-allowed opacity-50" : "cursor-pointer"}`}
      onClick={() => !locked && onSelect(lane)}
      disabled={locked}
      title={locked ? "Upgrade required to access this lane" : undefined}
    >
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          {selected && <span className="h-2 w-2 rounded-full bg-violet" />}
          <h3 className={`font-display text-base font-bold ${selected ? "text-violet" : "text-ink"}`}>
            {title}
          </h3>
        </div>
        {locked && (
          <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-slate-500">
            Upgrade
          </span>
        )}
      </div>
      <p className="mt-1.5 text-xs text-slate-500">{description}</p>
      <p className="mt-3 text-[10px] font-semibold uppercase tracking-wide text-slate-400">Best for</p>
      <p className="text-xs font-medium text-slate-700">{bestFor}</p>
      <div className="mt-3 flex flex-wrap gap-1.5">
        {badges.map((badge) => (
          <span
            key={badge}
            className={`rounded-full px-2.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
              selected ? "bg-violet/10 text-violet" : "bg-slate-100 text-slate-600"
            }`}
          >
            {badge}
          </span>
        ))}
      </div>
    </button>
  );
}
