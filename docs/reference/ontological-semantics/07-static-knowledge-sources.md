---
title: "Chapter 7: The Static Knowledge Sources: Ontology, Fact Database and Lexicons"
source: "Nirenburg, S. & Raskin, V. (2004). Ontological Semantics. MIT Press."
pdf_pages: "155-203"
notice: "Private reference copy -- not for distribution"
---

# 7. The Static Knowledge Sources: Ontology, Fact Database and Lexicons
In ontological semantics, static knowledge sources include the ontology, the fact database and, for each of the languages used in an application, a lexicon that includes an onomasticon, a lexicon of names (see Figure 22). The ontology provides a metalanguage for describing the meaning of lexical units of a language as well as for the specification of meaning encoded in TMRs. In order to accomplish this, the ontology lists the definitions of concepts that are understood as corresponding to classes of things and events in the world. Formatwise, the ontology is a collection of frames, or named collections of property-value pairs. The Fact DB contains a list of remembered instances of ontological concepts. In other words, if the ontology has a concept for CITY, the Fact
DB may contain entries for London, Paris or Rome; if the ontology has the concept for SPORTSEVENT, the Fact DB will have an entry for the Sydney Olympics.


The ontological semantic lexicon contains not just semantic information. However, when it comes to semantics, it specifies what concept, concepts, property or properties of concepts defined in the ontology must be instantiated in the TMR to account for the meaning of a particular lexical unit of input. Lexical units that refer to proper names are listed in the onomasticon. The entries in the onomasticon directly point to elements of the Fact DB. Onomasticon entries are indexed by name
(the way these words and phrases appear in the text), while in the corresponding entry of the Fact
DB the instances are named by appending a unique number to the name of their corresponding concept.


The notion of instantiation is central to ontological semantics. Instances of ontological concepts are produced during analysis of natural language texts and manipulated during their synthesis.
They are also used alongside concepts in a variety of inference making processes that derive conclusions based on the analysis of input but not overtly specified in the text. The Fact DB simply makes the information in TMRs produced within various applications permanently available for further processing, as needed.


Figure 22 illustrates the relationships among static knowledge sources. The modules in the lefthand column contain world knowledge, those in the right-hand column, elements of natural language. The modules in the top row refer to general entities, referring to any instance of a word or a concept; the modules in the bottom row specify instances of concepts and their names that point to the named concept instances.


|Ontology|Col2|
|---|---|
|||
|Fact DB<br>Object Instances<br>Event Instances|Fact DB<br>Object Instances<br>Event Instances|


**Figure 22. A schematic view of the interactions among the major ontological semantic**
**knowledge sources—the ontology, the Fact DB, the lexicon and the**
**onomasticon**


## 7.1 The Ontology
We have already introduced a format, the TMR, for representing text meaning in ontological semantics (see Chapter 6). It is time now to concentrate on the provenance of the most essential building blocks of the TMR, specifically, the meanings of most open-class lexical items that constitute the starting point for the compositional semantic process that, if successful, leads to a basic
TMR (the compositional semantic processing is described in detail in Chapter 8 below). In ontological semantics, such lexical meanings are represented as expressions in a special metalanguage whose vocabulary labels representations of events, objects and their properties and whose syntax is specially designed to facilitate expressing complex lexical meanings. The representations of the meaning of individual events, objects and their properties are organized in a structure called an ontology.


The difference between the language of the TMR and the language of the ontology largely parallels the distinction between the description languages (those that do not contain predication, in linguistic terms) and assertion languages (that contain predication) in AI knowledge representation systems such as NIKL (Kaczmarek _et al._ 1986) or KRYPTON (Brachman _et al._ 1983). The most important difference between ontological semantics and knowledge representation in AI is the former’s accent on broad practical coverage of semantic phenomena and the latter’s accent on the theoretical completeness and non-contradictoriness of the formal representation system.


In order to build large and useful natural language processing systems one has to go beyond formalism and actually commit oneself to a detailed version of a “constructed reality” (Jackendoff
1983). Interpreting the meanings of textual units is really feasible only in the presence of a detailed world model whose elements are triggered (either directly or indirectly, individually or in combinations) by the appearance in the input text of various textual units whose lexicon entries contain pointers to certain ontological concepts.


World model elements should be interconnected through a set of properties, which will enable the world modeler to build descriptions of complex objects and processes in a compositional fashion, using as few basic primitive concepts as possible. At the same time, having the complete description of a world as its main objective, an ontological semanticist will not have the motivation or inclination to spend time on searching for the smallest set of basic concepts that could be combined to provide a complete description of the world. Parsimony is desirable and justified only if the completeness and clarity of the description is not jeopardized. Indeed, parsimony often stands in a trade-off relation with the simplicity of knowledge formulation and ease of its manipulation.
In other words, in practical approaches, it may be well worth one’s while to allow larger sets of primitives in exchange for being able to represent meaning using simpler and more transparent expressions. It is clear from the above that we believe that, as in software engineering, where programs must be readily understandable by both computers and people, ontological (and other static) knowledge in an ontological semantic system must be readily comprehensible to people who acquire and inspect it as well as to computer programs that are supposed to manipulate it.


In ontological semantics, the real primitives are properties—attributes of concepts and relations among concepts. These properties are not just uninterpreted labels but rather functions from their domains (sets of ontological elements whose semantics they help describe) into value sets. The latter can thus also be considered primitive elements in the ontology. All other concepts are named sets of property-value pairs that refer to complex objects which are described using combinations of the primitives. What this means is that ontological semantics features a relatively small set of primitive concepts but at the same time has a rather rich inventory of elements available for representing and manipulating lexical meaning.


An ontological model must define a large set of generally applicable categories for world description. among the types of such categories are:


- perceptual and common sense categories necessary for an intelligent agent to interact with, manipulate and refer to states of the outside world;

- categories for encoding interagent knowledge which includes one's own as well as other agents’ intentions, plans, actions and beliefs;

- categories that help describe metaknowledge (i.e., knowledge about knowledge and its manipulation, including rules of behavior and heuristics for constraining search spaces in various processor components);

- means of encoding categories generated through the application of the above inference knowledge to the contents of an agent’s world model.


The choice of categories is not a straightforward task, as anyone who has tried realistic-scale world description knows all too well. Here are some examples of the issues encountered in such


an undertaking:


- Which of the set of attributes pertinent to a certain concept should be singled out as ‘conceptforming’ and thus have named nodes in the ontology corresponding to them, and which others should be accessible only through the concept of which they are properties? As an example, consider whether one should further subdivide the class VEHICLE into WATERVEHICLE, LAND-VEHICLE, AIR-VEHICLE; or, rather, into ENGINE-VEHICLE, ANIMALPROPELLED-VEHICLE, GRAVITY-PROPELLED-VEHICLE; or, perhaps, into CARGO-VEHICLE,
PASSENGER-VEHICLE, TOY-VEHICLE, MIXED-CARGO-AND-PASSENGER-VEHICLE? Or maybe it is preferable to have a large number of small classes, such as WATER-PASSENGER-ANIMALPROPELLED-VEHICLE, of which, for instance, ROWBOAT will be a member?

- Which entities should be considered objects and which ones relations? Should we interpret a cable connecting a computer and a terminal as a relation (just kidding)? Or should we rather define it as a PHYSICAL-OBJECT and then specify its typical role in the static episode or
‘scene’ involving the above three objects? Should one differentiate between RELATIONs (links between ontological concepts) and ATTRIBUTEs (mappings from ontological concepts into symbolic or numerical value sets)? Or rather define ATTRIBUTEs as one-place RELATIONs? Is it a good idea to introduce the ontological category of attribute value set with its members being primitive unstructured meanings (such as the various scalars and other, unordered, sets of properties)? Or is it better to define them as full-fledged ontological concepts, even though a vast majority of relations defined in the ontology would not be applicable to them (such a list will include case relations, meronymy, ownership, causals, etc.)? As an example of a decision on how to define an attribute, consider the representation of colors. Should we represent colors symbolically, as, say, red, blue, etc. or should we rather define them through their spectrum wavelengths, position on the white/black scale and brightness (cf. Schubert _et_
_al_ . 1983)?

- How should we treat sets of values? Should we represent _The Julliard quartet_ as one concept or a set of four? What about _The Pittsburgh Penguins_ ? What is an acceptable way of representing complex causal chains? How does one represent a concept corresponding to the
English phrase _toy gun_ ? Is it a gun? Or a toy? Or none of the above? Or is it perhaps the influence of natural language and a peculiar choice of meaning realization on the part of the producer that poses this problem—maybe we do not need to represent this concept at all?


In most of the individual cases such as the above, there is considerable leeway in making representation decisions. Additionally, there is always some leeway in topological organization of the tangled hierarchy, which most often is not crucially important. In other words, many versions of an ontological world model, while radically different on the surface, may be, in fact, essentially the same ontology, with different assignment of importance values among the properties of a concept. For example, physical objects may first be classified by color and then by size, shape or texture. However, unless there are good heuristics about priorities among such cross-classifying properties, there will be _n!_ different topologies for the ontological hierarchy for _n_ properties at each level. There is no reason to waste time arguing for or against a particular ordering, though various considerations of convenience in description may arise.


Sometimes such choices go beyond ontology proper. In Section 8.5 below, we discuss the various possibilities of distributing the meaning components between propositional and parameterized


representations in TMRs. These differences influence the way in which ontological hierarchies are structured. In some other cases, some components of lexical meaning representation are relegated to the lexicon instead of being specified directly in the ontology. For example, the ontology of Dahlgren _et al._ (1989) uses the individual / group distinction (e.g., _wolf / pack_ ) very high in the hierarchy as one of the basic ontological dichotomies, while the ontology used in each of the implementations of ontological semantics relegates this distinction to a set representation in the
TMR (and, consequently, a similar representation in the semantics zone of the lexicon entry for words denoting groups).


It is important to realize that the differences in the topology of the ontological hierarchy and in the distribution of knowledge among the ontology, TMR parameters and the lexicon are relatively unimportant. What is much more crucial is the focus on coverage and on finding the most appropriate grain size of semantic description relative to the needs of an application (see Section 9.3.6
below).


### 7.1.1 The Format of Mikrokosmos Ontology
In this section, we formally introduce the syntax and the semantics of the ontology, the former, using a BNF while the latter more informally, by commenting on the semantics of the notation elements and illustrating the various ontological representation decisions. We introduce the semantics of the ontology incrementally, with the semantics of new features appearing after they are introduced syntactically. In the BNF, once again, “{ }” are used for grouping; “[ ]” means optional (i.e., 0 or 1); “+” means 1 or more; and “*” means 0 or more.


`ONTOLOGY ::= CONCEPT+`


An ontology is organized as a set of concepts, each of which is a named collection of properties with their values at least partially specified. For example, the ontological concept PAY can be represented, in a simplified manner, as follows:


pay definition “to compensate somebody for goods or services rendered”
agent human theme commodity patient human


Remember that in the above ontological definition, PAY, HUMAN, COMMODITY, AGENT, THEME,
DEFINITION and BENEFICIARY are not English words, as might be construed, but rather names of ontological concepts that must be given only the semantics assigned to them in their ontological definitions. DEFINITION, AGENT, THEME and BENEFICIARY are the properties that have values (or fillers) assigned to them at this stage in the specification of the concept PAY. In terms of the underlying representation language for the ontology, concepts are frames and properties are slots in these frames—this is, of course, the standard interpretation of concepts and properties in all frame-based representation schemata (e.g., Minsky 1975, Bobrow and Winograd 1977, Schank and Abelson 1977). An important notational convention is that each concept in the filler position represents all the concepts in the subtree of the ontology of which it is the root. This means, for example, that if the concept of PAY is used to represent the meaning of the sentence _John paid Bill_
_ten dollars_, _John_ and _Bill_ will match HUMAN (because they will be understood as instances of


people) while _ten dollars_ will match COMMODITY. The above representation is in an important sense a shorthand. We will present a more varied and detailed picture of the actual constraints
(values, fillers) for concepts as we continue this presentation.


`CONCEPT := ROOT | OBJECT-OR-EVENT | PROPERTY`


Concepts come in three different syntactic formats, corresponding to semantic and topological differences in the organization of the ontology. First of all, ontological concepts are not simply an unconnected set. They are organized in an inheritance hierarchy (we will see how in a short while). This device is common in knowledge representation in AI because it facilitates economies of search, storage and access to ontological concepts. Semantically, the first difference among the concepts is that of “free-standing” versus “bound” concepts. The former represent OBJECT and
EVENT types that are instantiated in a TMR. The latter represent PROPERTY types that categorize the OBJECTs and the EVENTs and are not normally individually instantiated but rather become slots in instantiated OBJECTs and EVENTs. [73]


`ROOT ::= **ALL** DEF-SLOT TIME-STAMP-SLOT SUBCLASSES-SLOT`


