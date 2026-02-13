type EvidenceItem = {
  chunkId: string;
  snippet?: string;
  page?: number | null;
  section?: string | null;
  index?: number | null;
  sourceUrl?: string | null;
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
              <li
                key={item.chunkId}
                id={`evidence-${item.index ?? item.chunkId}`}
                className="rounded-lg border border-sand px-3 py-2"
              >
                <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500">
                  <span className="font-semibold text-slate-700">
                    {item.index ? `[${item.index}]` : item.chunkId}
                  </span>
                  {item.sourceUrl && (
                    <a className="text-ink underline" href={item.sourceUrl} target="_blank" rel="noreferrer">
                      Open source
                    </a>
                  )}
                </div>
                <div className="mt-1 text-[11px] text-slate-500">
                  {item.section && <span>{item.section}</span>}
                  {item.section && item.page ? <span> · </span> : null}
                  {item.page ? <span>Page {item.page}</span> : null}
                </div>
                {item.snippet && <p className="mt-1">{item.snippet}</p>}
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}
