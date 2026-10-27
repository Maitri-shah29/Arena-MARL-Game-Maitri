import os
import sys
from reportlab.lib.pagesizes import letter
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether, HRFlowable
)
from reportlab.pdfgen import canvas

class NumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            if self._pageNumber > 1:
                self.draw_header_footer(num_pages)
            canvas.Canvas.showPage(self)
        canvas.Canvas.save(self)

    def draw_header_footer(self, page_count):
        self.saveState()
        self.setFont("Helvetica-Bold", 8)
        self.setFillColor(colors.HexColor("#0284C7"))
        self.drawString(54, letter[1] - 36, "ResQ-MARL // Project Pivot Master Plan & Team Roadmap")
        self.setFont("Helvetica", 8)
        self.setFillColor(colors.HexColor("#64748B"))
        self.drawRightString(letter[0] - 54, letter[1] - 36, "3-Person Role Division & Implementation Guide")
        self.setStrokeColor(colors.HexColor("#CBD5E1"))
        self.setLineWidth(0.5)
        self.line(54, letter[1] - 42, letter[0] - 54, letter[1] - 42)
        
        # Footer
        page_text = f"Page {self._pageNumber} of {page_count}"
        self.drawRightString(letter[0] - 54, 32, page_text)
        self.drawString(54, 32, "Confidential Project Master Plan — ResQ-MARL Development Team")
        self.line(54, 44, letter[0] - 54, 44)
        self.restoreState()