The root is a unique concept in the ontology. It does not inherit properties from anywhere, as it is the top node in the inheritance hierarchy. It has the two special slots (properties), DEF-SLOT and
TIME-STAMP-SLOT that are used for administrative purposes of human access and control and do not typically figure in the processing by an application program, and another special slot that lists all the concepts that are its immediate SUBCLASSES. The above slots belong to the very small
ONTOLOGY-SLOT subtree of the property branch of the ontology. They are clearly “service” properties that do not carry much semantic content and are needed to support navigation in the ontology as well as facilitate its acquisition and inspection. TIME-STAMP-SLOT is used for version control and quality control of the ontology, and we will not list it in the examples for the sake of saving space. In the extant implementations of ontological semantics, the root concept is called
ALL (see Figure 23, where the TIME-STAMP property is routinely omitted for readability):


**Figure 23. ALL, the top concept in the Mikrokosmos ontology.**


73. They may, however, be instantiated in a TMR by means of a _reification_ operation (e.g., Russell and Norvig 1995), thereby making them stand-alone instances in the TMR.


`OBJECT-OR-EVENT ::= CONCEPT-NAME DEF-SLOT TIME-STAMP-SLOT ISA-SLOT [SUBCLASSES-SLOT] [INSTANCES-SLOT] OTHER-SLOT*`


OBJECTs and EVENTs have names, definitions and time stamps. They are descendants of some other OBJECT or EVENT, respectively, as indicated by the IS-A slot; some of them have SUBCLASSES, some have (remembered) instances stored in the Fact DB (see Section 7.2). And finally, they possess unique value sets for particular properties that differentiate them from other concepts. This latter information, introduced under OTHER-SLOT, is stored as fillers of the RELATION
and ATTRIBUTE properties (see below).


`PROPERTY ::= RELATION | ATTRIBUTE | ONTOLOGY-SLOT`


Properties are the ontology’s conceptual primitives. As an example, in the Mikrokosmos implementation of ontological semantics, there are about 300 such properties that help to define about
6000 concepts. Properties appear in the ontology in two guises, as defined types of concepts in the property branch and as slots in the definitions of objects and events. We will first explain how the latter are used and then will describe the properties as concepts.


`OTHER-SLOT ::= RELATION-SLOT | ATTRIBUTE-SLOT`


`RELATION-SLOT ::= RELATION-NAME FACET CONCEPT-NAME+`


`ATTRIBUTE-SLOT ::= ATTRIBUTE-NAME FACET {number | literal}+`


`FACET ::= value | sem | default | relaxable-to | not | default-measure | inv | time-range | info-source`


A slot is the basic mechanism for representing relationships between concepts. In fact, the slot is the fundamental metaontological predicate, based on which the entire ontology can be described axiomatically (see Section 7.1.6 below). Several kinds of fillers that properties can have are described by introducing the device of facet in the representation language in order to handle the different types of constraints. All properties (slots) have all permissible facets defined for them
(though not necessarily filled in every case), except as mentioned for the special slots below. In the latest implementation of ontological semantics, permissible facets are as follows (the facets
TIME-RANGE and SOURCE will be discussed in Section 7.2 below, the section on Fact DB):


VALUE: the filler of this facet is an actual value; it may be the instance of a concept, a literal symbol, a number, or another concept (in the case of the ontology slots, see below). Most of the constraints in TMR are realized as fillers of the VALUE facet. In the ontology, in addition to ontology slots, the VALUE facet is used to carry factual truths, e.g., that Earth has exactly one moon:


earth
...
number-of-moons value 1
...


SEM: the filler of a SEM facet is either another concept or a literal, number, or a scalar range (see below). In any case, this kind of filler serves as a selectional restriction on the filler of the slot. It is through these selectional restrictions that concepts in the ontology are related (or linked) to other concepts in the ontology (in addition to taxonomic links). The constraints realized through the SEM facet are abductive, that is, it is expected that they might be violated in certain cases. (17)
returns to the ontological concept pay, now with the appropriate facets added.


pay definition value “to compensate somebody for goods or services rendered”
agent sem human theme sem commodity patient sem human


Indeed, the AGENT or PATIENT of paying may be not a HUMAN but, for example, an ORGANIZATION; the THEME of paying may be an EVENT, as in _John repaid Bill’s hospitality by giving a lec-_
_ture in his class_ . It is important to recognize that the filler of theme cannot be “relaxed”
indefinitely. To mark the boundaries of abductive relaxation, the RELAXABLE-TO facet is used (see below).


DEFAULT: the filler of a DEFAULT facet is the most frequent or expected constraint for a particular property in a given concept. This filler is always a subset of the filler of the SEM facet. In many cases, no DEFAULT filler can be determined for a property. PAY, however, does have a clear
DEFAULT filler for its THEME property:


pay definition value “to compensate somebody for goods or services rendered”
agent sem human theme default money sem commodity patient sem human


RELAXABLE-TO: this facet indicates to what extent the ontology permits violations of the selectional constraints listed in the SEM facet, e.g., in nonliteral usage such as a metaphor or metonymy.
The filler of this facet is a concept that indicates the maximal set of possible fillers beyond which the text should be considered anomalous. Continuing with ever finer description of the semantics of PAY, we can arrive at the following specification:


pay definition value “to compensate somebody for goods or services rendered”
agent sem human relaxable-to organization theme default money sem commodity relaxable-to event patient sem human relaxable-to organization


The DEFAULT, SEM and RELAXABLE-TO facets are used in the procedure for matching what amounts to multivalued selectional restrictions. In cases when multiple facets are specified for a property, the program first attempts to perform the match on the selectional restrictions in
DEFAULT facet fillers, where available. If it fails to find a match, then the restrictions in SEM facets are used and, failing that, those in RELAXABLE-TO facets.


NOT: this facet is used for specifying that its fillers should be excluded from the set of acceptable fillers of a slot, even if other facets, such as, for instance, SEM, list fillers of which the fillers of not are a subset. This is just a shorthand device (essentially, set difference) to allow the developers of


the ontology to avoid long lists of acceptable fillers—see an example in the discussion of inheritance in Section 7.1.2 below.


DEFAULT-MEASURE: this facet is used for a rather special purpose of specifying a measuring unit for the number or numerical range that fills the VALUE, DEFAULT, SEM or RELAXABLE-TO facet of the same slot. It is needed to keep the types of numerical fillers to a minimum—they can still be only a number, a set of numbers or a numerical range. If dimensionality is added to the fillers, then there will be at least as many different types of such fillers as there are measuring units
(actual measuring units are defined as concepts in the ontology). In other words, the number 5
could stand for 5 meters, five dollars or five degrees Kelvin. The example below shows a typical use of the facet DEFAULT-MEASURE:


money
...
amount default-measure monetary-unit sem >= 0
...


This specification of the content of the AMOUNT property of MONEY allows us to correct, once again, the deliberate simplification in the specification of the semantics of PAY—the filler of the default facet of its theme is actually an amount of money, not simply the concept MONEY. In the corrected example, we use the shorthand notation MONEY.AMOUNT to represent the filler of a particular property of a concept:


pay definition value “to compensate somebody for goods or services rendered”
agent sem human relaxable-to organization theme default money.amount sem commodity relaxable-to event patient sem human relaxable-to organization
As can be seen from the DEFAULT-MEASURE facet, the facet facility can be used not only to list specific constraints but also to qualify those constraints in various ways. In fact, in the Mikrokosmos implementation of ontological semantics, the facet facility was used, for example, to specify the SALIENCY of a particular property for the identity of a concept (e.g., that a table has a flat top is a more salient fact than the number of legs it has) or the TOLERANCE of a particular value that showed how strict or fuzzy the boundaries of a certain numeric range were. Eventually, SALIENCY
came to be represented as a kind of MODALITY (see Section 8.5.3) and the semantics of TOLERANCE was subsumed by RELAXABLE-TO. The above developments underscore the complexity and the need to make choices of expressive means in building a metalanguage for representing meaning in texts (TMR), in the world (the ontology and the Fact DB) and the lexis of a language (the lexicon).


The INV facet is used to mark the fact that a particular filler was obtained by traversing an inverse relation from another concept. TIME-RANGE is a facet used only in facts, that is, concept instances


and specifies the temporal boundaries within which the information listed in the fact is correct.
The value of this facet is used to support truth maintenance operations.The INFO-SOURCE facet is used to record the source of the particular information element stored in a slot. It may be a URL or a bibliographical reference.


ONTOLOGY-SLOTs, as already mentioned, are special properties, in that they do not have a worldoriented semantics. In other words, they are used to record auxiliary information as well as information about the topology of the ontological hierarchy rather than semantic constraints on concepts. The small ontological subtree of ONTOLOGY-SLOT is illustrated in Figure 24.


**Figure 24. The auxiliary slots in the ontology, the ONTOLOGY-SLOT subtree**


`ONTOLOGY-SLOT ::= ONTOLOGY-SLOT-NAME DEF-SLOT TIME-STAMP-SLOT ISA-SLOT [SUBCLASSES-SLOT] DOMAIN-SLOT ONTO-RANGE-SLOT INVERSE-SLOT`


`DEF-SLOT ::= DEFINITION value “an English definition string”`


`TIME-STAMP-SLOT ::= time-stamp value time-date-and-username+`


`ISA-SLOT ::= IS-A value { ALL | CONCEPT-NAME+ | RELATION-NAME+ | ATTRIBUTE-NAME+ }`


`SUBCLASSES-SLOT ::= subclasses value {CONCEPT-NAME+ | RELATION-NAME+ | ATTRIBUTE-NAME+}`


`INSTANCES-SLOT ::= instances value instance-name+`


`INSTANCE-OF-SLOT ::= instance-of value concept-name+`


`DOMAIN-SLOT ::= domain sem concept-name+`


`INVERSE-SLOT ::= inverse value relation-name`


`ONTO-RANGE-SLOt ::= REL-RANGE-SLOT | ATTR-RANGE-SLOT`


The semantics of the properties that are children of ONTOLOGY-SLOT is as follows:


DEFINITION: This slot is mandatory in all concepts and instances. It has only a VALUE facet whose filler is a definition of the concept in English intended predominantly for human consumption during the knowledge acquisition process, for instance, to help establish that a candidate for a new


ontological concept is not, in fact, synonymous with an existing concept.


TIME-STAMP: This is used to encode a signature showing who created this concept and when, as well as an update log for the concept. In some applications of ontological semantics this information is stored in a separate set of log files that are not part of the ontology proper.


IS-A: This slot is mandatory for all concepts except ALL which is the root of the hierarchy.
Instances do not have an IS-A slot. This slot has only a VALUE facet which is filled by the names of the immediate parents of the concept. A concept missing an IS-A slot is called an orphan. Ideally, only ALL should be an orphan in the ontology.


SUBCLASSES: This slot is mandatory for all concepts except the leaves (concepts that do not have children). Note that instances do not count as ontological children. This slot also has only a
VALUE facet which is filled by the names of the children of the concept.


INSTANCES: This slot is present in any concept that has remembered instances associated with it in the Fact DB. A concept may, naturally, have both SUBCLASSES and INSTANCES. There is no requirement that only leaf concepts have instances. This slot also has only a VALUE facet filled by the names of the instances of this concept. This and the next slot provide cross-indexing capabilities between the ontology and the Fact DB.


INSTANCE-OF: This slot is mandatory for all instances and is present only in instances, that is, in the TMR and in Fact DB. It has only a VALUE facet that is filled by the name of the concept of which the Fact DB element, where the INSTANCE-OF slot appears, is an instance.


INVERSE: This slot is present in all relations and only in relations. It has only a VALUE facet which is filled by the name of the RELATION which is the inverse of the relation in which the INVERSE
slot appears. For example, the inverse of the relation PART-OF is the relation HAS-PARTS. The
INVERSE slot is used to cross-index relations.


DOMAIN: This slot is present in all properties and only in them. It has only a SEM facet which is filled by the names of concepts that can be in the domain of this property, that is, the concepts in which such properties can appear as slots. A DOMAIN slot uses a VALUE facet only when a property is reified, that is, made into a free-standing frame in the TMR, usually because it is the head of a proposition or because there is a need to add a qualifying constraint to it, which in the representation language we have used cannot be done for a slot. (Incidentally, this is also the formal reason why a property, if not reified, cannot become head of a TMR proposition—see Section
8.2.1) However, typically a property enters a text meaning representation (TMR) as a slot in an instance of an OBJECT, EVENT, or other TMR constructs (e.g., DISCOURSE-RELATION).


RANGE: This slot is also present in all properties and only in properties. It too has only a SEM facet.
In relations, the SEM facet is filled with the names of concepts that are in the range of this relation, that is, can be its values. In an attribute, the SEM facet can be filled by any of the possible literal or numerical values permissible for that attribute. The filler can also be a numerical range specified using appropriate mathematical comparison operators (such as >, <, etc.). Again, the RANGE slot usually does not use its VALUE facet since typically instances of a property in a TMR are recorded


in a slot in some other instance.


`RELATION ::= RELATION-NAME DEF-SLOT TIME-STAMP-SLOT ISA-SLOT [SUBCLASSES-SLOT] DOMAIN-SLOT REL-RANGE-SLOT INVERSE-SLOT`


