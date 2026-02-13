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
            <h1 className="text-3xl font-semibold text-slate-900">{children}</h1>
          ),
          h2: ({ children }) => (
            <h2 className="text-2xl font-semibold text-slate-900">{children}</h2>
          ),
          h3: ({ children }) => (
            <h3 className="text-xl font-semibold text-slate-900">{children}</h3>
          ),
          p: ({ children }) => (
            <p className="rounded-xl bg-white/80 px-5 py-4 text-[16px] leading-8 text-slate-800">
              {children}
            </p>
          ),
          ul: ({ children }) => (
            <ul className="list-disc space-y-3 rounded-xl bg-white/80 px-7 py-4 text-[16px] leading-8 text-slate-800">
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
