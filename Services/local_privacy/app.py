from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Any, Iterable, Iterator

from fastapi import FastAPI
from pydantic import BaseModel

try:
    import spacy
    from presidio_analyzer import AnalyzerEngine, RecognizerResult
    from presidio_analyzer.nlp_engine import NlpArtifacts, NlpEngine
    from presidio_anonymizer import AnonymizerEngine
    from presidio_anonymizer.entities import OperatorConfig
except Exception:  # pragma: no cover - lets /health report missing deps cleanly
    spacy = None
    AnalyzerEngine = None
    RecognizerResult = None
    NlpArtifacts = None
    NlpEngine = object
    AnonymizerEngine = None
    OperatorConfig = None


app = FastAPI(title="VitaMR Local Privacy Service", version="0.1.0")

PII_ENTITIES = [
    "PERSON",
    "LOCATION",
    "DATE_TIME",
    "PHONE_NUMBER",
    "EMAIL_ADDRESS",
    "US_SSN",
    "US_DRIVER_LICENSE",
    "US_PASSPORT",
    "US_BANK_NUMBER",
    "US_ITIN",
    "US_MBI",
    "MEDICAL_LICENSE",
]

RELATIVE_TIMING_RE = re.compile(
    r"\b("
    r"(?:in|for|after|within|over)\s+(?:\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\s+"
    r"(?:day|days|week|weeks|month|months|year|years)"
    r"|(?:\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\s+"
    r"(?:day|days|week|weeks|month|months|year|years)\s+ago"
    r"|several\s+(?:days|weeks|months|years)"
    r"|post-?op(?:erative)?\s+day\s+\d+"
    r")\b",
    re.IGNORECASE,
)

PERSON_CONTEXT_RE = re.compile(
    r"\b(?:patient|pt|name|guardian|parent|mother|father|sibling|sister|brother|spouse|wife|husband|relative|emergency contact)\s*:\s*"
    r"(?P<name>[A-Z][A-Za-z'-]+(?:\s+[A-Z][A-Za-z'-]+){0,3})",
    re.IGNORECASE,
)

PERSON_NAME_RE = re.compile(
    r"\b(?P<name>[A-Z][A-Za-z'-]+\s+[A-Z][A-Za-z'-]+(?:\s+[A-Z][A-Za-z'-]+){0,2})\b"
)

NON_PERSON_TERMS = {
    "chief complaint",
    "history present",
    "review systems",
    "physical exam",
    "assessment plan",
    "ct abdomen",
    "ct pelvis",
    "ca 125",
    "genetic counseling",
    "lynch syndrome",
    "cardiopulmonary exercise",
    "exercise test",
    "uploaded file",
}


class ScrubTextRequest(BaseModel):
    text: str
    language: str = "en"


class EntityResponse(BaseModel):
    entity_type: str
    text: str
    start: int
    end: int
    score: float


class ScrubTextResponse(BaseModel):
    status: str
    scrubbed_text: str
    replacement_count: int
    preserved_clinical_timing: bool
    warnings: list[str]
    entities: list[EntityResponse]


@dataclass(frozen=True)
class EngineBundle:
    analyzer: Any
    anonymizer: Any


_engines: EngineBundle | None = None


def get_engines() -> EngineBundle:
    global _engines
    if _engines is None:
        if AnalyzerEngine is None or AnonymizerEngine is None or spacy is None:
            raise RuntimeError("Presidio dependencies are not installed.")
        _engines = EngineBundle(AnalyzerEngine(nlp_engine=LocalBlankEnglishNlpEngine()), AnonymizerEngine())
    return _engines


class LocalBlankEnglishNlpEngine(NlpEngine):
    """Small local NLP engine that avoids runtime spaCy model downloads."""

    def __init__(self) -> None:
        self._nlp = None

    def load(self) -> None:
        if self._nlp is None:
            self._nlp = spacy.blank("en")

    def is_loaded(self) -> bool:
        return self._nlp is not None

    def process_text(self, text: str, language: str) -> NlpArtifacts:
        self.load()
        doc = self._nlp(text or "")
        return NlpArtifacts(
            entities=[],
            tokens=doc,
            tokens_indices=[token.idx for token in doc],
            lemmas=[token.lemma_ if token.lemma_ else token.text.lower() for token in doc],
            nlp_engine=self,
            language=language,
        )

    def process_batch(
        self,
        texts: Iterable[str],
        language: str,
        batch_size: int = 1,
        n_process: int = 1,
        **kwargs: Any,
    ) -> Iterator[tuple[str, NlpArtifacts]]:
        for text in texts:
            yield text, self.process_text(text, language)

    def is_stopword(self, word: str, language: str) -> bool:
        self.load()
        return bool(self._nlp.vocab[word].is_stop)

    def is_punct(self, word: str, language: str) -> bool:
        self.load()
        return bool(self._nlp.vocab[word].is_punct)

    def get_supported_entities(self) -> list[str]:
        return []

    def get_supported_languages(self) -> list[str]:
        return ["en"]


