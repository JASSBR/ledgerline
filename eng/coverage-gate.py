#!/usr/bin/env python3
"""Fails CI when production-code line coverage drops below the threshold.

Reads the Cobertura reports written by Microsoft.Testing.Platform, merges hits per line across test
projects, and ignores what is not hand-written production code: tests, AppHost wiring, EF migrations.
Usage: python3 eng/coverage-gate.py [threshold-percent]
"""
import collections
import glob
import sys
import xml.etree.ElementTree as ET

THRESHOLD = float(sys.argv[1]) if len(sys.argv) > 1 else 80.0
EXCLUDED_PACKAGES = ("Tests", "AppHost")
EXCLUDED_PATHS = ("/Migrations/", "/obj/", "/tests/")

reports = glob.glob("**/TestResults/**/*.cobertura.xml", recursive=True)
if not reports:
    sys.exit("No Cobertura report found. Run: dotnet test --coverage --coverage-output-format cobertura")

hits = collections.defaultdict(dict)
for report in reports:
    for package in ET.parse(report).getroot().iter("package"):
        name = package.get("name") or ""
        if any(excluded in name for excluded in EXCLUDED_PACKAGES):
            continue
        for cls in package.iter("class"):
            filename = cls.get("filename") or ""
            if any(excluded in filename for excluded in EXCLUDED_PATHS):
                continue
            lines = hits[(name, filename)]
            for line in cls.iter("line"):
                number, count = int(line.get("number")), int(line.get("hits"))
                lines[number] = max(lines.get(number, 0), count)

per_package = collections.defaultdict(lambda: [0, 0])
for (package, _), lines in hits.items():
    per_package[package][0] += sum(1 for count in lines.values() if count > 0)
    per_package[package][1] += len(lines)

for package, (covered, total) in sorted(per_package.items()):
    print(f"{package:40} {covered:5}/{total:5}  {100 * covered / total:5.1f}%")

covered = sum(c for c, _ in per_package.values())
total = sum(t for _, t in per_package.values())
percent = 100 * covered / total
print(f"{'TOTAL':40} {covered:5}/{total:5}  {percent:5.1f}%  (gate: {THRESHOLD:.0f}%)")
sys.exit(0 if percent >= THRESHOLD else f"Coverage {percent:.1f}% is below the {THRESHOLD:.0f}% gate.")
