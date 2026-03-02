---
title: "Ontology-to-Tools Compilation — Main Paper (p.1-27)"
source: "Zhou, X. et al. (2025). Ontology-to-tools compilation for executable semantic constraint enforcement in LLM agents. arXiv:2602.03439"
notice: "Reference copy for internal analysis"
---

Ontology-to-tools compilation for executable semantic constraint enforcement in LLM agents


Preprint Cambridge Centre for Computational Chemical Engineering ISSN 1473 – 4273

## **Ontology-to-tools compilation for executable** **semantic constraint enforcement in LLM agents**

**Xiaochi Zhou** [1], **Patrick Butler** [1], **Changxuan Yang** [3], **Simon Rihm** [5], **Thitikarn Angkanaporn** [1], **Jethro Akroyd** [1] [,] [2] [,] [4], **Sebastian Mosbach** [1] [,] [2] [,] [4], **Markus Kraft** [1] [,] [2] [,] [3] [,] [5]


released: February 4, 2026



1 Department of Chemical Engineering
and Biotechnology
University of Cambridge
Philippa Fawcett Drive
Cambridge, CB3 0AS
United Kingdom


3 MIT, Chemical Engineering
77 Massachusetts Avenue, Room E17-504
Cambridge, MA 02139 USA



2 CARES
Cambridge Centre for Advanced
Research and Education in Singapore
1 Create Way
CREATE Tower, #05-05
Singapore, 138602


4 CMCL
No. 9, Journey Campus
Castle Park
Cambridge
CB3 0AX
United Kingdom



5 CMPG
GRIPS – Gründerinnenzentrum Pirmasens
Delaware Avenue 1–3
66953 Pirmasens
Germany


Preprint No. 343


_Keywords:_ Large language models; autonomous agents; knowledge graphs; scientific information extraction


**Edited by**

Computational Modelling Group
Department of Chemical Engineering and Biotechnology
University of Cambridge
Philippa Fawcett Drive
Cambridge, CB3 0AS
United Kingdom


