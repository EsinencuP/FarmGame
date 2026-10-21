from pathlib import Path
import importlib.util, json
import pdfplumber
from PIL import Image

WORK=Path(r'C:/Users/esinencuP/.codex/visualizations/2026/10/12/01a1316d-41a6-7b00-8409-a5110ef7f494/attestation_work')
SKILL=Path(r'C:/Users/esinencuP/.codex/plugins/cache/openai-primary-runtime/documents/26.1011.11113/skills/documents/render_docx.py')
REPORT=Path(r'D:/Evo Web Design/FarmGame/Docs/Attestation/My_Little_Farm_Attestation_Report_Expanded.docx')
spec=importlib.util.spec_from_file_location('docx_renderer',SKILL)
renderer=importlib.util.module_from_spec(spec); spec.loader.exec_module(renderer)
# The packaged converter cannot find LibreOffice on this Windows host.
# Use the verified native Word export as the PDF conversion fallback,
# retaining the packaged renderer's Poppler rasterization and page naming.
renderer.convert_to_pdf=lambda *args,**kwargs:(str(WORK/'report.pdf'),'Native Microsoft Word PDF export')
paths=renderer.rasterize(str(REPORT),str(WORK/'render'),150,False,False)
pages=[]
with pdfplumber.open(WORK/'report.pdf') as pdf:
    for i,page in enumerate(pdf.pages):
        text=page.extract_text() or ''
        chars=page.chars
        pages.append({'page':i+1,'chars':len(text),'start':text[:90],'end':text[-110:],
                      'min_x':min((c['x0'] for c in chars),default=0),
                      'max_x':max((c['x1'] for c in chars),default=0),
                      'max_bottom':max((c['bottom'] for c in chars),default=0),
                      'width':page.width,'height':page.height})
(WORK/'page_audit.json').write_text(json.dumps(pages,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'pages':len(paths),'short_pages':[p for p in pages if p['chars']<500],
                  'horizontal_overflow':[p for p in pages if p['min_x']<45 or p['max_x']>page.width-40]},ensure_ascii=False))
