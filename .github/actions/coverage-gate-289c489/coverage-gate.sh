#!/usr/bin/env bash
# Coverage Gate Script for CI/CD
# Parses Cobertura XML coverage reports and enforces a threshold.
# Generates a PR-comment markdown file with a badge and a coverage breakdown.
#
# This is the org's SHARED coverage gate. It supports three modes:
#
#   --coverage-file FILE   Aggregate gate over a single merged Cobertura report.
#                          This is the DEFAULT/legacy mode and its verdict +
#                          pr-comment.md are BYTE-IDENTICAL to the org's
#                          pre-upgrade script (the backward-compat contract that
#                          strategos/basileus/DataFerry depend on). By default
#                          it gates LINE coverage only; branch gating in this
#                          mode is OPT-IN via --metrics line+branch.
#   --per-project DIR      Gate EACH *.cobertura.xml in DIR independently.
#   --per-assembly FILE    Gate EACH <package> in a single merged report
#                          independently. An empty / all-excluded set FAILS
#                          (no vacuous pass).
#
# Exit codes:
#   0 - Coverage meets threshold
#   1 - Coverage below threshold (or invalid usage / missing file)
#   2 - Aggregate mode only: coverage file not found (preserves the org's
#       historical exit-2 "file not found" contract for --coverage-file)

set -uo pipefail

# Default values
COVERAGE_FILE=""
PER_PROJECT_DIR=""
PER_ASSEMBLY_FILE=""
THRESHOLD="${COVERAGE_THRESHOLD:-80}"
OUTPUT_DIR="."
VERBOSE=false
# Aggregate-mode metric selection: "line" (org default, line-only gate) or
# "line+branch" (opt-in; also gates branch coverage). Per-project and
# per-assembly modes always gate line+branch.
METRICS="line"

# Exclude globs for --per-project / --per-assembly modes (non-shipping units:
# pure test-support projects, or non-shipping assemblies). Seeded from the
# EXCLUDE env var (whitespace-separated), then appended to by each --exclude
# flag. In --per-project mode globs match the Cobertura FILE name; in
# --per-assembly mode they match the <package> NAME.
EXCLUDE_GLOBS=()
if [[ -n "${EXCLUDE:-}" ]]; then
    # shellcheck disable=SC2206  # intentional word-splitting of the env list
    EXCLUDE_GLOBS=(${EXCLUDE})
fi

# Per-assembly threshold map (--per-assembly mode only). Each rule maps an
# assembly-name GLOB to a "LINE[/BRANCH]" floor, letting risk-tiered repos hold
# security/boundary assemblies higher than host wiring (issue #21). Raw rules
# are seeded from the ASSEMBLY_THRESHOLDS env var (newline-separated; '#'
# comments and blank lines ignored) and appended to by each --assembly-threshold
# flag, then parsed into the AT_* parallel arrays AFTER arg parsing (once the
# logging helpers exist). An assembly that matches NO rule falls back to the
# global --threshold for both line and branch, so an EMPTY map reproduces
# today's per-assembly behavior exactly.
ASSEMBLY_THRESHOLD_RAW=()
if [[ -n "${ASSEMBLY_THRESHOLDS:-}" ]]; then
    while IFS= read -r _at_raw_line; do
        ASSEMBLY_THRESHOLD_RAW+=("$_at_raw_line")
    done <<< "$ASSEMBLY_THRESHOLDS"
fi
AT_GLOBS=()
AT_LINE=()
AT_BRANCH=()

# Colors for terminal output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

