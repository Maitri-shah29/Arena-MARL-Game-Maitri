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
        self.drawString(54, letter[1] - 36, "ResQ-MARL // Disaster Rescue Transformation Blueprint")
        self.setFont("Helvetica", 8)
        self.setFillColor(colors.HexColor("#64748B"))
        self.drawRightString(letter[0] - 54, letter[1] - 36, "Complete AI Behavior & Obstacle Engineering Guide")
        self.setStrokeColor(colors.HexColor("#CBD5E1"))
        self.setLineWidth(0.5)
        self.line(54, letter[1] - 42, letter[0] - 54, letter[1] - 42)
        
        # Footer
        page_text = f"Page {self._pageNumber} of {page_count}"
        self.drawRightString(letter[0] - 54, 32, page_text)
        self.drawString(54, 32, "Confidential Implementation Blueprint — ResQ-MARL Development Team")
        self.line(54, 44, letter[0] - 54, 44)
        self.restoreState()

def build_blueprint_pdf(filename="RESQ_MARL_DISASTER_RESCUE_BLUEPRINT.pdf"):
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
        fontName='Helvetica-Bold', fontSize=12, leading=15,
        textColor=c_primary, spaceBefore=10, spaceAfter=4, keepWithNext=True
    )
    body_style = ParagraphStyle(
        'Body', parent=styles['Normal'],
        fontName='Helvetica', fontSize=8.5, leading=12,
        textColor=c_text, spaceAfter=4
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
    story.append(Paragraph("ResQ-MARL: DISASTER RESCUE BLUEPRINT", title_style))
    story.append(Paragraph("Hierarchical AI Behaviors, Earthquake Obstacles, Aesthetics & Team Action Plan", subtitle_style))
    story.append(Paragraph("<b>Target Scenario:</b> Post-Earthquake Urban Search & Rescue (USAR) &nbsp;|&nbsp; <b>Framework:</b> Unity C# + ML-Agents &nbsp;|&nbsp; <b>Status:</b> Complete Blueprint", meta_style))

    # Executive Summary Box
    summary_html = (
        "<b>Executive Blueprint Summary:</b><br/>"
        "This master document provides the practical implementation guide to transform Arena into an <b>Autonomous Disaster Response & USAR Testbed</b>. "
        "It introduces <b>Hierarchical AI</b> (ML-Agents locomotion + C# rescue state machine) so survivors follow drones to safety without retraining models, "
        "specifies <b>4 realistic earthquake debris types</b> (Fallen Pillars, Tilted Slabs, Rubble Mounds, Barricades), delivers an emergency visual makeover, "
        "and partitions the work into clear responsibilities for a 3-person team."
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
    story.append(Spacer(1, 6))

    # Section 1: System Mapping
    story.append(Paragraph("1. Direct Codebase Mapping: Entertainment Game to Disaster System", h1_style))
    mapping_data = [
        [Paragraph("Codebase Script", table_header), Paragraph("Previous Game Role", table_header), Paragraph("New Disaster Rescue Role", table_header), Paragraph("Key Code Action", table_header)],
        [Paragraph("HideAndSeekAgent.cs", table_cell_bold), Paragraph("Seekers hunting hiders", table_cell), Paragraph("Autonomous Rescue Swarm", table_cell), Paragraph("Drones sweep separate sectors to avoid dogpiling.", table_cell)],
        [Paragraph("AgentActions.cs", table_cell_bold), Paragraph("Hiders freeze on tag", table_cell), Paragraph("Trapped Survivors", table_cell), Paragraph("Add states: Trapped -> Escorting -> Rescued.", table_cell)],
        [Paragraph("GameController.cs", table_cell_bold), Paragraph("Check tag count", table_cell), Paragraph("Mission Controller", table_cell), Paragraph("Trigger win when all victims reach Safe Triage Zone.", table_cell)],
        [Paragraph("BoxHolding.cs", table_cell_bold), Paragraph("Grab/lock boxes", table_cell), Paragraph("Debris Manipulator", table_cell), Paragraph("Robots push/clear fallen beams to open paths.", table_cell)],
        [Paragraph("ArchitectManager.cs", table_cell_bold), Paragraph("Spawn walls & boxes", table_cell), Paragraph("Site Damage Mapper", table_cell), Paragraph("Add tools for Pillars, Tilted Slabs, and Rubble.", table_cell)],
        [Paragraph("ArchitectUI.cs", table_cell_bold), Paragraph("Build HUD", table_cell), Paragraph("Incident Command HUD", table_cell), Paragraph("Show Victims Located and Evacuated counters.", table_cell)]
    ]
    t_map = Table(mapping_data, colWidths=[105, 110, 125, 164])
    t_map.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), c_primary),
        ('GRID', (0, 0), (-1, -1), 0.5, c_border),
        ('TOPPADDING', (0, 0), (-1, -1), 3),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 3),
        ('LEFTPADDING', (0, 0), (-1, -1), 5),
        ('RIGHTPADDING', (0, 0), (-1, -1), 5),
    ]))
    story.append(t_map)
    story.append(Spacer(1, 6))

    # Section 2: Hierarchical AI
    story.append(Paragraph("2. Hierarchical AI: Changing AI Behavior Without Retraining", h1_style))
    story.append(Paragraph("• <b>Low-Level Locomotion Layer:</b> Existing trained ONNX neural networks handle continuous physics movement, room navigation, and obstacle avoidance.", bullet_style))
    story.append(Paragraph("• <b>High-Level Mission State Machine (C#):</b> Overrides agent behavior based on mission events:", bullet_style))
    story.append(Paragraph("  - <b>State 1: Trapped / Sheltered:</b> Survivors shelter under debris using trained hiding weights.", bullet_style))
    story.append(Paragraph("  - <b>State 2: Being Escorted:</b> When a rescue drone arrives within 1.2m, the survivor stops running away and follows the drone toward the Safe Triage Zone.", bullet_style))
    story.append(Paragraph("  - <b>State 3: Safely Evacuated:</b> Upon touching the green Safe Zone collider, the survivor freezes, turns green, and registers in the mission triage counter.", bullet_style))
    story.append(Paragraph("  - <b>Swarm Sector Retasking:</b> When Drone A claims Victim 1, Drones B & C divert to unexplored rooms, eliminating redundant chasing.", bullet_style))
    story.append(Spacer(1, 6))

    # Section 3: 4 Earthquake Obstacles
    story.append(Paragraph("3. Four Realistic Earthquake Debris Obstacles", h1_style))
    obs_data = [
        [Paragraph("Obstacle Type", table_header), Paragraph("Unity Construction", table_header), Paragraph("Dimensions / Scale", table_header), Paragraph("Disaster Rescue Role", table_header)],
        [Paragraph("<b>1. Collapsed Pillar</b>", table_cell_bold), Paragraph("Stretched Cylinder or heavy Cube", table_cell), Paragraph("(0.8, 0.8, 5.0) laying horizontally", table_cell), Paragraph("Heavy fallen structural column blocking doorway.", table_cell)],
        [Paragraph("<b>2. Tilted Slab / Ramp</b>", table_cell_bold), Paragraph("Use existing Ramp.prefab", table_cell), Paragraph("(2.5, 1.2, 4.0) angled at 25-35°", table_cell), Paragraph("Broken ceiling slab forming crawlspace for victims.", table_cell)],
        [Paragraph("<b>3. Rubble Mound</b>", table_cell_bold), Paragraph("Compound of 3-4 rotated concrete cubes", table_cell), Paragraph("(1.5, 0.7, 1.8) irregular pile", table_cell), Paragraph("Fractured concrete heap blocking rover vision.", table_cell)],
        [Paragraph("<b>4. Hazard Barricade</b>", table_cell_bold), Paragraph("Thin wall with yellow/black hazard tape", table_cell), Paragraph("(3.0, 1.2, 0.2) partition", table_cell), Paragraph("Emergency barrier placed to seal dangerous zones.", table_cell)]
    ]
    t_obs = Table(obs_data, colWidths=[110, 124, 130, 140])
    t_obs.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), c_secondary),
        ('GRID', (0, 0), (-1, -1), 0.5, c_border),
        ('TOPPADDING', (0, 0), (-1, -1), 3),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 3),
        ('LEFTPADDING', (0, 0), (-1, -1), 5),
        ('RIGHTPADDING', (0, 0), (-1, -1), 5),
    ]))
    story.append(t_obs)
    story.append(Spacer(1, 6))

    # Section 4: 3-Person Work Division
    story.append(Paragraph("4. Three-Person Role Division & Work Plan", h1_style))
    team_data = [
        [Paragraph("Member", table_header), Paragraph("Core Focus", table_header), Paragraph("Exact Implementation Tasks", table_header)],
        [
            Paragraph("<b>Person 1</b><br/>AI & Rescue Lead", table_cell_bold),
            Paragraph("<b>Triage Zone & Escort AI</b>", table_cell),
            Paragraph("• Add SafeTriageZone.cs trigger to arena floor.<br/>• Attach SurvivorRescueState.cs to hiders to enable follow/escort logic.<br/>• Update GameController.cs win condition to all victims evacuated.", table_cell)
        ],
        [
            Paragraph("<b>Person 2</b><br/>3D Debris Lead", table_cell_bold),
            Paragraph("<b>Earthquake Debris & Arena</b>", table_cell),
            Paragraph("• Build Pillar, Slab/Ramp, Rubble Mound, and Barricade prefabs.<br/>• Add new debris tools (keys 1-5) into ArchitectManager.cs.<br/>• Build 1 ready-to-demo Collapsed Building layout preset.", table_cell)
        ],
        [
            Paragraph("<b>Person 3</b><br/>Visual & HUD Lead", table_cell_bold),
            Paragraph("<b>Visual Reskin & Mission HUD</b>", table_cell),
            Paragraph("• Reskin drones (Hazard Orange + flashing beacon) and survivors (distress pulse).<br/>• Redesign ArchitectUI.cs into Incident Command HUD with live triage counts.<br/>• Retexture arena floor to cracked concrete with green Safe Base perimeter.", table_cell)
        ]
    ]
    t_team = Table(team_data, colWidths=[100, 120, 284])
    t_team.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), c_primary),
        ('GRID', (0, 0), (-1, -1), 0.5, c_border),
        ('TOPPADDING', (0, 0), (-1, -1), 4),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 4),
        ('LEFTPADDING', (0, 0), (-1, -1), 5),
        ('RIGHTPADDING', (0, 0), (-1, -1), 5),
    ]))
    story.append(t_team)
    story.append(Spacer(1, 6))

    # Section 5: Implementation Sequence
    story.append(Paragraph("5. Step-by-Step Implementation Timeline", h1_style))
    story.append(Paragraph("<b>Day 1 (AI & Safe Zone):</b> Add SafeTriageZone.cs and SurvivorRescueState.cs. Test that survivors follow drones to safety upon touch.", bullet_style))
    story.append(Paragraph("<b>Day 2 (Debris Prefabs):</b> Create the 4 earthquake debris prefabs and hook them into ArchitectManager.cs tools 1-5.", bullet_style))
    story.append(Paragraph("<b>Day 3 (Aesthetics & HUD):</b> Apply orange drone materials, searchlight scanner, survivor badges, and emergency HUD counters.", bullet_style))
    story.append(Paragraph("<b>Day 4 (Integration & Rehearsal):</b> Load the collapsed building preset and rehearse the 5-minute defense demo.", bullet_style))

    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"Disaster Rescue Blueprint PDF generated successfully: {filename}")

if __name__ == "__main__":
    build_blueprint_pdf()
