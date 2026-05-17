#!/usr/bin/env python3
# =============================================================================
# regenerate-dbsf-oracle.py
#
# Generates / verifies the Qdrant DBSF (Distribution-Based Score Fusion) parity
# oracle at src/Strategos.Ontology.Tests/Retrieval/Fixtures/qdrant-dbsf-oracle.json.
#
# Calls qdrant_client.hybrid.fusion.distribution_based_score_fusion directly
# (qdrant-client pinned to 1.12.1 in scripts/requirements.txt). Degenerate
# inputs that raise ZeroDivisionError inside qdrant's implementation (single-
# element lists, zero-variance lists) are caught per-list and emitted using
# the documented Strategos 0.5-convention extension — see
# src/Strategos.Ontology/Retrieval/RankFusion.DistributionBased.cs.
#
# Discovered import path (verified against qdrant-client==1.12.1):
#   from qdrant_client.hybrid.fusion import distribution_based_score_fusion
#   from qdrant_client.http.models import ScoredPoint
#
# ScoredPoint construction note: in 1.12.1 the model accepts string ids, so we
# pass document_id directly as id without an int↔str mapping layer.
#
# Usage:
#   python3 -m pip install -r scripts/requirements.txt
#   python3 scripts/regenerate-dbsf-oracle.py            # regenerate oracle
#   python3 scripts/regenerate-dbsf-oracle.py --check    # parity-check only
#
# The --check mode regenerates in-memory and exits non-zero if the committed
# oracle JSON does not match byte-for-byte. CI uses --check to guard against
# silent drift (see .github/workflows/ci.yml#dbsf-parity-guard).
# =============================================================================

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from qdrant_client.hybrid.fusion import distribution_based_score_fusion
from qdrant_client.http.models import ScoredPoint

REPO_ROOT = Path(__file__).resolve().parent.parent
FIXTURE_PATH = (
    REPO_ROOT
    / "src"
    / "Strategos.Ontology.Tests"
    / "Retrieval"
    / "Fixtures"
    / "qdrant-dbsf-oracle.json"
)


def _scored_points(items: list[tuple[str, float]]) -> list[ScoredPoint]:
    """Build a fresh ScoredPoint list. We rebuild on every call because the
    real qdrant function mutates point.score in-place during normalization."""
    return [
        ScoredPoint(id=doc_id, version=0, score=score, payload={})
        for (doc_id, score) in items
    ]


def _normalize_via_qdrant(items: list[tuple[str, float]]) -> dict[str, float] | None:
    """Call qdrant's per-list normalization indirectly via a single-list fusion.

    Returns:
      - dict mapping doc_id → normalized score, OR
      - None when qdrant raises ZeroDivisionError (single-element / zero-variance
        list — caller applies the Strategos 0.5-convention fallback).
    """
    if not items:
        return {}
    try:
        # Single-list fusion: limit large enough to retain every doc.
        fused = distribution_based_score_fusion(
            responses=[_scored_points(items)],
            limit=len(items),
        )
    except ZeroDivisionError:
        # Strategos extension: qdrant raises here for single-element lists
        # (N-1 == 0 in the variance computation) and for zero-variance lists
        # (high - low == 0 in the rescale). Both map to 0.5 per Strategos.
        return None
    # fused order is by score desc; we just need the per-doc score.
    return {pt.id: pt.score for pt in fused}