usage() {
    cat << EOF
Usage: $(basename "$0") [OPTIONS]

Modes (exactly one of --coverage-file, --per-project, or --per-assembly is required):
    --coverage-file FILE    Gate a single Cobertura XML report (the merged
                            aggregate). Writes a PR-comment markdown file. This
                            is the legacy/default mode; by default it gates LINE
                            coverage only and its output is byte-compatible with
                            the org's historical gate.
    --per-project DIR       Gate EACH *.cobertura.xml in DIR independently.
                            Fails if ANY project is below the threshold on
                            line OR branch coverage. Writes a per-project
                            pass/fail table to the PR-comment markdown.
    --per-assembly FILE     Gate EACH <package> in a single MERGED Cobertura
                            report independently — one <package name="..">
                            per shipping assembly. Fails if ANY assembly is
                            below the threshold on line OR branch coverage.
                            An empty / all-excluded package set FAILS (no
                            vacuous pass). Writes a per-assembly pass/fail
                            table to the PR-comment markdown.

Options:
    --threshold PERCENT     Minimum coverage percentage (default: 80). Applies
                            to line coverage always, and to branch coverage in
                            per-project / per-assembly modes (and in aggregate
                            mode when --metrics line+branch is set). A unit with
                            no branch-rate skips the branch check (not a failure).
    --metrics MODE          (--coverage-file only) "line" (default, gates line
                            only — matches the org's historical aggregate gate)
                            or "line+branch" (also gates branch coverage).
    --exclude GLOB          (--per-project / --per-assembly, repeatable) Skip
                            a unit whose name matches GLOB. In --per-project
                            the glob matches the Cobertura FILE name; in
                            --per-assembly it matches the <package> NAME. Use
                            for non-shipping units. May also be supplied via
                            the EXCLUDE env var as a whitespace-separated list.
    --assembly-threshold RULE
                            (--per-assembly only, repeatable) A risk-tiered
                            floor "GLOB = LINE[/BRANCH]" applied to each
                            <package> whose NAME matches GLOB. The FIRST matching
                            rule wins (declare the most specific first); an
                            assembly matching no rule falls back to --threshold.
                            Omitting BRANCH (e.g. "*.Host = 70") gates line only
                            and skips the branch check — for host wiring you do
                            not want to chase branch %. May also be supplied via
                            the ASSEMBLY_THRESHOLDS env var as a newline-separated
                            list ('#' comments and blank lines ignored).
    --output-dir DIR        Directory for output files (default: current directory)
    --baseline PATH         Accepted for backward compatibility; currently unused.
    --verbose               Enable verbose output
    -h, --help              Show this help message

Examples:
    # Aggregate gate (existing CI behavior — line-only, byte-compatible output)
    $(basename "$0") --coverage-file ./coverage/Cobertura.xml --threshold 80 --output-dir ./output

    # Aggregate gate that ALSO gates branch coverage (opt-in)
    $(basename "$0") --coverage-file ./coverage/Cobertura.xml --threshold 80 --metrics line+branch

    # Per-project gate, excluding a test-support project
    $(basename "$0") --per-project ./coverage --threshold 80 --exclude '*TestSupport*'
    EXCLUDE='*TestSupport* *Fixtures*' $(basename "$0") --per-project ./coverage

    # Per-assembly gate over one merged report, excluding a non-shipping assembly
    $(basename "$0") --per-assembly ./coverage/merged.cobertura.xml --threshold 80 --exclude 'Bifrost.Scheduling.Testing'

    # Per-assembly gate with risk-tiered floors (security/boundary held higher,
    # host wiring line-only); the global --threshold is the fallback floor.
    $(basename "$0") --per-assembly ./coverage/merged.cobertura.xml --threshold 80 \\
        --assembly-threshold '*Identity*   = 85/75' \\
        --assembly-threshold '*.Core       = 80/70' \\
        --assembly-threshold '*.Host       = 70'
    ASSEMBLY_THRESHOLDS=\$'*Identity* = 85/75\\n*.Core = 80/70' \\
        $(basename "$0") --per-assembly ./coverage/merged.cobertura.xml --threshold 80
EOF
    exit 1
}

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_verbose() {
    if [[ "$VERBOSE" == "true" ]]; then
        echo -e "[DEBUG] $1"
    fi
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --coverage-file)
            COVERAGE_FILE="$2"
            shift 2
            ;;
        --per-project)
            PER_PROJECT_DIR="$2"
            shift 2
            ;;
        --per-assembly)
            PER_ASSEMBLY_FILE="$2"
            shift 2
            ;;
        --exclude)
            EXCLUDE_GLOBS+=("$2")
            shift 2
            ;;
        --assembly-threshold)
            ASSEMBLY_THRESHOLD_RAW+=("$2")
            shift 2
            ;;
        --threshold)
            THRESHOLD="$2"
            shift 2
            ;;
        --metrics)
            METRICS="$2"
            shift 2
            ;;
        --output-dir)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --baseline)
            # Accepted for backward compatibility with the org's historical CLI;
            # baseline-diff gating was never implemented. Consume and ignore.
            shift 2
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            log_error "Unknown option: $1"
            usage
            ;;
    esac
done

# Validate required arguments: exactly one mode must be selected.
MODE_COUNT=0
[[ -n "$COVERAGE_FILE" ]] && MODE_COUNT=$((MODE_COUNT + 1))
[[ -n "$PER_PROJECT_DIR" ]] && MODE_COUNT=$((MODE_COUNT + 1))
[[ -n "$PER_ASSEMBLY_FILE" ]] && MODE_COUNT=$((MODE_COUNT + 1))

