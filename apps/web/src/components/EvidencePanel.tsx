type EvidenceItem = {
  chunkId: string;
  snippet?: string;
};

type EvidenceSection = {
  title: string;
  items: EvidenceItem[];
};

export default function EvidencePanel({ sections }: { sections: EvidenceSection[] }) {
  if (!sections.length) {
    return (
      <div className="rounded-2xl border border-dashed border-sand p-6 text-center text-sm text-slate-500">
        No evidence available for this summary.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {sections.map((section) => (
        <div key={section.title} className="rounded-2xl bg-white/70 p-4">
          <h4 className="text-sm font-semibold">{section.title}</h4>
          <ul className="mt-2 space-y-2 text-xs text-slate-600">
            {section.items.map((item) => (
              <li key={item.chunkId} className="rounded-lg border border-sand px-3 py-2">
                <span className="font-semibold text-slate-700">{item.chunkId}</span>
                {item.snippet && <p className="mt-1">{item.snippet}</p>}
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}
