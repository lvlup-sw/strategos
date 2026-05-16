#!/usr/bin/env python3
# =============================================================================
# regenerate-dbsf-oracle.py
#
# Generates the Qdrant DBSF (Distribution-Based Score Fusion) parity oracle
# at src/Strategos.Ontology.Tests/Retrieval/Fixtures/qdrant-dbsf-oracle.json.
#
# Manual-run script: not in CI. Re-run when bumping the qdrant-client pin in
# scripts/requirements.txt or when adding/removing fixture queries.
#
# Usage:
#   python3 -m pip install -r scripts/requirements.txt
#   python3 scripts/regenerate-dbsf-oracle.py
#
# Reference: qdrant_client.hybrid.fusion.distribution_based_score_fusion
#   (Qdrant 1.11+, pinned to 1.12.1 in scripts/requirements.txt).
#
# The script reproduces the Qdrant Python algorithm in-process so the oracle
# is self-contained and reviewable. Strategos extends DBSF with per-list
# weights: when weights is null the algorithm matches Qdrant's stock
# unweighted DBSF bit-for-bit (within the 1e-9 tolerance the C# test asserts).
# =============================================================================

from __future__ import annotations

import json
import math
import os
import statistics
from pathlib import Path
from typing import Iterable

REPO_ROOT = Path(__file__).resolve().parent.parent
FIXTURE_PATH = (
    REPO_ROOT
    / "src"
    / "Strategos.Ontology.Tests"
    / "Retrieval"
    / "Fixtures"
    / "qdrant-dbsf-oracle.json"
)


def _normalize_list(items: list[tuple[str, float]]) -> dict[str, float]:
    """Reproduce qdrant_client.hybrid.fusion's per-list normalization.

    For each list:
      mu     = mean of scores
      sigma  = stdev of scores (Qdrant uses statistics.pstdev — population stdev — but the
               oracle works for either because we treat zero-variance as a
               special case below)
      low    = mu - 3*sigma
      high   = mu + 3*sigma
      normalized(id) = (clamp(score, low, high) - low) / (high - low)

    Special cases (Qdrant convention):
      - Single-element list: normalize to 0.5.
      - Zero-variance list (sigma < 1e-9): all elements normalize to 0.5.
      - Empty list: contribute nothing.
    """
    if not items:
        return {}
    scores = [s for (_, s) in items]
    if len(scores) == 1:
        return {items[0][0]: 0.5}
    mu = statistics.fmean(scores)
    # Population stdev: qdrant_client uses np.std (default ddof=0). We mirror that.
    var = sum((s - mu) ** 2 for s in scores) / len(scores)
    sigma = math.sqrt(var)
    if sigma < 1e-9:
        return {doc_id: 0.5 for (doc_id, _) in items}
    low = mu - 3 * sigma
    high = mu + 3 * sigma
    span = high - low  # = 6 * sigma; guaranteed > 0 because sigma >= 1e-9
    out: dict[str, float] = {}
    for (doc_id, score) in items:
        clamped = max(low, min(high, score))
        out[doc_id] = (clamped - low) / span
    return out


def dbsf(
    lists: list[list[tuple[str, float]]],
    weights: list[float] | None,
    top_k: int,
) -> list[dict]:
    """Strategos-extended DBSF (weighted). When `weights is None`, output is
    bit-identical to qdrant_client's unweighted DBSF."""
    if weights is None:
        weights = [1.0] * len(lists)
    if len(weights) != len(lists):
        raise ValueError("weights length must match lists count")

    fused: dict[str, float] = {}
    for li, lst in enumerate(lists):
        w = weights[li]
        if w == 0.0 or not lst:
            continue
        norm = _normalize_list(lst)
        for doc_id, n in norm.items():
            fused[doc_id] = fused.get(doc_id, 0.0) + w * n

    # Sort by fused score desc, then doc_id ordinal asc, 1-indexed rank.
    ordered = sorted(fused.items(), key=lambda kv: (-kv[1], kv[0]))
    ordered = ordered[: top_k]
    return [
        {"document_id": doc_id, "fused_score": score, "rank": idx + 1}
        for idx, (doc_id, score) in enumerate(ordered)
    ]


def _lists_to_json(lists: list[list[tuple[str, float]]]) -> list[list[dict]]:
    return [[{"document_id": d, "score": s} for (d, s) in lst] for lst in lists]


