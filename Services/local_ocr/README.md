# VitaMR Local OCR Service

Local FastAPI sidecar for extracting text from images and PDFs before any privacy or API step.

## Run

```powershell
cd services\local_ocr
python -m venv .venv
.\.venv\Scripts\pip install -r requirements.txt
.\.venv\Scripts\uvicorn app:app --host 127.0.0.1 --port 8000
```

The planned C# endpoint is `http://localhost:8000/extract_text`.