if [[ "$MODE_COUNT" -gt 1 ]]; then
    log_error "--coverage-file, --per-project, and --per-assembly are mutually exclusive"
    usage
fi

if [[ "$MODE_COUNT" -eq 0 ]]; then
    log_error "One of --coverage-file, --per-project, or --per-assembly is required"
    usage
fi

if [[ -n "$PER_PROJECT_DIR" && ! -d "$PER_PROJECT_DIR" ]]; then
    log_error "Per-project directory not found: $PER_PROJECT_DIR"
    exit 1
fi

if [[ -n "$PER_ASSEMBLY_FILE" && ! -f "$PER_ASSEMBLY_FILE" ]]; then
    log_error "Per-assembly merged report not found: $PER_ASSEMBLY_FILE"
    exit 1
fi

# Validate --metrics value (aggregate mode only; harmless otherwise).
if [[ "$METRICS" != "line" && "$METRICS" != "line+branch" ]]; then
    log_error "--metrics must be 'line' or 'line+branch' (got: $METRICS)"
    usage
fi

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Extract coverage data from Cobertura XML
# Using xmllint or grep/sed as fallback
extract_coverage() {
    local file="$1"

    # Try to extract line-rate from coverage element
    if command -v xmllint &> /dev/null; then
        local line_rate=$(xmllint --xpath "string(//coverage/@line-rate)" "$file" 2>/dev/null || echo "")
        if [[ -n "$line_rate" ]]; then
            # Convert to percentage (line-rate is 0-1)
            echo "$line_rate" | awk '{printf "%.2f", $1 * 100}'
            return
        fi
    fi

    # Fallback: grep/sed approach
    local line_rate=$(grep -oP 'line-rate="[0-9.]*"' "$file" | head -1 | grep -oP '[0-9.]+')
    if [[ -n "$line_rate" ]]; then
        echo "$line_rate" | awk '{printf "%.2f", $1 * 100}'
        return
    fi

    echo "0.00"
}

# Extract branch coverage
extract_branch_coverage() {
    local file="$1"

    if command -v xmllint &> /dev/null; then
        local branch_rate=$(xmllint --xpath "string(//coverage/@branch-rate)" "$file" 2>/dev/null || echo "")
        if [[ -n "$branch_rate" ]]; then
            echo "$branch_rate" | awk '{printf "%.2f", $1 * 100}'
            return
        fi
    fi

    local branch_rate=$(grep -oP 'branch-rate="[0-9.]*"' "$file" | head -1 | grep -oP '[0-9.]+')
    if [[ -n "$branch_rate" ]]; then
        echo "$branch_rate" | awk '{printf "%.2f", $1 * 100}'
        return
    fi

    echo "N/A"
}

# Decide pass/fail for a single (line%, branch%) pair against EXPLICIT line and
# branch thresholds. An empty branch threshold — or a branch value of "N/A" (no
# branch-rate in the report) — skips the branch check. Echoes "pass"/"fail" and
# returns 0/1. This is the tiered-aware primitive; per-assembly mode passes a
# rule's floor here, while check_thresholds wraps it with the global THRESHOLD.
check_thresholds_against() {
    local line_pct="$1"
    local branch_pct="$2"
    local line_thr="$3"
    local branch_thr="$4"

    local line_int
    line_int=$(echo "$line_pct" | awk '{print int($1)}')
    if [[ "$line_int" -lt "$line_thr" ]]; then
        echo "fail"
        return 1
    fi

    if [[ -n "$branch_thr" && -n "$branch_pct" && "$branch_pct" != "N/A" ]]; then
        local branch_int
        branch_int=$(echo "$branch_pct" | awk '{print int($1)}')
        if [[ "$branch_int" -lt "$branch_thr" ]]; then
            echo "fail"
            return 1
        fi
    fi

    echo "pass"
    return 0
}

# Decide pass/fail for a single (line%, branch%) pair against the global
# THRESHOLD (line + branch). Used by per-project mode and as the per-assembly
# fallback for assemblies that match no tiered rule. A branch value of "N/A"
# skips the branch check. Echoes "pass"/"fail"; returns 0/1.
check_thresholds() {
    check_thresholds_against "$1" "$2" "$THRESHOLD" "$THRESHOLD"
}