`ATTRIBUTE ::= ATTRIBUTE-NAME DEF-SLOT TIME-STAMP-SLOT ISA-SLOT [SUBCLASSES-SLOT] DOMAIN-SLOT ATTR-RANGE-SLOT`


`REL-RANGE-SLOT ::= RANGE SEM CONCEPT-NAME+`


`ATTR-RANGE-SLOT ::= RANGE SEM { number | literal }*`


The above definitions introduce RELATIONs and ATTRIBUTEs as free-standing concepts, not properties (slots) in other concepts (frames). The difference between RELATIONs and ATTRIBUTEs boils down to the nature of their fillers: RELATIONs have references to concepts in their RANGE slots;
ATTRIBUTEs, references to elements—individual, sets or ranges—taken from (numerical or symbolic—see below) specific value sets.


`CONCEPT-NAME ::= name-string`


`INSTANCE-NAME ::= name-string`


`ONTOLOGY-SLOT-NAME ::= name-string`


`RELATION-NAME ::= name-string`


`ATTRIBUTE-NAME ::= name-string`


`NAME-STRING ::= alpha {alpha | digit}* {- {alpha | digit}+ }*`


A word is in order about naming conventions. While, syntactically, names of concepts and instances, are arbitrary name strings, semantically, further conventions are introduced in any implementation of ontological semantics, in order to maintain order and uniformity in representations. All concept names in the ontology are alphanumeric strings with the addition of only the hyphen character. No accents are permitted on any of the characters. Such enhancements are permitted only in lexicons. As far as ontology development is concerned, all symbols that we encounter can be classified into one of the following types:


- concept names: typically English phrases with at most four words in a name, separated by hyphens;

- instance names: following the standard practice in AI, an instance is given a name by appending the name of the concept of which this instance is INSTANCE-OF with a hyphen followed by an arbitrary but unique integer;

- references: fillers of the format concept.property.[facet] or instance.property that indicate that a filler is bound by reference to the filler in another concept or instance; for example,


car-32
color car-35.color


which says that the color of car-32 is the same as that of car-35;

- literal (nonnumerical) constants: these are also usually English words, in fact single words most of the time;

- the special symbols: NONE, NIL, UNKNOWN, NOTHING, NOT, AND, OR, REPEAT, UNTIL, as described below;


- other miscellaneous symbols used in the various implementations of ontological semantics, including:

   - TMR symbols;

    - lexicon symbols;

    - numbers and mathematical symbols.


A (real) number is any string of digits with a possible decimal point and a possible +/- sign; a literal is any alphanumeric string starting with an alphabetical symbol. We will not formally define them any further. As mentioned above, the legal format of a filler in any implementation of ontological semantics can be a string, a symbol, a number, a numerical (scalar) or a literal (symbolic)
range. Strings are typically used as fillers (of VALUE facets) of ontology slots representing useroriented, non-ontological properties of a concept, such as DEFINITION or TIME-STAMP. A symbol in a filler can be an ontological concept. This signifies that the actual filler can be either the concept in question or any of the concepts that are defined as its subclasses. In addition to concept names and special keywords (such as facet names, etc.), we also allow symbolic value sets as legal primitives in a world model. For instance, we can introduce symbolic values for the various colors—RED, BLUE, GREEN, etc.—as legal values of the property COLOR, instead of defining any of the above color values as separate concepts. Numbers, numerical ranges and symbolic ranges
(e.g., april—june) are also legal fillers in the representation language. Note that symbolic ranges are only meaningful for ordered value sets and that, for numerical range values, one can locally specify a measuring unit. The measuring unit is introduced in the ontology through the filler of the
DEFAULT-MEASURE facet. If no DEFAULT-MEASURE is specified locally, the system will use the
(default) unit listed in the definition of each scalar attribute in the ontology. In the Dionysus implementation there was another syntactic convention: to prepend the ampersand, &, to symbolic value set members in order to distinguish them from ontological entity names, that were marked by the asterisk, and instances from the Fact DB, that were marked with the percent symbol, %. In the Mikrokosmos implementation, value set members receive names different from concept names, and instances are recognized by the unique number appended to the concept name.


Individual numerical values, numerical value sets and numerical (scalar) ranges are fillers of the range slot for SCALAR-ATTRIBUTEs. The values can be absolute and relative. If the input text to be processed contains an overt reference to a quantity, e.g., _a ten-foot pole_, then the filler of the appropriate property, LENGTH-ATTRIBUTE, is represented as a number with a measuring unit specified—in this case, the number will be 10, and the measuring unit, feet (this value is the filler of the DEFAULT-MEASURE facet on LENGTH-ATTRIBUTE). A property which can be measured on a scale can also be described in an input in relative terms. We can say _The temperature is 90 degrees_
_today_ or _It is very hot today_ . Relative references to property values are represented in ontological semantics using abstract scales, usually running from 0 (the lowest possible value) to 1 (the highest possible value). Thus, the meaning of _hot_ in the example above will be represented as the range [0.75 - 1] on the scale of temperature (we often notate this as > .75). If we want to compare two different relative values of a property, we will need to consult the definitions of the corresponding concepts where the ranges of acceptable absolute values of such properties for a given concept are listed. For example, the temperature of water runs between 0 and 100 degrees Centigrade, so hot water, if represented on an abstract scale as above, will, in fact translate into an abso


lute, measured, scale as the range between 75 and 100 degrees. At the same time, temperature of bath water, would range between, say 20 and 50 degrees Centigrade. Therefore, _a hot bath_ will be represented in absolute terms as a range between 42.5 and 50 degrees.


Literal symbols in the ontology are used to stop unending decomposition of meanings. These symbols are used to fill certain slots (namely, they are fillers of LITERAL-ATTRIBUTEs) and are defined in the ontology in the range slots of the definitions of their respective LITERALATTRIBUTEs. Some characteristics of literal symbols worth noting include:


- Literal symbols are used in our representations in much the same way as the qualitative values used in qualitative physics and other areas of AI that deal with modeling and design of physical artifacts and systems (de Kleer and Brown, 1984; Goel, 1992).

- Literal symbols are either binary or refer to (approximate) positions along an implied scale, that is, over an ordered set of symbols—e.g., days of the week or planets of the Solar system, counted from Mercury to Pluto, whose status as a planet has, as a matter of fact, been recently thrown into doubt. For binary values, it is often preferable to use attribute-specific literal symbols rather than a generic pair (such as YES or NO, or ON or OFF).

- Literal symbols are often used when there is no numerical scale in common use in physical or social models of the corresponding part of the world. For example, OFFICIAL-ATTRIBUTE has in its range the literals OFFICIAL and UNOFFICIAL. Although one can talk about an event or a document being more official than another, there is no obvious scale in use in the world for this attribute. The two literals seem to serve well as the range of this attribute.

- It is not always true that literal attributes are introduced in the absence of a numerical scale in the physical or social world. A classical example of this is COLOR. Although several welldefined numerical scales for representing color exist in models of physics (such as the frequency spectrum, hue and intensity scales, etc.), such a scale does not serve our purposes well at all. First of all, it would make our TMRs more or less unreadable for a human if it has a frequency in MHz, a hue range and a value of intensity in place of a literal such as RED or
GREEN. Moreover, it makes lexicon acquisition more expensive; lexicographers will have to consult a physics reference to find out the semantic mapping for the word _red_ instead of quickly using their own intuitive understanding of its meaning. The above consideration is strongly predicated, however, on the expected granularity of description. It would be, in fact, much more preferable to use a non-literal representation of color to support processing of texts in which color differences are centrally important.
Four special fillers—NOTHING, NIL, UNKNOWN and NONE, are used in the various implementations of ontological semantics. NIL means that the user has not specified a filler and there is no filler to be inherited. UNKNOWN means that a filler exists but is not (yet) specified. NONE means that there can be no filler, and the user (or the system) overtly specified this. For instance, if for a certain property in a certain concept there cannot be found a default filler—that is, when several potential fillers are equally probable—then the user will have to enter NONE as the filler of this default facet. The special symbol NOTHING has been introduced to block inheritance. It will be discussed, together with other issues concerning inheritance, in the next section.


### 7.1.2 Inheritance
When talking about inheritance, we only concentrate on contentful issues relating to the expressive power of the ontology metalanguage. We see ontological semantics as guided by the theory


of inheritance (e.g., Touretzky 1984, 1986, Thomason _et al_ . 1987, Touretzky _et al._ 1987, Thomason and Touretzky 1991) but do not aspire to contributing to further development of the theory of inheritance. Our approach to inheritance is fully implementation-oriented.


The inheritance hierarchy, which is implemented using IS-A and SUBCLASSES slots, is the backbone of the ontology. When two concepts, X and Y, are linked via an IS-A relation (that is, X IS-A
Y), then X inherits slots (with their facets and fillers) from Y according to the following rules:


- All slots that have not been overtly specified in X, with their facets and fillers, but are specified in Y, are inherited into X.

- ONTOLOGY-SLOTs (IS-A, SUBCLASSES, DEFINITION, TIME-STAMP, INSTANCE-OF, INSTANCES,
INVERSE, DOMAIN, RANGE) are excluded from this rule. They are not inherited from the parent.

- If a slot appears both in X and Y, then the filler from X takes precedence over the fillers from
Y.

- Use the filler NOTHING to locally block inheritance on a property. If a parent concept has a slot with some facets and fillers and if some of its children have NOTHING as the filler of the
SEM facet for that same slot, then the slot will not be inherited from the parent. Since the local slot in the child has NOTHING as its filler, no instance of any OBJECT or EVENT or any number or literal will match this symbol. As such, no filler is acceptable to this slot and this slot will never be present in any instance of this concept. This has the same effect as removing the slot from the concept. For example, ANIMAL has the property MATERIAL-OF
filled by AGRICULTURAL-PRODUCT; HUMAN IS-A ANIMAL and it inherits the slot MATERIAL-OF
from ANIMAL; however, the filler of this slot in HUMAN is, for obvious reasons, NOTHING.
Note that in descendants of HUMAN it is entirely possible to reintroduce fillers other than
NOTHING in the MATERIAL-OF slot, for instance, in news reports about transplants or cloning.

- Block the inheritance of a filler that is introduced through the NOT facet. Thus, the filler
HUMAN will be introduced through the facet NOT in the THEME slot of BUY, while the SEM
facet will list OBJECT as its filler (and HUMAN is a descendant of OBJECT). This is our way of saying that, in the extant implementations of ontological semantics, people cannot be bought or sold (which, incidentally, may turn out to be a problem for processing news reports about slavery in the Sudan or buying babies for adoption).


Regular inheritance of a slot simply incorporates all fillers for the slot from all ancestors (concepts reached over the IS-A relation) into the inheriting concept. For example, a kitchen has a stove, a refrigerator, and other appliances. A room has walls, a ceiling, a floor, and so on. A
kitchen, being a room, has the appliances as well as a floor, etc. Blocking inheritance indicates that a slot or slot/filler combination that appears in an ancestor should not be incorporated into the inheriting concept.


There are two reasons for blocking inheritance using NOTHING. First, in a subtree in the ontology, all but a few concepts might have a particular property (slot). It is much easier to put the slot at the root of the subtree and block it in those few concepts (or subtrees) which do not have that slot rather than putting the slot explicitly in each of the concepts that do take the slot. For example, all
EVENTS take the agent slot except PASSIVE-COGNITIVE-EVENTs and INVOLUNTARY-PERCEPTUAL


EVENTs. We can put the AGENT slot (with the SEM constraint ANIMAL) in EVENT and put a SEM
NOTHING in PASSIVE-COGNITIVE-EVENT and INVOLUNTARY-PERCEPTUAL-EVENT. This will effectively block the AGENT slot in the subtrees rooted under these two classes of EVENT while all other
EVENTs will still automatically have the AGENT slot.


A second, stronger reason for introducing this mechanism comes from the needs of lexical semantics. Sometimes, the SEM-STRUC zone of the lexicon entry for certain words (see Section 7.2) will have to refer to a property (slot) defined for an entire class of concepts, even though a few concepts in that class do not actually feature that property. For example, in the SEM-STRUC of the
Spanish _activo_, we must refer to the AGENT of EVENT without knowing what EVENT it is. This requires us to add an AGENT slot to EVENT even though there are two subclasses of EVENT that do not have AGENT slots. An alternative would be to list every type of EVENT other than the above two in the SEM-STRUC of the lexicon entry for this word. This, however, is not practical at all. In a sense, this mechanism is introducing the power of default slots just like we have a DEFAULT facet in a slot. We can specify a slot for a class of concepts which acts like a default slot: it is present in every concept unless there is an explicit SEM NOTHING filler in it.


While multiple inheritance is allowed and is indicated by the presence of more than one filler in the IS-A slot in a concept, no extant implementation of ontological semantics has fully developed sufficiently formal methods for using multiple inheritance.


