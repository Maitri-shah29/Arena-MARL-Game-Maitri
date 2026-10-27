import os
import sys
from reportlab.lib.pagesizes import letter
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch
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
        self.setFont("Helvetica", 8)
        self.setFillColor(colors.HexColor("#64748B"))
        
        # Header
        self.drawString(54, letter[1] - 36, "ARENA // Multi-Agent Reinforcement Learning & PPO Architecture Guide")
        self.setStrokeColor(colors.HexColor("#CBD5E1"))
        self.setLineWidth(0.5)
        self.line(54, letter[1] - 42, letter[0] - 54, letter[1] - 42)
        
        # Footer
        page_text = f"Page {self._pageNumber} of {page_count}"
        self.drawRightString(letter[0] - 54, 32, page_text)
        self.drawString(54, 32, "Confidential & Technical Documentation — Arena MARL Game System")
        self.line(54, 44, letter[0] - 54, 44)
        self.restoreState()

def build_pdf(filename="Arena_MARL_and_PPO_Guide.pdf"):
    doc = SimpleDocTemplate(
        filename,
        pagesize=letter,
        leftMargin=54,
        rightMargin=54,
        topMargin=54,
        bottomMargin=54
    )

    styles = getSampleStyleSheet()

    # Custom palette
    c_primary = colors.HexColor("#0F172A")    # Deep Slate
    c_accent = colors.HexColor("#0284C7")     # Sky Blue
    c_secondary = colors.HexColor("#0369A1")  # Darker Sky
    c_text = colors.HexColor("#334155")       # Slate text
    c_muted = colors.HexColor("#64748B")      # Muted slate
    c_card_bg = colors.HexColor("#F8FAFC")    # Cool light gray
    c_border = colors.HexColor("#E2E8F0")     # Light border
    c_highlight = colors.HexColor("#F0FDF4")  # Pale green tint
    c_warning = colors.HexColor("#FFFBEB")    # Pale amber tint

    # Typography Styles
    title_style = ParagraphStyle(
        'DocTitle',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=26,
        leading=32,
        textColor=c_primary,
        spaceAfter=6
    )

    subtitle_style = ParagraphStyle(
        'DocSubTitle',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=13,
        leading=18,
        textColor=c_accent,
        spaceAfter=15
    )

    meta_style = ParagraphStyle(
        'DocMeta',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=9,
        leading=13,
        textColor=c_muted,
        spaceAfter=25
    )

    h1_style = ParagraphStyle(
        'SectionH1',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=16,
        leading=21,
        textColor=c_primary,
        spaceBefore=14,
        spaceAfter=8,
        keepWithNext=True
    )

    h2_style = ParagraphStyle(
        'SectionH2',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=12,
        leading=16,
        textColor=c_secondary,
        spaceBefore=10,
        spaceAfter=5,
        keepWithNext=True
    )

    body_style = ParagraphStyle(
        'BodyDark',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=9.5,
        leading=14.5,
        textColor=c_text,
        spaceAfter=8
    )

    bullet_style = ParagraphStyle(
        'BulletText',
        parent=body_style,
        leftIndent=15,
        firstLineIndent=-10,
        spaceAfter=4
    )

    callout_style = ParagraphStyle(
        'CalloutText',
        parent=styles['Normal'],
        fontName='Helvetica-Oblique',
        fontSize=9,
        leading=14,
        textColor=colors.HexColor("#1E293B")
    )

    code_style = ParagraphStyle(
        'CodeText',
        parent=styles['Normal'],
        fontName='Courier',
        fontSize=8.5,
        leading=12,
        textColor=colors.HexColor("#0F172A")
    )

    formula_style = ParagraphStyle(
        'FormulaText',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=11,
        leading=15,
        alignment=1, # Center
        textColor=c_primary
    )

    table_header = ParagraphStyle(
        'TableHeader',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=9,
        leading=12,
        textColor=colors.white
    )

    table_cell = ParagraphStyle(
        'TableCell',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=8.5,
        leading=12,
        textColor=c_text
    )

    table_cell_bold = ParagraphStyle(
        'TableCellBold',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=8.5,
        leading=12,
        textColor=c_primary
    )

    story = []

    # ─────────────────────────────────────────────────────────────────────────
    # COVER / HEADER BLOCK
    # ─────────────────────────────────────────────────────────────────────────
    story.append(Paragraph("ARENA: MULTI-AGENT REINFORCEMENT LEARNING", title_style))
    story.append(Paragraph("From Foundations to Emergent Intelligence: Architecture, Self-Play & Deep PPO Analysis", subtitle_style))
    story.append(Paragraph("<b>Author / Environment:</b> Arena MARL Engine &nbsp;|&nbsp; <b>Framework:</b> Unity ML-Agents &amp; Barracuda &nbsp;|&nbsp; <b>Status:</b> Complete Technical Dossier", meta_style))
    story.append(HRFlowable(width="100%", thickness=1.5, color=c_accent, spaceBefore=0, spaceAfter=14))

    # EXECUTIVE SUMMARY BOX
    summary_html = (
        "<b>Executive Brief:</b> This document provides a comprehensive, end-to-end reference for the <b>Arena</b> project. "
        "It covers the operational game loop, the visual physics engine, the fundamental principles of Reinforcement Learning (RL), "
        "Multi-Agent Reinforcement Learning (MARL), an exhaustive deep dive into <b>Proximal Policy Optimization (PPO)</b>, "
        "the 200-million-step evolutionary history of emergent tactics, and practical methodologies for benchmarking "
        "and visualizing AI progression against conventional scripted game bots."
    )
    summary_p = Paragraph(summary_html, callout_style)
    summary_table = Table([[summary_p]], colWidths=[letter[0]-108])
    summary_table.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), c_card_bg),
        ('BOX', (0,0), (-1,-1), 1, c_border),
        ('PADDING', (0,0), (-1,-1), 10),
    ]))
    story.append(summary_table)
    story.append(Spacer(1, 14))

    # ─────────────────────────────────────────────────────────────────────────
    # SECTION 1: HOW THE GAME WORKS RIGHT NOW
    # ─────────────────────────────────────────────────────────────────────────
    story.append(Paragraph("1. System Architecture & Game Loop", h1_style))
    story.append(Paragraph(
        "<b>Arena</b> is an interactive multi-agent reinforcement learning environment where two adversarial teams—"
        "<b>Hiders (Team 0, Cyan)</b> and <b>Seekers (Team 1, Crimson)</b>—compete on a bounded platform. "
        "Unlike static academic benchmarks, Arena introduces human environment modification and live co-play.",
        body_style
    ))

    story.append(Paragraph("The Three-Phase Gameplay Loop", h2_style))
    story.append(Paragraph("<b>1. The Architect Phase (Build Mode):</b> When a round begins in <code>Test.unity</code>, time is paused (<code>Time.timeScale = 0</code>). The human player acts as the arena architect with an energy budget of 100 points to place barricade walls (40 energy), spawn movable crates (25 energy), demolish existing pieces (35 energy), or pre-lock boxes to a team (20 energy). The resulting layout is persistently serialized to <code>player_layout.json</code>.", bullet_style))
    story.append(Paragraph("<b>2. The Grace Period (Warmup):</b> Once the round is unpaused, a tactical grace period initiates (the first 12% to 40% of the match). Seekers are frozen in place with laser scanners offline. Hiders take advantage of this head start to scramble across the room, push crates, block entryways, and find covert hiding positions.", bullet_style))
    story.append(Paragraph("<b>3. The Active Simulation Phase:</b> Seekers are unchained, deploying dynamic laser scanners and raycast vision cones. Hiders earn continuous positive rewards for remaining occluded from all seeker sightlines. Seekers earn rewards for maintaining line-of-sight and tagging hiders. The round concludes when the time limit expires or all hiders are captured.", bullet_style))
    story.append(Paragraph("<b>4. Player Co-Play (Possession):</b> Pressing <b>Tab</b> allows the player to possess an agent in real time, moving with WASD, grabbing/moving crates with E, and locking/unlocking crates with L/U. The remaining agents continue running their neural network inference policies.", bullet_style))
    story.append(Paragraph("<b>5. Multi-Angle Tactical Cameras:</b> The camera system provides 3 switchable modes: Isometric Tactical (Key 1), Top-Down Radar Overview (Key 2), and Dynamic Chase (Key 3), with full right-mouse orbit and mouse-wheel zoom.", bullet_style))

    story.append(Spacer(1, 8))

    # ─────────────────────────────────────────────────────────────────────────
    # SECTION 2: SCENE LIGHTING & VISUAL SYSTEM FIX
    # ─────────────────────────────────────────────────────────────────────────
    story.append(Paragraph("2. Visual Physics & Scene Lighting Calibration", h1_style))
    story.append(Paragraph(
        "During initial playtesting, the arena appeared excessively dark and murky. An investigation of the Universal Render Pipeline (URP) "
        "and Physically Based Rendering (PBR) assets revealed four critical root causes:",
        body_style
    ))

    story.append(Paragraph("<b>• Metallic Reflection Failure:</b> In PBR shaders, setting <code>Metallic = 0.85</code> eliminates diffuse light, forcing the surface to reflect the surrounding environment. Because the camera background was set to solid near-black, surfaces reflected pure darkness. Lowering Metallic to 0.1 and Smoothness to 0.35 restored standard diffuse light absorption.", bullet_style))
    story.append(Paragraph("<b>• Albedo Base Color Crushing:</b> The floor material base color was configured to a dim navy (0.2, 0.25, 0.35), attenuating texture brightness by 75%. Resetting base colors to (0.55, 0.62, 0.72) made the cybernetic floor tiles crisp and visible.", bullet_style))
    story.append(Paragraph("<b>• Directional & Ambient Starvation:</b> Directional light was dimmed to 0.85 with a cold tint, while ambient light was minimal. Sun intensity was raised to 1.35 with a clean solar spectrum, and ambient probe intensity was boosted to 1.25.", bullet_style))
    story.append(Paragraph("<b>• Post-Processing Contrast & Vignette:</b> A post-processing volume applied high contrast (15) and a heavy vignette (0.38), crushing shadow details into pitch black. Contrast was normalized to 6, exposure lifted to +0.45, and vignette reduced to 0.18.", bullet_style))

    story.append(Spacer(1, 10))

    # ─────────────────────────────────────────────────────────────────────────
    # SECTION 3: WHAT IS REINFORCEMENT LEARNING?
    # ─────────────────────────────────────────────────────────────────────────
    story.append(Paragraph("3. Reinforcement Learning: The Ground Truth", h1_style))
    story.append(Paragraph(
        "Reinforcement Learning (RL) is a computational approach to learning through goal-directed interaction. "
        "Unlike Supervised Learning (which requires labeled datasets of 'correct' moves) or Unsupervised Learning (which clusters unlabeled patterns), "
        "RL agents discover how to behave entirely by taking actions within an environment and observing scalar feedback: <b>rewards</b> and <b>penalties</b>.",
        body_style
    ))

    # RL vs Scripted Table
    rl_table_data = [
        [
            Paragraph("Dimension", table_header),
            Paragraph("Traditional Scripted Game AI", table_header),
            Paragraph("Reinforcement Learning (RL) AI", table_header)
        ],
        [
            Paragraph("<b>Core Logic</b>", table_cell_bold),
            Paragraph("Handcrafted Finite State Machines (FSM), Behavior Trees, or A* NavMesh heuristics.", table_cell),
            Paragraph("Deep Neural Networks mapping high-dimensional sensor data to action probability distributions.", table_cell)
        ],
        [
            Paragraph("<b>Adaptation</b>", table_cell_bold),
            Paragraph("Rigid and predictable. If a player places a wall, NavMesh pathfinding breaks unless re-baked.", table_cell),
            Paragraph("Dynamic generalization. The agent reacts to unexpected geometry using continuous 360° ray sensors.", table_cell)
        ],
        [
            Paragraph("<b>Development</b>", table_cell_bold),
            Paragraph("Developer manually programs every behavior (e.g., 'if seeker near, crouch behind crate').", table_cell),
            Paragraph("Developer designs only the reward function; agents discover complex tactics through trial and error.", table_cell)
        ],
        [
            Paragraph("<b>Discovery</b>", table_cell_bold),
            Paragraph("Zero emergent discovery. The AI cannot do anything the programmer did not anticipate.", table_cell),
            Paragraph("Emergent strategies. Discovers unintended tool use, room sealing, and coordinated team plays.", table_cell)
        ]
    ]
    t_rl = Table(rl_table_data, colWidths=[100, 195, 209])
    t_rl.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), c_primary),
        ('GRID', (0,0), (-1,-1), 0.5, c_border),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, c_card_bg]),
        ('PADDING', (0,0), (-1,-1), 6),
    ]))
    story.append(t_rl)
    story.append(Spacer(1, 10))

    story.append(Paragraph("The Mathematical Formulation: Markov Decision Process (MDP)", h2_style))
    story.append(Paragraph(
        "An RL environment is formalized as a Markov Decision Process defined by the tuple <b>(S, A, P, R, &gamma;)</b>: "
        "<br/>• <b>State Space (S):</b> The state at time <i>t</i>, <i>s<sub>t</sub></i>, contains all relevant platform observations (agent positions, velocities, rotation angles, crate coordinates, team IDs)."
        "<br/>• <b>Action Space (A):</b> Continuous movement vectors (X, Z thrust and yaw rotation) plus discrete branch actions (Grab/Release Box, Lock/Unlock Box)."
        "<br/>• <b>Transition Dynamics (P):</b> Unity PhysX engine computing the physical state of the next step <i>s<sub>t+1</sub></i> given action <i>a<sub>t</sub></i>."
        "<br/>• <b>Reward Function (R):</b> Scalar reward <i>r<sub>t</sub></i> given to agents based on visibility, survival, capture, and out-of-bounds penalties."
        "<br/>• <b>Discount Factor (&gamma; = 0.99):</b> Weights immediate vs. future rewards, prioritizing long-term survival over short-term gains.",
        body_style
    ))

    story.append(Spacer(1, 14))

    # ─────────────────────────────────────────────────────────────────────────
    # SECTION 4: MULTI-AGENT RL & SELF-PLAY
    # ─────────────────────────────────────────────────────────────────────────
    story.append(Paragraph("4. Multi-Agent RL (MARL) & Self-Play Dynamics", h1_style))
    story.append(Paragraph(
        "When multiple autonomous learners operate in the same shared environment, the problem transitions from an MDP to a "
        "<b>Markov Game (Stochastic Game)</b>. This introduces two profound phenomena: <b>Non-Stationarity</b> and <b>Co-Evolution</b>.",
        body_style
    ))

    story.append(Paragraph("The Challenge of Non-Stationarity", h2_style))
    story.append(Paragraph(
        "In single-agent RL, the environment is stationary: a given action in state <i>s</i> always yields predictable reward distributions. "
        "In MARL, because the opponents are simultaneously learning and modifying their neural policies, an action that worked yesterday "
        "(e.g., hiding behind a pillar) fails today because the Seekers learned to flank that pillar. "
        "The ground is constantly moving beneath each agent's feet.",
        body_style
    ))

    story.append(Paragraph("Self-Play & The Competitive Arms Race", h2_style))
    story.append(Paragraph(
        "To achieve robust mastery without human demonstrations, the environment utilizes <b>Self-Play</b>. "
        "Instead of training against a fixed bot, agents train against current and historical versions of themselves. "
        "In the project's training configuration (<code>arena_train.yaml</code>), the self-play engine maintains an Elo rating matrix "
        "(initialized at 1200.0) and periodically swaps opponents with past checkpoints (<code>swap_steps: 2000</code>).",
        body_style
    ))

    # Arms race diagram box
    race_html = (
        "<b>The Self-Play Arms Race Loop:</b><br/>"
        "<b>1. Generation N:</b> Hiders discover they can run away from Seekers &rarr; Hider win-rate climbs.<br/>"
        "<b>2. Generation N+1:</b> Seekers adapt, learning coordinated flanking to corner Hiders &rarr; Seeker win-rate climbs.<br/>"
        "<b>3. Generation N+2:</b> Hiders discover movable crates and block doorways &rarr; Hiders regain dominance.<br/>"
        "<b>4. Generation N+3:</b> Seekers learn to exploit gaps in barricades and rush during the grace period &rarr; Equilibrium shifts."
    )
    race_p = Paragraph(race_html, callout_style)
    race_table = Table([[race_p]], colWidths=[letter[0]-108])
    race_table.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), c_card_bg),
        ('BOX', (0,0), (-1,-1), 1, c_accent),
        ('PADDING', (0,0), (-1,-1), 8),
    ]))
    story.append(race_table)
    story.append(Spacer(1, 12))

    # ─────────────────────────────────────────────────────────────────────────
    # SECTION 5: DEEP DIVE INTO PPO (PROXIMAL POLICY OPTIMIZATION)
    # ─────────────────────────────────────────────────────────────────────────
    story.append(Paragraph("5. Deep Dive: Proximal Policy Optimization (PPO)", h1_style))
    story.append(Paragraph(
        "<b>Proximal Policy Optimization (PPO)</b>, published by Schulman et al. (OpenAI, 2017), is the state-of-the-art policy gradient "
        "algorithm powering Arena. It combines the sample efficiency of first-order optimization with the stability of trust-region methods.",
        body_style
    ))

    story.append(Paragraph("The Policy Gradient Dilemma & Policy Collapse", h2_style))
    story.append(Paragraph(
        "In basic Policy Gradient methods (such as REINFORCE), the agent updates its neural network weights &theta; along the gradient "
        "of expected reward: &nabla;J(&theta;) = E[&nabla;<sub>&theta;</sub> log &pi;<sub>&theta;</sub>(a|s) Q(s,a)]. "
        "However, standard gradient descent has a fatal flaw in reinforcement learning: <b>the step size dilemma</b>. "
        "<br/>• If the step size (learning rate) is too small, training takes years. "
        "<br/>• If the step size is slightly too large, the neural network weights can shift radically. Unlike supervised learning where bad data "
        "only causes a momentary spike in loss, a bad policy update in RL causes the agent to collect catastrophic, degraded experience in the next round. "
        "The agent forgets everything it learned, falling off a performance cliff into <b>policy collapse</b>.",
        body_style
    ))

    story.append(Paragraph("Why TRPO Needed a Successor", h2_style))
    story.append(Paragraph(
        "OpenAI previously solved this with <b>Trust Region Policy Optimization (TRPO)</b>, which enforced a strict statistical boundary "
        "using Kullback-Leibler (KL) divergence: KL(&pi;<sub>old</sub>, &pi;<sub>new</sub>) &le; &delta;. While mathematically robust, "
        "TRPO required computing second-order derivatives (the Fisher Information Matrix) and running conjugate gradient optimization. "
        "It was computationally heavy, slow, and incompatible with complex architectures. <b>PPO was engineered to achieve the exact same trust-region stability using simple, elegant first-order gradient clipping.</b>",
        body_style
    ))

    story.append(Spacer(1, 6))

    # PPO MATH BOX
    ppo_eq_html = (
        "<b>THE PPO CLIPPED SURROGATE OBJECTIVE FUNCTION</b><br/><br/>"
        "<b>L<sup>CLIP</sup>(&theta;) = &Ecirc;<sub>t</sub> [ min( r<sub>t</sub>(&theta;) &Acirc;<sub>t</sub> , &nbsp; "
        "clip( r<sub>t</sub>(&theta;) , 1 - &epsilon; , 1 + &epsilon; ) &Acirc;<sub>t</sub> ) ]</b>"
    )
    p_eq = Paragraph(ppo_eq_html, formula_style)
    t_eq = Table([[p_eq]], colWidths=[letter[0]-108])
    t_eq.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), colors.HexColor("#EFF6FF")),
        ('BOX', (0,0), (-1,-1), 1.5, c_accent),
        ('PADDING', (0,0), (-1,-1), 10),
    ]))
    story.append(t_eq)
    story.append(Spacer(1, 10))

    story.append(Paragraph("Deconstructing the Formula Term by Term", h2_style))
    story.append(Paragraph(
        "<b>1. The Probability Ratio r<sub>t</sub>(&theta;):</b><br/>"
        "<code>r<sub>t</sub>(&theta;) = &pi;<sub>&theta;</sub>(a<sub>t</sub>|s<sub>t</sub>) / &pi;<sub>&theta;old</sub>(a<sub>t</sub>|s<sub>t</sub>)</code><br/>"
        "This measures how much more or less likely the new policy makes action <i>a<sub>t</sub></i> compared to the old policy. "
        "If <i>r<sub>t</sub>(&theta;) = 1</i>, the policies are identical. If <i>r<sub>t</sub>(&theta;) = 1.3</i>, the new policy is 30% more likely to take that action.",
        body_style
    ))

    story.append(Paragraph(
        "<b>2. The Advantage Function &Acirc;<sub>t</sub>:</b><br/>"
        "<code>&Acirc;<sub>t</sub> = Q(s<sub>t</sub>, a<sub>t</sub>) - V(s<sub>t</sub>)</code><br/>"
        "The advantage tells us whether a specific action was <i>better or worse than average</i>. "
        "The Critic network estimates the baseline value <i>V(s)</i> (how good the current position is). "
        "If the actual cumulative return exceeds <i>V(s)</i>, &Acirc;<sub>t</sub> is positive (a great move!). If it underperforms, &Acirc;<sub>t</sub> is negative.",
        body_style
    ))

    story.append(Paragraph(
        "<b>3. The Clipping Mechanism clip(r<sub>t</sub>, 1 - &epsilon;, 1 + &epsilon;):</b><br/>"
        "The parameter &epsilon; (set to <code>0.2</code> in <code>arena_train.yaml</code>) defines our safety boundary: [0.8, 1.2]. "
        "It acts as a strict governor on how much the policy can change in a single update.",
        body_style
    ))

    story.append(Paragraph(
        "<b>4. The Min Operator (The Pessimistic Bound):</b><br/>"
        "Why take the minimum between the unclipped and clipped objective? This creates a conservative lower bound: "
        "<br/>• <b>Case A (&Acirc;<sub>t</sub> &gt; 0, Great Action):</b> We want to increase the probability of this action. The ratio <i>r<sub>t</sub></i> grows. "
        "However, once <i>r<sub>t</sub></i> exceeds <i>1 + &epsilon; (1.2)</i>, the clipping caps the term at <i>(1 + &epsilon;)&Acirc;<sub>t</sub></i>. "
        "The gradient becomes zero! The network is prohibited from becoming recklessly overconfident on a single lucky roll. "
        "<br/>• <b>Case B (&Acirc;<sub>t</sub> &lt; 0, Catastrophic Action):</b> We want to decrease the probability. The ratio <i>r<sub>t</sub></i> drops. "
        "If <i>r<sub>t</sub></i> drops below <i>1 - &epsilon; (0.8)</i>, clipping stops pushing the gradient once the penalty is accounted for, preventing the distribution from collapsing to zero.",
        body_style
    ))

    story.append(Spacer(1, 14))

    story.append(Paragraph("The Complete Actor-Critic Loss Function", h2_style))
    story.append(Paragraph(
        "In production (including Unity ML-Agents), PPO does not just optimize the policy. It trains an <b>Actor-Critic</b> architecture "
        "sharing representation layers, combining three distinct objectives into a single unified loss function:",
        body_style
    ))

    loss_total_html = (
        "<b>L<sup>TOTAL</sup>(&theta;) = &Ecirc;<sub>t</sub> [ L<sup>CLIP</sup>(&theta;) "
        "- c<sub>1</sub> L<sup>VF</sup>(&theta;) + c<sub>2</sub> S[&pi;<sub>&theta;</sub>](s<sub>t</sub>) ]</b>"
    )
    p_loss = Paragraph(loss_total_html, formula_style)
    t_loss = Table([[p_loss]], colWidths=[letter[0]-108])
    t_loss.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), c_card_bg),
        ('BOX', (0,0), (-1,-1), 1, c_border),
        ('PADDING', (0,0), (-1,-1), 8),
    ]))
    story.append(t_loss)
    story.append(Spacer(1, 8))

    story.append(Paragraph("<b>• Value Function Loss L<sup>VF</sup>(&theta;):</b> Squared error between the Critic's state estimation and actual discounted returns: <i>(V<sub>&theta;</sub>(s<sub>t</sub>) - V<sub>target</sub>)<sup>2</sup></i>. Trains the Critic to accurately evaluate board states.", bullet_style))
    story.append(Paragraph("<b>• Entropy Bonus S[&pi;<sub>&theta;</sub>]:</b> Measures the randomness/entropy of the policy distribution. In <code>arena_train.yaml</code>, <code>beta: 0.005</code> rewards exploration, ensuring agents don't prematurely converge on a sub-optimal repetitive loop before discovering tool use.", bullet_style))
    story.append(Paragraph("<b>• Generalized Advantage Estimation (GAE, &lambda; = 0.95):</b> Blends temporal difference residuals over time horizon (<i>time_horizon: 64</i>), balancing bias and variance in advantage calculations.", bullet_style))

    story.append(Paragraph("Config Hyperparameters Decoded (from arena_train.yaml)", h2_style))

    hyper_data = [
        [Paragraph("Hyperparameter", table_header), Paragraph("Value in Config", table_header), Paragraph("Engineering Purpose", table_header)],
        [Paragraph("<code>batch_size</code>", table_cell_bold), Paragraph("1024", table_cell), Paragraph("Number of experience transitions sampled per gradient descent update.", table_cell)],
        [Paragraph("<code>buffer_size</code>", table_cell_bold), Paragraph("10240", table_cell), Paragraph("Experiences collected across parallel environments before running policy updates.", table_cell)],
        [Paragraph("<code>learning_rate</code>", table_cell_bold), Paragraph("0.0003 (linear decay)", table_cell), Paragraph("Adam optimizer learning rate, decreasing toward zero as models mature.", table_cell)],
        [Paragraph("<code>beta (entropy)</code>", table_cell_bold), Paragraph("0.005", table_cell), Paragraph("Strength of entropy bonus to encourage agents to try novel actions.", table_cell)],
        [Paragraph("<code>epsilon (clip)</code>", table_cell_bold), Paragraph("0.2", table_cell), Paragraph("PPO clipping threshold [0.8, 1.2], restricting radical policy jumps.", table_cell)],
        [Paragraph("<code>lambd (GAE)</code>", table_cell_bold), Paragraph("0.95", table_cell), Paragraph("GAE smoothing parameter trading off immediate reward variance vs. long-term bias.", table_cell)],
        [Paragraph("<code>num_epoch</code>", table_cell_bold), Paragraph("3", table_cell), Paragraph("Number of full passes over the buffer data during each training cycle.", table_cell)],
        [Paragraph("<code>max_steps</code>", table_cell_bold), Paragraph("2,000,000 (train) / 200M (catalog)", table_cell), Paragraph("Total training budget before final neural weights are exported to ONNX.", table_cell)]
    ]
    t_hyper = Table(hyper_data, colWidths=[110, 110, 284])
    t_hyper.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), c_primary),
        ('GRID', (0,0), (-1,-1), 0.5, c_border),
        ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, c_card_bg]),
        ('PADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_hyper)
    story.append(Spacer(1, 10))

    # ─────────────────────────────────────────────────────────────────────────
    story.append(PageBreak())
    # SECTION 6: THE 4 EVOLUTIONARY ERAS
    # ─────────────────────────────────────────────────────────────────────────
    story.append(Paragraph("6. The Four Evolutionary Eras of Emergence", h1_style))
    story.append(Paragraph(
        "By analyzing the model checkpoints in <code>Unity Project/Assets/NNModels</code>, we can trace the historical emergence "
        "of intelligence across 200 Million simulation steps:",
        body_style
    ))

    eras_data = [
        [Paragraph("Era / Steps", table_header), Paragraph("Primary Behaviors", table_header), Paragraph("Strategic Significance", table_header)],
        [
            Paragraph("<b>Era 1: Toddler Phase</b><br/>(0 – 50K Steps)", table_cell_bold),
            Paragraph("Random twitching, continuous collisions with walls, aimless wandering. Objects are ignored.", table_cell),
            Paragraph("Untrained neural baseline. Random weights; gradient vectors establishing basic motor correlation.", table_cell)
        ],
        [
            Paragraph("<b>Era 2: Chaser Phase</b><br/>(1M – 10M Steps)", table_cell_bold),
            Paragraph("Seekers run straight toward Hiders; Hiders sprint in reverse. Resembles simple playground tag.", table_cell),
            Paragraph("Policy discovers basic objective rewards (proximity and capture), but lacks spatial depth.", table_cell)
        ],
        [
            Paragraph("<b>Era 3: Occlusion Phase</b><br/>(20M – 75M Steps)", table_cell_bold),
            Paragraph("Hiders realize walls break seeker vision cones. Seekers learn corner-peeking and angle clearance.", table_cell),
            Paragraph("Emergence of spatial geometry awareness. Agents utilize static walls to manipulate sightlines.", table_cell)
        ],
        [
            Paragraph("<b>Era 4: Fortification Phase</b><br/>(100M – 200M Steps)", table_cell_bold),
            Paragraph("<b>Emergent Tool Use:</b> During the grace period, Hiders drag crates into doorways and trigger <code>lockTeam</code>. Seekers learn to probe barricade seams.", table_cell),
            Paragraph("<b>Peak MARL Intelligence:</b> Construction and shelter building invented purely through self-play survival pressure. No human coded this logic.", table_cell)
        ]
    ]
    t_eras = Table(eras_data, colWidths=[110, 195, 199])
    t_eras.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), c_primary),
        ('GRID', (0,0), (-1,-1), 0.5, c_border),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, c_card_bg]),
        ('PADDING', (0,0), (-1,-1), 5),
    ]))
    story.append(t_eras)
    story.append(Spacer(1, 10))

    # ─────────────────────────────────────────────────────────────────────────
    # SECTION 7: PROVING AI IMPROVEMENT
    # ─────────────────────────────────────────────────────────────────────────
    story.append(Paragraph("7. Demonstrating AI Superiority Over Traditional Game AI", h1_style))
    story.append(Paragraph(
        "To convince an observer that Arena is powered by genuine Reinforcement Learning and not scripted bot logic, "
        "the system provides three concrete verification pillars:",
        body_style
    ))

    story.append(Paragraph("<b>1. Generational A/B Testing (<code>GenerationComparer.cs</code>):</b> Pressing <b>[G]</b> opens the checkpoint comparer. On the identical room layout, pitting an Era 1 checkpoint (5k steps) against Seekers results in an instant 100% capture rate within 5 seconds. Switching to the 200M FCSP checkpoint immediately shifts the hidden survival rate to &gt;85%, demonstrating mathematical policy convergence.", bullet_style))
    story.append(Paragraph("<b>2. The Dynamic Architect Stress Test:</b> Scripted bots rely on pre-baked NavMesh waypoints. If an obstacle is introduced at runtime, scripted bots freeze or clip into walls. In Arena, the player can build asymmetric mazes during the Architect Phase; because agents perceive continuous physics via 360° raycasts, they navigate and barricade custom rooms with zero re-baking.", bullet_style))
    story.append(Paragraph("<b>3. Real-Time Telemetry & Perception Rays:</b> Traditional bots hide internal states. In Arena, seeker laser scanners (<code>SeekerLaserScanner.cs</code>), tactical rings (<code>AgentTacticalRing.cs</code>), and ray perception sensors can be visualized in real time, exposing how the neural net evaluates vision cones and decision outputs.", bullet_style))

    # Build the document
    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"Successfully generated PDF: {filename}")

if __name__ == "__main__":
    out_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Arena_MARL_and_PPO_Guide.pdf")
    build_pdf(out_path)
