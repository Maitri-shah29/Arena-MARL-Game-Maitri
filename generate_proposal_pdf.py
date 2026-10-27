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
        self.drawString(54, letter[1] - 36, "ResQ-MARL // Earthquake Search & Rescue Digital Twin Platform")
        self.setFont("Helvetica", 8)
        self.setFillColor(colors.HexColor("#64748B"))
        self.drawRightString(letter[0] - 54, letter[1] - 36, "Final Technical & Academic Proposal")
        self.setStrokeColor(colors.HexColor("#CBD5E1"))
        self.setLineWidth(0.5)
        self.line(54, letter[1] - 42, letter[0] - 54, letter[1] - 42)
        
        # Footer
        page_text = f"Page {self._pageNumber} of {page_count}"
        self.drawRightString(letter[0] - 54, 32, page_text)
        self.drawString(54, 32, "Confidential Project Proposal — Autonomous Disaster Response Testbed")
        self.line(54, 44, letter[0] - 54, 44)
        self.restoreState()

def build_proposal_pdf(filename="Arena_Final_Product_Proposal.pdf"):
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
        fontName='Helvetica-Bold', fontSize=21, leading=25,
        textColor=c_primary, spaceAfter=4
    )
    subtitle_style = ParagraphStyle(
        'DocSubTitle', parent=styles['Normal'],
        fontName='Helvetica', fontSize=11.5, leading=15,
        textColor=c_accent, spaceAfter=10
    )
    meta_style = ParagraphStyle(
        'DocMeta', parent=styles['Normal'],
        fontName='Helvetica', fontSize=8.5, leading=12,
        textColor=c_muted, spaceAfter=14
    )
    h1_style = ParagraphStyle(
        'H1', parent=styles['Normal'],
        fontName='Helvetica-Bold', fontSize=13, leading=16,
        textColor=c_primary, spaceBefore=12, spaceAfter=5, keepWithNext=True
    )
    body_style = ParagraphStyle(
        'Body', parent=styles['Normal'],
        fontName='Helvetica', fontSize=8.5, leading=12.5,
        textColor=c_text, spaceAfter=5
    )
    bullet_style = ParagraphStyle(
        'Bullet', parent=body_style,
        leftIndent=14, firstLineIndent=-10, spaceAfter=3
    )
    callout_style = ParagraphStyle(
        'Callout', parent=styles['Normal'],
        fontName='Helvetica-Oblique', fontSize=8.5, leading=12.5,
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

    # Title & Metadata
    story.append(Paragraph("ResQ-MARL: URBAN SEARCH & RESCUE DIGITAL TWIN", title_style))
    story.append(Paragraph("An Autonomous Multi-Agent RL Simulation for Post-Earthquake Survivor Extraction", subtitle_style))
    story.append(Paragraph("<b>Target Domain:</b> Disaster Robotics & USAR &nbsp;|&nbsp; <b>Framework:</b> Unity ML-Agents &amp; OpenXR &nbsp;|&nbsp; <b>Status:</b> Final Review Proposal", meta_style))

    # Executive Summary Box
    summary_html = (
        "<b>Executive Summary & Academic Value:</b><br/>"
        "Post-earthquake search and rescue in structurally collapsed buildings poses extreme risks to human responders. "
        "This project pivots our Multi-Agent Reinforcement Learning engine into <b>ResQ-MARL</b>: an interactive simulation "
        "and digital twin testbed that trains autonomous robot swarms (UAVs/UGVs) to navigate rubble, detect trapped victims "
        "using thermal sensors, clear passage corridors, and guide survivors back to a designated Safe Triage Zone. "
        "It incorporates a <b>Virtual Incident Command Station</b> with hand-tracking/VR to inspect 3D damage and map real-world facilities."
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

    # Section 1: System Mapping
    story.append(Paragraph("1. Direct System & Codebase Mapping (Game to Disaster Testbed)", h1_style))
    story.append(Paragraph("All existing subsystems map seamlessly to real-world disaster robotics functions without starting from scratch:", body_style))

    mapping_data = [
        [Paragraph("Legacy Game Element", table_header), Paragraph("ResQ-MARL Disaster Function", table_header), Paragraph("Technical & Real-World Justification", table_header)],
        [
            Paragraph("<b>Seekers (AI Agents)</b>", table_cell_bold),
            Paragraph("<b>Autonomous Rescue UAV/UGV Swarm</b>", table_cell),
            Paragraph("Autonomous drones & robotic crawlers trained via PPO to cooperatively sweep occluded ruins.", table_cell)
        ],
        [
            Paragraph("<b>Hiders (AI Agents)</b>", table_cell_bold),
            Paragraph("<b>Trapped Earthquake Victims</b>", table_cell),
            Paragraph("Survivors sheltering under structural pockets awaiting location and extraction.", table_cell)
        ],
        [
            Paragraph("<b>Movable Crates / Blocks</b>", table_cell_bold),
            Paragraph("<b>Structural Debris & Fallen Rubble</b>", table_cell),
            Paragraph("Dynamic obstacles cleared using robotic manipulators (BoxHolding.cs) to open passage corridors.", table_cell)
        ],
        [
            Paragraph("<b>Laser Scanners</b>", table_cell_bold),
            Paragraph("<b>LiDAR & Thermal Life-Sign Sensors</b>", table_cell),
            Paragraph("Infrared line-of-sight sensors detecting survivor thermal signatures through rubble crevices.", table_cell)
        ],
        [
            Paragraph("<b>Architect Mode & Layouts</b>", table_cell_bold),
            Paragraph("<b>Incident Command & Digital Twin Mapper</b>", table_cell),
            Paragraph("Coordinators map real-world building blueprints (PlayerLayout.cs) to run pre-mission sweep simulations.", table_cell)
        ]
    ]
    t_mapping = Table(mapping_data, colWidths=[110, 164, 230])
    t_mapping.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), c_primary),
        ('GRID', (0, 0), (-1, -1), 0.5, c_border),
        ('TOPPADDING', (0, 0), (-1, -1), 4),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 4),
        ('LEFTPADDING', (0, 0), (-1, -1), 6),
        ('RIGHTPADDING', (0, 0), (-1, -1), 6),
    ]))
    story.append(t_mapping)
    story.append(Spacer(1, 8))

    # Section 2: VR & Hand Movement
    story.append(Paragraph("2. Virtual Incident Command Station (VR & Hand Tracking)", h1_style))
    story.append(Paragraph("Directly addressing the review committee's feedback regarding hands-on interaction and VR immersion:", body_style))
    story.append(Paragraph("• <b>Natural Gesture Debris Manipulation:</b> Using a standard laptop webcam with MediaPipe (or VR hand controllers), the human disaster coordinator uses pinch and grasp gestures to pick up, clear, and stabilize virtual debris in real time.", bullet_style))
    story.append(Paragraph("• <b>Hazard Tagging & Waypointing:</b> The operator points to mark structural collapse zones or assign tactical patrol waypoints for the autonomous swarm to prioritize.", bullet_style))
    story.append(Paragraph("• <b>Immersive 1:1 Scale Damage Survey:</b> Responders can enter a first-person VR perspective to inspect structural damage safely before human teams enter the physical site.", bullet_style))
    story.append(Spacer(1, 8))

    # Section 3: Safe Triage & Sim-to-Real
    story.append(Paragraph("3. Safe Triage Zone & Real-World Digital Twin Workflow", h1_style))
    story.append(Paragraph("The platform introduces two vital end-to-end operational capabilities:", body_style))
    story.append(Paragraph("• <b>Victim Extraction to Safe Triage Base:</b> Upon locating a victim, the rescue robot establishes an evacuation corridor and guides the survivor back to a designated green Triage Base, shifting the mission objective from simple detection to complete survival extraction.", bullet_style))
    story.append(Paragraph("• <b>Real-Life Facility Import (Sim-to-Real):</b> By importing floor plans via JSON (already implemented in PlayerLayout.cs), rescue teams can digitize real earthquake-damaged facilities (schools, hospitals, offices) and evaluate multiple swarm search policies prior to deployment.", bullet_style))
    story.append(Spacer(1, 8))

    # Section 4: Operational Metrics
    story.append(Paragraph("4. Quantitative Mission & Safety Evaluation Metrics", h1_style))
    story.append(Paragraph("The system replaces gaming scores with objective disaster response metrics exported to CSV:", body_style))

    metrics_data = [
        [Paragraph("Metric", table_header), Paragraph("Definition", table_header), Paragraph("Operational Impact", table_header)],
        [Paragraph("<b>Mean Time to Rescue (MTTR)</b>", table_cell_bold), Paragraph("Average elapsed seconds to detect and extract survivors.", table_cell), Paragraph("Critical for maximizing survival within the 72-hour Golden Window.", table_cell)],
        [Paragraph("<b>Swarm Coverage Rate (%)</b>", table_cell_bold), Paragraph("Percentage of the building surveyed by thermal sensors.", table_cell), Paragraph("Ensures zero uninspected rooms or dead zones remain.", table_cell)],
        [Paragraph("<b>Blind-Spot Void Density</b>", table_cell_bold), Paragraph("Unexplored rubble pockets displayed on the floor heatmap.", table_cell), Paragraph("Directs human secondary search teams to high-risk shadowed voids.", table_cell)],
        [Paragraph("<b>Debris Obstruction Factor</b>", table_cell_bold), Paragraph("Delay inflicted by collapsed beams and rubble.", table_cell), Paragraph("Evaluates whether robotic clearing or alternative routing is optimal.", table_cell)]
    ]
    t_metrics = Table(metrics_data, colWidths=[130, 184, 190])
    t_metrics.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), c_secondary),
        ('GRID', (0, 0), (-1, -1), 0.5, c_border),
        ('TOPPADDING', (0, 0), (-1, -1), 4),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 4),
        ('LEFTPADDING', (0, 0), (-1, -1), 6),
        ('RIGHTPADDING', (0, 0), (-1, -1), 6),
    ]))
    story.append(t_metrics)
    story.append(Spacer(1, 8))

    # Section 5: Demo Script
    story.append(Paragraph("5. The 5-Minute Professor Review Script", h1_style))
    story.append(Paragraph("A structured oral and visual walkthrough engineered for maximum marks during the final defense:", body_style))
    story.append(Paragraph("<b>Minute 1: The Problem & Humanitarian Need:</b> Frame post-earthquake structural hazards and introduce ResQ-MARL as an autonomous multi-agent simulation testbed and digital twin.", bullet_style))
    story.append(Paragraph("<b>Minute 2: Incident Command & Debris Setup:</b> Load the 'Collapsed Building' scenario. Demonstrate real-time debris manipulation and hazard zone marking.", bullet_style))
    story.append(Paragraph("<b>Minute 3: Thermal / LiDAR Swarm Sweep:</b> Launch simulation. Press 'H' to reveal the live floor heatmap demonstrating dynamic thermal coverage and dead-zone identification.", bullet_style))
    story.append(Paragraph("<b>Minute 4: Multi-Agent Policy Comparison:</b> Compare naive baseline search against our 200M self-play PPO policy, showing emergent sector division and cooperative exploration.", bullet_style))
    story.append(Paragraph("<b>Minute 5: Extraction to Safe Base & Telemetry Export:</b> Guide victim into the Safe Triage Base. Click 'Export Audit Report' to display the generated CSV dossier containing MTTR and coverage statistics.", bullet_style))

    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"ResQ-MARL Proposal PDF generated successfully: {filename}")

if __name__ == "__main__":
    build_proposal_pdf()
