from pathlib import Path
path = Path(r"c:/Temp/ReportFW/ReportFW/DOA/Views/Editor/Index.cshtml")
text = path.read_text(encoding="utf-8")
text = text.replace('.row-style-match').replace('???','??')
