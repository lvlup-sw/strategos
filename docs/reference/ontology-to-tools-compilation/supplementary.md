---
title: "Ontology-to-Tools Compilation — Supplementary Information (p.28-47)"
source: "Zhou, X. et al. (2025). Ontology-to-tools compilation for executable semantic constraint enforcement in LLM agents. arXiv:2602.03439"
notice: "Reference copy for internal analysis"
---

## 7 Supplementary Information

##### **7.1 Supplementary Note 1: Background and related work**

**7.1.1** **LLM-based agents and ReAct-style reasoning**


Large language model (LLM)–based agents couple a generative model with an explicit
action space, where each step conditions on a textual context (for example, a user request,
intermediate results, and tool outputs) and selects the next action such as calling a tool,
querying a knowledge graph, or producing a natural-language response. [48, 53, 56, 60]
External capabilities are typically exposed through structured interfaces (for example,
function signatures with typed arguments), and tool responses are returned as additional
context to enable multi-step interaction with external systems. [48, 53]


ReAct-style agents organise this interaction as an explicit loop of _reasoning_ and _acting_,
alternating between intermediate reasoning traces and tool actions, and incorporating tool
observations into subsequent decisions. [60] In information extraction, this framing supports decomposing long inputs, validating intermediate structures, and iteratively refining
partial outputs through repeated reasoning–acting cycles.


**7.1.2** **Tool-calling interfaces and the Model Context Protocol**


Earlier tool-calling interfaces for LLMs typically expose application-specific functions
through proprietary schemas or prompt templates, and integration logic is often implemented separately for each deployment. [8, 27, 31] The Model Context Protocol (MCP)
instead defines an open client–server protocol for connecting LLM applications to external tools and data sources. In MCP, servers register tools, resources, and prompts with
machine-readable schemas; clients discover these capabilities and request tool invocations through a standardised messaging layer. [35, 38] MCP supports structured tool definitions, streaming of results, and standardised error reporting across heterogeneous hosts
and models, enabling reuse of the same tool servers across different LLM applications.


**7.1.3** **The World Avatar and its chemistry ontologies**


The World Avatar (TWA) is a cross-domain digital ecosystem that represents entities,
processes, and their interdependencies using ontologies and RDF knowledge graphs, accessed and modified by software agents at runtime. [4] The reticular-chemistry stack of
TWA is organised around two domain-generic core ontologies and one application ontology. OntoSpecies represents chemical species, identifiers, and characterisation data (with
extensions for IR and elemental analysis). OntoSyn (OntoSynthesis) represents synthesis procedures as sequences of SynthesisStep operations transforming input Species
into ChemicalOutput products. OntoMOPs acts as an application ontology for metal–
organic polyhedra (MOPs), modelling MOP instances and their building units and linking
OntoSyn procedures and OntoSpecies reactants into the MOP design space. [4, 24, 40, 45]
Previous work implemented a MOP synthesis pipeline in TWA that used LLMs guided by


26


ontology-aligned JSON schemas to populate these chemistry knowledge graphs. [45]


**7.1.4** **LLM-based knowledge instantiation and ontology grounding**


Large language models are increasingly used to construct and maintain knowledge graphs,
by inducing schemas, populating instances, or interacting with graph backends. [28, 44]
From the perspective of this work, two lines are most relevant: schema-level approaches
that define or refine extraction targets, and instance-level approaches that extract and validate concrete assertions.


**Schema-level** **(T-Box).** Schema-level pipelines treat the extraction schema as a contract that defines what types, fields, and constraints outputs should satisfy. PARSE refines JSON Schemas as first-class artefacts for extraction, but optimises primarily syntactic constraints encoded at the JSON-schema level rather than ontology axioms. [50]
LLMs4SchemaDiscovery mines candidate scientific schemas from text with human feedback, again focusing on schema discovery rather than using a fixed ontology T-Box as the
contract. [47] SCHEMA-MINERpro adds ontology grounding by mapping discovered
schemas to reference ontologies, but grounding remains a stage separate from instance
creation. [46] AutoSchemaKG induces schemas and triples at scale without a predefined
domain T-Box, enabling cross-domain graphs but decoupling extraction from ontologyencoded constraints such as units and identification rules. [3]