@app.get("/health")
def health() -> dict[str, str]:
    try:
        get_engines()
    except Exception as exc:
        return {"status": "unavailable", "detail": str(exc)}
    return {"status": "ready"}


@app.post("/scrub_text", response_model=ScrubTextResponse)
def scrub_text(request: ScrubTextRequest) -> ScrubTextResponse:
    engines = get_engines()
    text = request.text or ""
    timing_markers = RELATIVE_TIMING_RE.findall(text)

    analyzer_results = engines.analyzer.analyze(
        text=text,
        entities=PII_ENTITIES,
        language=request.language,
    )
    analyzer_results.extend(_detect_local_person_names(text))
    analyzer_results = [
        result
        for result in analyzer_results
        if not _overlaps_relative_timing(text, result.start, result.end)
        and not _is_clinical_false_positive(text, result.start, result.end, result.entity_type)
    ]
    analyzer_results = _dedupe_overlapping_results(analyzer_results)

    operators = {
        entity: OperatorConfig("replace", {"new_value": f"[{entity}]"})
        for entity in PII_ENTITIES
    }
    anonymized = engines.anonymizer.anonymize(
        text=text,
        analyzer_results=analyzer_results,
        operators=operators,
    )

    scrubbed_text = anonymized.text
    warnings: list[str] = []
    preserved_clinical_timing = all(marker in scrubbed_text for marker in timing_markers)
    if not preserved_clinical_timing:
        warnings.append("One or more relative clinical timing markers changed during scrubbing.")

    entities = [
        EntityResponse(
            entity_type=result.entity_type,
            text=text[result.start : result.end],
            start=result.start,
            end=result.end,
            score=float(result.score),
        )
        for result in analyzer_results
    ]

    return ScrubTextResponse(
        status="PRESIDIO_SCRUBBED",
        scrubbed_text=scrubbed_text,
        replacement_count=len(entities),
        preserved_clinical_timing=preserved_clinical_timing,
        warnings=warnings,
        entities=entities,
    )


def _overlaps_relative_timing(text: str, start: int, end: int) -> bool:
    return any(match.start() < end and match.end() > start for match in RELATIVE_TIMING_RE.finditer(text))


def _detect_local_person_names(text: str) -> list[Any]:
    if RecognizerResult is None:
        return []

    results: list[Any] = []
    for pattern, score in ((PERSON_CONTEXT_RE, 0.9), (PERSON_NAME_RE, 0.65)):
        for match in pattern.finditer(text):
            name = match.group("name").strip()
            start = match.start("name")
            end = match.end("name")
            if _looks_like_non_person_name(name):
                continue

            results.append(RecognizerResult("PERSON", start, end, score))

    return results


def _dedupe_overlapping_results(results: list[Any]) -> list[Any]:
    kept: list[Any] = []
    for result in sorted(results, key=lambda item: (item.start, -(item.end - item.start), -float(item.score))):
        overlap = next(
            (
                existing
                for existing in kept
                if existing.start < result.end and existing.end > result.start
            ),
            None,
        )
        if overlap is None:
            kept.append(result)
            continue

        if float(result.score) > float(overlap.score):
            kept.remove(overlap)
            kept.append(result)

    return sorted(kept, key=lambda item: item.start)


def _looks_like_non_person_name(value: str) -> bool:
    normalized = re.sub(r"[^a-z0-9\s]+", " ", value.lower())
    normalized = re.sub(r"\s+", " ", normalized).strip()

    if normalized in NON_PERSON_TERMS:
        return True

    return bool(
        re.search(
            r"\b(?:patient|dob|phone|address|clinic|hospital|doctor|dr|md|rn|rd|ct|mri|xray|spo2|sp02|api|dolly|vita|test|source|file|date)\b",
            normalized,
        )
    )


def _is_clinical_false_positive(text: str, start: int, end: int, entity_type: str) -> bool:
    value = text[start:end].strip()
    normalized = re.sub(r"\s+", " ", value).lower()

    if normalized in {"spo2", "sp02", "o2", "rd", "registered dietitian", "dietitian"}:
        return True

    if entity_type == "US_DRIVER_LICENSE" and re.fullmatch(r"x\d+", normalized):
        return True

    if entity_type == "DATE_TIME" and re.fullmatch(r"\d+\s*-\s*year\s*-\s*old", normalized):
        return True

    if entity_type == "DATE_TIME" and re.fullmatch(r"\d+\s*-\s*minute(?:\s+daily)?\s+walk", normalized):
        return True

    return False
