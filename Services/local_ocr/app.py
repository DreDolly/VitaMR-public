from __future__ import annotations

import os
import tempfile
from pathlib import Path

from fastapi import FastAPI, File, UploadFile
from pydantic import BaseModel

try:
    import fitz
    from paddleocr import PaddleOCR
except Exception:  # pragma: no cover - lets /health report missing deps cleanly
    fitz = None
    PaddleOCR = None


app = FastAPI(title="VitaMR Local OCR Service", version="0.1.0")
_ocr: PaddleOCR | None = None


class ExtractTextResponse(BaseModel):
    status: str
    filename: str
    text: str
    page_count: int = 0
    warnings: list[str] = []


def get_ocr() -> PaddleOCR:
    global _ocr
    if _ocr is None:
        if PaddleOCR is None:
            raise RuntimeError("PaddleOCR dependencies are not installed.")
        _ocr = PaddleOCR(use_angle_cls=True, lang="en", show_log=False)
    return _ocr


@app.get("/health")
def health() -> dict[str, str]:
    if fitz is None or PaddleOCR is None:
        return {"status": "unavailable", "detail": "OCR dependencies are not installed."}
    return {"status": "ready"}


@app.post("/extract_text", response_model=ExtractTextResponse)
async def extract_text(file: UploadFile = File(...)) -> ExtractTextResponse:
    suffix = Path(file.filename or "upload").suffix.lower()
    with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as temp:
        temp.write(await file.read())
        temp_path = temp.name

    try:
        if suffix == ".pdf":
            return _extract_pdf(temp_path, file.filename or "upload.pdf")

        return _extract_image(temp_path, file.filename or "upload")
    finally:
        try:
            os.remove(temp_path)
        except OSError:
            pass


def _extract_image(path: str, filename: str) -> ExtractTextResponse:
    ocr = get_ocr()
    result = ocr.ocr(path, cls=True)
    lines = _flatten_paddle_lines(result)
    return ExtractTextResponse(
        status="OCR_TEXT_EXTRACTED" if lines else "OCR_NO_TEXT_FOUND",
        filename=filename,
        text="\n".join(lines),
        page_count=1,
    )


def _extract_pdf(path: str, filename: str) -> ExtractTextResponse:
    if fitz is None:
        raise RuntimeError("PyMuPDF is not installed.")

    warnings: list[str] = []
    page_text: list[str] = []
    doc = fitz.open(path)

    for index, page in enumerate(doc):
        text = page.get_text("text").strip()
        if text:
            page_text.append(f"--- Page {index + 1} ---\n{text}")
            continue

        warnings.append(f"Page {index + 1} had no embedded text; image OCR for PDF pages is not enabled in v0.")

    return ExtractTextResponse(
        status="PDF_TEXT_EXTRACTED" if page_text else "PDF_NO_TEXT_FOUND",
        filename=filename,
        text="\n\n".join(page_text),
        page_count=len(doc),
        warnings=warnings,
    )


def _flatten_paddle_lines(result: object) -> list[str]:
    lines: list[str] = []
    if not result:
        return lines

    for page in result:
        if not page:
            continue
        for item in page:
            if len(item) >= 2 and isinstance(item[1], (list, tuple)) and item[1]:
                lines.append(str(item[1][0]))

    return lines