**Instance-level (A-Box).** Instance-level pipelines focus on extracting entities, relations,
and events as concrete assertions. KGGen uses strict JSON specifications to extract and
refine triples, improving structural consistency, but semantic validity with respect to a
concrete ontology is enforced via post-hoc checks rather than constraint-aware instance
construction. [33] Other pipelines align extracted relations to ontology predicates or use
ontologies for filtering and validation, but typically keep ontology enforcement separate
from the tool interfaces that govern how instances are created. [22, 23, 36]


**MCP-assisted** **LLM-based** **solutions.** MCP has emerged as a general mechanism for
connecting LLM applications to tools and data sources. [35, 38] Existing MCP-based
knowledge-graph servers often expose generic CRUD-style interfaces over graph backends, demonstrating tool-based access but typically leaving ontological correctness to
application logic or post-hoc validation rather than compiling T-Box axioms into tool signatures and runtime checks. [18]


**TWA** **agent** **composition** **framework** Prior to recent LLM-based agent systems, The
World Avatar (TWA) explored how semantic descriptions can support the _discovery_, _com-_
_position_, and _execution_ of software agents within a knowledge-graph ecosystem. [69]
introduced a semantic agent composition framework for TWA, in which agents are described using a lightweight agent ontology and grounded execution metadata to enable
automated composition of agent workflows across domains.


27


This work motivates a view of ontologies not only as data models but also as interfaces to
execution, where semantic descriptions govern which agents can be composed and how
they may interact. In contrast to agent discovery and composition based on pre-defined
interfaces, the present work compiles domain ontologies into executable tool interfaces
and run-time constraints that directly regulate generative behaviour.

##### **7.2 Supplementary Methods**


This section provides extended implementation and evaluation details referenced in the
main Methods, including dataset curation and annotation protocol, evaluation metrics and
matching rules (including the CBU formula-only criterion), MCP server/tool specifications, and runtime policies (iterations, error handling, and repair strategies).


28


Agentcreated
MCP tools



Predefined
scripts



T/A
Boxes



Predefined
MCP tools



LLM-powered
Agents



**Figure 7.6:** _Ontology-to-tools compilation workflow._ _A preparation agent takes as input_
_the_ _ontology_ _T-Box_ _and_ _a_ _small_ _set_ _of_ _manually_ _authored,_ _domain-agnostic_
_meta-prompts_ _for_ _extraction_ _and_ _KG_ _construction._ _It_ _synthesises_ _(i)_ _an_
_ontology-specific MCP server exposing ontology-aware tools with machine-_
_checkable_ _schemas_ _and_ _(ii)_ _domain-_ _and_ _task-specific_ _instantiation_ _prompts_
_(for synthesis steps, reaction chemicals, characterisation entities, and CBUs)_
_that steer the runtime agent._ _A ReAct-style instantiation agent then executes_
_the_ _generated_ _prompts_ _and_ _invokes_ _the_ _MCP_ _tools_ _to_ _construct_ _knowledge-_
_graph instances from documents._


29


**Figure 7.7:** _UML workflow of the two-agent framework for ontology-aligned knowledge-_
_graph_ _construction_ _within_ _the_ _World_ _Avatar_ _(TWA)_ _environment._ _The_ _**MCP**_
_**Creation**_ _**Agent**_ _runs_ _in_ _the_ _TWA_ _context_ _and takes_ _the_ _domain_ _ontology_ _(T-_
_Box;_ _schema) and an MCP Creation Prompt to generate a KG instantiation_
_library._ _The_ _**MCP Integration Agent**_ _then takes an MCP Integration Prompt_
_plus the validated library (and its function descriptions) to package it into a_
_deployable MCP server, exposing these functions as callable MCP tools and_
_registering them into a shared MCP tool pool for reuse by downstream LLM_
_agents, including TWA workflows._