# True if the given filename matches any configured exclude glob.
is_excluded() {
    local name="$1"
    local glob
    for glob in "${EXCLUDE_GLOBS[@]+"${EXCLUDE_GLOBS[@]}"}"; do
        # shellcheck disable=SC2053  # RHS is an intentional glob pattern
        if [[ "$name" == $glob ]]; then
            return 0
        fi
    done
    return 1
}

# Parse one "GLOB = LINE[/BRANCH]" rule into the AT_* parallel arrays. Trims
# surrounding whitespace, ignores blank lines and '#' comments, and tolerates
# spaces around '=' and '/' (so "*Identity* = 85 / 75" is valid). A malformed
# rule — missing '=', empty glob, or a non-numeric / out-of-range (>100)
# percentage — is a hard error (exit 1) rather than a silently dropped floor.
parse_assembly_threshold_rule() {
    local rule="$1"
    # Trim leading/trailing whitespace.
    rule="${rule#"${rule%%[![:space:]]*}"}"
    rule="${rule%"${rule##*[![:space:]]}"}"
    [[ -z "$rule" ]] && return 0
    [[ "$rule" == \#* ]] && return 0

    if [[ "$rule" != *=* ]]; then
        log_error "Invalid --assembly-threshold rule (expected 'GLOB = LINE[/BRANCH]'): $rule"
        exit 1
    fi

    local glob="${rule%%=*}"
    local spec="${rule#*=}"
    # Trim whitespace around the glob and the spec.
    glob="${glob#"${glob%%[![:space:]]*}"}"; glob="${glob%"${glob##*[![:space:]]}"}"
    spec="${spec#"${spec%%[![:space:]]*}"}"; spec="${spec%"${spec##*[![:space:]]}"}"

    if [[ -z "$glob" ]]; then
        log_error "Invalid --assembly-threshold rule (empty glob): $rule"
        exit 1
    fi

    local line_thr branch_thr
    line_thr="${spec%%/*}"
    if [[ "$spec" == */* ]]; then
        branch_thr="${spec#*/}"
    else
        branch_thr=""
    fi
    # Strip any inner spaces (e.g. "85 / 75").
    line_thr="${line_thr//[[:space:]]/}"
    branch_thr="${branch_thr//[[:space:]]/}"

    if ! [[ "$line_thr" =~ ^[0-9]+$ ]] || [[ "$line_thr" -gt 100 ]]; then
        log_error "Invalid line threshold '$line_thr' in --assembly-threshold rule: $rule"
        exit 1
    fi
    if [[ -n "$branch_thr" ]] && { ! [[ "$branch_thr" =~ ^[0-9]+$ ]] || [[ "$branch_thr" -gt 100 ]]; }; then
        log_error "Invalid branch threshold '$branch_thr' in --assembly-threshold rule: $rule"
        exit 1
    fi

    AT_GLOBS+=("$glob")
    AT_LINE+=("$line_thr")
    AT_BRANCH+=("$branch_thr")
}

# Resolve the effective (line, branch) floor for one assembly NAME against the
# per-assembly threshold map. The FIRST matching glob wins (declare the most
# specific rules first). Emits "LINE<TAB>BRANCH" where BRANCH is empty when the
# matched rule omitted a branch floor (=> the branch check is skipped — the
# "host wiring: don't chase branch %" case). An assembly matching no rule falls
# back to the global THRESHOLD for both.
effective_thresholds() {
    local name="$1"
    local n="${#AT_GLOBS[@]}"
    local i
    for (( i = 0; i < n; i++ )); do
        # shellcheck disable=SC2053  # RHS is an intentional glob pattern
        if [[ "$name" == ${AT_GLOBS[$i]} ]]; then
            printf '%s\t%s\n' "${AT_LINE[$i]}" "${AT_BRANCH[$i]}"
            return 0
        fi
    done
    printf '%s\t%s\n' "$THRESHOLD" "$THRESHOLD"
}

# Extract per-package (assembly) rows from a single MERGED Cobertura report.
# Emits one TAB-separated record per <package>:  name<TAB>line%<TAB>branch%
# where line%/branch% are 0-100 with two decimals (branch% is "N/A" if the
# package has no branch-rate attribute). Works on the xmllint path and the
# grep/sed fallback (xmllint absent).
extract_assembly_rows() {
    local file="$1"
    local emitted=0

    if command -v xmllint &> /dev/null; then
        local packages
        packages=$(xmllint --xpath "//package/@name" "$file" 2>/dev/null \
            | tr ' ' '\n' | grep -oP '(?<=name=")[^"]+' || echo "")

        local pkg
        for pkg in $packages; do
            local line_rate branch_rate line_pct branch_pct
            line_rate=$(xmllint --xpath "string(//package[@name='$pkg']/@line-rate)" "$file" 2>/dev/null || echo "")
            branch_rate=$(xmllint --xpath "string(//package[@name='$pkg']/@branch-rate)" "$file" 2>/dev/null || echo "")

            if [[ -n "$line_rate" ]]; then
                line_pct=$(echo "$line_rate" | awk '{printf "%.2f", $1 * 100}')
            else
                line_pct="0.00"
            fi
            if [[ -n "$branch_rate" ]]; then
                branch_pct=$(echo "$branch_rate" | awk '{printf "%.2f", $1 * 100}')
            else
                branch_pct="N/A"
            fi

            printf '%s\t%s\t%s\n' "$pkg" "$line_pct" "$branch_pct"
            emitted=1
        done
    fi

    if [[ "$emitted" -eq 0 ]]; then
        # Fallback: parse each <package ...> open tag with grep/sed. Real
        # ReportGenerator / coverlet output orders name before the rate attrs.
        while IFS= read -r tag; do
            [[ "$tag" =~ name=\"([^\"]+)\" ]] || continue
            local pkg_name="${BASH_REMATCH[1]}"

            local line_pct="0.00" branch_pct="N/A"
            if [[ "$tag" =~ line-rate=\"([0-9.]+)\" ]]; then
                line_pct=$(echo "${BASH_REMATCH[1]}" | awk '{printf "%.2f", $1 * 100}')
            fi
            if [[ "$tag" =~ branch-rate=\"([0-9.]+)\" ]]; then
                branch_pct=$(echo "${BASH_REMATCH[1]}" | awk '{printf "%.2f", $1 * 100}')
            fi

            printf '%s\t%s\t%s\n' "$pkg_name" "$line_pct" "$branch_pct"
        done < <(grep -oP '<package[^>]+>' "$file")
    fi
}

# Parse the raw per-assembly threshold rules now that the logging helpers are
# defined (parse_assembly_threshold_rule exits 1 on a malformed rule). The map
# is consumed only by --per-assembly mode; if rules were supplied in another
# mode, warn rather than silently ignore them so the misconfiguration is visible.
for _at_rule in "${ASSEMBLY_THRESHOLD_RAW[@]+"${ASSEMBLY_THRESHOLD_RAW[@]}"}"; do
    parse_assembly_threshold_rule "$_at_rule"
done
if [[ "${#AT_GLOBS[@]}" -gt 0 && -z "$PER_ASSEMBLY_FILE" ]]; then
    log_warn "Per-assembly threshold map supplied but mode is not --per-assembly; the map is ignored"
fi

# ---------------------------------------------------------------------------
# Per-assembly mode: gate each <package> in a single MERGED Cobertura report.
# Each <package name="AssemblyName" line-rate=".." branch-rate=".."> is one
# shipping assembly. Fails if ANY (non-excluded) assembly is below threshold
# on line OR branch. An empty / fully-excluded set FAILS (no vacuous pass).
# ---------------------------------------------------------------------------
if [[ -n "$PER_ASSEMBLY_FILE" ]]; then
    log_info "Coverage Gate Analysis"
    log_info "======================"
    log_info "Mode: per-assembly"
    log_info "Merged report: $PER_ASSEMBLY_FILE"
    log_info "Threshold: ${THRESHOLD}%"
    log_info "Output directory: $OUTPUT_DIR"

    PR_COMMENT_FILE="$OUTPUT_DIR/pr-comment.md"
    OVERALL_STATUS="passing"
    ROWS=""
    CONSIDERED=0

    while IFS=$'\t' read -r asm_name asm_line asm_branch; do
        [[ -z "$asm_name" ]] && continue

        if is_excluded "$asm_name"; then
            log_info "Excluding ${asm_name} (matched exclude glob)"
            ROWS+="| ${asm_name} | - | - | - | :fast_forward: Excluded |\n"
            continue
        fi

        CONSIDERED=$((CONSIDERED + 1))

        # Resolve this assembly's effective floor: a matching tiered rule, else
        # the global THRESHOLD. An empty branch floor means "don't gate branch"
        # (rendered as "-" in the Threshold column).
        IFS=$'\t' read -r asm_line_thr asm_branch_thr < <(effective_thresholds "$asm_name")
        if [[ -n "$asm_branch_thr" ]]; then
            asm_thr_label="${asm_line_thr}/${asm_branch_thr}"
        else
            asm_thr_label="${asm_line_thr}/-"
        fi

        # '|| true' keeps a failing assembly from aborting the loop — we branch
        # on the echoed "pass"/"fail" string, not the exit status.
        asm_result=$(check_thresholds_against "$asm_line" "$asm_branch" "$asm_line_thr" "$asm_branch_thr") || true

        if [[ "$asm_result" == "pass" ]]; then
            log_info "PASS ${asm_name}: line ${asm_line}% / branch ${asm_branch}% (floor ${asm_thr_label})"
            ROWS+="| ${asm_name} | ${asm_line}% | ${asm_branch}% | ${asm_thr_label} | :white_check_mark: Pass |\n"
        else
            log_error "FAIL ${asm_name}: line ${asm_line}% / branch ${asm_branch}% (floor ${asm_thr_label})"
            ROWS+="| ${asm_name} | ${asm_line}% | ${asm_branch}% | ${asm_thr_label} | :x: Fail |\n"
            OVERALL_STATUS="failing"
        fi
    done < <(extract_assembly_rows "$PER_ASSEMBLY_FILE")

    if [[ "$CONSIDERED" -eq 0 ]]; then
        log_error "No (non-excluded) <package> assemblies found in: $PER_ASSEMBLY_FILE"
        OVERALL_STATUS="failing"
    fi

    # Write the per-assembly PR comment table.
    {
        echo "## :bar_chart: Per-Assembly Coverage Report"
        echo
        echo "| Assembly | Line | Branch | Threshold | Status |"
        echo "|----------|------|--------|-----------|--------|"
        echo -en "$ROWS"
        echo
        echo "---"
        if [[ "${#AT_GLOBS[@]}" -gt 0 ]]; then
            echo "<sub>Generated by coverage-gate.sh | Per-assembly gate | Tiered thresholds (Threshold column is line/branch; '-' = branch not gated); assemblies matching no rule use the global ${THRESHOLD}%</sub>"
        else
            echo "<sub>Generated by coverage-gate.sh | Per-assembly gate | Threshold: ${THRESHOLD}% (line + branch)</sub>"
        fi
    } > "$PR_COMMENT_FILE"

    log_info "PR comment written to: $PR_COMMENT_FILE"

    if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
        echo "threshold=$THRESHOLD" >> "$GITHUB_OUTPUT"
        echo "status=$OVERALL_STATUS" >> "$GITHUB_OUTPUT"
    fi

    if [[ "$OVERALL_STATUS" == "failing" ]]; then
        log_error "Per-assembly coverage gate FAILED"
        exit 1
    fi

    log_info "Per-assembly coverage gate PASSED"
    exit 0
fi

# ---------------------------------------------------------------------------
# Per-project mode: gate each *.cobertura.xml in the directory independently.
# ---------------------------------------------------------------------------
if [[ -n "$PER_PROJECT_DIR" ]]; then
    log_info "Coverage Gate Analysis"
    log_info "======================"
    log_info "Mode: per-project"
    log_info "Coverage dir: $PER_PROJECT_DIR"
    log_info "Threshold: ${THRESHOLD}%"
    log_info "Output directory: $OUTPUT_DIR"

    PR_COMMENT_FILE="$OUTPUT_DIR/pr-comment.md"
    OVERALL_STATUS="passing"
    ROWS=""
    CONSIDERED=0

    shopt -s nullglob
    for cov_file in "$PER_PROJECT_DIR"/*.cobertura.xml; do
        base="$(basename "$cov_file")"

        if is_excluded "$base"; then
            log_info "Excluding ${base} (matched exclude glob)"
            ROWS+="| ${base} | - | - | :fast_forward: Excluded |\n"
            continue
        fi

        CONSIDERED=$((CONSIDERED + 1))
        proj_line=$(extract_coverage "$cov_file")
        proj_branch=$(extract_branch_coverage "$cov_file")
        # '|| true' keeps `set -e` from aborting on a failing project — we
        # branch on the echoed "pass"/"fail" string, not the exit status.
        proj_result=$(check_thresholds "$proj_line" "$proj_branch") || true

        if [[ "$proj_result" == "pass" ]]; then
            log_info "PASS ${base}: line ${proj_line}% / branch ${proj_branch}%"
            ROWS+="| ${base} | ${proj_line}% | ${proj_branch}% | :white_check_mark: Pass |\n"
        else
            log_error "FAIL ${base}: line ${proj_line}% / branch ${proj_branch}% (threshold ${THRESHOLD}%)"
            ROWS+="| ${base} | ${proj_line}% | ${proj_branch}% | :x: Fail |\n"
            OVERALL_STATUS="failing"
        fi
    done
    shopt -u nullglob

    if [[ "$CONSIDERED" -eq 0 ]]; then
        log_error "No (non-excluded) *.cobertura.xml files found in: $PER_PROJECT_DIR"
        OVERALL_STATUS="failing"
    fi

    # Write the per-project PR comment table.
    {
        echo "## :bar_chart: Per-Project Coverage Report"
        echo
        echo "| Project | Line | Branch | Status |"
        echo "|---------|------|--------|--------|"
        echo -en "$ROWS"
        echo
        echo "---"
        echo "<sub>Generated by coverage-gate.sh | Per-project gate | Threshold: ${THRESHOLD}% (line + branch)</sub>"
    } > "$PR_COMMENT_FILE"

    log_info "PR comment written to: $PR_COMMENT_FILE"

    if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
        echo "threshold=$THRESHOLD" >> "$GITHUB_OUTPUT"
        echo "status=$OVERALL_STATUS" >> "$GITHUB_OUTPUT"
    fi

    if [[ "$OVERALL_STATUS" == "failing" ]]; then
        log_error "Per-project coverage gate FAILED"
        exit 1
    fi

    log_info "Per-project coverage gate PASSED"
    exit 0
fi

# ===========================================================================
# Single-file (aggregate) mode — LEGACY / DEFAULT.
#
# This path is BYTE-COMPATIBLE with the org's pre-upgrade coverage-gate.sh:
# identical stdout lines, identical pr-comment.md, identical exit codes. It
# gates LINE coverage only by default (the historical contract); branch gating
# in aggregate mode is opt-in via --metrics line+branch. Do not "improve" the
# wording/format here without updating the frozen baseline + parity test, or
# strategos/basileus/DataFerry will silently drift.
# ===========================================================================

# Validate input file exists (org contract: exit 2 on a missing aggregate file).
if [[ ! -f "$COVERAGE_FILE" ]]; then
    echo "::error::Coverage file not found: $COVERAGE_FILE"
    exit 2
fi

# ----------------------------------------------------------------------------
# Aggregate parsing helpers — reproduce the org baseline's grep/sed extraction
# and per-package breakdown EXACTLY (two-decimal percentages, document order,
# the tri-state yellow-zone badge color). These intentionally mirror the
# legacy script rather than the per-project/per-assembly extractors above.
# ----------------------------------------------------------------------------

# Extract attribute value from XML using grep/sed (portable, no xmllint needed)
agg_extract_xml_attr() {
    local file="$1"
    local element="$2"
    local attr="$3"

    grep -o "<${element}[^>]*${attr}=\"[^\"]*\"" "$file" 2>/dev/null | \
        head -1 | \
        sed -n "s/.*${attr}=\"\([^\"]*\)\".*/\1/p"
}

agg_parse_line_coverage() {
    local file="$1"
    local line_rate
    line_rate=$(agg_extract_xml_attr "$file" "coverage" "line-rate")
    if [[ -z "$line_rate" ]]; then
        echo "0.00"
        return
    fi
    awk "BEGIN { printf \"%.2f\", $line_rate * 100 }"
}

agg_parse_branch_coverage() {
    local file="$1"
    local branch_rate
    branch_rate=$(agg_extract_xml_attr "$file" "coverage" "branch-rate")
    if [[ -z "$branch_rate" ]]; then
        echo "0.00"
        return
    fi
    awk "BEGIN { printf \"%.2f\", $branch_rate * 100 }"
}

# Line-only threshold check (the org's historical aggregate gate).
agg_check_threshold() {
    local coverage="$1"
    local threshold="$2"
    awk "BEGIN { exit !($coverage >= $threshold) }"
}

# Tri-state badge color: red if failing, yellow if within +5% of threshold,
# else brightgreen. Mirrors the org's get_coverage_badge_color.
agg_badge_color() {
    local coverage="$1"
    local threshold="$2"

    if ! agg_check_threshold "$coverage" "$threshold"; then
        echo "red"
        return
    fi

    local yellow_zone is_yellow
    yellow_zone=$(awk "BEGIN { print $threshold + 5 }")
    is_yellow=$(awk "BEGIN { print ($coverage < $yellow_zone) ? 1 : 0 }")

    if [[ "$is_yellow" -eq 1 ]]; then
        echo "yellow"
    else
        echo "brightgreen"
    fi
}

# Per-package breakdown rows: "| name | XX.XX% |" lines in document order,
# matching the org baseline byte-for-byte (two-decimal percentages).
agg_package_rows() {
    local file="$1"
    grep -o '<package[^>]*>' "$file" 2>/dev/null | while read -r line; do
        local name rate percentage
        name=$(echo "$line" | sed -n 's/.*name="\([^"]*\)".*/\1/p')
        rate=$(echo "$line" | sed -n 's/.*line-rate="\([^"]*\)".*/\1/p')
        if [[ -n "$name" && -n "$rate" ]]; then
            percentage=$(awk "BEGIN { printf \"%.2f\", $rate * 100 }")
            printf '| %s | %s%% |\n' "$name" "$percentage"
        fi
    done
}

LINE_COVERAGE=$(agg_parse_line_coverage "$COVERAGE_FILE")
BRANCH_COVERAGE=$(agg_parse_branch_coverage "$COVERAGE_FILE")
BADGE_COLOR=$(agg_badge_color "$LINE_COVERAGE" "$THRESHOLD")

# Verdict. The DEFAULT (--metrics line) gates line only — the org contract.
# --metrics line+branch additionally gates branch (opt-in), without changing
# the byte-compatible pr-comment.md layout.
if agg_check_threshold "$LINE_COVERAGE" "$THRESHOLD"; then
    GATE_STATUS="PASS"
    LINE_PASSES=1
else
    GATE_STATUS="FAIL"
    LINE_PASSES=0
fi

BRANCH_PASSES=1
if [[ "$METRICS" == "line+branch" ]]; then
    # Only gate branch when a branch-rate is actually present (N/A => skip).
    BRANCH_ATTR=$(agg_extract_xml_attr "$COVERAGE_FILE" "coverage" "branch-rate")
    if [[ -n "$BRANCH_ATTR" ]]; then
        BRANCH_INT=$(echo "$BRANCH_COVERAGE" | awk '{print int($1)}')
        if [[ "$BRANCH_INT" -lt "$THRESHOLD" ]]; then
            BRANCH_PASSES=0
        fi
    fi
fi

echo "Line coverage: ${LINE_COVERAGE}%"
echo "Branch coverage: ${BRANCH_COVERAGE}%"

# URL-encode the percentage sign for shields.io.
BADGE_URL="https://img.shields.io/badge/coverage-${LINE_COVERAGE}%25-${BADGE_COLOR}"

# Per-project breakdown rows (document order, two decimals).
PROJECT_ROWS=""
while IFS= read -r row; do
    [[ -n "$row" ]] && PROJECT_ROWS="${PROJECT_ROWS}${row}
"
done < <(agg_package_rows "$COVERAGE_FILE")

# Write the PR comment markdown — byte-identical to the org baseline.
PR_COMMENT_FILE="$OUTPUT_DIR/pr-comment.md"
cat > "$PR_COMMENT_FILE" << EOF
## Coverage Report

![Coverage](${BADGE_URL})

### Summary

| Metric | Value | Threshold | Status |
|--------|-------|-----------|--------|
| Line Coverage | ${LINE_COVERAGE}% | ${THRESHOLD}% | ${GATE_STATUS} |
| Branch Coverage | ${BRANCH_COVERAGE}% | - | - |

### Per-Project Breakdown

| Project | Coverage |
|---------|----------|
${PROJECT_ROWS}
---
*Generated by coverage-gate.sh*
EOF

# Output for GitHub Actions (org-compatible keys).
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    echo "line-coverage=${LINE_COVERAGE}" >> "$GITHUB_OUTPUT"
    echo "branch-coverage=${BRANCH_COVERAGE}" >> "$GITHUB_OUTPUT"
    echo "gate-status=${GATE_STATUS}" >> "$GITHUB_OUTPUT"
fi

# Exit code. Default (line-only) matches the org contract exactly. With
# --metrics line+branch, a failing branch also fails the gate.
if [[ "$LINE_PASSES" -eq 1 ]]; then
    echo "::notice::Coverage ${LINE_COVERAGE}% meets threshold ${THRESHOLD}%"
else
    echo "::error::Coverage ${LINE_COVERAGE}% is below threshold ${THRESHOLD}%"
fi

if [[ "$LINE_PASSES" -eq 1 && "$BRANCH_PASSES" -eq 1 ]]; then
    exit 0
fi

if [[ "$BRANCH_PASSES" -eq 0 ]]; then
    echo "::error::Branch coverage ${BRANCH_COVERAGE}% is below threshold ${THRESHOLD}% (--metrics line+branch)"
fi

exit 1