**E-Mail:** [mk306@cam.ac.uk](mailto:mk306@cam.ac.uk)
**World Wide Web:** [https://como.ceb.cam.ac.uk/](https://como.ceb.cam.ac.uk/)


### GROUP


**Abstract**


We introduce _ontology-to-tools_ _compilation_ as a proof-of-principle mechanism
for coupling large language models (LLMs) with formal domain knowledge. Within
_The_ _World_ _Avatar_ (TWA), ontological specifications are compiled into executable
tool interfaces that LLM-based agents must use to create and modify knowledge
graph instances, enforcing semantic constraints during generation rather than through
post-hoc validation. Extending TWA’s semantic agent composition framework, the
Model Context Protocol (MCP) and associated agents are integral components of
the knowledge graph ecosystem, enabling structured interaction between generative models, symbolic constraints, and external resources. An agent-based workflow translates ontologies into ontology-aware tools and iteratively applies them to
extract, validate, and repair structured knowledge from unstructured scientific text.
Using metal–organic polyhedra synthesis literature as an illustrative case, we show
how executable ontological semantics can guide LLM behaviour and reduce manual schema and prompt engineering, establishing a general paradigm for embedding
formal knowledge into generative systems.


**Highlights**


  - Ontological constraints are compiled into executable tools for LLM agents
within The World Avatar.


  - Tool-using LLM agents iteratively extract and instantiate knowledge under semantic constraints.


  - A synthesis literature case study in The World Avatar demonstrates rule-consistent,
stateful generation.


1


#### **Contents**

**1** **Introduction** **4**


**2** **Knowledge-Graph** **Construction** **from** **Metal–Organic** **Polyhedra** **Synthesis**
**Literature** **6**


**3** **Design of Ontology-to-Tools Compilation Framework** **7**


**4** **Performance of Ontology-to-tool compilation** **9**


4.1 Ontology-compiled MCP tools enable semantically valid and contentcorrect end-to-end graph instantiation . . . . . . . . . . . . . . . . . . . 14


4.2 Constraint feedback improves completeness of instantiated synthesis steps 14


4.3 Dominant synthesis-step error modes and improvement priorities . . . . . 15


**5** **Discussion** **16**


5.1 Limitations . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . 16


5.2 Future work . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . 16


**6** **Methods** **17**


6.1 Preparation stage . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . 17


6.2 Instantiation stage . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . 19


6.3 Grounding stage . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . 20


6.4 Preparation of evaluation dataset . . . . . . . . . . . . . . . . . . . . . . 21


6.5 Evaluation metrics and methodology . . . . . . . . . . . . . . . . . . . . 21


6.6 CBU derivation for metal-organic polyhedra . . . . . . . . . . . . . . . . 22


6.7 Models and inference settings . . . . . . . . . . . . . . . . . . . . . . . 23


**7** **Supplementary Information** **26**


7.1 Supplementary Note 1: Background and related work . . . . . . . . . . . 26


7.1.1 LLM-based agents and ReAct-style reasoning . . . . . . . . . . . 26


7.1.2 Tool-calling interfaces and the Model Context Protocol . . . . . . 26


7.1.3 The World Avatar and its chemistry ontologies . . . . . . . . . . 26


7.1.4 LLM-based knowledge instantiation and ontology grounding . . . 27


7.2 Supplementary Methods . . . . . . . . . . . . . . . . . . . . . . . . . . 28


7.2.1 Iteration 1: System Meta Prompt . . . . . . . . . . . . . . . . . . 33


2


7.2.2 Iteration 1: User Meta Prompt . . . . . . . . . . . . . . . . . . . 34


7.2.3 Extension Ontology: System Meta Prompt . . . . . . . . . . . . 35


7.2.4 Extension Ontology: User Meta Prompt . . . . . . . . . . . . . . 36


7.2.5 Illustrative trace of constraint-triggered repair . . . . . . . . . . . 38


7.3 Supplementary Evaluation Results . . . . . . . . . . . . . . . . . . . . . 39


7.3.1 Detailed instance visualisation for a UMC-1 synthesis . . . . . . 39


**References** **46**


3


#### **1 Introduction**

Extracting structured data from scientific literature is a critical and routine step for enabling machine-actionable scientific workflows, including hypothesis generation, datadriven modelling, and synthesis prediction. [15, 25, 57]. In reticular chemistry, for example, literature extraction can support synthesis predictions of novel materials and estimation of properties such as gas adsorption, and provide means to identify gaps in the
immediate chemical space [4, 19, 24, 68]. Moreover, structuring literature makes it possible to link extracted records to complementary data sources, expanding their context and
increasing their value beyond what is reported in any single paper [28, 32].


However, turning scientific text into machine-actionable data remains challenging. Scientific text requires precise interpretation and is domain-specific, while downstream uses,
such as data-driven modelling, require consistently defined records that are comprehensively validated [15, 25, 49, 57]. These records need clear roles and concept boundaries,
normalized units and values, and, crucially, unambiguous entity references [32, 49, 55].
For example, a reagent may be mentioned without an explicit role (precursor _vs_ solvent), experimental conditions may be expressed in inconsistent unit forms ( _e.g._ C _vs_
Degrees Celsius), and the same chemical may appear under multiple names or abbreviations; downstream workflows therefore require canonical identifiers ( _e.g._, CAS numbers
or InChIKeys) and consistent typing to support validation, deduplication, and cross-paper
integration [40, 45].


Ontologies are one natural option for specifying these roles, boundaries, and constraints:
an ontology is a formal, machine-readable description of a domain, typically decomposed
into a T-Box that defines concepts and relations, and an A-Box that instantiates them with
concrete entities and facts [16, 17]. In such knowledge graphs, entities are represented
by Internationalized Resource Identifiers (IRIs), which serve as unique identifiers for individual instances, such as specific chemical species. In parallel, recent large language
models (LLMs) have enabled few-shot and zero-shot adaptation for extraction tasks, and
can apply prompt-based, soft definitions of roles and boundaries by interpreting context
and producing schema-shaped records [57].


Pipelines that aim to produce structured data ready for downstream use often combine
ontologies (to define target roles, relations, and constraints) with extractors (LLMs or
otherwise) to interpret text. However, downstream use typically requires that extracted
records conform to the ontology, including valid types and relations, normalized values
and units, and grounded identifiers. This concentrates effort in constraint enforcement,
such as validation, normalization, and grounding or alignment, implemented within the
extraction system or as a separate step [32, 55, 57]. In non-LLM pipelines, this enforcement is commonly realized through domain-specific components such as trained models,
hand-engineered rules, and ontology-aligned assembly procedures [32, 55].


Many other approaches shift this work to post-hoc processing: they first generate schemashaped records, then apply deterministic validation and ontology/database alignment to
ensure the outputs satisfy required fields, normalization, cross-field consistency, and identifier grounding [31, 49, 57, 58]. As a result, achieving ontology-conformant data often
depends on hand-built validation/alignment logic outside the extractor, rather than being


4


enforced during extraction itself.


Across these families, the common limitation is how downstream requirements are enforced: they are implemented either as bespoke, domain-specific pipeline logic around
extraction or as a separate post-hoc layer for validation, normalization, and grounding
and alignment, concentrating expert effort in code that must be revised as schemas and
scope change.


Recently, large language models (LLMs) have increasingly supported tool calling, where
a model invokes external functions (scripts and APIs) during generation to produce structured outputs, perform deterministic checks, and retrieve supporting evidence. Frameworks such as the Model Context Protocol (MCP) standardize this interaction by providing a common interface for registering tools and exchanging typed inputs and outputs [1, 27, 28, 31, 34, 37, 44, 48, 56, 60]. These capabilities make it possible to enforce
structure and domain constraints during extraction, rather than relying solely on post hoc
validation.


Such constraints are particularly important in scientific domains, where extracted information must align with well-defined ontologies and existing knowledge bases. The World
Avatar [4] is a large-scale dynamic knowledge graph for chemistry that provides curated
T-Boxes and extensive A-Box instances covering synthesis descriptions, metal organic
polyhedra, and chemical species [24, 40, 45]. As a result, it offers a natural grounding target for tool-augmented LLM extraction, supplying ready-to-use semantic schemas against
which generated outputs can be validated and instantiated.


Motivated by the constraint-enforcement bottleneck identified above, we introduce a novel
ontology-to-tools compilation framework that turns an ontology into executable LLMcallable tools, enabling constraints to be enforced during extraction rather than by bespoke
pipeline logic or post-hoc validation. To our knowledge, no prior system has transformed
an ontology into an LLM-executable constraint enforcement layer. In our approach, an
LLM-driven agent performs document-level extraction and invoking compiled tools to
construct the knowledge graph directly: tool calls instantiate individuals and relations
with built-in constraint checks and structured feedback on violations. The same compilation framework also yields lexical grounding tools. These tools link surface text mentions
to ontology-defined entities using lexical labels and evidence from a reference knowledge
graph. In practice, they align extracted mentions to the IRIs of existing instances in the
graph, for example an already recorded chemical species.


Our contribution is: (1) an ontology-to-tools compilation framework for generating ontologyaligned prompts and MCP tools from a _T-Box_ and meta-prompts; (2) an ontology-constrained
KG construction procedure that enforces constraints at creation time via tool calls with
structured feedback; and (3) an ontology-driven grounding workflow that generates lexical grounding tools from the _T-Box_ and endpoint evidence for identifier alignment. We
demonstrate the ontology-to-tools compilation framework on metal–organic polyhedra
(MOP) synthesis literature in The World Avatar, covering ontology-constrained knowledgegraph construction from synthesis text, followed by lexical grounding to canonical identifiers.


From a machine-intelligence perspective, the central contribution is a compilation mechanism that transforms symbolic constraints into executable action interfaces for generative


5


models. This reframes constraint enforcement from prompt- or schema-based output constraints and post-hoc validation into run-time interaction with a structured environment.
The resulting behaviour is not achieved through prompt engineering or grammar restriction, but through tool-mediated interaction with a persistent symbolic state. The chemistry
case study serves as a concrete instantiation of this general mechanism.

#### **2 Knowledge-Graph Construction from Metal–Organic** **Polyhedra Synthesis Literature**


We demonstrate the ontology-to-tool compilation framework by constructing grounded,
ontology-consistent knowledge graphs from metal–organic polyhedra synthesis literature.
Figure 1 illustrates the core knowledge-graph representation produced for metal–organic
polyhedra (MOP) synthesis papers. The results reported in this section are obtained
from 30 MOP-related publications, with each paper treated as a full-text input including
manuscript text, tables, and supplementary information when available.


The task definition is to convert unstructured MOP synthesis literature into grounded,
ontology-consistent knowledge-graph instances. Given a full-text synthesis paper, the output is a set of interlinked instances capturing synthesis procedures, chemical reagents, and
the reported MOP, with extracted entities linked to existing knowledge graph instances.


The representation draws on multiple complementary domain ontologies, including OntoSynthesis [45] for modelling synthesis events, ordered steps, conditions, and reagent
usage; OntoSpecies [40] for canonical chemical species identities and identifiers; OntoMOPs [24] for MOP products, structural concepts, and derived chemical building units
(CBUs); and OM-2 for representing and normalizing quantities and units in synthesis descriptions. Chemical species are grounded at the usage-instance level by linking extracted
instances to canonical OntoSpecies entries.


This task is challenging because it requires the coordinated use and alignment of multiple ontologies when analysing a single paper. Procedural knowledge, canonical chemical
identity, and product- and structure-level material knowledge, including derived CBUs,
must be populated consistently and linked across ontology boundaries. Moreover, the
relevant information is often reported at different levels of detail, and the resulting knowledge graph must keep track of where each piece of information comes from, so that roles,
amounts, and other qualifiers remain correctly associated with the appropriate chemical
usage and synthesis steps. In addition, CBU derivation requires connecting extracted synthesis evidence to product representations and external chemical or crystallographic data
while maintaining consistent identifiers across all ontological layers.


For each paper, the framework produces three interconnected outputs. First, a structured
synthesis recipe captures ordered synthesis steps, step-level actions, and associated conditions, together with additional synthesis-related details such as reagent usage and provenance information.


Second, a set of grounded chemical species instances represents chemical entities uniquely,
with contextual qualifiers such as role, amount, and units attached to each occurrence in


6


the synthesis, and with links to canonical OntoSpecies records for cross-paper integration.


Third, derived chemical building units (CBUs), including reusable metal nodes or clusters
and organic ligands, are obtained by combining extracted evidence with database-backed
chemical and crystallographic data. These CBUs are instantiated under OntoMOPs and
linked back to the reported MOP product and the synthesis in which they were produced.


Figure 1 shows an example ontology instance produced for a representative synthesis.
Panel (A) presents an excerpt of the instances subgraph, _i.e._ A-Box, including a synthesis event linked to an ordered sequence of steps, step-scoped reagent usage, and the
reported MOP product, together with attributes such as yield and representation links.
Panel (B) shows a compact, record-style projection of the same instances in canonical
slot–value form used for inspection and downstream querying. In this representation,
contextual qualifiers remain attached to individual usage instances, while selected inputs
are grounded to canonical OntoSpecies entries via identity links. Together, the outputs
integrate procedural structure, species grounding, and product- and CBU-level semantics
to yield a single, queryable knowledge-graph instance.

#### **3 Design of Ontology-to-Tools Compilation Framework**


This section describes how the ontology-consistent knowledge-graph instances defined in
Section 2 are constructed from unstructured documents. The focus here is on the compilation and execution mechanisms that translate ontology specifications into executable
tools and use them to perform constrained extraction. Figure 2 provides an overview of
the end-to-end workflow. The compiled tool interfaces collectively define the action space
available to the LLM agent, constraining generation through executable semantics rather
prompt- or schema-based constraints on the output.


As illustrated in Figure 2, the framework has two stages: (1) the _preparation stage_, which
compiles an ontology into tools and prompts, and (2) the _instantiation stage_, which runs
a tool-using agent to construct the knowledge graph from papers. In the _preparation_
_stage_, the preparation agent consumes an ontology schema (T-Box) together with domainagnostic meta-prompts (instruction templates) and generates ontology-aligned runtime
prompts, supporting scripts, and LLM-callable tool interfaces exposed via the Model
Context Protocol (MCP). These tools implement ontology-aware instance construction
with built-in validation logic. In the _instantiation stage_, the instantiation agent applies the
generated prompts, scripts, and LLM-callable tools to each document, invoking tools to
create and link individuals and relations; constraint violations are returned as structured
feedback to support iterative completion and repair.


The compilation layer treats the T-Box as a machine-readable contract. It specifies which
classes, relations, attributes, and constraints are allowed. From this contract, the framework generates executable tool interfaces with explicitly specified inputs, outputs, and
validation behaviour. These tools are the only way to create or modify structured instances, so constraints are checked and repaired during construction rather than only after
the fact.


During instantiation, the extraction agent interprets document content and issues tool calls


7


**Figure 1:** _**Example**_ _**ontology**_ _**instance**_ _**produced**_ _**by**_ _**the**_ _**instantiation**_ _**agent.**_ _(A)_ _A-_
_Box_ _subgraph_ _instantiated_ _under_ _OntoSyn/OntoMOPs_ _for_ _synthesis_ _S1,_ _in-_
_cluding_ _ordered_ _synthesis_ _steps,_ _chemical_ _inputs,_ _and_ _product_ _UMC-1_ _(with_
_yield_ _and_ _representation_ _links)._ _(B)_ _Compact_ _record_ _projection_ _(A-Box_ _nor-_
_mal form) of the same instance in canonical slot–value form; onsyn:* abbre-_
_viates_ _ontosyn:*,_ _and_ _selected_ _ChemicalInput_ _entities_ _are_ _grounded_ _via_
_owl:sameAs to ontospecies:Species._


8


to construct synthesis steps, usage instances, species links, and product entities. External chemical and crystallographic resources are integrated as callable tools within the
same execution framework, enabling canonical species identification and database-backed
derivation of higher-level entities such as CBUs. All extracted, grounded, and derived entities are instantiated into a knowledge graph under the constraints defined by the ontology
contract. Together, these components implement an end-to-end workflow in which ontology specifications and unstructured literature jointly drive the construction of grounded,
internally consistent knowledge-graph instances, without relying on post-hoc validation
or manual schema-specific repair.

#### **4 Performance of Ontology-to-tool compilation**


This section evaluates the ontology-to-tool compilation framework and the resulting knowledgegraph construction on the same curated corpus of 30 metal–organic polyhedra (MOP)
synthesis articles used throughout this work. Two questions matter. First, are the generated graphs semantically healthy, _i.e._ do they satisfy the ontology constraints and remain
structurally consistent under instantiation? Second, is the extracted and grounded content
accurate with respect to the source literature?


To answer these questions, we compare the generated outputs against manual ground-truth
annotations in predefined JSON record formats covering four target categories: grounded
and derived CBUs, characterisation entities, synthesis steps, and reaction chemicals. Section 6.4 describes the dataset in detail. We report _graph-recoverable_ performance first.
This measures whether the information required by the evaluation schema is actually
present in the constructed knowledge graph in the expected ontology form, _i.e._ as individuals and relations that can be retrieved deterministically. Concretely, for each target
JSON record type, we use a fixed SPARQL query to reconstruct the record from the graph,
and we score the reconstructed records against the ground truth. SPARQL querying provides the recoverability test because it requires the relevant facts to be encoded as explicit
triples with the correct links between entities, rather than appearing only as free text, partial attributes, or disconnected nodes. The SPARQL queries are fixed in advance and are
not adapted per paper or per predicted graph. They are derived from the target ontological
schema (T-Box) and the corresponding JSON record definitions, and are written once to
specify how each record should be read out from any valid instantiation. As a result, a
prediction is counted as graph-recoverable only if it can be reconstructed through these
schema-derived queries. Figure 3 summarises aggregate performance, class imbalance,
and per-paper variability. Figure 4 isolates the effects of external grounding services and
constraint feedback. Figure 5 analyses dominant step-level error sources and highlights
priorities. Exact scores and ablations appear in Tables 7.2 and 7.3. Our evaluation is designed to assess interaction-based constraint enforcement by analysing its effects on graph
recoverability and accuracy.


9


**Figure 2:** _Ontology-to-tools_ _compilation_ _as_ _an_ _executable_ _semantic_ _control_ _layer_ _for_
_LLM-based agents._ _Symbolic ontological definitions (T-Box) within The World_
_Avatar are compiled into executable tool interfaces and validators that define_
_the action space available to a large language model during generation. Rather_
_than_ _producing_ _free-form_ _text,_ _the_ _LLM_ _interacts_ _with_ _a_ _persistent_ _symbolic_
_state_ _by_ _invoking_ _ontology-aligned_ _actions_ _that_ _create,_ _modify,_ _and_ _validate_
_graph_ _instances._ _Constraint_ _violations_ _trigger_ _structured_ _feedback,_ _enabling_
_iterative_ _repair_ _and_ _grounding_ _to_ _external_ _resources._ _This_ _reframes_ _seman-_
_tic_ _constraint_ _enforcement_ _from_ _post-hoc_ _validation_ _or_ _constrained_ _decoding_
_into run-time interaction with an evolving symbolic environment, allowing the_
_model to operate as a stateful, ontology-aware agent._


10


**(a)** [Aggregate graph-recoverable]



5000


4000


3000


2000


1000



**(b)** Task imbalance





1.0


0.8


0.6


0.4


0.2



performance







0.0

|precision recall|Col2|
|---|---|
|||


|Col1|Col2|Col3|
|---|---|---|
||||
||||

CBU Char. Steps Chem.


**(c)** Per-paper F1 distribution

1.0


0.8


0.6


0.4


0.2


0.0
CBU Char. Steps Chem.



**(d)** Per-paper recall variability

1.0


0.8


0.6


0.4


0.2


0.0
CBU Char. Steps Chem.



0
CBU Char. Steps Chem.



**(e)** Best vs worst papers
Best-3 Worst-3



0.97



1.0


0.8


0.6


0.4


0.2



1.00







0.0

|0.|Col2|
|---|---|
|**0.**|**00**|


|Col1|Col2|
|---|---|
|||


|Col1|Col2|
|---|---|
|||


|Col1|Col2|
|---|---|
|||

CBU Char. Steps Chem.


**Figure 3:** _**Core**_ _**end-to-end**_ _**results**_ _**across**_ _**four**_ _**extraction/instantiation**_ _**domains.**_ _(a)_
_Aggregate_ _graph-recoverable_ _precision,_ _recall_ _and_ _F1_ _for_ _grounded/derived_
_CBUs,_ _characterisation_ _entities,_ _synthesis_ _steps_ _and_ _reaction_ _chemicals_ _(Ta-_
_ble 7.2). (b) Task imbalance in ground-truth positives (Table 7.1). (c) Per-paper_
_F1 distributions across the 30-paper benchmark._ _(d) Per-paper recall variabil-_
_ity, highlighting recall-limited categories._ _(e) Best–worst paper contrast (mean_
_F1 over top-3 vs bottom-3 papers) summarising dataset heterogeneity._


11


**(a)** Ablation impact on end-to-end

performance



**(b)** Steps precision–recall shift



1.0


0.8


0.6


0.4


0.2



















1.0


0.8


0.6


0.4


0.2







0.0

|Col1|Col2|Col3|
|---|---|---|
||||


|Col1|Col2|Col3|
|---|---|---|
||||
||||

CBU Char. Steps Chem.



0.0

|Col1|Col2|
|---|---|
|||


|Col1|Col2|
|---|---|
|||


|Col1|Col2|
|---|---|
|||

Precision Recall F1



**(c)**


3


2


1



**Constraint feedback: illustrative failure modes and simple counts**



**No-feedback output**
```text
Step 1: Add Bis(cyclopentadienyl)...
!! Step 1: Add Bis(cyclopentadienyl)...
Step 2: Add DMF
!! Step 2: Add [CH2(C6H4)2(CO2)2]
Step 3: Add DMF
Step 4: Add H2O

```

**Constraint feedback**

- Step numbers unique within a synthesis

- Reduces redundant / fragmented steps

- Prevents regression to N/A placeholders


A: dup stepNumber + non-monotonic _Conceptual_
B: #steps − #unique signatures
C: regression to placeholders (full→no-fb)



**With feedback**
```text
Step 1: Add Bis(cyclopentadienyl)...
Step 2: Add DMF
Step 3: Add [CH2(C6H4)2(CO2)2]
Step 4: Add H2O

```

_Schematic (not a measured output)_





0
A Integrity B Inflation C Placeholder

50% affected 50% affected 100% affected


**Figure 4:** _**Component necessity and the role of constraint feedback.**_ _(a) Ablation impact_
_on end-to-end F1 by category (Table 7.3)._ _(b) Steps-only precision–recall–F1_
_shift under feedback removal, illustrating the dominant effect on step complete-_
_ness/recoverability._ _(c)_ _Constraint_ _feedback:_ _illustrative_ _failure_ _modes_ _and_
_paired_ ∆ _error_ _counts_ _computed_ _over_ _aligned_ _full_ _vs_ _no-feedback_ _syntheses._
_The excerpt shows typical no-feedback degradations (e.g. step-number integrity_
_and redundancy), while the mini-plot quantifies three paired error proxies (A–_
_C; defined in-panel) with bootstrap confidence intervals._


12


**(a)** Error sources ranked by contribution


1



104



**(b)** Bias signature (FN vs FP profile)

25



20





2


3


4


5


6


7


8


9


10



0 20 40 60 80 100
Errors





10


5


0









15





0 5 10 15 20 25
FP share (%)








```text
1. atmosphere       2. addedChemical.amts   3. washingSolvent.amts
4. addedChemical.names  5. sealedVessel      6. usedDevice
7. duration        8. washingSolvent.names  9. targetTemperature
10. temperature

```

**(d)**



**(c)** Improvement roadmap (Pareto)


0.94


0.92



0 20 40 60 80 100
Cumulative share of papers (%)



Error concentration across papers





100


80


60


40


20


0



0.90


0.88









0.86


0.84


0.82


0.80


0 5 10 15 20
Top N fixed fields



**Figure 5:** _**Error anatomy and improvement priorities for synthesis steps.**_ _(a) Top error-_
_contributing_ _fields_ _(FP_ _vs_ _FN)_ _aggregated_ _over_ _the_ _benchmark._ _(b)_ _Field-_
_level_ _bias_ _signature_ _(FN-share_ _vs_ _FP-share),_ _separating_ _recall-limited_ _from_
_precision-limited fields._ _(c) Hypothetical improvement roadmap (Pareto):_ _cu-_
_mulative F1 if the top-N error-contributing fields were corrected. (d) Error con-_
_centration across papers (Lorenz-style curve), showing whether a small subset_
_of papers accounts for a disproportionate share of step errors._


13


##### **4.1 Ontology-compiled MCP tools enable semantically valid and content-** **correct end-to-end graph instantiation**

To assess whether ontology-compiled MCP tools produce outputs that are both semantically valid under the ontology and content-correct, we evaluate end-to-end graph-recoverable
performance and summarise the results in Figure 3. Figure 3a shows strong overall performance (micro-F1 0.826; Table 7.2), with high precision (0.844) and task-dependent recall
(0.808). It also shows clear differences across categories. Synthesis steps perform best
overall (F1 0.843). Reaction chemicals have perfect precision (1.000) but much lower
recall (0.582). This suggests the system often avoids uncertain chemical mentions and
misses some valid ones, especially when names are written in inconsistent forms (Table 7.2).


These scores reflect both semantic and content correctness. A prediction counts as correct
only if it is instantiated with the right entity types, relations, and required fields. It must
also be recoverable by fixed SPARQL queries that reconstruct the expected JSON records.
Errors in either content or structure, including missing individuals, wrong types, missing
or incorrect links, or unfilled required slots, prevent recovery and are counted as false
negatives.


Figure 3b quantifies the benchmark imbalance. This explains why micro-aggregates are
dominated by high-volume categories and motivates reporting macro-averaged scores
(macro precision 0.865, macro recall 0.735, macro F1 0.785; Table 7.2). Figure 3c shows
broad per-paper F1 distributions across all categories. Figure 3d indicates that recall variability drives the lower tail for some domains. Figure 3 further shows that performance
varies widely across papers. The best–worst contrasts suggest that differences in how authors report experiments, and how much concrete detail they include, have a strong impact
on what the system can reliably extract and reconstruct from the graph.


In all, the results indicate that ontology-compiled MCP tools support semantically valid
graph construction with strong content accuracy, while remaining errors are concentrated
in recall-sensitive categories and in papers with sparse or unevenly reported evidence.

##### **4.2 Constraint feedback improves completeness of instantiated syn-** **thesis steps**


To assess the contribution and necessity of constraint feedback, we ablate the feedback
channel and compare end-to-end instantiation scores. Figure 4a summarises the categorylevel impact of this ablation and shows that synthesis steps are among the most affected
outputs. We therefore analyse synthesis-step behaviour in more detail.


In the ablated setting, we disable ontology-derived validation feedback at run time. We
manually modify the AI-generated execution script and comment out the code paths that
return feedback to the agent. This removes checks for required fields, unit and value
normalization, step ordering and continuity, and other consistency constraints that are
otherwise applied incrementally as synthesis steps are constructed. The agent therefore
generates outputs without receiving signals about missing fields, invalid values, or struc

14


tural violations.


Table 7.3 shows a substantial drop in synthesis-step F1 when constraint feedback is removed, driven primarily by lower recall rather than precision. Figure 4b visualises this
shift, showing that without feedback the system produces fewer synthesis-step instances
that are complete enough to be recovered by the fixed SPARQL evaluation queries.


Figure 4c helps explain the underlying failure modes. Without feedback, step outputs
more frequently violate basic structural expectations, including inconsistent or missing
step numbers and unnecessary fragmentation into extra steps. The figure reports paired
differences between full and no-feedback runs on the same papers using three proxy measures computed from the instantiated outputs. These capture increases in step-number
inconsistencies, inflation in the number of steps, and regressions to placeholder or default
values. Together, these results show that constraint feedback is important for guiding
the model toward synthesis-step structures that are complete, well-formed, and reliably
instantiable.

##### **4.3 Dominant synthesis-step error modes and improvement priori-** **ties**


To identify the main sources of remaining limitations in synthesis-step extraction and
to guide future improvements, we analyse synthesis-step errors in detail using Figure 5.
Synthesis steps are the core output of the pipeline because they represent the ordered
experimental procedure and link together materials, conditions, and outcomes. Errors at
this level therefore have a direct impact on the usefulness of the extracted knowledge
graph, which motivates a more detailed analysis than for the other categories.


Figure 5a shows that synthesis-step errors are not evenly distributed across fields. A small
number of fields account for a large share of the total errors. These include fields that
encode step order, action descriptions, and key parameters. Errors arise both as missing
fields, where required information is not extracted or instantiated, and as extra fields,
where values are produced but do not correspond to the ground truth. This concentration
indicates that overall performance is limited by a small set of recurring failure points
rather than uniform noise across the schema.


Figure 5b further separates fields by error type. Some fields are recall-limited, meaning the system often fails to extract values that are present in the paper. Other fields are
precision-limited, meaning the system more often produces incorrect or unnecessary values. This distinction suggests different improvement strategies: recall-limited fields benefit from better coverage and normalization, while precision-limited fields require tighter
constraints and filtering.


Building on this ranking, Figure 5c presents an improvement roadmap under a conservative fix-by-field assumption. It estimates how synthesis-step F1 would increase if the
highest-impact fields were corrected one at a time. The curve shows diminishing returns,
where correcting only a few top contributors yields a large fraction of the achievable improvement.


Finally, Figure 5d shows that synthesis-step errors are unevenly distributed across pa

15


pers. A minority of papers accounts for a large fraction of the total errors, suggesting that
targeted analysis of the most difficult papers can complement global field-level improvements.

#### **5 Discussion**


This work demonstrates a minimal constructive example of how generative models can
be coupled to formal systems through executable semantics. Rather than treating symbolic knowledge as static context or validation targets, ontologies are operationalised as
run-time constraints that shape agent behaviour through interaction. This shifts the role
of large language models from text generators to stateful programs operating within a
structured environment. More broadly, ontology-to-tools compilation suggests a pathway
for operationalising symbolic constraints as run-time, feedback-driven mechanisms that
shape model behaviour, rather than encoding them only as static representations.

##### **5.1 Limitations**


The current evaluation covers one ontology and one document collection. We use a MOPfocused ontology and a benchmark of 30 MOP synthesis papers. The benchmark targets four information types: CBUs, characterisation entities, synthesis steps, and reaction
chemicals. We did not test how results change when the ontology is updated and the
pipeline is re-run. We also did not evaluate the approach in a different scientific domain.


Some extraction tasks remain harder than others. Chemical species identification shows
high precision but lower recall, which means the system often misses valid mentions. This
is likely due to variation in naming and reporting styles in the papers.


The main constraint on broader evaluation is the lack of curated datasets. Building reliable ground truth in new domains, or under revised ontologies, is still costly and timeconsuming.

##### **5.2 Future work**


Future work will directly address the limitations identified above by systematically evaluating robustness under ontology change. We will modify the ontology and re-run the
full compilation and instantiation pipeline to quantify how executable tool interfaces and
prompts adapt to evolving schemas, and to what extent regeneration can replace manual
pipeline re-engineering. In parallel, we will apply the framework to additional scientific
domains with distinct ontological structures to assess transfer beyond MOP synthesis.


Where suitable curated data is available, we will expand evaluation to larger and more heterogeneous corpora and to additional extraction targets. Particular emphasis will be placed
on recall-limited tasks, especially chemical species identification and normalisation, while
preserving the current level of precision, in order to better characterise the trade-offs introduced by enforcing ontological constraints at generation time. These experiments will


16


allow us to quantify the stability of compiled action interfaces under schema and domain
changes.

#### **6 Methods**


The methodology is organized as a staged pipeline that separates ontology-driven compilation, ontology-constrained KG construction, and grounding. The _preparation_ _stage_
compiles an ontology T-Box and a small set of domain-agnostic meta-prompts into executable artefacts. These include a static JSON iteration plan, ontology- and task-specific
instantiation prompts, and an ontology-specific MCP server that exposes ontology-aware
construction and validation tools. The _instantiation stage_ executes the compiled plan for
each document using a ReAct-style tool-use loop. It incrementally constructs A-Box individuals and relations in a persistent Turtle store and validates them as it goes. When
constraints are violated, the tools return diagnostics that guide repair. The _grounding_
_stage_ aligns selected constructed instance IRIs to canonical entities in an existing knowledge graph. It generates an ontology-conditioned lookup interface from the target T-Box
and endpoint evidence. It then applies deterministic lexical matching to produce an explicit IRI mapping. This mapping is applied by rewriting instance IRIs or by adding explicit owl:sameAs links. Domain-specific derivation modules ( _e.g._ CBU derivation from
crystallographic resources) are treated as downstream enrichment steps and are described
separately from the core KG construction and grounding pipeline.

##### **6.1 Preparation stage**


The preparation stage converts an ontology T-Box into (i) a static, executable JSON plan
for extraction and KG construction and (ii) an ontology-specific MCP server that exposes
the operations referenced by that plan. The only manual inputs are a set of _domain-_
_agnostic meta-prompts for extraction and KG construction_ that are reused across papers
and domains. These prompts are provided in Listings 7.2.1, 7.2.2, 7.2.3, and 7.2.4. As
summarised in Figures 7.7 and 7.8, these meta-prompts guide an LLM to derive the task
decomposition, synthesise ontology-aware scripts, materialise an MCP server, and generate instantiation prompts capturing task-specific interpretation rules.


**JSON-based** **task** **decomposition.** A _task_ _decomposition_ _agent_ breaks the ontologyguided extraction problem into a small number of ordered steps. The result is written as a
JSON plan, illustrated in Figure 7.8, which the instantiation agent can follow directly.


Each step in the plan states (i) what to do, (ii) what text prompt to use for extraction,
and (iii) what prompt to use for knowledge-graph updates. The plan also lists what information each step reads and writes, such as the source paper, intermediate notes, and
generated graph files. Optional sub-steps can be included for follow-up passes, such as
enrichment or correction. Finally, the plan specifies which MCP tool groups are available
at each step and which tools are needed, including their expected inputs and outputs. The
outcome is a static, executable procedure that drives document processing in a fixed order.


17


**Script and MCP tool generation from the T-Box.** Given the T-Box and a set of domainagnostic meta-prompts (Listings 7.2.1, 7.2.2, 7.2.3, and 7.2.4), the MCP Creation Agent
generates an ontology-aware Python script that supports the classes and properties needed
for the extraction scenario.


The script is built on top of a manually written, ontology-independent helper library. This
library handles Turtle loading and saving, cross-step state management, deterministic IRI
minting, and other generic utilities. The generated code is required to call these helpers
rather than reimplement them. This keeps domain-specific logic separate from shared
infrastructure.


The meta-prompts enforce a standard code structure. The script initialises an RDFLib
graph, provides utilities to create and reuse instances by class, and defines functions to
add links for each property. The generated code also distinguishes two kinds of constraints. _Hard_ constraints come from the formal T-Box axioms. They include class hierarchy, domain and range typing, datatype restrictions, and any modelled cardinalities.
These axioms determine tool inputs and outputs and drive run-time checks that prevent
invalid triples. _Soft_ constraints come from natural-language annotations in the ontology.
For example, rdfs:comment may state inclusion or exclusion rules, naming conventions, or deduplication and reuse policies. These annotations are treated as guidance that
complements, but does not override, the formal axioms.


**MCP server construction.** After the ontology-aware functions are generated, the MCP
Integration Agent turns them into MCP tools. It reads each function signature and docstring and follows an integration meta-prompt. This process produces ontology-specific
MCP servers, as shown in Figure 7.7.


The server exposes each function as an MCP tool with an ontology-derived name and
a typed argument schema. It also attaches short usage instructions drawn from T-Box
annotations. Tools are grouped into simple tool sets that match the JSON task plan, such
as entity creation, attribute and relation completion, and cross-document linking. The
resulting MCP server is then registered in the shared MCP tool pool and becomes available
to all LLM-powered agents.


**Design principles for instantiation MCP servers.** Among the generated MCP servers,
the instantiation MCP server is central at runtime, and its design principles are enforced
during preparation. First, tool interfaces are defined in terms of ontology concepts, relations, and constraints: the meta-prompts require the underlying scripts to respect which
classes may be connected by which properties, expected units, and (where applicable)
reuse of concepts from reference ontologies (e.g. OM for units). Second, tool descriptions
are ontology-guided: function signatures and natural-language instructions are derived
from the T-Box, including rdfs:comment annotations that capture intended meanings
and preferred usage patterns. Third, the server is wired to a persistent Turtle (.ttl) store
that acts as cross-step memory, enabling reuse and incremental extension of previously
created instances across multiple iterations.


18


**Instantiation prompt generation** In addition to tools and scripts, the preparation agent
generates _domain- and task-specific instantiation prompts_ that capture _soft interpretation_
_constraints_ not reliably encoded as formal axioms. These prompts encode operational
definitions and heuristic decision rules that guide boundary setting and classification during extraction ( _e.g._ what counts as a synthesis step and how to delimit it; how to recognise
a HeatChill step; how to infer experimental atmosphere such as air vs. inert from textual cues). The instantiation agent uses these prompts alongside the JSON plan and the
MCP tool schemas to decide what evidence to extract and when to invoke which tools;
hard schema constraints are enforced by the ontology-aware tools during instance construction.

##### **6.2 Instantiation stage**


The instantiation stage executes the task specification produced during preparation to construct ontology-aligned knowledge graphs from documents. Guided by the JSON-based
decomposition (Figure 7.8), the runtime follows the iteration-oriented structure shown in
Figure 7.6, with an instantiation agent acting as a ReAct-style controller over MCP tools
and agent-generated, runtime intermediate results (e.g. condensed passages and hints, extracted snippets, tool-call inputs/outputs, logs, and completion markers), together with a
persistent Turtle store used for incremental KG updates.


**Loading** **MCP** **tools** **for** **instantiation.** Before processing a paper, the system loads
two kinds of MCP servers. The first kind contains the ontology-specific instantiation
tools generated in the preparation stage. The second kind provides shared utilities, such
as external knowledge access and general text processing.


For chemical identifiers and properties, we use a third-party PubChem MCP server [1] . Access to crystallographic metadata is provided by a custom CCDC MCP server implemented in this work. A configuration file then lists the available tools and their input and
output schemas. The instantiation agent reads this file so it can call both the local instantiation tools and the external PubChem and CCDC tools through a single, consistent
interface.


**Plan-driven ReAct execution.** After the MCP tools are loaded, the instantiation agent
follows the JSON plan step by step. Each step specifies the goal, the prompts to use, the
files to read and write, and the tool groups that are allowed. Figure 7.8 shows an example
plan step.


The agent runs each step in a ReAct-style loop. It first reads the paper and produces intermediate notes, such as condensed passages, extracted snippets, normalised names, and
lookup results. It then calls ontology-specific MCP tools to create entities, add attributes,
and link relations in the Turtle store. A step ends when the expected outputs for that step
have been produced, including any follow-up passes used for enrichment.


1
PubChem-MCP-Server (GitHub): [https://github.com/JackKuo666/PubChem-MCP-Server](https://github.com/JackKuo666/PubChem-MCP-Server)


19


Each tool call returns both results and validation feedback. The feedback reports whether
the requested update satisfies the ontology constraints. If a violation is detected, for example a missing required field, a type mismatch, or an invalid unit (Figure 1), the tool
returns an error with an explanation. The agent then retries with corrected inputs. It may
also extract missing evidence from the paper or query external resources before calling
the tool again.


**Ontology-constrained** **function** **calls.** Ontology constraints are checked at tool-call
time by the instantiation MCP server. The server functions follow the design rules set in
the preparation stage. As illustrated in Figure 7.9, a function such as create_temperature
looks up the relevant OM classes and properties. It checks the numeric value and unit
against the T-Box. Only then does it create the corresponding individuals in the graph.
Similar checks are applied to other quantities, relations, and class memberships.


Each call returns both the requested result and a check report. If the call violates a constraint, the server returns an error with an explanation. The instantiation agent uses this
feedback in the next ReAct step to revise inputs, add missing fields, or trigger a repair
action.


**Turtle-based persistent memory.** Throughout the instantiation stage, all MCP tools operate over a Turtle-encoded knowledge graph (.ttl) that serves as persistent cross-step
memory. The instantiation MCP server reads from and writes to this store, updating it after
each successful creation, enrichment, or repair operation. Instances created in earlier iterations can be looked up, linked, and incrementally extended in later ones, and identifiers
are reused according to the deduplication and IRI-minting logic generated in the preparation scripts. Intermediate Turtle snapshots, together with the file-level Inputs/Outputs
markers, record the state of the extraction run at each iteration, so that processing can be
resumed from a given point or audited after the fact.

##### **6.3 Grounding stage**


After ontology-constrained KG construction, we apply a grounding stage to align constructed instance IRIs to canonical entities in a reference knowledge graph. The purpose
is to normalize identity across documents and pipelines by linking locally minted IRIs
to existing KG IRIs, enabling deduplication and integration. Grounding operates over
selected ontology classes and produces a deterministic IRI mapping that is materialized
either by rewriting IRIs or by adding owl:sameAs links.


**Grounding** **tool** **generation** Grounding tools are generated by LLM agents using the
target ontology T-Box and KG endpoint evidence as the only domain-specific inputs. The
goal of this stage is to compile a grounding interface that supports canonicalization of
constructed instances against a reference knowledge graph.


Given a target ontology schema (T-Box) and a SPARQL endpoint for the existing knowledge graph, the grounding generation agents automatically synthesize three tool scripts.


20


First, a sampling script analyzes the ontology schema and endpoint to identify relevant
classes and label-like predicates and to estimate instance distributions. Second, a label
collection script collect and cache labels and identifiers for selected classes from the target
KG and builds a local label index. Third, a query and lookup interface script is generated
that provides SPARQL accessors and deterministic fuzzy-lookup functions over the local
label index.


The resulting query and lookup interface is exposed as an ontology-specific MCP server
and becomes available as callable grounding tools in the runtime pipeline. This generation
process follows the same ontology-driven tool compilation paradigm as KG construction,
while keeping grounding logic domain-agnostic at runtime.


**Lexical grounding** For each selected constructed instance, the grounding agent extracts
one or more surface strings ( _e.g._ rdfs:label and ontology-specific alternative name
properties) and queries the target-KG MCP lookup server, which returns a ranked, finite
set of candidate matches from a locally cached label index. The agent then applies a deterministic selection policy over this candidate set to choose a canonical target IRI and produces an explicit mapping from constructed IRIs to reference-KG IRIs. Finally, the mapping is materialized either by rewriting IRIs in the RDF graph or by adding owl:sameAs
links; the same procedure supports both single-file and batch grounding over collections
of Turtle outputs.

##### **6.4 Preparation of evaluation dataset**


We curated a benchmark of 30 scientific articles reporting MOP synthesis and characterisation, focusing on papers where the target entities and relations are explicitly described
in the text. Ground truth was annotated in a predefined JSON record format inherited
from the prior (non-MCP) workflow and retained here to enable like-for-like comparison
across pipelines. This schema is aligned with the ontology T-Box in the sense that each
task category specifies the corresponding entity types, relations, and attribute slots to be
captured, but annotation is performed directly in JSON rather than as ontology instances.


We manually populated the JSON records for each paper following written guidelines that
enforce consistent interpretation of entity/slot definitions and alignment with T-Box intent
( _e.g._, inclusion/exclusion criteria and expected value types). The resulting benchmark
contains 6,705 annotated items across synthesis steps, reaction chemicals, characterisation
entities, and chemical building units (CBUs) (Table 7.1).

##### **6.5 Evaluation metrics and methodology**


We evaluate end-to-end extraction _coupled_ _with_ _ontology_ _instantiation_ via a JSON-toJSON protocol. For each task category, system outputs are first instantiated as ontology
individuals and relations in a Turtle graph. We then apply a fixed, manually designed set
of category-specific SPARQL queries (held constant across all runs) to retrieve the target
individuals, relations, and literal attributes implied by the T-Box. Query bindings are


21


deterministically serialised into JSON records conforming to the same predefined schema
used for annotation. This evaluates instantiation quality rather than text extraction alone:
any missing individuals, missing links, or uninstantiated required slots are not returned by
the SPARQL queries and are therefore counted as false negatives.


We compute precision, recall, and F1 by matching predicted and ground-truth JSON
records using _slot-to-slot exact match_ after basic normalisation. Normalisation is limited
to non-semantic formatting fixes ( _e.g._, trimming whitespace, normalising casing where
appropriate, and applying simple canonical forms for common unit strings when the
schema expects a controlled label). For record sets where ordering is not semantically
meaningful (notably synthesis steps), matching is _order-insensitive_ : predicted records are
aligned to ground truth under a one-to-one assignment that maximises the number of
exactly matched slots, and any unmatched predicted or ground-truth records contribute
to false positives or false negatives, respectively. Metrics are reported per category and
aggregated over all papers; we additionally report micro-aggregated scores across all categories and macro-averaged scores across categories.


The same SPARQL-to-JSON conversion and matching procedure is applied to all comparative baselines and ablation settings, ensuring that all results reflect the same end-toend criterion of producing evaluation-recoverable, correctly instantiated outputs. Finally,
CBU items correspond to _grounded/derived_ CBUs constructed by combining paper evidence with database-backed normalisation and derivation; they are therefore evaluated as
an end-to-end grounding task rather than a pure surface-form extraction task.

##### **6.6 CBU derivation for metal-organic polyhedra**


In addition to ontology-driven extraction from textual synthesis procedures, the framework instantiates an agent for automated Chemical Building Unit (CBU) derivation for
metal–organic polyhedra (MOPs). This agent is implemented as a ReAct-style controller
that orchestrates MCP-based access to crystallographic and chemical databases, and then
materialises the resulting CBUs directly into The World Avatar knowledge graph.


The CBU derivation workflow proceeds as follows:


   - **MCP-based** **access** **to** **crystallographic** **data.** Given a MOP identifier ( _e.g._ a
CCDC deposition code), the agent uses an MCP server wrapping the CCDC database
to locate and download the corresponding .res and .cif files. A Python-backed
MCP tool parses these files to extract the asymmetric unit, symmetry operations,
and atom/site information required for structural analysis.


   - **Identification of CBUs.** Using the parsed crystallographic information, the agent
invokes MCP tools for graph-based analysis of the coordination network ( _e.g._ bond
connectivity, coordination environments, linker topology). These tools segment the
structure into metal nodes and organic linkers, and classify them into reusable CBU
types suitable for downstream materials design workflows.


   - **External chemical enrichment via PubChem and related services.** For each organic linker candidate, the agent calls MCP servers that wrap external databases


22


such as PubChem to retrieve standard identifiers (InChI, InChIKey, SMILES), synonyms, and basic physicochemical properties. This enrichment step normalises the
CBUs against widely used chemical identifiers and supports cross-database linkage.


   - **Ontology-aligned** **CBU** **instantiation.** The derived and enriched CBUs are then
passed to the instantiation MCP server, which creates ontology-consistent instances
representing metal nodes, linkers, and their connectivity. The resulting MOP CBU
descriptions are written into the Turtle-based persistent memory and become part
of the shared knowledge graph that can be reused by subsequent extraction and
reasoning tasks.


By combining MCP-based access to CCDC, PubChem, and ontology-aware instantiation
tools, the agent autonomously bridges crystallographic data and ontology-level CBU representations for MOPs, without manual scripting or ad hoc intermediate formats.

##### **6.7 Models and inference settings**


Components in the pipeline are driven by large language models (LLMs), with different
models assigned to different roles. Unless otherwise stated, we use deterministic decoding
for tool-calling (temperature=0) and enforce schema validation on structured outputs
at tool boundaries. For non-tool free-form generations ( _e.g._ narrative rationales or intermediate notes), we retain the same decoding settings unless explicitly noted to ensure
reproducibility across runs.


**Model assignments.** We use gpt-4.1 for document-level extraction; gpt-4o for knowledgegraph instantiation and construction actions; gpt-5 for chemical building unit (CBU)
derivation as well as script and prompt generation; and gpt-4o-mini for the agent-based
query interface, where latency and cost are prioritised.


**Inference and validation.** Tool calls are executed with deterministic decoding (temperature=0)
to minimise stochastic variation in argument selection and to make constraint-violation
feedback comparable across runs. Structured outputs produced at tool boundaries are
validated against the corresponding schemas, and violations are surfaced to the agent as
explicit error messages to drive iterative repair until a valid instance is produced.


**Token usage and cost.** Script and prompt generation incurred a total LLM cost of $5.70.
For end-to-end processing of 30 papers, the total LLM cost for document-level extraction
and instantiation/grounding actions was $100.56 ($71.61 + $28.95), corresponding to an
average per-article cost of $3.35.


23


#### **Data availability**

The curated benchmark dataset (30 MOP synthesis papers with _>_ 6,000 annotations), together with the curated ground truth, output files, and evaluation data used in this study,
are available via the Cambridge Data Repository [(doi:10.17863/CAM.126228)](https://doi.org/10.17863/CAM.126228) and via
Dropbox [2] .

#### **Code availability**


Code for the ontology-to-tools compilation workflow, including the MCP servers/tools
and evaluation scripts, will be made available on GitHub [3] .

#### **Acknowledgements**


This research was supported by the National Research Foundation, Prime Minister’s Office, Singapore under its Campus for Research Excellence and Technological Enterprise
(CREATE) programme. This project has received funding from the European Union’s
Horizon Europe research and innovation programme under grants 101074004 (C2IMPRESS),
101188248 (CLIMATE-ADAPT4EOSC), and 101226137 (TOGETHER).


M.K. gratefully acknowledges the support of the Alexander von Humboldt Foundation
and the Massachusetts Institute of Technology. S.D.R. acknowledges financial support
from Fitzwilliam College, Cambridge, and the Cambridge Trust.


We thank all contributors who assisted with data collection, annotation, and quality control for the manually curated MOP synthesis benchmark. In particular, we thank Mingxi
Lu, Yichen Sun, Yuan Gao, Kuhan Thayalan, and Matthew Olatunji for annotation support.


For the purpose of open access, the author has applied a Creative Commons Attribution
(CC BY) licence to any Author Accepted Manuscript version arising from this submission.

#### **Competing interests**


The authors declare no competing interests.


2https://www.dropbox.com/scl/fi/u0dtyhyfa6cp7cr60jckq/Data-Public.zip
3https://github.com/TheWorldAvatar/mcp-tool-layer/


24


#### **Author contributions**

X.Z. implemented the system, conducted the experiments, and wrote the manuscript. P.B.
and C.Y. led data curation and annotation. T.A. contributed to data curation. J.A. contributed to manuscript writing. S.R. contributed to data curation and provided domain
expertise. M.K. contributed to ideation, conceptualisation, manuscript writing, and funding acquisition. All authors reviewed and approved the final manuscript.


25