30


**Figure 7.8:** _Class_ _diagram_ _of_ _the_ _structured_ _task_ _specification:_ _a_ _task_ _decomposition_
_model,_ _guided_ _by_ _the_ _Ontology_ _T-Box_ _and_ _a_ _meta-prompt,_ _generates_ _itera-_
_tions that bundle and KG-building steps, file flows (inputs/outputs), optional_
_sub-iterations, and MCP tool configurations._


31


**Figure 7.9:** _Illustrative_ _example_ _of_ _the_ _interaction_ _between_ _the_ _OM_ _T-Box_ _(top),_ _a_ _sin-_
_gle_ _create_temperature_ _call_ _within_ _the_ _MCP_ _server_ _logic_ _(middle),_ _and_
_the resulting OM instance (bottom)._ _The top box shows the ontology T-Box,_
_which_ _defines_ _schema-level_ _constraints_ _for_ _temperature_ _quantities,_ _such_ _as_
_the relevant classes and properties._ _The middle box shows an example script_
_call that enforces these constraints by looking up the unit, validating the nu-_
_meric value, and only then creating the corresponding OM individuals for the_
_temperature value and its unit._ _The bottom box shows the resulting example_
_instance that satisfies the T-Box constraints, linking the created quantity to a_
_valid unit and a compliant numerical value._


32


**7.2.1** **Iteration 1:** **System Meta Prompt**


You are a knowledge graph construction prompt expert specializing in


ITERATION 1 KG building prompts.


ITERATION 1 is special: it only creates top-level entity instances from


paper content, WITHOUT creating any related entities (inputs,

outputs, sub-components, or detailed steps).


Your task:

1. Analyze the provided ontology (T-Box) to identify the top entity


type (the main class that represents the overall procedure/process)

2. Extract all relevant rules, constraints, and identification


guidelines from rdfs:comment annotations

3. Generate a focused, tool-oriented KG building prompt for ITERATION 1


CRITICAL CONSTRAINTS FOR ITER1:

- ONLY create instances of the top-level entity class (one per


procedure described in the paper)

- Do NOT create any related entities (inputs, outputs, sub-components,


steps) in this iteration

- Link each top-level entity to its source document

- Apply strict identification rules from the ontology to determine what


qualifies as a valid instance

- Include all cardinality, scope, exclusion, and linking rules from the


ontology


OUTPUT REQUIREMENTS:

- Start with "Follow these generic rules for any iteration." followed


by the global rules

- Include MCP tool-specific guidance (error handling, IRI management,


check_existing_* usage)

- Include explicit identification section (document identifier handling)

- Clearly state the task scope: create top-level entities only, no


related entities

- List all constraints from the ontology rdfs:comment for the top-level


entity class

- Include clear termination conditions

- Be concise and tool-oriented (this prompt is for an MCP agent using


function calls)

- Be completely domain-agnostic (no specific compound types, no


domain-specific terminology)


Do NOT include:

- Domain-specific examples (\eg specific compound names, specific


synthesis types)

- Variable placeholders like {doi} or {paper_content} - these will be


added programmatically


33


- Verbose ontology explanations - focus on actionable rules

- Any mention of specific chemical entities, materials, or


domain-specific concepts


**7.2.2** **Iteration 1:** **User Meta Prompt**


Based on the ontology and available MCP tools below, generate a KG


building prompt for ITERATION 1.


ITERATION 1 SCOPE:

- Create ONLY top-level entity instances (the main procedure/process


class)

- Do NOT create any related entities (inputs, outputs, sub-components,


steps)

- Link to source document


ONTOLOGY (T-Box):

{tbox}


MCP MAIN SCRIPT (Available Tools) [Python]:

{mcp_main_script}


The MCP Main Script shows all available tools with their descriptions,


parameters, and usage guidance. Use this to understand what tools

are available and how they should be called.


REQUIREMENTS:

1. Extract ALL rules from the top-level entity class rdfs:comment,