### 7.1.3 Case Roles for Predicates
The semantic properties help to describe the nature of objects and events. Some of these properties constrain the physical properties of OBJECTs, e.g., TEXTURE, LENGTH or MASS, or EVENTs, e.g.,
INTENSITY. Some others, introduce similar “inherent” properties of non-physical, that is, social or mental objects or events, e.g., PRECONDITION, DESCRIBES or HAS-GOVERNMENT. Still others, are applicable to the description of any kind of OBJECT or EVENT, e.g., HAS-PARTS. There is, however, a group of relations that has a special semantics. These relations describe connections between events on the one hand and objects or other events (or between a verb and its arguments, adjuncts and complements, in linguistic terminology) that the “main” events are in some sense “about.” In other words, they allow one to contribute to the description of the semantics of propositions through the specification of their semantic arguments. These arguments are typical roles that a predicate can take; they appear as properties of events in the TMR, as well as in the ontology and the Fact DB.


The first descriptions of similar phenomena in linguistics were independently proposed in the
1960s by Gruber (1965) and Fillmore (1968, 1971, 1977), who called his approach case grammar.
Since then, case grammar has had a major impact on both theoretical and computational linguistics (e.g., Bruce 1975; Grimshaw 1990; Levin and Rappaport Hovav 1995) and has found its way, in varying forms, into knowledge representation for reasoning and natural language processing systems. An overview and comparison of several theories of case grammar in linguistics can be found in Cook (1989); reviews of case systems as they are used in natural language processing, for example, in Bruce (1975), Winograd (1983) or Somers (1986).


In case grammar, a case relation (or case role, or simply case) is a semantic role that an argument
(typically, a noun) can have when it is associated with a particular predicate, (typically, a verb).


While many linguistic theories of case have been proposed, all of them have in common two primary goals: 1) to provide an adequate semantic description of the verbs of a given language, and
2) to offer a universal approach to sentence semantics (see Cook, 1989, ix). Unfortunately for our purposes, most approaches to case grammar in linguistics remain, at base, syntactic, and indeed talk about language-dependent arguments of verbs and nouns, not of language-independent properties of events, general declarations about the universality of approach notwithstanding.


Another issue is the actual inventory of the case roles. It has been amply noted that there are about as many systems of case roles as there are theories and applications that use them. We view this state of affairs as necessary and caused by the difficulty of balancing the grain size of description against coverage and ease of assignment of case role status to semantic arguments. The case roles must be manipulated by people during the knowledge acquisition stage of building an implementation of ontological semantics, that is, when the ontology and the lexicons are constructed. This makes it desirable, on the one hand, to use a small inventory of case roles—or risk the acquirers spending long minutes selecting and constraining an appropriate set of case roles to describe an event; on the other hand, it is imperative that the case roles are defined in a straightforward way and correspond to a clearly cut and identifiably coherent subset of reality—or risk the acquirers metaphorically or metonymically extending the semantics of some roles beyond their intended purview.


In what follows, we describe the set of case roles defined in the CAMBIO/CREST implementation of ontological semantics. This set has been the subject of much development and modification over the years, as the earlier implementations of ontological semantics used distinctly different inventories. We expect that any future applications, with their specific goals, sublanguage and subworld and granularity, will involve further modifications to the inventory of case roles. In the examples that accompany the specification of the case roles, we take the liberty of marking using **boldface** the textual elements whose semantic description will fill the corresponding case role slot in the semantic description of the appropriate event in the TMR.


**Table 4: Agent**


Definition The entity that causes or is responsible for an action


Semantic Constraints Agents are either intentional, that is, in our judgment, humans or higher animals, or forces


Syntactic Clues The subject in a transitive sentence is often, but not always, the agent. In languages with grammatical cases, a nominative, ergative or absolutive case marker often triggers an agent. Here and in the rest of the specifications of case roles, the syntactic clues are presented as defeasible heuristics rather than strong constraints.


Examples **Kathy** ran to the store.
**The storm** broke some windows.
**Du Pont Co.** said **it** agreed to form a joint venture in gas separation technology with **L'Air Liquide S.A** ., an industrial gas company based in Paris.


**Table 4: Agent**


Definition The entity that causes or is responsible for an action


Notes 1. _Du Pont Co._ and _l’Air Liquide S.A._ are metonymical agents—
see Section 8.4.2;
2. after the resolution of co-reference (see Section 8.6.1), _it_ will be assigned appropriate semantic content that will fill the agent case role of the event corresponding to _agree_ .
3. In the last example, the two companies are both treated as agents of the event corresponding to _forming a joint venture—_ see
Section 7.1.4 below.


**Table 5: Theme**


Definition The entity manipulated by an action


Semantic Constraints Themes are seldom human


Syntactic Clues Direct objects of transitive verbs; subjects in intransitive sentences and verbal complements are often themes. In languages with grammatical cases, nominals in accusative often trigger themes.


Examples John kicked **the ball** .
**The price** is high.
**The ball** rolled down the hill.
John said **that Mary was away** .
Bridgestone Sports Co. has set up **a company** in Taiwan with a local concern and a Japanese trading house.


Notes While not particularly hard to detect, probably because of the relative reliability of syntactic clues, this case role ends up covering probably more heterogeneous phenomena than it should—there is a clear intuitive difference between the themes realized in language by objects and those realized by (sentential) complements or by direct objects and by subjects.


**Table 6: Patient**


Definition The entity that is affected by an action


Semantic Constraints Typically, patients are human


**Table 6: Patient**


Definition The entity that is affected by an action


Syntactic Clues Indirect objects often end up interpreted as patients, when the above semantic constraint holds; subjects of verbs whose meanings are involuntary perceptual events and subjects of non-agentive verbs (e.g., _feel, experience, suffer_ ) are interpreted as patients.
In languages with grammatical cases, dative forms often trigger patients.


Examples Mary gave a book to **John.**
**Fred** heard music.
**Bill** found **himself** entranced.


Notes Relatively easy to identify when a theme is also present, as in the first example above. The definition of this role is admittedly difficult to distinguish from that of theme. Early implementations of ontological semantics, instead of a single patient role, used several: experiencer (for _Fred_ in the second example and _Bill_ in the third) and beneficiary (for _John_ in the first example). Unlike the second example, in _Fred listened to music_, Fred is interpreted as the AGENT of the underlying event because the event implies intentionality—indeed, one hears music much too often when one would rather not hear it.


**Table 7: Instrument**


Definition The object or event that is used in order to carry out an action.


Semantic Constraints None.


Syntactic Clues Prepositions _with_ and _by_, in their appropriate senses, may trigger the case role instrument. In some languages there is a special case marker that is a clue for instrument—e.g., the instrumental case in
Russian.


Examples Seymour cut the salami with **a knife** .
Armco will establish a new company by **spinning off its general**
**steel department** .


Notes Sometimes, across languages, instruments are elevated syntactically to the subject positions, as in _The knife cut the salami easily_ .


**Table 8: Source**


A starting point for various types of movement and transfer (used
Definition in verbs of motion, transfer of possession, mental transfer, etc.)


Semantic Constraints Sources are primarily objects


Syntactic Clues Prepositional clues are available (see Nirenburg 1980 for details), e.g., the English _from_ in one of its senses; in some languages there is a special case marker that is a clue for source—e.g., ablative in
Latin or elative in Finnish; however, one cannot, as in agent, theme or patient, expect a clue for source on the basis of grammatical function, such as subject or direct object.


Examples The goods will be shipped from **Japan.**
Susan bought the book from **Jane.**
TWA Flight 884 left **JFK** at about 11 p.m.


Notes We avoid treating events as sources in sentences like _John went_
_from working 12 hours a day to missing work for weeks at a time_
by interpreting the events in the corresponding TMR as free-standing propositions, with a discourse relation between them. One can envisage making the opposite choice and thus relaxing the above semantic constraint. One rationale for our choice comes from the application of MT: we cannot count on the availability of the _go_
_from_ construction used in this way in languages other than
English. Therefore, we analyze the input further, stressing not the way the two propositions are connected in the source language but rather reporting the actual sequence of events.


**Table 9: Destination**


An endpoint for various types of movement and transfer (used in
Definition verbs of motion, transfer of possession, mental transfer, etc.)


Semantic Constraints Destinations are primarily objects


Syntactic Clues Prepositional clues are available, e.g., the English _to_ or _toward_ in one of their senses; in some languages there is a special case marker that is a clue for destination—e.g., allative (or destinative)
in Finnish; however, one cannot, as in agent, theme or patient, expect a clue for source on the basis of grammatical function, such as subject or direct object.


Examples John took his mother to **the theater** .
Cindy brought the money to **me.**
Hilda gave **John** an idea.


**Table 9: Destination**


An endpoint for various types of movement and transfer (used in
Definition verbs of motion, transfer of possession, mental transfer, etc.)


Notes Considerations parallel to those in the notes on the case role source apply here.


**Table 10: Location**


Definition The place where an event takes place or where an object exists


Semantic Constraints Locations are typically objects


Syntactic Clues Prepositions that have locative senses ( _in, at, above_, etc.) and, in some languages with grammatical cases, special case values, e.g., locative in Eastern Slavic languages or essive in Finnish.


Examples The milk is in **the refrigerator** .
The play by Marlowe will be performed at **the Shakespeare The-**
**ater** .


Notes The meaning of location (as well as time, treated parametrically—
see Section 8.5.2 below) must be posited whenever an instantiation of an event or an object occurs. In fact, imparting spatiotemporal characteristics to an event type can be considered a defining property of instantiation (as well as in indexation in the philosophy of language). If no candidate for a filler is available, either in the input text or in the Fact DB, abductively overridable DEFAULT or
SEM values can be propagated from the corresponding concepts or, alternatively, through contextual inferences.


**Table 11: Path**


The route along which an entity (i.e., a theme) travels, physically
Definition or otherwise


Semantic Constraints Paths are typically objects


Syntactic Clues Some prepositions, such as _along, down, up, through, via, by way_
_of, around_, etc., in their appropriate senses, trigger the case role
PATH.


**Table 11: Path**


The route along which an entity (i.e., a theme) travels, physically
Definition or otherwise


Examples Mary ran down **the hill** .
The plane took **the polar route** from Korea to Chicago.
He went through **a lot of adversity** to get to where he is now.


Notes The meanings that can be represented using PATH can also be specified by other means, for instance, by proliferating the number of free-standing propositions in the TMR and connecting them with overt discourse relations (cf. the notes to the case role
SOURCE where this device was mentioned for the case when the candidate for the case role’s filler was an event). It can be argued that such means are available for all case roles. It is, however, a matter of a trade-off between the parsimony of the case role inventory and ease of assigning an element of input to a particular case role, at acquisition time or at processing time.


**Table 12: Manner**


Definition The style in which something is done.


Semantic Constraints Manner is typically a scalar attribute.


Syntactic Clues Manner is triggered by some adverbials.


Examples She writes **easily** .
Bell Atlantic acquired GTE **very fast** .


Notes This case role accommodates some typical scalars comfortably, treating their semantics along the lines of adjectival semantics (see
Raskin and Nirenburg 1995, 1998); the grain size of the definition is deliberately coarse—otherwise, assignment will be complicated; this case role is used as a hold-all in ontological semantics to link any event modifier that cannot be assigned to one of the above case roles.


### 7.1.4 Choices and Trade-Offs in Ontological Representations.
In Section 6.6. above, we established that, for a given state of static resources, including ontology, a given format of the TMR and a given analysis procedure, there is no paraphrase in TMRs, that is, a given textual input, under the above conditions, will always result in the same TMR. This is the result of all the choices that were made at definition and acquisition time of both static and dynamic knowledge sources. As a matter of policy, at definition time, ontological semantics strives to make a single set of choices on every phenomenon that is perceived by the developers as


allowing in principle several different ways of treatment. Of course, one is never guaranteed that the ontological semantic knowledge sources will not contain means for expressing a particular content in more than one way. In fact, to check that this is not the case is far from trivial, and it might well be impossible to avoid such an eventuality. Obviously, ontological semantics attempts to preclude this from happening in every case when this possibility is detected.


Eliminating multiple representation possibilities involves making a number of choices and tradeoffs. We already alluded to some such choices in the notes for the case roles in the previous section. Here we would like to illustrate some further and more generally applicable decisions of this kind.


In the Dionysus implementation of ontological semantics, the set of case roles included several
“co-roles”—e.g., CO-AGENT, CO-THEME, or ACCOMPANIER. These were defined as entities that behaved like agents or themes but always in conjunction with some other agent or theme, thus fulfilling, in some sense, an auxiliary role, e.g., _John_ (AGENT) _wrote a book with Bill_ (CO-AGENT) or
_The Navy christened the new frigate_ (THEME) _The Irreversible_ (CO-THEME). In the former case, the choice taken, for example, in the Mikrokosmos and CAMBIO/CREST implementations of ontological semantics is to make the grain size of description somewhat more coarse and declare that the AGENT and the CO-AGENT are members of a set that fills the AGENT role of WRITE. What we lose in granularity here is the shade of meaning that John was somehow more important as the author of the book than Bill. However, this solution is perfectly acceptable in most subject domains.


