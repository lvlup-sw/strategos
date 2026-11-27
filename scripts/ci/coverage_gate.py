"""Check TRX and Cobertura reports for scripts/ci/coverage.sh.

project: the TRX ran and passed at least one test with no failures, and the
         Cobertura report names each assembly once and has measurable lines.
merged:  the merged report covers a Strategos shipping assembly and its exact
         line counters meet the threshold. A rounded line-rate could otherwise
         turn 79.99% green.
"""
import argparse
import sys
import xml.etree.ElementTree as ET


def fail(message):
    sys.exit(f'error: {message}')


def counter(value):
    if value is None or not value.isdigit():
        fail(f'invalid report counter {value!r}')
    return int(value)


def check_trx(path):
    counters = ET.parse(path).getroot().findall('./{*}ResultSummary/{*}Counters')
    if len(counters) != 1:
        fail(f'{path}: TRX requires one result summary')
    values = counters[0].attrib
    executed, passed, total = (counter(values.get(key)) for key in ('executed', 'passed', 'total'))
    if not 0 < executed == passed <= total or counter(values.get('failed')) != 0:
        fail(f'{path}: the project did not execute and pass its selected tests')
    print(f'{path}: {passed} tests passed')


def check_coverage(path, *, allow_empty=False):
    root = ET.parse(path).getroot()
    names = [package.get('name') for package in root.findall('./packages/package')]
    if not names and allow_empty and root.find('./packages') is not None \
            and root.get('lines-valid', '0') == root.get('lines-covered', '0') == '0':
        return root, names
    if not names or not all(names) or len(set(names)) != len(names):
        fail(f'{path}: coverage requires uniquely named assemblies')
    valid, covered = counter(root.get('lines-valid')), counter(root.get('lines-covered'))
    if valid <= 0 or covered > valid:
        fail(f'{path}: coverage has no measurable lines or invalid totals')
    return root, names


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    project = commands.add_parser('project')
    project.add_argument('trx')
    project.add_argument('cobertura')
    project.add_argument('--allow-empty', action='store_true')
    merged = commands.add_parser('merged')
    merged.add_argument('cobertura')
    merged.add_argument('--threshold', type=int, required=True)
    args = parser.parse_args()

    if args.command == 'project':
        check_trx(args.trx)
        check_coverage(args.cobertura, allow_empty=args.allow_empty)
        return
    root, names = check_coverage(args.cobertura)
    if not any(name.startswith('Strategos') and not name.endswith('Tests') for name in names):
        fail('coverage has no Strategos shipping assembly')
    valid, covered = counter(root.get('lines-valid')), counter(root.get('lines-covered'))
    print(f'Aggregate line coverage {covered}/{valid} ({covered * 100 / valid:.2f}%), threshold {args.threshold}%')
    if covered * 100 < valid * args.threshold:
        fail(f'aggregate line coverage {covered}/{valid} is below {args.threshold}%')


if __name__ == '__main__':
    main()