especially:

  - Scope (what qualifies as a valid instance)

  - Different forms / methods / variations rules

  - Exclusions (what NOT to create)

  - Cardinality requirements

  - Linking requirements

  - Conservative behavior guidelines

  - Critical exclusions for extraction


2. Include global MCP rules:


  - Tool invocation rules (never call same tool twice with identical


args)

  - IRI management (must create before passing, use check_existing_*


tools)

  - Error handling (status codes, already_attached, retryable)

  - Placeholder policies

  - Termination conditions (run_status: done)


34


3. Include identification section:


  - Document identifier handling (treat as sole task identifier, reuse


consistently)

  - Entity focus guidance (for when entity_label/entity_uri provided)


4. Be concise and actionable - this is for an MCP agent making function


calls


5. Be completely domain-agnostic:


  - Do NOT mention specific compound types, materials, or chemical


entities

  - Use generic terminology that applies to any domain

  - Adapt wording from the ontology to be domain-neutral where possible


Generate the prompt now (do NOT include variable placeholders - those


will be added programmatically):


**7.2.3** **Extension Ontology:** **System Meta Prompt**


You are an expert in creating knowledge graph building prompts for


extension ontologies.


Extension ontologies are simpler ontologies that extend a main ontology


with additional specialized information. They use MCP tools to

build A-Boxes that link to the main ontology’s A-Box.


Your task is to analyze a T-Box ontology and MCP tools to create a KG


building prompt that:

1. Provides a clear task route for building the extension A-Box

2. Emphasizes using MCP tools to populate the A-Box

3. Requires comprehensive population (making certain MCP function calls


compulsory)

4. Emphasizes IRI reuse from the main ontology A-Box

5. Includes domain-specific requirements extracted from the T-Box


comments

6. Forbids fabrication - only use information from the paper


CRITICAL RULES:

- Read ALL classes, properties, and rdfs:comment fields in the T-Box

- Extract domain-specific requirements from rdfs:comment (\eg required


fields, cardinality constraints)

- Focus on HOW to build the A-Box, not just WHAT to extract

- Emphasize the connection between the extension A-Box and the main


ontology A-Box

- Make the prompt actionable with clear steps

- Output ONLY the prompt text (no markdown fences, no commentary)


35


**7.2.4** **Extension Ontology:** **User Meta Prompt**


Generate a KG building prompt for an extension ontology.


T-Box (analyze to understand the ontology structure and requirements):

```turtle
{tbox}
```

MCP Main Script (understand available tools, their parameters, and


calling sequences):


```python
{mcp_main_script}
```

Your prompt MUST:


State the task - Extend the main ontology A-Box with the extension A-Box


Provide a task route - Give a recommended sequence of steps for


building the KG


Emphasize MCP tools - Instruct to use the MCP server to populate the


A-Box


Require IRI reuse - Emphasize reusing existing IRIs from the main


ontology A-Box


Extract T-Box requirements - Include any compulsory requirements from


rdfs:comment (\eg required fields, minimum instances)


Forbid fabrication - Only use information from the paper content


Include tool-specific notes - If the T-Box or domain requires special


handling (\eg external database integration, data transformations),

include those notes


Structure your output as:


```text
Your task is to extend the provided A-Box of [MainOntology] with the


[extension ontology] A-Box, according to the paper content.


You should use the provided MCP server to populate the [extension


ontology] A-Box.


Here is the recommended route of task:


[Step-by-step guidance based on T-Box structure]


36


Requirements:


[List of requirements based on T-Box rdfs:comment and MCP tool


constraints]


Special note:


[Any domain-specific notes based on T-Box comments]


Here is the DOI for this run (normalized and pipeline forms):


- DOI: {{doi_slash}}

- Pipeline DOI: {{doi_underscore}}


Here is the [MainOntology] A-Box:


{{main_ontology_a_box}}


Here is the paper content:


{{paper_content}}
```


CRITICAL:


Extract domain-specific requirements from the T-Box rdfs:comment