In the case of the purported case role CO-THEME, when a solution similar to that we just suggested for CO-AGENT is impossible, and this is, indeed, the case in the second example above, a treatment may be suggested that avoids using exclusively case roles for connecting elements of meaning in the TMR. In this example, the lexicon entry for _christen_ uses the ontological concept GIVE-NAME:


give-name
...
theme default human has-name sem name sem object has-name sem name


...


The corresponding part of the TMR is filled by the input sentence as follows:


give-name-20
...
theme value ship-11
has-name value _The Irreversible_
...


The problem of representing the meaning of the example accurately is solved this way not at the level of such general properties as case roles but rather at the level of an individual ontological concept, GIVE-NAME, whose semantics uses both a case role (THEME) of the event itself and a non


case-role property (HAS-NAME) of that case role’s filler. This kind of solution always invites itself when there is a necessity to avoid the introduction of a possibly superfluous general category.
Because CO-THEME would have to be introduced for a small set of phenomena and its presence would make the processes of knowledge acquisition and text analysis more complicated, it is preferable to provide for the ontological representation of the phenomena without generalization, that is, in the definitions of individual lexical items (viz., _christen_ ) and ontological concepts (viz.
GIVE-NAME).


As a result of reasoning along these lines, between the Dionysus and Mikrokosmos implementations of ontological semantics, the inventory of case roles was shrunk at least twofold, mostly at the expense of co-roles and case roles that were judged to be better interpreted in an alternative preexisting manner—as discourse relations, defined in the ontology and used in TMRs, among free-standing propositions (cf. the notes for the case role source in the previous section; see also
Section 8.6.3 below on discourse relations). This is not only parsimony, at its purest, but also elimination of a possibility for paraphrase in TMR: leaving those case roles in would have made it possible to represent the same meaning either with their help or using the discourse relations.


### 7.1.5 Complex Events
In order to represent the meaning of connected text, not simply that of a sequence of ostensibly independent sentences, several things must happen. One of the most obvious connections across sentence boundaries is co-reference. The TMR in ontological semantics allows for the specification of co-reference, and special procedures exist for treating at least facets of this phenomenon in extant applications of ontological semantics (see Section 8.6.1). Discourse relations among propositions can also hold across sentence boundaries, and ontological semantics includes facilities for both detecting and representing them.


There are, however, additional strong connections among elements of many texts. These have to do with the understanding that individual propositions may hold well-defined places in “routine,”
“typical” sequences of events (often called complex events, scripts or scenarios—see Section 3.7
above) that happen in the world, with a well-specified set of object-like entities that appear in different roles throughout that sequence. For example, if the sequence of events describes a state visit, the “actors” may, under various circumstances, include the people who meet (the “principals”), their handlers, security personnel and journalists, possibly, a guard of honor; the “props”
may include airplanes, airports, meeting spaces, documents, etc. All these actors and props will fill case roles and other properties in the typical component events of the standard event sequence for a state visit, such as travel, arrival, greetings, discussions, negotiations, press conferences, joint statements, etc. The component events are often optional; alternatively, some component events stand in a disjunctive relation with some others (that is, of several components only one may actually be realized in a particular instantiation of the overall complex event), and their relative temporal ordering may be fuzzy.


Such typical scripts can be expressed in natural language using expository texts or narratives, sets of the above (indeed, one conceptual story can be “gathered” from several textual sources), plus text in tables, pictures, TV and movie captions, etc. The notion of script is clearly recursive, as every component event can itself be considered a script, at a different level of granularity. The notion of script, under a variety of monikers, was popularized in computer science by Minsky


(1975), Schank and Abelson (1977), Charniak (1972) and their colleagues in the 1970s. However, at that time, no realistic-size implementation of natural language processing using scripts could be undertaken, in part, because there was no clear idea about the required inventory of knowledge sources, their relations and content. Script-based theories of semantics were proposed in theoretical linguistics (Fillmore 1985, Raskin 1986) but were overshadowed by the fashion for formal semantics (see Section 3.5.1 above). Moreover, the size of the task of creating the ontological semantic knowledge sources was at the time underestimated by the practitioners and overestimated by critics. It can be said that ontological semantics is a descendant of the script-oriented approach to natural language processing, especially in the strategic sense of accentuating semantic content, that is the quantity and quality of stored knowledge required for descriptions and applications. Ontological semantics certainly transcends the purview and the granularity levels of the older approach as well as offering an entirely different take on coverage of world and language knowledge and on its applicability.


In the complex-event-based approach to processing text inputs, the complex events in the ontology that get instantiated from the text input provide expectations for processing further sentences in a text. Indeed, if a sentence in a text can be seen as instantiating, in the nascent TMR, a complex event, the analysis and disambiguation of subsequent sentences can be aided by the expectation that propositions contained in them are instantiations of event types that are listed as components of the activated complex event. Obviously, the task of activating the appropriate complex event from the input is far from straightforward. Also, not all sentences and clauses in the input text necessarily fit a given complex event—there can be deviations and fleeting extraneous meanings that must be recognized as such and connected to other elements of the TMR
through regular discourse relations, that is, through a weaker connection than that among the elements of a complex event.


Complex events usually describe situations with multiple agents. Each of these agents can be said, in some sense, to carry out their own plans that are made manifest through the reported component events in a complex event. Plans are special kinds of complex events that describe the process of attaining a goal by an agent or its proxies. Goals are represented in ontological semantics as postconditions (effects) of events (namely, steps in plans or components of general complex events). For example, if an agent’s goal is to own a TV set, this goal would be attained on a successful completion of one of a number of possible plans. In other words, it will be listed in the ontology as the postcondition (effect) of such events as BUY, BORROW, LEASE, STEAL, MANUFACTURE. Note that the plans can be activated only if all the necessary preconditions for their triggering hold. Thus, the ontology, in the precondition property of BUY, for example, will list the requirement that the agent must have enough money (see McDonough 2000).


Manipulating plans and goals is especially important in some applications of ontological semantics, for instance, in advice giving applications where the system is entrusted with recognizing the intentions (goals) of an agent or a group of agents based on processing texts about their behavior.
Goal- and plan-directed processing relies on the results of the analysis of textual input, as recorded in the basic TMR, as well as the complementary knowledge about relevant (complex)
events and objects and their instances, stored in the ontology and the Fact DB, and instantiated in the extended TMR. It is clear that reasoning based on the entire amount of knowledge in the extended TMR can be much richer than if only those facts mentioned in the input texts were used


for inference making. Richer possibilities for reasoning would yield better results for any NLP
application, provided it is supplied with the requisite inference making programs, for instance, for resolving translation mismatches. The reason we are making a distinction among NLP applications is the extent to which an application depends on such capabilities. For example, MT practitioners have typically assumed that this application does not really need machinery for inference making. This belief is clearly based on the perception that acquiring the knowledge necessary to support reasoning is prohibitively expensive or even outright infeasible, and therefore one must make do with simpler approaches. Of course, should MT developers be able to obtain such resources, they would use them. Ontological semantics has among its goals that of supplying application builders with exactly this kind of knowledge.


Of course, as mentioned above, in addition to the knowledge, efficient reasoning procedures must be developed. Such procedures must conform to a number of constraints, an example of which is the following. It is common knowledge that, unless a limit is imposed on making inferences from knowledge units in rich knowledge bases, the inferencing process can go too far or even not halt at all. In advanced applications, for example, advice giving, a good candidate for such a limit is deriving the active goals and plans of all relevant agents in the world. However, even applications that involve more or less direct treatment of basic text meaning, such as machine translation, will benefit from making fewer inferences. There will always be difficult cases, such as the need to understand the causal relation in _The soldiers fired at the women and I saw some of them fall_ to select the correct reference for _them_ —in Hebrew, for example, the choice of the pronoun (the masculine _otam_ or the feminine _otan_ will depend on the gender of the antecedent). Such cases are not overly widespread, and a prudent system would deliberately trigger the necessary inferences when it recognizes that there is a need for them. In general, any event is, in fact, complex, that is, one can almost always find subevents of an event; whether and to what extent it is necessary to develop its HAS-PARTS property is a matter of grain size dictated by whether an application needs this information for reasoning.


Complex events are represented in ontological semantics using the ontological property HASPARTS. It has temporal semantics if it appears in events, and spatial semantics if it appears in physical objects, e.g., to indicate that an automobile consists of an engine, wheels, the chassis, etc.
The properties PRECONDITION and EFFECT also carry information necessary for various kinds of reasoning and apply to any events, complex or otherwise. Complex events require an extension to the specification format. The reason for that is the need to bind the case roles and other property values in component events to establish co-reference. Also, the HAS-PARTS slot of complex events should allow for the specification of rather advanced combinations of component events. Therefore, the format of the filler of HAS-PARTS in complex events should allow a) Boolean operators
**and**, **or** and **not** and b) loop statements. Complex events also need statements about partial temporal ordering of their components. For this purpose, a special new property, COMPONENT-RELATIONS is introduced.


Component events in a complex event have a peculiar status. They are not regular instances of concepts, as in the ontology no instantiation occurs—instantiation is one of the two main operations in generating TMRs, the other being matching selectional restrictions in order to combine individual concept instances—but their meaning is different from that of the general concepts to which they are related. In other words, asking questions in the context of a class at school is


clearly different from the general idea of asking questions. In order to represent this difference, the notion of ontological instance is introduced. In an ontological instance, some properties are constrained further as compared to their “parent” concept. The constraints typically take the form of cross-reference to the filler of another component event in the same complex event.


For reasons of clarity and convenience, instead of describing the component events and component relations directly in the fillers of corresponding slots in the concept specification for the complex event, we use the device of reification by just naming them in a unique way in that location
(we identify ontological instances by appending letters, not numbers as in the case of real instances) and describe their content separately, at the same level as the main complex event. As a result, the format of the ontological description of a complex event is a set of ontological concept frames.


Reification in ontological semantics is a mechanism for allowing the definition of properties on properties by elevating properties from the status of slots in frames to the level of a free-standing concept frame. It is desirable from the point of view of nonproliferation of elements of metalanguage to avoid introducing a concept of, say DRIVER if it could always be referred to as
DRIVE.AGENT. However, this brings about certain difficulties. For example, if we want to state that somebody is a DRIVER of TRUCKs, we would have to say that there is an instance of DRIVE in which the theme is TRUCK and the AGENT is the person in question. There is no direct relationship between THEME and AGENT, and it would take a longer inference chain to realize that TRUCK is, in fact, the value of a property of DRIVER, too, not only of DRIVE. The more properties one would want to add to DRIVER and not to DRIVE, the more enticing it would be to reify the property
DRIVE.AGENT and treat it as a separate concept. In principle, we can use reification on the fly, while building a TMR, when we need to add a property to a property, which is prohibited in the static knowledge sources such as the ontology and the lexicon. As we will see in the example below, reification also facilitates the specification of complex events.


In the example below, we present a simplified view of the complex event TEACH. As illustrated,
TEACH has as PRECONDITION two EVENTs—that the teacher knows the material and the students do not; as EFFECT, it has the EVENT that the students (now) know the material. The process of teaching is presented as follows: the teacher presents the material to the students, the students ask the teacher questions about this material and the teacher answers these questions. The above is admittedly a gross simplification of the actual state of affairs but will serve well for the purposes of illustration.


The ontological instances introduced in the process are: TEACH-KNOW-A, -B and -C, TEACHDESCRIBE, TEACH-REQUEST-INFO, TEACH-ANSWER, TEACH-AFTER-A and -B. The constraints in these instances are all references to fillers of slots in other components of the complex event or the complex event itself. Reference is expressed using the traditional dot notation (m.s[.f] is read as ‘the filler of the [facet f of the] slot s of the frame m’). Ontological instances are not indexed in the Fact DB. They appear in appropriate slots of complex events and their fillers are all references to fillers of other ontological instances within the same complex event or the complex event itself.
They are PART-OF (INVERSE of HAS-PARTS) of the complex event in which they are listed but
INSTANCE-OF their corresponding basic concept, that is, TEACH-DESCRIBE-A is the first ontological instance of DESCRIBE that is at the same time PART-OF TEACH.


teach is-a value communicative-event agent sem human default teacher theme sem knowledge destination sem human default student precondition default (teach-know-a teach-know-b)
effect default teach-know-c has-parts value (teach-describe
**repeat** (teach-request-information teach-answer)
**until** teach-know-c)
component-relations value (teach-after-a teach-after-b)
component-modalities value (teach-modality-a)


teach-know-a instance-of value know patient value teach.agent.sem theme value teach.theme.sem


teach-know-b instance-of value know patient value teach.destination.sem theme value teach.theme.sem


teach-modality-a type value epistemic scope value teach-know-b value value 0


teach-know-c instance-of value know patient value teach.destination.sem theme value teach.theme.sem


teach-describe instance-of value describe agent value teach.agent.sem theme value teach.theme.sem destination value teach.destination.sem


teach-request-information instance-of value request-information agent value teach.destination.sem theme value teach.theme.sem destination value teach.agent.sem


