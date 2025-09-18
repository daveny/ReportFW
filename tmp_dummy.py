from pathlib import Path
path = Path(r"c:/Temp/ReportFW/ReportFW/DOA/Views/Editor/Index.cshtml")
text = path.read_text(encoding="utf-8")
marker = "  valueColorPairs.appendChild(row);\n  }\n\n  btnAddColorPair"
if marker not in text:
    raise SystemExit('marker not found')
new_lines = [
  "  valueColorPairs.appendChild(row;)"]