fields. Do NOT invent requirements. ALL requirements must be

justified by the T-Box or MCP tool constraints.


Output EXACTLY the structure shown above. Do NOT add any additional


sections after {{paper_content}}. This is the END of the prompt.


Generate the prompt now:


37


**7.2.5** **Illustrative trace of constraint-triggered repair**


Listing 1 shows a representative repair cycle triggered by an ontology constraint on measurement units. The ontology restricts temperature units to a controlled vocabulary that
includes _degree Celsius_ . When the instantiation agent first instantiates a temperature condition using the verbatim unit token _C_ from text, the ontology-aware MCP tool rejects
the input and returns a structured validation error listing allowed values. The agent then
normalises the unit to an allowed entry and retries, producing a valid instance.


**Listing 1:** _Representative MCP trace: ontology-constrained unit repair (C →_ _degree Cel-_
_sius)._


Input evidence (paper text):


"... heated at 120 C for 12 h ..."


Attempt 1 (verbatim unit token):


Tool: mop.create_temperature_condition

Input: { "context": "reaction_step_3",


"value": 120,

"unit": "C" }


Tool response (constraint violation):


{ "ok": false,


"error_type": "OntologyConstraintViolation",

"field": "unit",

"message": "Unit value ’C’ is not permitted by the ontology.",

"allowed_values": ["degree Celsius", "kelvin"]

}


Repair (from tool feedback):


 - Map shorthand unit token "C" to the allowed vocabulary entry


"degree Celsius".

 - Retry tool call with corrected unit.


Attempt 2 (normalised unit):


Tool: mop.create_temperature_condition

Input:


{ "context": "reaction_step_3",


"value": 120,

"unit": "degree Celsius"

}


Tool response (success):


{


"ok": true,

"instance_iri": "twa:TemperatureCondition_0f3a...",

"validated": true

}


38


##### **7.3 Supplementary Evaluation Results**

Tables 7.4–7.6 report per-paper extraction performance against the full ground truth for
the three evaluated outputs: synthesis steps (Table 7.4), chemical building units under a
formula-only matching criterion (Table 7.5), and characterisation entities (Table 7.6). For
each paper (indexed by DOI), we provide true positives (TP), false positives (FP), false
negatives (FN), and the derived precision, recall, and F1 scores, followed by an overall
aggregate row.


**Table 7.1:** _Benchmark_ _profile_ _(30_ _papers)._ _Ground-truth_ _positives_ _are_ _computed_ _as_
_TP_ + _FN from Table 7.2._


Category Papers Ground-truth positives
CBU 30 164
Characterisation 30 926
Steps 30 4940
Chemicals 30 675
Total 30 6705


**7.3.1** **Detailed instance visualisation for a UMC-1 synthesis**


The Supplementary Information includes a detailed visualisation of one instantiated ontology subgraph corresponding to the synthesis of the metal–organic polyhedron UMC-1
(Supplementary Fig. 7.10). The figure shows the complete set of instantiated individuals
and relations for this synthesis recipe under the OntoSyn and OntoMOPs ontologies, including typed synthesis steps, chemical inputs, and the resulting product entity, without
the abstraction and condensation used in the main-text example (Fig. 1).


The graph was initially rendered from the RDF serialisation using an RDF-to-Graphviz
visualisation tool [4] and then manually edited to adjust the layout for presentation and page
fitting.