teach-answer instance-of value answer agent value teach.agent.sem theme value teach-request-information.theme.sem destination value teach.destination.sem


teach-after-a domain value teach-describe range value teach-request-information


teach-after-b domain value teach-request-information range value teach-answer


### 7.1.6 Axiomatic definition of ontology.
To summarize the basic decisions made in defining the ontology, we present its axiomatic definition. An earlier version of this definition was originally formulated by Kavi Mahesh (1996), on the basis of the Mikrokosmos implementation of ontological semantics.


The axioms collectively define a correct and consistent representation in the ontology and what does not. These axioms define the up-to-date view of the ontology in ontological semantics and provide a precise framework for discussing the implications of introducing additional features and complexities in ontological representations.


The axioms below use the following symbols:


Variables: p, r, s, t, u, v, w, x, y, and z.


Meta-ontological predicates: frame, concept, instance, slot and ancestor. Frame, concept, and instance are one-place predicates; ancestor is a two place predicate, indicating whether the second argument is an ancestor of the first. Slot is a 4-place predicate, its arguments being the concept, the slot, the facet, and the filler. Slot is the basic predicate. The rest of the meta-ontological predicates can be derived on its basis with the help of the constants listed below: a frame is a named set of slots, a concept is a frame in whose slots the facets VALUE, SEM, DEFAULT and RELAXABLE-TO
may appear; an instance is a frame in whose slots only the facet VALUE appears. An ancestor of a concept is a concept that is among the fillers of the IS-A slot of the latter (or, recursively, of one of its ancestors).


`Other predicates: =, ≠, ∈, ∉, ⊂, ∩, ∪, string, literal, reference and scalar. The predicate ∈ is to be read as _belongs to_ and indicates membership in a set. The predicate ⊂ is used in a generic sense and includes the relationship between a scalar range and its subranges. String, literal, and scalar are one-place predicates indicating whether an entity is a string, a scalar (i.e., a number or a range of numbers), or a literal symbol. Reference is a two-place predicate whose arguments are an entity and a slot and whose semantics is that the entity is bound to the filler of the slot.`


`Logical symbols: ¬, ∧, ∨, ∀, ∃, ⇒, ⇔`


Constants from the ontology: ALL, OBJECT, EVENT, PROPERTY, RELATION, ATTRIBUTE, LITERALATTRIBUTE, SCALAR-ATTRIBUTE, IS-A, INSTANCE-OF, SUBCLASSES, INSTANCES, DEFINITION, TIMESTAMP, DOMAIN, RANGE, INVERSE, NOTHING, VALUE, SEM, DEFAULT, NOT, RELAXABLE-TO,
DEFAULT-MEASURE.


The list of axioms follows:


1. A frame is a concept or an instance


`frame(x) ⇔ concept(x) ∨ instance(x)`
`concept(x) ⇒ ¬ instance(x)`
`instance(x) ⇒ ¬ concept(x)`


2. Every concept except ALL must have an ancestor.


`concept(x) ⇔ (x = all) ∨ ( ∃ y concept(y) ∧ slot(x, is-a, value, y))`


3. No concept is an INSTANCE-OF anything


`concept(x) ⇒ ¬ ∃ y slot(x, instance-of, value, y)`


4. If a concept x IS-A y then is in the SUBCLASSES of y.


`slot(x, is-a, value, y) ⇔ slot(y, subclasses, value, x)`


5. Every instance must have a concept that is its INSTANCE-OF.


`instance(x) ⇔ ∃ y concept(y) ∧ slot(x, instance-of, value, y)`


6. No instance is an IS-A of anything.


`instance(x) ⇒ ¬ ∃ y slot(x, is-a, value, y)`


7. If an instance x is an instance-of a concept y, then x is in the instances of y.


`slot(x, instance-of, value, y) ⇔ slot(y, instances, value, x)`


8. Instances do not have INSTANCES or SUBCLASSES.


`instance(x) ⇒ ( ¬ ∃ y slot(y, instance-of, value, x)) ∧ ( ¬ ∃ y slot(y, is-a, value, x))`


9. If y is an ancestor of x, then x and y are concepts and either x = y or x IS-A y or x IS-A z and y is an ancestor of z.


`ancestor(x,y) ⇔ concept(x) ∧ concept(y) ∧ ((x = y) ∨ slot(x, is-a, value, y) ∨ ( ∃ z slot(x, is-a, value, z) ∧ ancestor(z,y)))`


10. A concept is either ALL or has one of OBJECT, EVENT and PROPERTY as an ancestor.


`concept(x) ⇔ (x = all) ∨ ancestor(x, object) ∨ ancestor(x, event) ∨ ancestor(x, property)`


11. No concept has more than one of OBJECT, EVENT and PROPERTY as ancestors.


`concept(x) ⇒ ¬ (ancestor(x, object) ∧ ancestor(x, event))`
`concept(x) ⇒ ¬ (ancestor(x, object) ∧ ancestor(x, property))`


`concept(x) ⇒ ¬ (ancestor(x, event) ∧ ancestor(x, property))`


12. Every frame has a DEFINITION and a TIME-STAMP slot, each filled by a string.


`frame(x) ⇒ slot(x, definition, value, y) ∧ string(y) ∧ slot(x, time-stamp, value, z) ∧ string(z)`


13. If y is a slot in a concept, then y IS-A PROPERTY.


`slot(x, y, w, z) ⇒ ancestor(y, property)`


14. Every PROPERTY is either a RELATION or an ATTRIBUTE. No PROPERTY is both.


`slot(x, is-a, value, property) ⇒ (x=relation) ∨ (x=attribute)`
`ancestor(x, relation) ⇒ ¬ ancestor(x, attribute)`
`ancestor(x, attribute) ⇒ ¬ ancestor(x, relation)`


15. If concept x IS-A ATTRIBUTE and y is a slot in x, then y is one of IS-A, SUBCLASSES,
DEFINITION, TIME-STAMP, DOMAIN and RANGE.


`slot(x, y, w, z) ∧ ancestor(x, attribute) ⇒ y ∈ {is-a, subclasses, definition, time-stamp, domain, range}`


16. If concept x IS-A RELATION and y is a slot in x, then y is one of IS-A, SUBCLASSES,
DEFINITION, TIME-STAMP, DOMAIN, RANGE and INVERSE.


`slot(x, y, w, z) ∧ ancestor(x, attribute) ⇒ y ∈ {is-a, subclasses, definition, time-stamp, domain, range, inverse}`


17. Property slots in frames can be filled either directly or by reference to the filler in a slot of another concept, that is, by reference.


`∀ y slot(x, y, w, z) ⇒ frame(z) ∨ scalar(z) ∨ literal(z) ∨ ∃ t ( slot(s, t, u, v) ∧ reference(z, slot(s, t, u, v)))`


18. Fillers of INVERSE slot are always RELATIONs.


`slot(x, inverse, value, y) ⇒ ancestor(y, relation)`


19. If y is the INVERSE of x then x is the INVERSE of y.


`slot(x, inverse, value, y) ⇔ slot(y, inverse, value, x)`


20. There is only one INVERSE for every RELATION.


`slot(x, inverse, value, y) ⇒ ¬ ∃ z (slot(x, inverse, value, z) ∧ (y ≠ z))`


21. Fillers of domain slots must be OBJECTs, EVENTs or INSTANCEs.


`slot(x, domain, w, y) ⇒ object(y) ∨ event(y) ∨ instance(y)`


22. Fillers of RANGE slots of relations must be OBJECTs, EVENTs, INSTANCEs or NOTHING.


`slot(x, range, w, y) ∧ ancestor(x, relation) ⇒ object(y) ∨ event(y) ∨ instance(y) ∨ nothing`


23. If x has a slot y then x must have an ancestor t that is in the DOMAIN slot of concept y.


`slot(x, y, w, z) ⇒ ∃ t slot(y, domain, sem, t) ∧ ancestor(x, t)`


24. If x has a slot y that is a RELATION filled by z then z must have an ancestor t that is in the
RANGE of the concept y or z must be NOTHING.


`slot(x, y, w, z) ∧ ancestor(y, relation) ⇒ (∃ t slot(y, range, sem, t) ∧ ancestor(z, t)) ∨ (z =`
nothing)


25. An INVERSE slot may be inherited or present implicitly: if x has a slot y that is a RELATION
filled by z then z has a slot u filled by v where v is an ancestor of x, and y has an INVERSE t that is an ancestor of u.


`slot(x, y, w, z) ∧ ancestor(y, relation) ∧ (z ≠ nothing) ⇒ ( ∃ u ∃ v slot(z, u, w, v) ∧ ancestor(x, v) ∧ ∃ t (slot(y, inverse, value, t) ∧ (ancestor(u, t) ∧ ancestor(t, u)))) ∨ ( ∃ t ∃ v slot(y, inverse, value, t) ∧ slot(t, range, sem, v) ∧ ancestor(x, v))`


26. Inheritance of RELATION slots: if x has a RELATION y as a slot filled by z, and x is an ancestor of t, then t also has a slot y that is filled a u that has z as one of its ancestors or is NOTHING.


`slot(x, y, sem, z) ∧ ancestor(y, relation) ∧ ancestor(t, x) ⇒ ∃ u (slot(t, y, sem, u) ∧ (ancestor(u, z) ∨ (u = nothing)))`


27. Inheritance of ATTRIBUTE slots: if x has an ATTRIBUTE y as a slot filled by z, and x is an ancestor of t, then t also has a slot y that is filled by a u that is either z or a subset of z or
NOTHING.


`slot(x, y, sem, z) ∧ ancestor(y, attribute) ∧ ancestor(t, x) ⇒ ∃ u (slot(t, y, sem, u) ∧ ((u = z) ∨`
`(u ⊂ z) ∨ (u = nothing)))`


28. Every slot y in an instance x of concept t is also a slot in concept t; in x, y is filled with a narrower range or a lower concept (or an instance thereof), using the value facet.


`slot(x, y, w, z) ∧ instance-of(x, t) ⇒ slot(t, y, v, u) ∧ w = value ∧ (( z ⊂ u) ∨ ancestor(z, u))`


29. Every slot of a concept has at least one of VALUE, SEM and DEFAULT facets.


`slot(x, y, w, z) ⇒ w ∈ {value, sem, default}`


30. Every slot y (other than IS-A, SUBCLASSES, DEFINITION, TIME-STAMP, DOMAIN, RANGE and
INVERSE) of a concept x has one of the following sets of facets: VALUE with or without
DEFAULT-MEASURE or NOT, either DEFAULT, SEM, or both, with or without RELAXABLE-TO,
NOT and DEFAULT-MEASURE.


`slot(x, y, w, z) ∧ y ∉ { IS-A SUBCLASSES DEFINITION TIME-STAMP DOMAIN RANGE INVERSE} ∧`
`t ⊂ {not default-measure} ∧ u ⊂ {default sem} ∧ v ⊂ { relaxable-to not default-measure } ⇒`
`w ⊂ {value t} ∨ w = { u ∪ v}`


31. Every attribute is either a SCALAR-ATTRIBUTE or a LITERAL-ATTRIBUTE but not both.


`slot(x, is-a, value, attribute) ⇒ (x = scalar-attribute) ∨ (x = literal-attribute)`
`ancestor(x, scalar-attribute) ⇒ ¬ ancestor(x, literal-attribute)`
`ancestor(x, literal-attribute) ⇒ ¬ ancestor(x, scalar-attribute)`


32. The range of a SCALAR-ATTRIBUTE can only be filled by a scalar.


`ancestor(x, scalar-attribute) ∧ slot(x, range, w, y) ⇒ scalar(y)`


33. The range of a LITERAL-ATTRIBUTE can only be filled by a literal.


`ancestor(x, literal-attribute) ∧ slot(x, range, w, y) ⇒ literal(y)`


34. If property y is one of PRECONDITION, EFFECT, HAS-PARTS, COMPONENT-RELATIONS, and
COMPONENT-MODALITIES, then its filler z is a frame s the fillers of whose slots are only references.


`slot(x, y, w, z) ∧ y ∈ {precondition effect has-parts component-relations componentmodalities}⇒ frame(z) ∧ ∀ t ∀ v ∃ u (slot(z, t, value, v) ∧ ( slot(s, u, p, r) ∧ reference(v, slot(s, u, p, r))))`


Note: this axiom is needed to define the class of ontological instances.


## 7.2 Fact DB
The knowledge required in a world model for ontological semantics includes not only an ontology, as sketched above, but also records of past experiences, both actually perceived and reported, depending on the application. The _lingua mentalis_ equivalent of a text is an episode, a unit of knowledge that encapsulates a particular experience of an intelligent agent, and which is typically represented as a TMR, a temporally and causally ordered network of object and event instances.


The ontology and the episodes are sometimes discussed in terms of the contents of two different types of memory: semantic and episodic (e.g., Tulving, 1985; in the philosophy of language, a similar distinction is captured by the terms non-contingent and contingent knowledge—see Bar


Hillel 1954). This distinction is reflected in ontological semantics by the opposition between concepts, stored in the ontology, and their instances (episodes, facts), stored in the Fact DB. The presence of a systematic representation and indexing method for episodic knowledge is not only necessary for processing natural language but is also an enablement condition for case-based reasoning (Kolodner and Riesbeck 1986, Kolodner 1984, Schank 1982) and analogical inference
(e.g., Carbonell 1983).


