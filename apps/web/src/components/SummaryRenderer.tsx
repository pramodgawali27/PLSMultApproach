import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

type SummaryRendererProps = {
  content: string;
};

export default function SummaryRenderer({ content }: SummaryRendererProps) {
  return (
    <div className="space-y-6">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          h1: ({ children }) => (
            <h1 className="text-3xl font-semibold text-slate-900">{children}</h1>
          ),
          h2: ({ children }) => (
            <h2 className="rounded-xl bg-ink px-5 py-3 text-xl font-semibold text-fog">
              {children}
            </h2>
          ),
          h3: ({ children }) => (
            <h3 className="rounded-lg border border-ink/10 bg-white/90 px-4 py-2 text-lg font-semibold text-ink">
              {children}
            </h3>
          ),
          p: ({ children }) => (
            <p className="text-[17px] leading-8 text-slate-900">{children}</p>
          ),
          ul: ({ children }) => (
            <ul className="list-disc space-y-3 pl-6 text-[17px] leading-8 text-slate-900">
              {children}
            </ul>
          ),
          li: ({ children }) => <li className="leading-8">{children}</li>,
          strong: ({ children }) => (
            <strong className="font-semibold text-ink">{children}</strong>
          ),
          a: ({ children, href }) => (
            <a className="font-semibold text-ink underline" href={href}>
              {children}
            </a>
          ),
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
