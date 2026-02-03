type SummaryRendererProps = {
  content: string;
};

function escapeHtml(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

export default function SummaryRenderer({ content }: SummaryRendererProps) {
  const paragraphs = content.split(/\n{2,}/).map((block, index) => (
    <p key={index} className="rounded-xl bg-white/80 px-4 py-3 text-sm leading-relaxed">
      {block.split("\n").map((line, lineIndex) => (
        <span key={lineIndex} className="block">
          {escapeHtml(line)}
        </span>
      ))}
    </p>
  ));

  return <div className="space-y-4">{paragraphs}</div>;
}