Instances in the Fact DB are indexed by the concept they correspond to and can be interrelated on temporal, causal and other properties. The instances list only those properties of the corresponding concepts that have been given actual fillers as a result of processing some textual input or coreferential specification. The fillers in instances cannot be concepts; instead, they can be concept instances, literal or scalar values or ranges and references to either other property slot fillers or to even system-external elements, such as, for instance, URLs. The latter facility is useful when a value is constantly changing, as, for example, is the exchange rate between two currencies. The only facet allowed in instances for specifying a semantic filler is VALUE. Instance frame slots may contain two additional facets—TIME-RANGE and INFO-SOURCE, both introduced in the BNF in
Section 7.1.1 above but used only in specifying the Fact DB.


TIME-RANGE is used for truth maintenance, it marks the beginning and end of the time period, during which the datum specified in a particular property is true. For example, informally, if I painted my car blue three years ago and repainted it red yesterday, then the time-range for the property blue of my car would start on that date three years ago and end yesterday. INFO-SOURCE is used to record the source of the particular datum stored in the fact DB. One reason for having this facet is that it is, in practice, very typical that some property of an object or an event is given different fillers in different source texts (for example, people’s ages are habitually reported differently in different stories or newspapers). Since it may be necessary to record different timed values of properties and different data sources, in the CAMBIO/CREST implementation of ontological semantics, instance frames are allowed to have as many slots of the same name as there are differences in their fillers on either TIME-RANGE or INFO-SOURCE facets. An alternative solution would have been to create a new instance for each unique combination of TIME-RANGE and INFO-SOURCE
fillers.


Figures 25-27 show some typical facts from the Cambio/CREST Fact DB.


**Figure 25. An instance of INDIVIDUAL-SPORTS-RESULT in the CAMBIO/TIDES**
**implementation of ontological semantics; this fact records Mi-Jin Yun’s gold medal in**
**women’s individual archery.**


**Figure 26. The personal profile of Mi-Jin Yun in the CAMBIO/TIDES Fact DB.**


**Figure 27. This is what the CAMBIO/TIDES Fact DB knows about South Korea**


In early implementations, in contrast to ontological concepts, instances in Fact DB were given both formal names (generated by appending a unique numerical identifier to their corresponding concept name) and, optionally, names by which they could be directly referred to in the onomasticon (see Section 7.4 below). Thus, in the Spanish onomasticon, there was an entry _Estados Uni-_
_dos de America_ that pointed to the named instance USA (aka NATION-213). In most later implementations, the onomasticon of any language refers the appropriate name to NATION-213
directly. Thus, names of instances remain squarely within onomasticons.


## 7.3 The Lexicon
In any natural language processing system, the lexicon supports the processes of analysis and generation of text or spoken language at all levels—tokenization (that is, roughly, lexical segmentation), part-of-speech tagging and morphological analysis, proper-name recognition, syntactic, semantic and discourse/pragmatic analysis; lexical selection, syntactic structure generation and morphological form generation.


The lexicon for a given language is a collection of superentries which are indexed by the citation form of the word or the phrasal lexical unit (set expression). A **superentry** includes all the lexemes which have the same base written form, regardless of syntactic category, pronunciation, or sense. Each lexicon **entry** is comprised of a number of **zones** corresponding to the various types of lexical information. The zones containing information for use by an NLP system are: CAT (lexical category), ORTH (orthography—abbreviations and variants), PHON (phonology), MORPH (morphological irregular forms, class or paradigm, and stem variants or “principal parts”), SYN
(syntactic features such as _attributive_ for adjectives), SYN-STRUC (indication of sentence- or phrase-level syntactic dependency, centrally including subcategorization) and SEM-STRUC (lexical semantics, meaning representation). The following scheme, in a BNF-like notation, summarizes the basic lexicon structure. Some additional information is added for human consumption in the
ANNOtations zone.

```
    superentry ::=
           ORTH OGRAPHIC- FORM : “form”
           ({syn-cat}: <lexeme> * ) *
    lexeme ::=
           CAT EGORY: {syn-cat}
           ORTH OGRAPHY:
                  VAR IANTS: “variants”*
                  ABB REVIATION S : “abbs”*
           PHON OLOGY: “phonology”*
           MORPH OLOGY:
                  IRREG ULAR-FORMS: (“form”
                              {irreg-form-name})*
                  PAR ADIGM: {paradigm-name}
                  STEM-V ARIANTS: (“form” {variant-name})*
           ANNO TATIONS:
                  DEF INITION: “definition in NL” *
                  EX AMPLES: “example”*
                  COMMENTS : “lexicographer comment”*
                  TIME-STAMP : {lexicog-id date-of-entry}*
           SYN TACTIC-FEATURES: (feature value)*
           SYN TACTIC- STRUC TURE: f-structure
           SEM ANTIC- STRUC TURE: lex-sem-specification
```

The following example illustrates the structure and content of the lexicon. The example shows not a complete superentry but just the first verbal sense of the English lexeme _buy_ :


buy-v1
cat v


morph stem-v bought v+past bought v+past-participle


anno def “when A buys T from S, A acquires possession of T previously owned by S, and S acquires a sum of money in exchange”
ex “Bill bought a car from Jane”
time-stamp dha; 12-13-94 ;the acquirer and the date


syn syn-class trans + ;redundant with SYN-STRUC; may be


;useful for some applications syn-struc root buy subj root $var1
cat n obj root $var2
cat n oblique root from cat prep opt +
obj root $var3
cat n

sem-struc buy agent value ^$var1
sem HUMAN
theme value ^$var2
sem OBJECT
source value ^$var3
sem HUMAN


The above states that the verb _buy_ takes a subject, a direct object and a prepositional adjunct, that its meaning is represented as an instance of the ontological concept BUY; that the AGENT of the concept BUY, which constitutes the meaning of the verb’s subject, is expected to be a HUMAN; that the THEME of the concept BUY, which is the meaning of the verb’s direct object, can be any
OBJECT; and that the SOURCE of the concept BUY, which constitutes the meaning of the verb’s prepositional adjunct, can be a HUMAN.


The presence of variables ( _$varN_ ) in the SYN-STRUC and SEM-STRUC zones of the lexicon is obviously intended to establish a kind of co-indexing. Indeed, it links syntactic arguments and adjuncts of the lexeme (if any) with the case roles and other ontological properties that the meanings ( _^$varN_ reads “the meaning of _$varN_ ”) of these syntactic arguments and adjuncts fill.


The meaning of the lexeme is established separately. For most open-class lexical units, the specification of meaning involves instantiating and often constraining one or more ontological concepts and/or values of parametrical elements of TMR (e.g., modality, style, aspect, etc.). The case of _buy-v1_ is rather simple, as all the constraints from the ontological concept that forms the basis of its meaning description will remain unchanged in the lexical meaning. To describe the meaning of the English words _acquire-v2_ and _acquire-v3,_ the senses used to refer to corporations buying corporations, the ontological concept BUY will be used as well, but in both these cases, it will be further constrained:


acquire-v2
cat v


anno def “when company A buys company, division, subsidiary, etc. of company
T from the latter”
ex “Alpha Inc acquired from Gamma Inc the latter’s candle division”


syn-struc


root acquire subj root $var1
cat n obj root $var2
cat n oblique root from cat prep opt +
obj root $var3
cat n

sem-struc buy agent value ^$var1
sem corporation theme value ^$var2
sem organization source value ^$var3
sem corporation


acquire-v3
cat v anno def “when company A buys company T”
ex “Bell Atlantic acquired GTE”
syn-struc root acquire subj root $var1
cat n obj root $var2
cat n sem-struc buy agent value ^$var1
sem CORPORATION
theme value ^$var2
sem CORPORATION
source value ^$var2.OWNED-BY
sem HUMAN


The constraints on the properties of BUY as used in the lexicon to specify the meaning of _acquire-_
_v2_ have been changed from the ontological concept to its occurrence in the lexicon entry. In
AGENT and SOURCE, HUMAN was replaced by CORPORATION. In THEME, OBJECT was narrowed down to ORGANIZATION. This mechanism—allowing the lexical meaning in lexicon entries to be specified using modified values of fillers in the concept that forms the basis of the meaning of the lexeme—is an important capability that keeps the ontology as a language-independent resource, while specifying lexical idiosyncrasies within the lexicon of a language. The alternative to this solution would lead to a separate concept for specifying the meaning of _acquire-v2_ (and _acquire-_
_v3_, too) and consequently, to separate concepts for meanings of lexemes from different languages.
This would entirely defeat the goal of language-independent meaning specification, as it would require establishing bilingual correspondences of meanings, essentially the same way as asemantic transfer MT systems establish correspondences of strings in various languages, sometimes


with further constraints of a syntactic nature. It is because of considerations such as the above that we fail to recognize the merits of developing different ontologies for different languages (e.g.,
Vossen 1998).


The above example illustrates an additional point. The SOURCE case role typically does not have a syntactic realization. However, the ontological concept BUY that we use, again, economically, to represent the meaning of _acquire-v3_, stipulates the presence of SOURCE and constrains it to
HUMAN. The meaning of _acquire-v3_ actually includes the (world knowledge) information about the source: it is the stockholders or, generally, owners of the corporation that is the meaning of the direct object of _acquire-v3_ . The lexicon entry, correspondingly, lists this information, using the dot notation to refer to the filler of the OWNED-BY slot of the frame for CORPORATION.


The attentive reader will have noticed by now that the above formulation of _acquire-v3_ leads to a violation of a precept of ontological semantics, specifically, that instances in the basic TMR do not contain those properties of the corresponding concept that are not overtly specified, either in an input text or, in some applications, by a human user. If the information about the source property is not mentioned, it should not be a part of the lexical entry. If it is mentioned, its value should not be specified by reference, but rather directly as the meaning of an appropriate syntactic constituent in the input text. The information in the SEM-STRUC zone of the above entry simply will remain recorded in the ontology as the filler of the default facet of the property OWNED-BY, that is, we will retain the capability of making the inference that companies are sold by their owners should such inference (which will be licensed by the extended TMR) be called for by a reasoning module of an application.


There is an important reason why this information should be recorded in the ontology and not in the lexicon. If it is recorded in the lexicon, as shown in the entry for _acquire-v3_ above, and an input containing _acquire_ has no explicit information about ownership, _acquire_ will be assigned its
_acquire-v3_ sense and the TMR will have the OWNED-BY property filled not with an actual value but rather with the potential, ontological filler for OWNED-BY of the THEME of BUY. Now, should further input contain a direct mention of ownership, the procedure will have to substitute the new filler for the old one. If, on the other hand, the information is recorded in the ontology, it will not be instantiated in the TMR until an explicit mention of ownership in the input or if the application calls for the use of information in the extended TMR. As a reminder for the reader, extended
TMRs contain those properties of the ontological concepts instantiated in the basic TMR that are not explicitly mentioned in the input; whose fillers are listed in SEM and DEFAULT facets and are, therefore, abductively defeasible.


In the ontological concept BUY the ownership information that we discuss above is recorded as follows:


BUY
...
theme default commodity sem object source default theme.owned-by sem human relaxable-to organization
...


We will return to the important issue of the proper place for recording semantic constraints—the ontology or the lexicon—in Section 9.1 below in the context of knowledge acquisition.


On the whole, all of the above examples were quite straightforward with respect to the linking relations: the grammatical subject is a natural clue for agency; direct objects very often signal themes, etc. [74] The relations between the syntactic and the semantic information in the ontological semantic lexicon can, however, be much more complicated. Thus, two values of the SYN-STRUC
zone may appear in a single entry, if they correspond to the same meaning, as expressed in the
SEM-STRUC zone of the entry (19); syntactic modification, as recorded in the SYN-STRUC zone, may not yield a parallel semantic modification in the SEM-STRUC zone (20); the semantics of a lexicon entry may be linkable to a component of the syntactic structure by reference rather than directly (25).


(19)

big-adj1
cat adj syn-struc 1 root $var1
cat n mods root big
2 root big cat adj subj root $var1
cat n sem-struc
1 2 size-attribute domain value ^$var1
sem physical-object range value                         - 0.75
relaxable-to                                   - 0.6


In the above example, there are two subcategorization patterns, marked 1 and 2, listed in SYNSTRUC. The former pattern corresponds to the attributive use of the adjective: the noun it modifies is assigned the variable _$var1_, and the entry head itself appears in the modifier position. The latter pattern presents the noun, bound to _$var1_, in the subject position and the adjective in the predicative position. Once againm in the SEM-STRUC zone, instead of variables bound to syntactic elements, the meanings of the elements referred to by these variables (and marked by a caret, ‘^’) are used. Thus, _^$var1_ reads as ‘the meaning of the element to which the variable _$var1_ is bound.’