def build_pivot_plan_pdf(filename="ResQ_MARL_Project_Pivot_Plan.pdf"):
    doc = SimpleDocTemplate(
        filename,
        pagesize=letter,
        leftMargin=54,
        rightMargin=54,
        topMargin=54,
        bottomMargin=54
    )

    styles = getSampleStyleSheet()

    c_primary = colors.HexColor("#0F172A")    # Deep Slate
    c_accent = colors.HexColor("#0284C7")     # Sky Blue
    c_secondary = colors.HexColor("#0369A1")  # Darker Sky
    c_text = colors.HexColor("#334155")       # Slate text
    c_muted = colors.HexColor("#64748B")      # Muted slate
    c_card_bg = colors.HexColor("#F8FAFC")    # Cool light gray
    c_border = colors.HexColor("#E2E8F0")     # Light border

    title_style = ParagraphStyle(
        'DocTitle', parent=styles['Normal'],
        fontName='Helvetica-Bold', fontSize=20, leading=24,
        textColor=c_primary, spaceAfter=4
    )
    subtitle_style = ParagraphStyle(
        'DocSubTitle', parent=styles['Normal'],
        fontName='Helvetica', fontSize=11, leading=15,
        textColor=c_accent, spaceAfter=10
    )
    meta_style = ParagraphStyle(
        'DocMeta', parent=styles['Normal'],
        fontName='Helvetica', fontSize=8.5, leading=12,
        textColor=c_muted, spaceAfter=14
    )
    h1_style = ParagraphStyle(
        'H1', parent=styles['Normal'],
        fontName='Helvetica-Bold', fontSize=12.5, leading=15.5,
        textColor=c_primary, spaceBefore=11, spaceAfter=5, keepWithNext=True
    )
    body_style = ParagraphStyle(
        'Body', parent=styles['Normal'],
        fontName='Helvetica', fontSize=8.5, leading=12,
        textColor=c_text, spaceAfter=5
    )
    bullet_style = ParagraphStyle(
        'Bullet', parent=body_style,
        leftIndent=12, firstLineIndent=-8, spaceAfter=3
    )
    callout_style = ParagraphStyle(
        'Callout', parent=styles['Normal'],
        fontName='Helvetica-Oblique', fontSize=8.5, leading=12,
        textColor=colors.HexColor("#1E293B")
    )
    table_header = ParagraphStyle(
        'TableHeader', parent=styles['Normal'],
        fontName='Helvetica-Bold', fontSize=8, leading=11,
        textColor=colors.white
    )
    table_cell = ParagraphStyle(
        'TableCell', parent=styles['Normal'],
        fontName='Helvetica', fontSize=7.5, leading=10.5,
        textColor=c_text
    )
    table_cell_bold = ParagraphStyle(
        'TableCellBold', parent=styles['Normal'],
        fontName='Helvetica-Bold', fontSize=7.5, leading=10.5,
        textColor=c_primary
    )

    story = []

    # Title & Header
    story.append(Paragraph("ResQ-MARL: PROJECT PIVOT MASTER PLAN", title_style))
    story.append(Paragraph("From Entertainment Game to Autonomous Disaster Response & USAR Testbed", subtitle_style))
    story.append(Paragraph("<b>Scope:</b> 3-Person Team Division, 3-Phase Timeline, and Feature Inventory &nbsp;|&nbsp; <b>Status:</b> Official Execution Plan", meta_style))

    # Executive Summary Box
    summary_html = (
        "<b>The Pivot Mission:</b> Repositioning our Multi-Agent Reinforcement Learning engine into <b>ResQ-MARL</b>, "
        "an interactive simulation and digital twin platform that trains autonomous robotic swarms (UAVs/UGVs) to navigate "
        "collapsed earthquake rubble, detect trapped victims via simulated thermal sensors, clear dynamic debris, and escort "
        "survivors to a designated Safe Triage Zone. Enables human incident commanders to survey sites and clear debris using "
        "<b>VR and touchless webcam hand tracking</b>."
    )
    t_summary = Table([[Paragraph(summary_html, callout_style)]], colWidths=[504])
    t_summary.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, -1), c_card_bg),
        ('BOX', (0, 0), (-1, -1), 1, c_accent),
        ('TOPPADDING', (0, 0), (-1, -1), 6),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 6),
        ('LEFTPADDING', (0, 0), (-1, -1), 10),
        ('RIGHTPADDING', (0, 0), (-1, -1), 10),
    ]))
    story.append(t_summary)
    story.append(Spacer(1, 8))

    # Section 1: 3-Person Division
    story.append(Paragraph("1. Work Breakdown: 3-Person Team Division", h1_style))
    story.append(Paragraph("Responsibilities are partitioned into 3 independent streams so all members can develop simultaneously:", body_style))

    roles_data = [
        [Paragraph("Team Member", table_header), Paragraph("Key Technical Responsibilities", table_header), Paragraph("Target Deliverables", table_header)],
        [
            Paragraph("<b>Person 1: Backend, AI & Simulation Lead</b>", table_cell_bold),
            Paragraph("• Implement Safe Triage Zone trigger & extraction routing.<br/>• Refactor capture rewards into rescue & survival milestone rewards.<br/>• Build automated 10-trial batch test runner in GenerationComparer.cs.", table_cell),
            Paragraph("Safe Triage Zone logic, survivor extraction state loop, and batch benchmark harness.", table_cell)
        ],
        [
            Paragraph("<b>Person 2: Spatial Analytics & Hand Tracking</b>", table_cell_bold),
            Paragraph("• Build 30x30 thermal/LiDAR floor heatmap (SpatialThermalHeatmap.cs).<br/>• Develop Python MediaPipe webcam hand-tracking script.<br/>• Implement Unity UDP gesture receiver for touchless debris movement.", table_cell),
            Paragraph("Live thermal coverage heatmap (key 'H') and touchless webcam hand controller.", table_cell)
        ],
        [
            Paragraph("<b>Person 3: UI, Presets & Mission Exporter</b>", table_cell_bold),
            Paragraph("• Reskin HUD into Emergency Incident Command Post.<br/>• Create 3 disaster blueprints (School, Corridor Chokepoint, Rubble Field).<br/>• Build one-click CSV mission telemetry exporter.", table_cell),
            Paragraph("Disaster Incident HUD, 3 realistic collapsed presets, and CSV mission data exporter.", table_cell)
        ]
    ]
    t_roles = Table(roles_data, colWidths=[120, 204, 180])
    t_roles.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), c_primary),
        ('GRID', (0, 0), (-1, -1), 0.5, c_border),
        ('TOPPADDING', (0, 0), (-1, -1), 4),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 4),
        ('LEFTPADDING', (0, 0), (-1, -1), 6),
        ('RIGHTPADDING', (0, 0), (-1, -1), 6),
    ]))
    story.append(t_roles)
    story.append(Spacer(1, 8))

    # Section 2: 3-Phase Execution
    story.append(Paragraph("2. Three-Phase Execution Roadmap", h1_style))
    story.append(Paragraph("<b>Phase 1: Logic & Triage Loop (Days 1–2):</b> Add the green Safe Triage Zone to the arena floor, update GameController.cs to tag victims as 'Rescued' and guide them to safety, and ensure ML-Agents models continue running smoothly.", bullet_style))
    story.append(Paragraph("<b>Phase 2: Heatmap, Gestures & Presets (Days 3–4):</b> Implement the 30x30 thermal sensor floor heatmap (toggleable with key 'H'). Deploy the Python webcam hand-tracking bridge to allow pinch-based debris manipulation. Configure the 3 collapsed building presets.", bullet_style))
    story.append(Paragraph("<b>Phase 3: Visual Polish & Defense Rehearsal (Day 5):</b> Reskin UI into an Emergency Command Post, connect one-click CSV mission telemetry export (logging MTTR and sweep clearance rate), and rehearse the 5-minute review presentation.", bullet_style))
    story.append(Spacer(1, 8))

    # Section 3: Feature Inventory (Add vs Remove)
    story.append(Paragraph("3. Feature Inventory: What to Add vs. What to Remove/Rename", h1_style))

    inventory_data = [
        [Paragraph("Category", table_header), Paragraph("Item", table_header), Paragraph("Action / Disaster Transformation", table_header)],
        [Paragraph("<b>New Feature</b>", table_cell_bold), Paragraph("Safe Triage Zone", table_cell), Paragraph("Designated recovery base where rescued victims are guided and secured.", table_cell)],
        [Paragraph("<b>New Feature</b>", table_cell_bold), Paragraph("Thermal Floor Heatmap", table_cell), Paragraph("Floor grid highlighting scanned rooms vs dangerous unexplored voids ('H' key).", table_cell)],
        [Paragraph("<b>New Feature</b>", table_cell_bold), Paragraph("Webcam Hand Controller", table_cell), Paragraph("Touchless hand gestures to pinch, lift, and clear virtual rubble via MediaPipe.", table_cell)],
        [Paragraph("<b>New Feature</b>", table_cell_bold), Paragraph("Disaster Presets", table_cell), Paragraph("Instant loading of collapsed school, blocked corridor, and rubble scenarios.", table_cell)],
        [Paragraph("<b>To Remove / Rename</b>", table_cell_bold), Paragraph("'Hide and Seek' Labels", table_cell), Paragraph("Replace with 'Urban Search and Rescue (USAR) Simulation & Digital Twin'.", table_cell)],
        [Paragraph("<b>To Remove / Rename</b>", table_cell_bold), Paragraph("'Hiders' & 'Seekers'", table_cell), Paragraph("Rename to 'Trapped Survivors' and 'Autonomous Rescue Swarm (UAVs/UGVs)'.", table_cell)],
        [Paragraph("<b>To Remove / Rename</b>", table_cell_bold), Paragraph("'Capture' / 'Demolish'", table_cell), Paragraph("Rename to 'Survivor Secured' and 'Heavy Excavation / Debris Removal'.", table_cell)]
    ]
    t_inv = Table(inventory_data, colWidths=[90, 150, 264])
    t_inv.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), c_secondary),
        ('GRID', (0, 0), (-1, -1), 0.5, c_border),
        ('TOPPADDING', (0, 0), (-1, -1), 4),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 4),
        ('LEFTPADDING', (0, 0), (-1, -1), 6),
        ('RIGHTPADDING', (0, 0), (-1, -1), 6),
    ]))
    story.append(t_inv)
    story.append(Spacer(1, 8))

    # Section 4: Visual Changes & Aesthetics
    story.append(Paragraph("4. Recommended Visual & Aesthetic Changes", h1_style))
    story.append(Paragraph("• <b>Rescue Drones (Seekers):</b> High-visibility hazard amber body rings with warm orange/amber thermal vision cones.", bullet_style))
    story.append(Paragraph("• <b>Trapped Survivors (Hiders):</b> Pulsing cyan life-sign rings with overhead status badges: [TRAPPED] -> [EXTRACTING] -> [SAFE].", bullet_style))
    story.append(Paragraph("• <b>Debris Blocks:</b> Retextured with cracked concrete, broken masonry, and hazard warning textures.", bullet_style))
    story.append(Paragraph("• <b>Safe Triage Base:</b> Illuminated green perimeter box with an emergency medical/hazard symbol on the floor.", bullet_style))
    story.append(Paragraph("• <b>Incident Command HUD:</b> High-contrast emergency response theme (Tactical Slate, Amber, Green) displaying real-time MTTR and survivor triage counters.", bullet_style))

    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"Pivot Plan PDF generated successfully: {filename}")

if __name__ == "__main__":
    build_pivot_plan_pdf()
