import base64
import os
import re
import tempfile
from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse

try:
    from docling.document_converter import DocumentConverter
except Exception as exc:  # pragma: no cover - import errors handled at runtime
    DocumentConverter = None
    _IMPORT_ERROR = str(exc)
else:
    _IMPORT_ERROR = None

app = FastAPI()


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/extract")
async def extract(file: UploadFile = File(...)):
    if DocumentConverter is None:
        raise HTTPException(status_code=500, detail=f"Docling import failed: {_IMPORT_ERROR}")

    suffix = os.path.splitext(file.filename or "")[1]
    with tempfile.TemporaryDirectory() as temp_dir:
        with tempfile.NamedTemporaryFile(delete=False, suffix=suffix, dir=temp_dir) as tmp:
            content = await file.read()
            tmp.write(content)
            tmp_path = tmp.name

        try:
            converter = DocumentConverter()
            result = converter.convert(tmp_path)
            markdown = _export_markdown(result, temp_dir)
            if not markdown:
                raise HTTPException(status_code=400, detail="Docling returned empty markdown.")
            images = _collect_images(temp_dir, markdown)
            return JSONResponse({"markdown": markdown, "images": images})
        except HTTPException:
            raise
        except Exception as exc:
            raise HTTPException(status_code=500, detail=str(exc))


def _export_markdown(result, image_dir):
    if hasattr(result, "export_to_markdown"):
        try:
            return result.export_to_markdown(image_dir=image_dir)
        except TypeError:
            return result.export_to_markdown()
    if hasattr(result, "document") and hasattr(result.document, "export_to_markdown"):
        try:
            return result.document.export_to_markdown(image_dir=image_dir)
        except TypeError:
            return result.document.export_to_markdown()
    return ""


def _collect_images(image_dir, markdown):
    image_pattern = re.compile(r"!\[(?P<alt>.*?)\]\((?P<path>.*?)\)")
    alt_by_path = {}
    for match in image_pattern.finditer(markdown or ""):
        alt_by_path[os.path.basename(match.group("path"))] = match.group("alt")

    images = []
    for name in os.listdir(image_dir):
        lower = name.lower()
        if not lower.endswith((".png", ".jpg", ".jpeg", ".webp", ".gif")):
            continue
        file_path = os.path.join(image_dir, name)
        with open(file_path, "rb") as handle:
            data = handle.read()
        if not data:
            continue
        content_type = _content_type_from_name(name)
        images.append(
            {
                "fileName": name,
                "contentType": content_type,
                "caption": alt_by_path.get(name, ""),
                "dataBase64": base64.b64encode(data).decode("utf-8"),
            }
        )
    return images


def _content_type_from_name(name):
    lower = name.lower()
    if lower.endswith(".png"):
        return "image/png"
    if lower.endswith(".jpg") or lower.endswith(".jpeg"):
        return "image/jpeg"
    if lower.endswith(".webp"):
        return "image/webp"
    if lower.endswith(".gif"):
        return "image/gif"
    return "application/octet-stream"