74. A version of LFG has been chosen as the syntactic framework to aid ontological semantics largely because it concentrates on the syntax-to-semantics linking; so that, for instance, we do not have to worry about the passive construction rearranging the above clues.


Among the constraints listed in the SEM-STRUC zone of an entry, are selectional restrictions (the noun must be a physical object) and relaxation information, which is used for treatment of unexpected (‘ill-formed’) input during processing.


Thus, an entry like the above should be read as follows:


- the first line is the head of the superentry for the adjective _big_ (in our terminology, an ‘entry’
is a specification of a single sense, while the ‘superentry’ is the set of such entries);

- the second line assigns a sense number to the entry within its superentry;

- next, the adjective is assigned to its lexical category;

- the first subcategorization pattern in the SYN-STRUC zone describes the Adj-N construction; the second subcategorization pattern describes the N-Copula-Adj construction;

- the SEM-STRUC zone defines the lexical semantics of the adjective by assigning it to the class of SIZE adjectives; stating that it is applicable to physical objects and that its meaning is a high-value range on the SIZE scale/property.


The two subcategorization patterns in the SYN-STRUC zone of the entry correspond to the same meaning. There is an even more important distinction between this lexical entry and those for the verbs _buy_ and _acquire_ . The meanings of entries for words that are heads of syntactic phrases or clauses, that is, predominantly verbs and nouns, are typically expressed by instantiating ontological concepts that describe their basic meaning, with optional further modification, in the lexicon entry itself, by either modifying property values of these concepts or introducing additional, often parametric, meaning elements from the ontology. In the case of modifiers—mostly adjectives and adverbs—the meaning is, in the simplest case, expressed by the filler of a property of another concept, namely, the concept that forms the basis for the meaning specification of the modifier’s syntactic head. Thus, in the entries for the verbs, the concepts that form the basis of specifying their meanings appear at the top level of the SEM-STRUC zone. In the entries for modifiers, such as _big_, the reference to the concept that is, in fact, the meaning of _big_, is introduced as the value of the domain of the property SIZE-ATTRIBUTE that forms the basis of the meaning of _big_ . This distinction is further marked notationally: in the verb entries the main concept refers to the syntactic constituent corresponding to the lexeme itself; in the entries for modifiers, the main concept refers to the syntactic constituent marked as _$var1_, the head of the modifier.


In the lexicon entries, the facet VALUE is used to refer to the meanings of the syntactic constituents mentioned in the SYN-STRUC zone, while the ontology provides the semantic constraints (selectional restrictions—see Section 8.2.2 below), recorded in the DEFAULT, SEM and RELAXABLE-TO
facets of its concepts; as was already shown, these constraints may be modified during the specification of the lexical meaning.


(20)

good-adj1
cat adj syn-struc
1 root $var1
cat n mods root good
2 root $var0


cat adj subj root $var1
cat n sem-struc modality type evaluative value value                - 0.75
relaxable-to                         - 0.6
scope ^$var1
attributed-to *speaker*


The meaning of _good_ is entirely parametrized, that is, the sem-struc zone of its entry does not contain any ontological concept to be instantiated in the TMR. Instead, the meaning of _good_ is expressed as a value of modality on the meaning of the element that _good_ modifies syntactically.
The meaning of _good_ is also non-compositional (see Section 3.5.2-3) in the sense that it deviates from the usual adjectival meaning function of highlighting a property of the noun the adjective modifies and—in a typical case—assigning a value to it.


The meaning of _good_ presents an additional problem: it changes with the meaning of the noun it modifies. This phenomenon is often referred to as plasticity (see Marx 1983; Raskin and Nirenburg 1995). We interpret _good_ in a sentence like (21) as, essentially, (22). We realize that, in fact,
_good_ in (21) may have a large variety of senses, some of which are illustrated in the possible continuations of (21) in (23). Obviously, _good_ may have additional senses when used to modify other nouns (24).


(21)   This is a good book.
(22)   The speakers evaluates this book highly.
(23)   ...because it is very informative.
...because it is very entertaining.
...because the style is great.
...because it looks great on the coffee table.
...because it is made very sturdy and will last for centuries.
(24)   This is a good breadmaker.
He is a good teacher.
She is a good baby.
Rice is good food.
In each case, _good_ selects a property of a noun and assigns it a high value on the evaluation scale associated with that property. The property changes not only from noun to noun but also within the same noun, depending on the context. The finest grain-size analysis requires that a certain property of the modified noun is contextually selected as the one on which the meaning of the noun and that of the adjective is connected. This is what many psychologists call a ‘salient’ property.


Now, it is difficult to identify salient properties formally, as is well known, for instance, in the scholarship on metaphor, where salience is the determining factor for the similarity dimension on which metaphors (and similes) are based (see, for instance, Black 1954-55, 1979; Davidson 1978;
Lakoff and Johnson 1980, Lakoff 1987; Searle 1979; on salience, specifically, see Tversky and
Kahnemann 1983). It is, therefore, wise to avoid having to search for the salient property, and the hypothesis of practical effability for MT (see Section 9.3.6 below) offers a justification for this.
What this means, in plainer terms, is that if we treat the meaning of _good_ unspecified with regard


to the noun property it modifies, there is a solid chance that there will be an adjective with a matching generalized, unspecified meaning like that in the target language as well.


In the extant implementations of ontological semantics, the representation solution for _good_, as illustrated in the entry, deliberately avoids the problem of determining the salient property by shifting the description to a coarser grain size, that is, scoping not over a particular property of an object or event but over an entire concept. This decision has so far been vindicated by the expectations of the current applications of ontological semantics—none so far has required a finer grain size. In MT, for example, this approach “gambles” on the availability across languages of a “plastic” adjective corresponding to the English _good_ —in conformance with the principle of practical effability that we introduce in the context of reducing polysemy (see Raskin and Nirenburg 1995
and Section 9.3.5 below).


Note that the issue of plasticity of meaning is not constrained to adjectives. It affects the analysis of nominal compounds and indeed makes it as notoriously difficult as it has proven to be over the years. In fact, analyzing nominal compounds, e.g., _the IBM lecture_, is even more difficult than analyzing adjectival modification because in the former case there is no specification of any property on which the connection can be made, even at the coarse grain size that we use in describing the meaning of _good_ . Indeed, IBM may be the filler of the properties OWNED-BY, LOCATION,
THEME as well as many others (cf. Section 8.2.2, especially examples 42-44, below).


Returning to the issues of linking, we observe that non-compositional adjectives also include also include temporal adjectives, such as _occasional_ (see below) as well as Vendler’s (1968) classes
A5-A8 of adjectives that “ascribe the adjective... to a whole sentence.”


(25)

occasional-adj1
cat adj syn-struc root $var1
cat n mods root occasional sem-struc
^$var1.agent-of aspect phase b/c/e iterationmultiple


The concept introduced in the SEM-STRUC zone of this entry corresponds neither to the lexeme itself nor to the noun the latter modifies syntactically. Rather, it introduces a reference to the
EVENT concept of which the meaning of the modified noun is AGENT.


The next example is even more complex and provides a good example of the expressive power of ontological semantics. There is no ontological concept TRY or, for that matter, FAIL or SUCCEED.
The corresponding meanings, when expressed in natural language, are represented parametrically, as values of the epiteuctic modality (see Section 8.5.3 below).


try-v3


syn-struc root try cat v subj root $var1
cat n xcomp root $var2
cat v form OR infinitive gerund sem-struc set-1 element-type refsem-1
cardinality >=1
refsem-1 sem event agent ^$var1
effect refsem-2
modality type epiteuctic scope refsem-2
value < 1
refsem-2 value ^$var2
sem event


The SEM-STRUC zone of the above example is interpreted as follows. SET-1 consists of one or more events whose properties are presented using the internal co-reference device REFSEM. This device, to which we referred in the section on ontology as reification, is necessary in the lexicon for the same reason: because property fillers in the format of our ontology must be strings or references in the dot notation. In other words, if these strings refer to concepts, then no properties of these concepts can be constrained in the fillers. So, the REFSEM mechanism is needed to reify the concept that would serve as a filler and constrain its properties in the free-standing specification of the concept instance in the TMR. The agent of each of the events in SET-1 is the meaning of the subject of the input sentence, essentially, the entity that does the trying. These events have an effect that is the meaning of the XCOMP in the source text and that must be an EVENT (once again, we must reify the filler of effect because it has a property of its own, being an EVENT). The meaning of _try_ includes the idea that the event that was attempted was not achieved. This is, as mentioned above, realized in ontological semantics parametrically as a value on the epiteuctic modality. It scopes over the desired effect of the agent’s actions and its value, < 1, records the lack of success.


To further clarify the meaning of _try_, as represented in the above entry, let us look at the sentence
_I tried to reach the North Pole_ . In it, _try_ is used in the sense described above and has the following meaning:


- the agent performs one or more actions that are not specified in the sentence;

- each of these actions (all of them, in reality, complex events) have the event of reaching the
North Pole as their EFFECT;

- the epiteuctic modality value states that the goal is not reached (that is, the speaker has not reached the North Pole).


Our philosopher colleagues may object at this point that the example above does not necessarily


imply the failure of each of the attempts and quote a sentence like _I tried and, moreover, suc-_
_ceeded in reaching the North Pole_ as a counterexample. Leaving aside the marginal acceptability of such a sentence (nobody talks like that unless one deliberately wants to make a humorous effect through excessive pedantry), this sentence should be actually characterized as a repair, that is _I_
_tried, no, actually, I succeeded in reaching the North Pole_ . _Moreover_ functions as a mark to cancel the meaning of the beginning of the sentence, similarly to the way _but_ does in _I tried to reach_
_the North Pole several times but succeeded only once_ . A different way of expressing the same position on the issue is to say that the meaning of _succeed_ automatically subsumes any attempts to succeed, thus making a mention of those attempts redundant. What is at issue is, of course, simply how to define the meaning of _try_ —as allowing for successful attempts or not—and the argument we have presented supports the latter choice.


We have established so far that the meaning of a lexeme can be represented as an ontological concept, as the property of an ontological concept or as the value of a parameter, that is, in a manner unrelated to any ontological concept other than the name of the parameter. This does not exhaust all the possibilities. Thus, many closed-class lexemes enjoy special treatment: personal pronouns, determiners, possessives and other deictic elements, such as _here_ or _now_, as well as copulae are treated as triggers of reference-finding procedures; some conjunctions may introduce discourse relations in the TMR; numerals and some special adjectives, e.g., _every_ and _all_, characterize set relations. Of course, the emphasis in ontological lexical semantics is on open-class lexical items.


## 7.4 The Onomasticon
Nouns can be common ( _table, sincerity_ ) or proper ( _World War II, Mr. Abernathy_ ). The common nouns are listed in the lexicon, where their meanings are typically explained in terms of the ontoogy. Proper nouns, or names, in ontological semantics are listed in the onomasticon, where their meanings are explained in terms of both the ontological categories to which they belong, and facts from the Fact DB to which they refer. Each such fact is, by definition, an instance of an ontological concept. Therefore, entries in the onomasticon name instances—specific and unique objects and events, not their types. For example, the Toyota Corolla with the Indiana license plate
45G9371 is an instance. But Toyota Corolla is a class of all the instances of this particular model of this particular car make and as such is not listed in the onomasticon but rather in the ontology.
However, _Toyota_ will be listed in the onomasticon because it refers to the name of a unique corporation, say, CORPORATION-433, in the Fact DB. Similarly, Passover 2000 is an instance of an event, while Passover is a concept.


In the CAMBIO/CREST implementation of ontological semantics, the phrasal entry _United States_
_of America_ is listed in the onomasticon as NATION and refers to Fact DB element NATION-213. In the case of proper names, the extended TMR is obtained by including information about it from the Fact DB, in addition to its ontological information. Thus, an input text might just mention the name (or alias, such as _USA_ or _US of A_ ) of the phrase, but its extended TMR will include both information and NATION (see Figure 28) and NATION-213 (FIGURE 29).


**Figure 28. The ontological concept NATION, a view of the inheritance paths from the root of the ontology and**
**a partial view of the properties of the concept.**


**Figure 29. A partial view of the fact UNITED STATES** **OF AMERICA.**


Ontological concepts used in categorizing entries in the onomasticon are given in Table 13:


**Table 13: Ontological Concepts Used in Onomasticon**


Animate Name of a living being (human, animal, plant or imaginary character like _Zeus_
or _Bucephalus_ )


Organization Name of an organization, real (e.g., _Toyota Corp., U.S. Senate, NATO, The US_
_Republican Party, Harvard University, McDonald’s_ ) or imaginary (e.g., _RUR_ )


Time-period Name of an event, e.g., _Christmas_ _2000_ or a period, e.g., _The Middle Ages_


Geographi- Name of a geographical entity: river, valley, mountain, lake, sea, ocean, astrocal-Entity nomical entity, etc. May contain a common noun identifying some geographical feature contained within a geographic name, such as _valley, mount,_ etc.
( _The Mississippi_, _The_ _Mississippi River_ )