def dbsf(
    lists: list[list[tuple[str, float]]],
    weights: list[float] | None,
    top_k: int,
) -> list[dict]:
    """Strategos-extended DBSF (weighted).

    Algorithm:
      - For each list, compute qdrant's per-list normalized scores via
        `distribution_based_score_fusion([list], limit=len(list))`.
      - When that raises ZeroDivisionError (single-element / zero-variance),
        emit the Strategos 0.5-convention fallback for every doc in that list.
      - Multiply per-doc normalized scores by `weights[li]` (default 1.0).
      - Sum across lists; sort fused desc with `DocumentId` ordinal tie-break;
        return top_k 1-indexed entries.

    When `weights is None`, output for non-degenerate inputs matches
    `qdrant_client.hybrid.fusion.distribution_based_score_fusion(lists, top_k)`
    bit-for-bit within float reordering noise (the C# parity test asserts
    ≤ 1e-9).
    """
    if weights is None:
        weights = [1.0] * len(lists)
    if len(weights) != len(lists):
        raise ValueError("weights length must match lists count")

    fused: dict[str, float] = {}
    for li, lst in enumerate(lists):
        w = weights[li]
        if w == 0.0 or not lst:
            continue
        norm = _normalize_via_qdrant(lst)
        if norm is None:
            # Strategos 0.5-convention fallback (qdrant raises here).
            norm = {doc_id: 0.5 for (doc_id, _) in lst}
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

    # 2. Single-element list — Strategos extension: normalize to 0.5
    # (qdrant raises ZeroDivisionError on N-1 == 0).
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

    # 3. Zero-variance list — Strategos extension: all 0.5
    # (qdrant raises ZeroDivisionError on high-low == 0).
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

    # 4. Outlier-heavy list — DBSF without clamping no longer blunts the
    # outlier; the algorithm still produces a deterministic ordering.
    queries.append(
        {
            "query_id": "q4-outlier-heavy",
            "description": "One list contains a large outlier.",
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
    # the other has a wide range. Exercises sigma-spread asymmetry.
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


def build_payload() -> dict:
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

    return {
        "schema_version": "1",
        "generator": "scripts/regenerate-dbsf-oracle.py",
        "algorithm": "Distribution-Based Score Fusion (Qdrant 2024)",
        "qdrant_client_pin": "1.12.1",
        "tolerance": "1e-9",
        "queries": out_queries,
    }


def _serialize(payload: dict) -> str:
    return json.dumps(payload, indent=2, sort_keys=False) + "\n"


def _write(payload: dict) -> int:
    FIXTURE_PATH.parent.mkdir(parents=True, exist_ok=True)
    with FIXTURE_PATH.open("w", encoding="utf-8") as fh:
        fh.write(_serialize(payload))
    print(f"Wrote {len(payload['queries'])} queries to {FIXTURE_PATH.relative_to(REPO_ROOT)}")
    return 0


def _check(payload: dict) -> int:
    expected = _serialize(payload)
    if not FIXTURE_PATH.exists():
        print(f"check FAILED: {FIXTURE_PATH} does not exist", file=sys.stderr)
        return 1
    with FIXTURE_PATH.open("r", encoding="utf-8") as fh:
        actual = fh.read()
    if actual == expected:
        print(f"check PASS: {FIXTURE_PATH.relative_to(REPO_ROOT)} matches real-qdrant output.")
        return 0

    # Diff per-query to surface which query/score diverges.
    try:
        expected_obj = json.loads(expected)
        actual_obj = json.loads(actual)
        diffs: list[str] = []
        for eq, aq in zip(expected_obj.get("queries", []), actual_obj.get("queries", [])):
            qid = eq.get("query_id")
            if eq.get("expected_fused") != aq.get("expected_fused"):
                diffs.append(qid)
        if diffs:
            print(
                "check FAILED: oracle mismatch on queries: " + ", ".join(diffs),
                file=sys.stderr,
            )
        else:
            print(
                "check FAILED: oracle differs from real-qdrant output (whitespace / key-order drift).",
                file=sys.stderr,
            )
    except json.JSONDecodeError:
        print("check FAILED: committed oracle is not valid JSON.", file=sys.stderr)
    return 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="Verify the committed oracle matches real-qdrant output; exit non-zero on drift.",
    )
    args = parser.parse_args()
    payload = build_payload()
    if args.check:
        return _check(payload)
    return _write(payload)


if __name__ == "__main__":
    raise SystemExit(main())