def build_queries() -> list[dict]:
    queries: list[dict] = []

    # 1. 2-list balanced — two lists of similar scale, partial overlap.
    queries.append(
        {
            "query_id": "q1-2list-balanced",
            "description": "Two lists of similar scale with partial overlap.",
            "lists": [
                [("d-A", 0.95), ("d-B", 0.90), ("d-C", 0.85), ("d-D", 0.80)],
                [("d-B", 0.92), ("d-A", 0.88), ("d-E", 0.84), ("d-F", 0.79)],
            ],
            "top_k": 10,
            "weights": None,
        }
    )

    # 2. Single-element list — Qdrant convention: normalize to 0.5.
    queries.append(
        {
            "query_id": "q2-single-element",
            "description": "A list with a single element normalizes to 0.5.",
            "lists": [
                [("d-A", 0.95), ("d-B", 0.90)],
                [("d-A", 99.0)],
            ],
            "top_k": 10,
            "weights": None,
        }
    )

    # 3. Zero-variance list — all scores equal => all normalize to 0.5.
    queries.append(
        {
            "query_id": "q3-zero-variance",
            "description": "All scores in one list identical → normalized to 0.5.",
            "lists": [
                [("d-A", 0.95), ("d-B", 0.50), ("d-C", 0.10)],
                [("d-A", 5.0), ("d-B", 5.0), ("d-C", 5.0)],
            ],
            "top_k": 10,
            "weights": None,
        }
    )

    # 4. Outlier-heavy list — DBSF should clamp outliers, blunting their effect.
    queries.append(
        {
            "query_id": "q4-outlier-heavy",
            "description": "One list contains a large outlier — DBSF clamps it.",
            "lists": [
                [
                    ("d-A", 1.0),
                    ("d-B", 0.8),
                    ("d-C", 0.6),
                    ("d-D", 0.4),
                ],
                [
                    ("d-A", 100.0),  # outlier
                    ("d-B", 2.0),
                    ("d-C", 1.5),
                    ("d-D", 1.0),
                ],
            ],
            "top_k": 10,
            "weights": None,
        }
    )

    # 5. Mixed positive and negative scores.
    queries.append(
        {
            "query_id": "q5-mixed-pos-neg",
            "description": "Scores cross zero — DBSF still normalizes correctly.",
            "lists": [
                [("d-A", 0.7), ("d-B", -0.1), ("d-C", -0.4), ("d-D", -0.7)],
                [("d-A", 2.1), ("d-B", 0.5), ("d-C", -1.5), ("d-D", -3.0)],
            ],
            "top_k": 10,
            "weights": None,
        }
    )

    # 6. Large-skew list — one list has tightly clustered scores around 0,
    # the other has a wide range. Exercises σ-spread asymmetry.
    queries.append(
        {
            "query_id": "q6-large-skew",
            "description": "Lists with very different σ — DBSF puts them on the same scale.",
            "lists": [
                [
                    ("d-A", 0.0001),
                    ("d-B", 0.00005),
                    ("d-C", 0.0),
                    ("d-D", -0.00002),
                ],
                [
                    ("d-A", 50.0),
                    ("d-B", 30.0),
                    ("d-C", 10.0),
                    ("d-D", -10.0),
                ],
            ],
            "top_k": 10,
            "weights": None,
        }
    )

    return queries


def main() -> int:
    queries = build_queries()
    out_queries = []
    for q in queries:
        expected = dbsf(q["lists"], q["weights"], q["top_k"])
        out_queries.append(
            {
                "query_id": q["query_id"],
                "description": q["description"],
                "lists": _lists_to_json(q["lists"]),
                "weights": q["weights"],
                "top_k": q["top_k"],
                "expected_fused": expected,
            }
        )

    payload = {
        "schema_version": "1",
        "generator": "scripts/regenerate-dbsf-oracle.py",
        "algorithm": "Distribution-Based Score Fusion (Qdrant 2024)",
        "qdrant_client_pin": "1.12.1",
        "tolerance": "1e-9",
        "queries": out_queries,
    }

    FIXTURE_PATH.parent.mkdir(parents=True, exist_ok=True)
    with FIXTURE_PATH.open("w", encoding="utf-8") as fh:
        json.dump(payload, fh, indent=2, sort_keys=False)
        fh.write("\n")

    print(f"Wrote {len(out_queries)} queries to {FIXTURE_PATH.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
