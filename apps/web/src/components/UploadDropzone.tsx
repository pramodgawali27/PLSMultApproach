import { useRef } from "react";

type UploadDropzoneProps = {
  onFileSelected: (file: File) => void;
  accept?: string;
};

export default function UploadDropzone({ onFileSelected, accept }: UploadDropzoneProps) {
  const inputRef = useRef<HTMLInputElement | null>(null);

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    const file = event.dataTransfer.files?.[0];
    if (file) onFileSelected(file);
  };

  return (
    <div
      className="flex flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-sand bg-white/70 px-6 py-6 text-center"
      onDragOver={(event) => event.preventDefault()}
      onDrop={handleDrop}
    >
      <p className="text-sm font-semibold">Drag & drop your document</p>
      <p className="text-xs text-slate-500">PDF, DOCX, TXT supported</p>
      <button
        type="button"
        className="rounded-full bg-moss px-4 py-2 text-xs font-semibold text-white"
        onClick={() => inputRef.current?.click()}
      >
        Browse files
      </button>
      <input
        ref={inputRef}
        type="file"
        accept={accept ?? ".pdf,.docx,.txt"}
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) onFileSelected(file);
        }}
      />
    </div>
  );
}
