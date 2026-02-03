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
      className={`w-full rounded-2xl border px-4 py-4 text-left transition ${
        selected ? "border-moss bg-white shadow-lg" : "border-sand bg-white/70"
      } ${locked ? "opacity-60" : "hover:border-moss"}`}
      onClick={() => !locked && onSelect(lane)}
      disabled={locked}
      title={locked ? "Upgrade required" : undefined}
    >
      <div className="flex items-center justify-between">
        <h3 className="font-display text-lg font-semibold">{title}</h3>
        {locked && <span className="text-xs font-semibold text-ember">Locked</span>}
      </div>
      <p className="mt-2 text-sm text-slate-600">{description}</p>
      <p className="mt-3 text-xs uppercase tracking-wide text-slate-500">Best for</p>
      <p className="text-sm font-medium">{bestFor}</p>
      <div className="mt-3 flex flex-wrap gap-2">
        {badges.map((badge) => (
          <span key={badge} className="badge bg-slate-100 text-slate-700">
            {badge}
          </span>
        ))}
      </div>
    </button>
  );
}
