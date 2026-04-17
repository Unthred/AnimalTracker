import os
import time
from typing import Any

from fastapi import FastAPI, File, Header, HTTPException, UploadFile
from fastapi.responses import JSONResponse

app = FastAPI(title="Animal recognition API", version="1.0.0")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


def _check_api_key(x_api_key: str | None) -> None:
    expected = os.getenv("API_KEY", "").strip()
    if not expected:
        return
    if (x_api_key or "").strip() != expected:
        raise HTTPException(status_code=401, detail="Invalid API key")


@app.post("/recognize")
async def recognize(
    image: UploadFile = File(...),
    x_api_key: str | None = Header(default=None, alias="X-Api-Key"),
) -> dict[str, Any]:
    _check_api_key(x_api_key)
    started = time.perf_counter()
    raw = await image.read()
    if not raw:
        raise HTTPException(status_code=400, detail="Empty image")

    # Placeholder: wire ONNX Runtime here using MODEL_CLASSIFIER_PATH / MODEL_DETECTOR_PATH when you have a known I/O schema.
    _ = os.getenv("MODEL_CLASSIFIER_PATH", "").strip()

    return {
        "modelVersion": os.getenv("MODEL_VERSION", "stub-1"),
        "processingMs": int((time.perf_counter() - started) * 1000),
        "detections": [],
        "imageLevelCandidates": [{"label": "unknown", "confidence": 0.0}],
        "warnings": [
            "Stub response: extend app/main.py with ONNX inference for your wildlife models (CPU: onnxruntime with CPUExecutionProvider)."
        ],
    }


@app.exception_handler(Exception)
async def unhandled(_, exc: Exception) -> JSONResponse:
    return JSONResponse(status_code=500, content={"detail": str(exc)})
