# VitaMR Local Privacy Service

Local FastAPI sidecar for Presidio-based text de-identification.

## Run

```powershell
cd services\local_privacy
python -m venv .venv
.\.venv\Scripts\pip install -r requirements.txt
.\.venv\Scripts\uvicorn app:app --host 127.0.0.1 --port 8001
```

The C# app calls `http://localhost:8001/scrub_text`.

