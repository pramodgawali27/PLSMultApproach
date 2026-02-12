import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

type SummaryRendererProps = {
  content: string;
};

export default function SummaryRenderer({ content }: SummaryRendererProps) {
  return (
    <div className="space-y-4">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          h1: ({ children }) => (
            <h1 className="text-2xl font-semibold text-slate-900">{children}</h1>
          ),
          h2: ({ children }) => (
            <h2 className="text-xl font-semibold text-slate-900">{children}</h2>
          ),
          h3: ({ children }) => (
            <h3 className="text-lg font-semibold text-slate-900">{children}</h3>
          ),
          p: ({ children }) => (
            <p className="rounded-xl bg-white/80 px-4 py-3 text-sm leading-relaxed text-slate-800">
              {children}
            </p>
          ),
          ul: ({ children }) => (
            <ul className="list-disc space-y-2 rounded-xl bg-white/80 px-6 py-3 text-sm text-slate-800">
              {children}
            </ul>
          ),
          li: ({ children }) => <li className="leading-relaxed">{children}</li>,
          img: ({ src, alt }) => (
            <img
              src={src}
              alt={alt ?? "Generated figure"}
              className="block w-full rounded-xl border border-slate-200 bg-white/80 p-2"
            />
          )
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}