[rdf2dot](https://giacomociti.github.io/rdf2dot/)


39


**Table 7.2:** _Overall_ _evaluation_ _summary_ _by_ _category_ _(system_ _output_ _recovered_ _from_ _in-_
_stantiated graphs vs. full ground truth)._ _CBU denotes grounded/derived CBUs_
_(Methods)._


Category TP FP FN Precision Recall F1
CBU 128 40 36 0.762 0.780 0.771
Characterisation 669 102 257 0.868 0.722 0.788
Steps 4225 857 715 0.831 0.855 0.843
Chemicals 393 0 282 1.000 0.582 0.736
**Micro-aggregate** **5415** **999** **1290** **0.844** **0.808** **0.826**
**Macro-average**     -     -     - **0.865** **0.735** **0.785**


**Table 7.3:** _Ablation study._ _CBU denotes grounded/derived CBUs (Methods)._


Setting CBU F1 Char. F1 Steps F1 Chem. F1
Full system **0.771** **0.788** **0.843** **0.736**

      - external tools 0.000 0.610 0.800 0.732

      - constraint feedback 0.768 0.788 0.572 0.724


40


**Table 7.4:** _Per-paper extraction results for synthesis steps against the full ground truth._


DOI TP FP FN Precision Recall F1
10.1002.anie.201811027 [11] 185 31 19 0.856 0.907 0.881
10.1002.anie.202010824 [12] 305 10 19 0.968 0.941 0.955
10.1002.chem.201604264 [30] 245 28 30 0.897 0.891 0.894
10.1002.chem.201700798 [20] 47 10 8 0.825 0.855 0.839
10.1002.chem.201700848 [10] 111 20 30 0.847 0.787 0.816
10.1021.acs.cgd.6b00306 [43] 35 10 4 0.778 0.897 0.833
10.1021.acs.chemmater.8b01667 [6] 105 35 24 0.750 0.814 0.781
10.1021.acs.inorgchem.4c02394 [54] 216 38 44 0.850 0.831 0.840
10.1021.acs.inorgchem.8b01130 [14] 207 74 70 0.737 0.747 0.742
10.1021.acsami.7b09339 [39] 117 35 40 0.770 0.745 0.757
10.1021.acsami.7b18836 [26] 80 24 6 0.769 0.930 0.842
10.1021.acsami.8b02015 [5] 118 28 37 0.808 0.761 0.784
10.1021.cg4018322 [41] 50 4 10 0.926 0.833 0.877
10.1021.ic050460z [21] 139 63 41 0.688 0.772 0.728
10.1021.ic402428m [29] 180 51 36 0.779 0.833 0.805
10.1021.ic501012e [52] 147 7 3 0.955 0.980 0.967
10.1021.ic802382p [42] 84 14 5 0.857 0.944 0.898
10.1021.ja042802q [51] 389 135 80 0.742 0.829 0.783
10.1021.ja105986b [67] 63 23 25 0.733 0.716 0.724
10.1021.jacs.7b00037 [62] 128 4 7 0.970 0.948 0.959
10.1021.jacs.8b10866 [63] 214 51 28 0.808 0.884 0.844
10.1039.C2CC34265K [9] 166 22 33 0.883 0.834 0.858
10.1039.C5CC05913E [2] 55 13 4 0.809 0.932 0.866
10.1039.C5DT04764A [66] 161 0 3 1.000 0.982 0.991
10.1039.C5RA26357C [7] 50 14 14 0.781 0.781 0.781
10.1039.C6CC04583A [65] 206 64 52 0.763 0.798 0.780
10.1039.C6DT02764D [64] 103 15 15 0.873 0.873 0.873
10.1039.C7CC01208J [61] 92 10 10 0.902 0.902 0.902
10.1039.C8DT02580K [13] 119 5 8 0.960 0.937 0.948
10.1039.D3QI01501G [59] 106 20 12 0.841 0.898 0.869
Overall 4223 858 717 0.831 0.855 0.843


41


**Table 7.5:** _Per-paper formula-only extraction results for chemical building units (CBUs)_
_against the full ground truth._


DOI TP FP FN Precision Recall F1
10.1021.acsami.7b18836 [26] 3 1 1 0.750 0.750 0.750
10.1039.C8DT02580K [13] 4 0 0 1.000 1.000 1.000
10.1002.chem.201700848 [10] 4 2 2 0.667 0.667 0.667
10.1021.acs.inorgchem.4c02394 [54] 8 2 2 0.800 0.800 0.800
10.1021.ic402428m [29] 8 0 0 1.000 1.000 1.000
10.1039.C5RA26357C [7] 2 0 0 1.000 1.000 1.000
10.1039.C6DT02764D [64] 2 2 2 0.500 0.500 0.500
10.1021.ic501012e [52] 4 0 0 1.000 1.000 1.000
10.1002.chem.201604264 [30] 8 2 2 0.800 0.800 0.800
10.1021.jacs.7b00037 [62] 1 3 3 0.250 0.250 0.250
10.1039.C7CC01208J [61] 2 2 2 0.500 0.500 0.500
10.1021.acschemmater.8b01667 [6] 6 2 0 0.750 1.000 0.857
10.1039.C5DT04764A [66] 3 3 3 0.500 0.500 0.500
10.1039.C6CC04583A [65] 10 0 0 1.000 1.000 1.000
10.1021.ic802382p [42] 2 0 0 1.000 1.000 1.000
10.1002.anie.202010824 [12] 7 3 3 0.700 0.700 0.700
10.1021.acs.inorgchem.8b01130 [14] 4 2 2 0.667 0.667 0.667
10.1039.C2CC34265K [9] 3 3 3 0.500 0.500 0.500
10.1021.acsami.7b09339 [39] 8 0 0 1.000 1.000 1.000
10.1002.anie.201811027 [11] 4 4 0 0.500 1.000 0.667
10.1021.acs.cgd.6b00306 [43] 0 2 2 0.000 0.000 0.000
10.1039.D3QI01501G [59] 2 0 2 1.000 0.500 0.667
10.1021.cg4018322 [41] 2 0 0 1.000 1.000 1.000
10.1039.C5CC05913E [2] 2 0 0 1.000 1.000 1.000
10.1021.ja105986b [67] 2 0 0 1.000 1.000 1.000
10.1021.ic050460z [21] 6 0 0 1.000 1.000 1.000
10.1021.jacs.8b10866 [63] 6 2 2 0.750 0.750 0.750
10.1021.ja042802q [51] 7 5 3 0.583 0.700 0.636
10.1021.acsami.8b02015 [5] 6 0 2 1.000 0.750 0.857
10.1002.chem.201700798 [20] 2 0 0 1.000 1.000 1.000
Overall 128 40 36 0.762 0.780 0.771


42


**Table 7.6:** _Per-paper_ _extraction_ _results_ _for_ _characterisation_ _entities_ _against_ _the_ _full_
_ground truth._


DOI TP FP FN Precision Recall F1
10.1002.anie.201811027 [11] 26 0 4 1.000 0.867 0.929
10.1002.anie.202010824 [12] 36 11 16 0.766 0.692 0.727
10.1002.chem.201604264 [30] 10 7 16 0.588 0.385 0.465
10.1002.chem.201700798 [20] 5 2 5 0.714 0.500 0.588
10.1002.chem.201700848 [10] 21 4 15 0.840 0.583 0.689
10.1021.acs.cgd.6b00306 [43] 8 1 1 0.889 0.889 0.889
10.1021.acs.chemmater.8b01667 [6] 36 3 10 0.923 0.783 0.847
10.1021.acs.inorgchem.4c02394 [54] 43 9 16 0.827 0.729 0.775
10.1021.acs.inorgchem.8b01130 [14] 38 1 11 0.974 0.776 0.864
10.1021.acsami.7b09339 [39] 29 1 11 0.967 0.725 0.829
10.1021.acsami.7b18836 [26] 17 2 2 0.895 0.895 0.895
10.1021.acsami.8b02015 [5] 19 2 8 0.905 0.704 0.792
10.1021.cg4018322 [41] 13 1 17 0.929 0.433 0.591
10.1021.ic050460z [21] 31 1 7 0.969 0.816 0.886
10.1021.ic402428m [29] 20 0 24 1.000 0.455 0.625
10.1021.ic501012e [52] 14 4 6 0.778 0.700 0.737
10.1021.ic802382p [42] 7 2 5 0.778 0.583 0.667
10.1021.ja042802q [51] 51 2 6 0.962 0.895 0.927
10.1021.ja105986b [67] 8 2 5 0.800 0.615 0.696
10.1021.jacs.7b00037 [62] 16 6 4 0.727 0.800 0.762
10.1021.jacs.8b10866 [63] 41 8 9 0.837 0.820 0.828
10.1039.C2CC34265K [9] 23 8 13 0.742 0.639 0.687
10.1039.C5CC05913E [2] 7 2 4 0.778 0.636 0.700
10.1039.C5DT04764A [66] 24 2 6 0.923 0.800 0.857
10.1039.C5RA26357C [7] 8 2 5 0.800 0.615 0.696
10.1039.C6CC04583A [65] 43 10 15 0.811 0.741 0.775
10.1039.C6DT02764D [64] 18 2 4 0.900 0.818 0.857
10.1039.C7CC01208J [61] 18 2 4 0.900 0.818 0.857
10.1039.C8DT02580K [13] 16 4 4 0.800 0.800 0.800
10.1039.D3QI01501G [59] 22 4 11 0.846 0.667 0.746
Overall 668 105 264 0.864 0.717 0.784


43


**Table 7.7:** _Per-paper extraction results for chemicals against the full ground truth._


DOI TP FP FN Precision Recall F1
10.1002.anie.201811027 [11] 8 0 6 1.000 0.571 0.727
10.1002.anie.202010824 [12] 24 0 6 1.000 0.800 0.889
10.1002.chem.201604264 [30] 22 0 13 1.000 0.629 0.772
10.1002.chem.201700798 [20] 3 0 6 1.000 0.333 0.500
10.1002.chem.201700848 [10] 12 0 7 1.000 0.632 0.774
10.1021.acs.cgd.6b00306 [43] 5 0 3 1.000 0.625 0.769
10.1021.acs.chemmater.8b01667 [6] 7 0 1 1.000 0.875 0.933
10.1021.acs.inorgchem.4c02394 [54] 24 0 14 1.000 0.632 0.774
10.1021.acs.inorgchem.8b01130 [14] 7 0 25 1.000 0.219 0.359
10.1021.acsami.7b09339 [39] 5 0 1 1.000 0.833 0.909
10.1021.acsami.7b18836 [26] 11 0 3 1.000 0.786 0.880
10.1021.acsami.8b02015 [5] 7 0 4 1.000 0.636 0.778
10.1021.cg4018322 [41] 5 0 14 1.000 0.263 0.417
10.1021.ic050460z [21] 12 0 3 1.000 0.800 0.889
10.1021.ic402428m [29] 28 0 5 1.000 0.848 0.918
10.1021.ic501012e [52] 18 0 12 1.000 0.600 0.750
10.1021.ic802382p [42] 7 0 7 1.000 0.500 0.667
10.1021.ja042802q [51] 31 0 14 1.000 0.689 0.816
10.1021.ja105986b [67] 6 0 5 1.000 0.545 0.706
10.1021.jacs.7b00037 [62] 14 0 13 1.000 0.519 0.683
10.1021.jacs.8b10866 [63] 30 0 29 1.000 0.508 0.674
10.1039.C2CC34265K [9] 17 0 15 1.000 0.531 0.694
10.1039.C5CC05913E [2] 6 0 6 1.000 0.500 0.667
10.1039.C5DT04764A [66] 12 0 26 1.000 0.316 0.480
10.1039.C5RA26357C [7] 7 0 9 1.000 0.438 0.609
10.1039.C6CC04583A [65] 23 0 12 1.000 0.657 0.793
10.1039.C6DT02764D [64] 11 0 5 1.000 0.688 0.815
10.1039.C7CC01208J [61] 11 0 9 1.000 0.550 0.710
10.1039.C8DT02580K [13] 11 0 3 1.000 0.786 0.880
10.1039.D3QI01501G [59] 9 0 6 1.000 0.600 0.750
Overall 393 0 282 1.000 0.582 0.736


44


**Figure 7.10:** _**Detailed ontology instance subgraph for a UMC-1 synthesis.**_

45


