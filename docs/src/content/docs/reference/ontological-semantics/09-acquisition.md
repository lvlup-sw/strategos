---
title: "Chapter 9: Acquisition of Static Knowledge Sources"
source: "Nirenburg, S. & Raskin, V. (2004). Ontological Semantics. MIT Press."
pdf_pages: "257-295"
notice: "Private reference copy -- not for distribution"
---

# 9. Acquisition of Static Knowledge Sources for Ontological Semantics
In Chapter 2, we define theory as a set of statements that determine the format of descriptions of phenomena in the purview of the theory. A theory is effective if it comes with an explicit methodology for acquiring these descriptions. A theory associated with an application is interested in descriptions that support the work of an application. We illustrated these relationships in Figure
40. Here we reproduce a modified version of that figure that specifies how that schema applies not to any application-oriented theory but concretely to ontological semantics (the interpretation of the general notions, given in red, for ontological semantics is given in green in the figure).


To recapitulate, the **theory** of ontological semantics includes the format and the semantics of the
TMR, the ontology, the Fact DB, the lexicons and the onomasticons as well as the generic processing architecture for analysis of meaning and its manipulation, including generation of text off of it. The **description** part in ontological semantics includes all the knowledge sources, both static and dynamic (generic procedures for extraction, representation and manipulation of meaning), implemented to provide full coverage for a language (or languages) and the world. In practice, ontological semantic description is always partial, covering only a subset of subject domains and sublanguages, and constantly under development, through the process of acquisition and as a side effect of the operation of any applications based on ontological semantics.


The **methodology** of ontological semantics consists of acquisition of the static knowledge sources and of the procedures for producing and manipulating TMRs. We addressed the latter in Chapter 8
above. Here, we focus on the former. In our presentation, we will not focus on the methodology of specific applications of ontological semantics beyond restating (cf. Section 6.7 above) that TMRs may be extended in a well-defined way to support a specific application and that such an extension may require a commensurate extension and/or modification of the static resources used by the application. We will start with a general discussion of the attainable levels of automation for acquiring static knowledge sources in ontological semantics. We will then address the specific techniques of acquisition for each of the static resources, ontology, Fact DB, lexicon, and onomasticon.


## 9.1 Automating Knowledge Acquisition in Ontological Semantics
Knowledge-based applications involving natural language processing have traditionally carried the stigma of being too expensive to develop, difficult to scale up and to reuse as well as incapable of processing a broad range of inputs. [85] The opinion about the high price of development was due to the perceived necessity to acquire all knowledge manually, using highly-trained and, therefore,


85. “Today's state-of-the-art rule-based methods for natural language understanding provide good performance in limited applications for specific languages. However, the _manual_ development of an understanding component using specific rules is costly as each application and language requires its own adaptation or, in the worst case, a completely new implementation. In order to address this cost issue, statistical modeling techniques are used in this work to replace the commonly-used hand-generated rules to convert the speech recognizer output into a semantic representation. The statistical models are derived from the automatic analyses of large corpora of utterances with their corresponding semantic representations. To port the semantic analyzer to different applications it is thus sufficient to train the component on the application- and language-specific data sets as compared to translating and adapting the rule-based grammar by hand” (Minker _et al_ . 2000, xiv).


expensive, human acquirers. The difficulty in scaling up was believed to reflect the deficiencies in description breadth, or coverage of material, in the acquisition task for any realistic application.
The all-too-real failure of knowledge-based processors on a broad range of inputs was attributed to the lack of depth (or, using our terminology, coarseness of the grain size) in the specification of world and language knowledge used by the meaning manipulation procedures.


**S** **y** **s** **t** **e** **m** **s** **a** **n** **d**


**R** **e** **s** **u** **l** **t** **s**


**Figure 40. Interrelationships between theory, methodology, descriptions and applications in ontological**
**semantics.**


In the consecutive implementations of ontological semantics, the above problems have been progressively addressed. While we cannot claim to have completely eliminated the need for controlling the acquisition process by people, we are satisfied that ontological semantics uses about as much automation in the acquisition process as is practical within the state of the art in statistical methods of text processing and human-computer interaction. In addition to that, the acquisition methodology takes advantage of all and any possibilities for minimizing human acquisition effort and maximizing the automatic propagation of semantic information recorded earlier over newly acquired material, as applicable. The use of inheritance in ontology; of information extraction


engines in acquiring facts for the Fact DB; as well as lexical rules and class-oriented syntactic dependency templates in the lexicon, are among the examples of such facilities. We have had numerous opportunities to port the resources of ontological semantics across applications, and found this task feasible and cost-effective, even within small projects. In the rest of this section, we briefly review the methodology of knowledge acquisition that has emerged over the years in ontological semantics.


Before a massive knowledge acquisition effort by teams of acquirers can start, there must be a preparatory step that includes, centrally, the specification of the formats and of the semantics of the knowledge sources, that is, the development of a theory. Once the theory is initially formulated (it is fully expected that the theory will be undergoing further development between implementations), the development of a toolkit for acquisition can start. The toolkit includes acquisition interfaces, statistical corpus processing tools, a set of text corpora, a set of machine-readable dictionaries (MRDs), a suite of pedagogical tools (knowledge source descriptions, an acquisition tutorial, a help facility) and a database management system to maintain the data acquired. In many ontology-related projects, the work on the knowledge specification format, on portability and on the acquisition interfaces becomes the focus of an entire enterprise (see, for instance, Ginsberg
1991, Genesereth and Fikes 1992, Gruber 1993, Farquhar _et al._ 1997 for a view from one particular research tradition). In such format-oriented efforts, it is not unusual to see descriptive coverage sufficient only for bootstrapping purposes. Ontological semantics fully recognizes the importance of fixed and rigorous formalisms as well as good human computer interaction practices. However, in the scheme of priorities, the content always remains the prime directive of an ontological semantic enterprise.


The preparatory step is in practice interleaved with the bootstrapping step of knowledge acquisition. Both steps test the expressive power of the formats and tools and seed the ontology and the lexicon in preparation for the massive acquisition step.


The bootstrapping of the ontology consists of:


- developing the specifications of the concepts at top levels of the ontological hierarchy, that is, the most general concepts;

- acquiring a rather detailed set of properties, the primitives in the representation system (for example, case roles, properties of physical objects, of events, etc.), because these will be used in the specifications of all the other ontological concepts;

- acquiring representative examples of ontological concepts that provide models (templates)
for specification of additional concepts; and

- acquiring examples of ontological concepts that demonstrate how to use all the expressive means in ontology specification, including the use of different facets, of sets, the ways of specifying complex events, etc., also to be used as a model by the acquirers, though not at the level of an entire concept.


The bootstrapping of the lexicon for the recent implementations of ontological semantics involved creating entries exemplifying:


- all the known types of syntax-to-semantics mapping (linking);


- using every legal kind of ontological filler—from a concept to a literal to a numerical or abstract range;

- using multiple ontological concepts and non-propositional material, such as modalities or aspectual values, in the specification of a lexical entry;

- using such expressive means as sets, refsems and other special representation devices.


The main purpose of this work is to allow the acquirer during the massive acquisition step to use the example entries as templates instead of deciding on the representation scheme for a meaning from first principles. As usual, practical acquisition leads to the necessity of revising and extending the set of such templates. This means that bootstrapping must be incremental, that is, one cannot expect for it to finish before the massive acquisition step. The preparatory step and bootstrapping are the responsibility of ontological semanticists who are also responsible for training acquirer teams and validating the results of massive knowledge acquisition. The complete set of types of work that ontological semanticists must do to facilitate a move from pure theory to an actual description includes:


- theory specification,

- acquisition tool design,

- resource collection,

- bootstrapping,

- management of acquisition teams:

    - training,

    - work process organization,

    - quality control.


At the step of massive knowledge acquisition, the acquirers use the results of the bootstrapping stage to add ontological concepts and lexicon entries to the knowledge base. It is important to understand that, in the acquisition environment of ontological semantics, acquirers do not manually record all the information that ends up in a static knowledge source unit—an ontological concept, a lexical entry or a fact. Following strict regulations, they attempt to minimally modify existing concepts and entries to produce new ones. Very typically, in the acquisition of an ontological concept, only a small subset of properties and property values are changed in a new definition compared to the definition of an ancestor or a sibling of a concept that is used as a starting template. Similarly, when acquiring a lexical entry, the most difficult part of the work is determining what concept(s) to use as the basis for the specification of the meaning of a lexical unit; the moment such a decision is made, the nature of the work becomes essentially the same as in ontological acquisition—determining which of the property values of the ontological concept to modify to fit the meaning. With respect to facts, the prescribed procedure is to use an information extraction system to fill ontologically inspired templates that become candidate entries in the fact database, so that the task of the acquirer is essentially just to check the consistency and validity of the resulting facts. At the end of the day, only a fraction of the information in the knowledge unit that is acquired at the massive acquisition step is recorded manually by the acquirer, thus imparting a rather high level of automation to the overall acquisition process.


The lists of candidate ontological concepts and lexicon entries to be acquired are included in the toolkit and are manipulated in prescribed ways. Acquirers take items off these lists for acquisition


but as a result of at least some acquisition efforts, new candidates are also added to these lists. For example, when a leaf is added to an ontological hierarchy, it often becomes clear that a number of its conceptual siblings are worth acquiring. When a word of a particular class is given a lexicon entry, it is enticing to immediately add the definitions of all the other members of this class. The above mechanism of augmenting candidate lists can be called deductive, paradigmatic or domaindriven (see Section 9.3.2 below). The alternative mechanism would be inductive, syntagmatic and corpus-driven and will involve adding words and phrases newly attested in a corpus to the list of lexicon acquisition candidates. Because the description of the meaning of some of such new words or phrases will require new concepts, the list of candidates for ontology acquisition can also be augmented inductively.


The results of the acquisition must be validated for breadth and depth of coverage as well as for accuracy. Breadth of coverage relates to the number of lexical entries, depth of coverage relates to the grain size of the description of each individual entry. The appropriate breadth of coverage is judged by the rate at which an ontological semantic application obtains inputs that are not attested in the lexicon. The depth of coverage is determined by the disambiguation needs and capabilities of an application that determine the minimum number of senses that a lexeme should have. In other words, the specification of meaning should not contain elements that cannot be used by application programs. Accuracy of lexical and ontological specification can be checked effectively only by using the acquired static knowledge sources in a practical application and analyzing the failures in such applications. Many of these failures will have to be eliminated by tightening or relaxing constraints on the specification of the static knowledge sources.


## 9.2 Acquisition of Ontology
Acquisition of ontology involves the following basic tasks:


- determining whether a meaning is worth introducing a new concept;

- finding a place for the concept in the ontology, that is determining which of the existing concepts in the ontology would best serve as the parent or sibling of the newly acquired concept;

- specifying properties for the new concept, making sure that it is different from its parents, children and siblings not only on ONTOLOGY-SLOT properties but rather in a more contentful way, through other ontological properties.


The main considerations in deciding on whether a new concept is warranted are:


- the desired grain size of description; for instance, if in a question answering system we do not expect questions concerning a particular property or set of properties (or, which amounts to the same, are content with the system failing on such questions), then the corresponding property becomes too fine-grained for inclusion in the ontology; for example, in the
CAMBIO/CREST implementation of ontological semantics for the application of question answering, in the domain of sports, no information was included about the regulation sizes and weights of the balls used in various games—baseball, basketball, etc., for the reason that we did not expect such questions to be asked of the system;

- the perception of whether a meaning is generic and language-independent (and, therefore,


should be listed in the ontology) or a language-specific “fluctuation” of some basic meaning
(and should, therefore be described in the lexicon for the language in question);

- the perception of whether a meaning is that of a concept (a type, a class of entities, a meaning, a _significatum_ ‘signified,’ a “variable”) or a fact (an instance, a token, an individual, a reference, a _denotatum_, a “constant”); for example, US-PRESIDENT is a concept, while _John Kennedy_ is the name (stored in the onomasticon) of an instance of US-PRESIDENT, namely, US-PRESIDENT-35; CORPORATION is a concept; _Ford Motor Company_ is the name of an instance of corporation; FORD-FOCUS, however, is a concept, a child of CAR-MAKE and
CAR-MODEL; my cousin Phyllis’s Ford Focus is an instance of the concept FORD-FOCUS; incidentally, if she calls her car Preston, this will probably not be general or useful enough knowledge to warrant being included in the onomasticon of an ontological semantic application;

- the perception of when the analysis and other meaning processing procedures would fail if particular concepts are not present in the ontology, e.g., the judgment that a particular disambiguation instance cannot be handled using dynamic selectional restrictions (see
Section 8.3.1 above).


With respect to language specificity, consider the example of the German _Schimmel_, ‘white horse.” There seems to be no reason to introduce an ontological concept for _white horse_, as this meaning is easily described in the lexicon by including in the SEM-STRUC field of the corresponding entry an instance of HORSE, with the property of COLOR constrained to WHITE. Also if this concept is introduced, the considerations of symmetry would lead to suggesting as many siblings for this concept as there are colors in the system applicable to horses.


To generalize further, it is a useful rule of thumb in ontology acquisition not to add an ontological concept if it differs from its parent only in the fillers of some of its attributes because, as we showed in Section 7.2 above, this is precisely the typical action involved in specifying a lexical meaning in the lexicon on the basis of a concept. It is a vote for introducing a new ontological concept if, in the corpus-driven mode of knowledge acquisition, no way can be found of relating a candidate lexeme or candidate sense of an attested lexeme to an existing concept or concepts by constraining some or all of its/their property values.


In other words, it is best to introduce new ontological concepts in such a way that they differ from their parents in the inventory of properties, not only in value sets on the properties that they share.
Barring that, if the difference between a concept and its parent is in the values of relations other than the children of ONTOLOGY-SLOT (e.g., IS-A or INSTANCES) then a new concept may also be warranted. Barring that, in turn, if there are differences between a concept and its ancestor on more than one attribute, a new concept should be favorably considered. Finally, if the constraint on an attribute in the parent is an entire set of legal fillers or if a relation has as its filler a generic constraint ‘OR EVENT OBJECT,’ and the child introduces stricter constraints, one may consider a new ontological concept. Experience in acquisition for ontological semantics shows that applying these rules can be learned relatively reliably, and compliance with them is easy to check.


The task of finding the most appropriate place to ‘hook’ a concept in the ontology is also complicated. Let us assume that we have already determined, using the above criteria, that TEACH
deserves to be a new ontological concept. The next task is to find one or more appropriate parents


or siblings for the concept. Acquirers use a mixture of clues for placing this concept in the ontological hierarchy. Experienced acquirers, well familiar with many branches of the ontological hierarchy, may think of an appropriate place or two right off the top of their heads, based on clues inherent in concept names. In some cases, this actually does save time. The reliance on name strings is, however, dangerous, because, as we explained in Sections 2.6.2.2 and 7.1.1 above, the names are elements of the ontological metalanguage and have a semantics of their own that is different from the lexical meaning of the English words that they may resemble. Therefore, when this clue is used, the acquirer must carefully read the definition of the concept and scan its properties and values to determine its actual meaning. The more reliable, though slower, procedure involves playing a version of the game of twenty questions—comparing the intended meaning of the candidate concept with concepts at the top of the ontological hierarchy and then descending this hierarchy to find the most appropriate match.


At the very top level of the ontological hierarchy of the CAMBIO/CREST implementation of the ontology (Figure 41), the choice is relatively easy: TEACH is an EVENT. There are three types of


**Figure 41. The top level of the ontology in all the implementations of**
**ontological semantics.**


events (Figure 42). Let us check whether TEACH fits into the mental event branch (Figure 43). Out of all the subclasses of mental event, COMMUNICATIVE-EVENT (Figure 44) seems to be the most suitable. COMMUNICATIVE-EVENT has another parent, SOCIAL-EVENT (Figure 45). A quick check shows that no other children of SOCIAL-EVENT are appropriate to serve as parents of TEACH.


**Figure 42. The top level of the event hierarchy.**


**Figure 43. Some types of mental events.**


**Figure 44. Multiple inheritance of communicative-event and some types**
**of communicative events.**


**Figure 45. Some types of social events.**


We need to check now whether the third child of event, PHYSICAL-EVENT or any of its descendants can also serve as a parent of TEACH. On inspection of the concept names of children of PHYSICALEVENT (see Figure 46), we may wish to check whether LIVING-EVENT has children that could be siblings of TEACH because the semantics of the concept name, _living event_, may suggest that it is appropriate. Inspection (see Figure 47) quickly demonstrates, however, that the name is, in fact, misleading in this case, as the subclasses of LIVING-EVENT do not seem to be appropriate as siblings or parents of TEACH (REAR-OFFSPRING also turns out to be a false lead). At this point, the decision can be safely made: to add TEACH as a child of COMMUNICATIVE-EVENT.


**Figure 46. Some types of physical events.**


**Figure 47. Types of living events**


The next task is to describe its meaning, that is, to check the fillers of the properties it inherits from COMMUNICATIVE-EVENT (60).


(60)

communicative-event agent sem animal theme sem OR event object instrument default OR communication-device natural-language destination sem OR animal social-event effect sem OR event object precondition sem OR event object


TEACH does, indeed, inherit all the above properties. The actual constraints (fillers) for them were shown in Section 7.1.5 above and repeated here partially as (61). Besides the properties in (60),
TEACH has an additional property, HAS-PARTS, which establishes it as a complex event (descriptions of the components of TEACH see also in Section 7.1.5).


(61)


teach is-a value communicative-event agent sem human default teacher theme sem knowledge destination sem human default student precondition default (teach-know-a teach-know-b)
effect default teach-know-c has-parts value (teach-describe
**repeat** (teach-request-info teach-answer)
**until** teach-know-c)


Finding the appropriate fillers, if any, for the various facets of a property is a separate acquisition task. For example, if there is a candidate filler that is strongly implied when no explicit reference to it is present in the input text, it should be listed in the DEFAULT facet of the property. Thus, for the AGENT property of TEACH, the default facet will be filled with TEACHER, because in a sentence like _Math was not taught well in his high school_, the implied AGENT of TEACH is clearly a subset of instances of the concept TEACHER. Of course, one example of this kind does not prove the point, but when combined with the acquirer’s knowledge of the world, it supports a useful rule of thumb. The acquirer also knows that any (adult) human can at times perform the social role of teacher, e.g., parents teaching their teenage children to drive. Therefore, one should expect many inputs in which the constraint on the agent of TEACH is more relaxed than the one in the DEFAULT
facet. This most commonly occurring constraint is recorded in the SEM facet of the property. If an input like the sentence _Gorillas teach their offspring essential survival skills_ can be expected in an application system, the constraint on the AGENT should be further relaxed to ANIMATE on the
RELAXABLE-TO facet (cf. Section 8.2.3). However, any attempt to relax the constraint on this property further, for example, in order to accommodate the sentence _Misfortune taught him a_
_good lesson_ should be denied, because the property AGENT in ontological semantics is constrained to HUMAN or FORCE, and rather than coercing misfortune into FORCE, the meaning of this sentence should be represented, roughly, as that of the sentence _He learned a good lesson as a result of a_
_misfortune_, thus reducing the different sense of _teach_ in this sentence to a metonymic shift on the appropriate sense of _learn_ .


The above procedure of finding the best place to connect a concept into the ontology is not as straightforward as may be deduced from the example. The procedure is predicated on the assumption that the constraints in the ontology become monotonically and progressively stricter as one descends the hierarchy. This was, indeed, the situation with TEACH on every one of the properties inherited. It is legal, however, for constraints in a child to be, in fact, looser than those in an ancestor. In fact, an ancestor may have inheritance on a property completely blocked using the special filler NOTHING, but a child could revert to a contentful filler. This state of affairs makes it dangerous to stop the search for the most appropriate place to include a new concept the moment some constraints become narrower than those expected in this concept. However, in practice, the monotonicity property holds in a much greater majority of cases.


Ontology acquisition may involve not only manipulation of property fillers. Sometimes (preferably, as seldom as possible, though), it is necessary to add a new property to the system. This might be necessary when a concept cannot be described using the extant inventory of properties; this


typically, though not exclusively, happens when describing new subject domains. If indeed new properties must be introduced, it is highly desirable that they contain as many concepts as possible in the domain property of their definition. For example, when extending the Mikrokosmos implementation of ontological semantics to accommodate the subject domain of sports in the CAMBIO/CREST implementation, it became necessary to introduce the literal attribute COMPETITIONSTAGE, whose domain property was filled with SPORTS-RESULT (a central concept for the domain)
and whose range was filled with the useful constants CLASSIFICATION, FINAL, PRELIMINARY,
QUALIFICATION, QUARTER-FINAL, RANKING, REPECHAGE, ROUND-OF-16, ROUND-OF-32, ROUNDOF-64 and SEMI-FINAL. The nature of the application dictates this grain size—we do not need to know any information about the above constants than just their names and the corresponding words or phrases in the languages processed by the system.


## 9.3 Acquisition of Lexicon
Acquisition of lexical knowledge is another crucial component of building natural language processing applications. The requirements for lexical knowledge and the grain size of the specification of lexical meaning also differ across different applications. Some of the applications require only a small amount of information. For example, a lexicon supporting a spelling checker must, at a minimum, only list all the possible word forms in a language. Some other applications require vast quantities of diverse kinds of data. For example, a comprehensive text analysis system may require information about word boundary determination (useful for compounding languages, such as Swedish, where the lexical entries would often match not complete words but parts of compound words); information about inflectional and derivational morphology, syntax, semantics and pragmatics of a lexical unit as well as possible connections among knowledge elements at these levels.


In what follows, we will describe some of the lexical acquisition procedures used over the years in the various implementations of ontological semantics.


### 9.3.1 General Principles of Lexical Semantic Acquisition
The ability to determine the appropriate meaning of a lexical entry or, for that matter, any language unit that has meaning, is something that the native speaker is supposed to possess subconsciously and automatically. However, an ordinary native speaker and even a trained linguist will find it quite difficult to explain what that meaning is exactly and how to derive it. As we showed in Section 6.1 above, it is often hard to separate meaning proper from presuppositions, entailments, and other inferences, often of an abductive or even probabilistic nature. Thus, for a lexical entry such as _marry_ it is easy to let into the lexicon all kinds of information about love, sex, fidelity, common abodes, common property, children, typical sleeping arrangements (double beds), etc. The meaning of the entry, however, includes only a legal procedure, recognized by the society in question, making, typically but not exclusively, one adult man and one adult woman into a family unit. As we discussed in Section 6.7 above, the information supporting inference resides largely in the PRECONDITION and EFFECT properties of EVENTs in the ontology, not in the lexicon.
We are discussing these matters in more detail in the section on semantic heuristics.


Another difficulty in lexical acquisition emerges from the commitment in ontological semantics—in keeping with Hayes’ (1979) admonition to stem the growth of the ratio of vocabulary size


in a metalanguage to that in its object language—to the paucity of the ontological metalanguage.
Numerous difficult decisions must be made on the lexical side—for example, whether to go with a potentially cumbersome representation of a sense within the existing ontology, on the one hand, or to revise the ontology by adding concepts to it, to make the representation easier and, often, more intuitively clear. The additions to ontology and the balance and trade-offs between an ontology and a lexicon have already been discussed (see Sections 9.1-2 above; cf. Mahesh 1996 or
Viegas and Raskin 1998), but if such a choice must be made, ontological semantics would tend to produce complicated entries in the lexicon rather than in the ontology, and to this effect it provides lexicon acquisition with more expressive means and looser metasyntactic restrictions than the ontology. As we demonstrated in Section 7.2 above, entire stories can be “told” in lexical entries using such devices as the various TMR parameters, refsems, and the ability to use more than one ontological concept in the specification of lexical meaning.


### 9.3.2 Paradigmatic Approach to Semantic Acquisition I: “Rapid Propagation”
The principle of complete coverage, to which ontological semantics is committed (see Nirenburg and Raskin 1996), means that every sense of every lexical item should receive a lexical entry, i.e., should be acquired. “Every” in this context means every word or phrase sense in a corpus on which an application is based. There is, however, an alternative interpretation of “every” as in
“every word in the language.” This does not seem very practical or implementable. There is, however, a way to move towards this goal quite rapidly and efficiently. We refer to this approach as
‘rapid propagation’ (see, for instance, Raskin and Nirenburg 1995). The linguistic principle on which it is based can be called ‘paradigmatic,’ or ‘thesaurus-based.’ The procedure for its implementation involves having a “master acquirer” produce a single sample entry for each class of lexemes, such that the remainder of the acquisition work will involve copying the “seed” entry and modifying it, often very slightly. One problem here might be that some of the classes will prove to be relatively small, in some cases of the most frequent and general words, these might be classes of one. However, this observation does not refute the obvious benefit of using a readymade template for speedy and uniform acquisition of items in a class. And some such classes are quite large.


One example of a large lexical class (over 250 members) whose acquisition can be rapidly propagated is that of the English adjectives of size. The meaning of all of these adjectives is described as a range on the size-attribute scale, and many of them differ from each other only in the numerical value of that range, while all the rest of the constraints in the semantic part of their entries remain the same as those in a sample entry, say, that for _big_ (see Example 19 in Section 7.2
above). Thus, the entries for _enormous_ and _tiny_ differ from that for _big_ in this way (as well as by the absence of the RELAXABLE-TO facet):


enormous-adj1
cat adj syn-struc
1 root $var1
cat n mods root $var0
2 root $var0


cat adj subj root $var1
cat n sem-struc
1 2 size-attribute domain value ^$var1
sem physical-object range value                          - 0.9


tiny-adj1
cat adj syn-struc
1 root $var1
cat n mods root $var0
2 root $var0
cat adj subj root $var1
cat n sem-struc
1 2 size-attribute domain value ^$var1
sem physical-object range value < 0.2


A slight variation of the template can be also used to account for many more adjectives. Thus, one sense of _fat_ (see below), as in _fat man_, utilizes, essentially, the same template with a different scale, MASS, substituted for SIZE, and an appropriate SEM facet specified for ^$var1:


fat-adj1
cat adj syn-struc
1 root $var1
cat n mods root $var0
2 root $var0
cat adj subj root $var1
cat n sem-struc


1 2 mass-attribute domain value ^$var1
sem animal range value                          - 0.75
relaxable-to                                   - 0.6


By varying the scales and the classes of modified nouns in the appropriate slots of the SEM-STRUC, as illustrated above, the semantic representations of many other types of adjectival senses based on numerical scales: quantity-related (e.g., _abundant, scarce, plentiful_ ), price-related (e.g., _afford-_
_able, cheap, expensive_ ), human-height-related (e.g., _tall, short, average-height_ ), human-massrelated (e.g., _fat, thin, emaciated, buxom, chubby_ ), container-volume-related (e.g., _capacious,_


_tight, spacious_ ), and others, were produced in the Mikrokosmos implementation of ontological semantics—to the total of 318 adjective senses, all acquired, basically, with one effort at the average rate of 18 entries per hour, including the several hours spent on the formulation and refinement of the template.


Similarly, by taking care of _good_ (see Example 20 in Section 7.2 above), we facilitate the acquisition of all adjectives whose meanings invoke evaluative modality, such as _bad_, _excellent_, _terrible_,
_mediocre_, etc. The creation of yet another versatile template, which is copied for each new adjective of the same class (116 adjective senses in the Mikrokosmos implementation of ontological semantics), has also made it possible to account for such senses as that of _comfortable_, with respect to clothing, furniture, etc., representing their meanings as ‘good for wearing’ or ‘good for sitting’:


comfortable-adj1
cat adj syn-struc
1 root $var1
cat n mods root $var0
2 root $var0
cat adj subj root $var1
cat n sem-struc


1 2 ^$var1 sem OR clothing furniture modality type evaluative value value                           - 0.75
relaxable-to                                    - 0.6
scope ^$var1
attributed-to _speaker_


An additional advantage of this approach is that it can use synonymy, antonymy, and other paradigmatic relations among words to generate lists of entries that can be acquired on the basis of a single lexical entry template. Availability of thesauri and similar online resources facilitates this method of acquisition. It also facilitates the acquisition of entries across languages. The single word senses acquired the way demonstrated for the adjectives above were all reused, without any semantic changes, in the Spanish lexicon and those for other languages. This, in fact, was an empirical corroboration of the principle of practical effability discussed in Section 9.3.6 below:
each of the English word senses was found to have an equivalent sense expressed in another language; what varies from language to language is, essentially, how these single senses will be grouped in a superentry. This capability underscores the rather high level of portability of ontological semantics across languages and applications.


### 9.3.3 Paradigmatic Approach to Lexical Acquisition II: Lexical Rules
The other paradigmatic approach to lexical acquisition finds economies in automatic propagation of lexicon entries on the basis of systematic relationships between classes of lexical entries, e.g.,


between verbs, such as _abhor_ (62), and corresponding deverbal adjectives (63), such as _abhor-_
_rent_ . Lexical rules came into fashion in computational lexical semantics in the early 1990s (see
Section 4.1 above). Ontological semantics uses the facility of lexical rules for actual massive lexical acquisition, always paying attention to the relative effort expended in formulating the rule versus that needed for specifying lexical entries for a class of words manually (see Viegas _et al_ .
1996b, Raskin and Nirenburg 1999). As a result, fewer lexical rules are proposed and those that are, generate numerous entries.


(62)

abhor-v1
cat v syn-struc root abhor obj root $var1
cat n sem-struc modality type evaluative value < 0.1
scope ^$var1
attributed-to _speaker_


(63)

abhorrent-adj1
cat adj syn-struc
1 root $var1
cat n mods root abhorrent
2 root abhorrent cat adj subj root $var1
cat n sem-struc modality type evaluative value < 0.1
scope ^$var1
attributed-to _speaker_


The lexical entry for _abhorrent_ is generated from that for _abhor_ using the following lexical rule:


LR-v-adj-1
lhs syn-struc root $var0
obj root $var1
cat n sem-struc modality type evaluative value < 0.1
scope ^$var1
attributed-to _speaker_
rhs syn-struc
1 root $var1
cat n mods root adj($var0)
2 root adj($var0)
cat adj subj root $var1
cat n sem-struc modality type evaluative value < 0.1
scope ^$var1
attributed-to _speaker_


Lexical rules overtly put in correspondence two types of lexical entry: that for the source entry and that for the target one. The binding of variables scopes over the entire rule, both its left-hand side (lhs) and the right-hand side (rhs). The above rule establishes that the semantics of _abhor_ and
_abhorrent_ is identical (this is not always the case; see the example of _criticize/critical_ below) but that the syntactic dependency changes from the verb to the adjective, as the direct object of the former becomes the head that the adjective modifies. The expression _adj($var0)_ stands for the adjective whose entry is generated by the rule. In the lexicon entry for the verb, an additional zone, LR, will be created, in which each lexical rule applicable to this verb is listed with the string that is the lexeme of the target entry. A practical consideration for the economy of acquisition effort is whether it is preferable to populate the LR zone of a lexical entry or immediately create the target entry or entries.


criticize-v1
cat v syn-struc root criticize subj root $var1
cat n obj root $var2
cat n sem-struc criticize agent value ^$var1
sem human


theme value ^$var2
theme OR event object modality type evaluative value < 0.5
scope ^$var2
attributed-to _speaker_


critical-adj2
cat adj syn-struc
1 root critical cat adj oblique root of cat prep obj root $var1
cat n
2 root critical cat adj oblique root of cat prep xcomp root $var1
cat v sem-struc modality type evaluative value < 0.5
scope ^$var1
attributed-to _speaker_


The lexical rule for the above pair differs from _LR-v-adj-1_ in several respects. The semantics of the verb includes a reference to an ontological concept with some of its properties listed. One of these properties, THEME, plays a central role in the relationship between the meaning of the verb and that of the adjective derived from it: the scope of the modality in the meaning of the adjective is the filler of the THEME property.


Note that the entry for _critical_ has a different content of the SYN-STRUC zone compared to that of
_abhorrent_ or other standard adjectives. The lexical rule, thus, will connect lexical elements similar to those in the examples with _criticize/critical_ : _John criticized the film / John was critical of the_
_film_ (corresponding to the first SYN-STRUC variant) or _Lucy criticized China’s handling of the spy_
_plane crisis / Lucy was critical of China’s handling of the spy plane crisis_ (corresponding to the second syn-struc variant).


LR-v-adj-2
lhs syn-struc root $var0
obj root $var2
cat n sem-struc
^$var0


theme value ^$var2
modality type evaluative value < 0.5
scope ^$var2
attributed-to _speaker_


rhs syn-struc
1 root adj($var0)
cat adj oblique root of cat prep obj root $var2
cat n
2 root adj($var0)
cat adj oblique root of cat prep xcomp root $var2
cat v sem-struc modality type evaluative value < 0.5
scope ^$var1
attributed-to _speaker_


The role played by THEME in the above rule will be assumed by other properties (typically, case roles) in other rules.
Thus, for the pair _abuse/abusive_, the adjective in _abusive behavior_ modifies the EVENT itself and in _abusive parent_, the AGENT of the EVENT. This means that the LR zone in the entry for _abuse_ will contain a reference to two different lexical rules for the production of the corresponding adjective entries. An alternative approach to specifying the format of the lexical rules would have been to try to formulate all the verb-adjective lexical rules as a single rule, with disjunctions in the text of the rule. It would have afforded some people the pleasure of making formal generalizations at the expense of clarity.


### 9.3.4 Steps in Lexical Acquisition
The steps in lexical acquisition may be presented as follows:

- **polysemy reduction** : decide how many senses for every word must be included into a lexicon entry: read the definitions of every word sense in a dictionary and try to merge as many senses as possible, so that a minimum number of senses remains;

- **syntactic description** : describe the syntax of every sense of the word;

- **ontological matching** : describe the semantics of every word sense by mapping it into an ontological concept, a property, a parameter value or any combination thereof;

- **adjusting lexical constraints** : constrain the properties of the concept property or parameter, if necessary;

- **linking** : link syntactic and semantic properties of a word sense.


### 9.3.5 Polysemy Reduction
We have basically two resources for capturing meaning, and their status is quite different: one of them, the speaker’s intuition, works very well for humans but not at all for machines (it is difficult


to represent it explicitly); the other, the set of human-oriented published dictionaries, represents meaning explicitly but is known to be faulty and unreliable and, moreover, does not contain sufficient amounts of information to allow automatic capturing of word meaning from them (e.g.,
Wilks _et al._ 1990, 1996, Guo 1995). From the point of view of computational applications, dictionaries also typically list too many different senses. In a computational lexicon that recognizes the same number of senses, it would be very difficult formally to specify how each of them differs from the others, and the human-oriented dictionaries do not always provide this information.
Thus, in a computational application, it becomes important to reduce the number of senses to a manageable set.


In his critique of Katz and Fodor (1963), Weinreich (1966) accused them of having no criteria for limiting polysemy, i.e., for determining when a sense should no longer be subdivided. Thus, having determined that one of the senses of _eat_ is ‘ingest by mouth,’ should we subdivide this sense of _eat_ into eating with a spoon and eating with a fork, which are rather different operations? Existing human-oriented dictionaries still do not have theoretically sound criteria for limiting polysemy of the sort Weinreich talked about. It might be simply not possible to formulate such criteria at any but the coarsest levels of accuracy. Dictionary compilers operate with their own implicit rules of thumb and under strict editorial constraints on overall size, but still the entries of a dictionary vary in grain size of description. And, again, the number of senses listed for each entry is usually quite high for the purposes of computational applications—after all, the more senses in an entry, the more complex the procedure for their disambiguation.


It is often difficult to reduce the number of senses for a word even in a computationally-informed lexical resource, as can be illustrated by an example from WordNet, a popular online lexical resource (Miller _et al_ . 1988; Fellbaum 1998). In WordNet, each sense in an entry is determined by a ‘synset,’ a set of synonyms, rather than by a verbal definition. The list below contains the 12
synsets WordNet lists for the adjective _good_ :


Sense 1: good (vs. evil) -- (morally admirable)
=> angelic, angelical, saintly, sainted -- (resembling an angel or saint in goodness)
=> beneficent, benevolent, gracious -- (doing or producing good)
=> white -- (“white magic”)


Also See-> good, moral, right, righteous, virtuous, worthy


Sense 2: good (vs. bad) -- (having positive qualities, asp. those desirable in a thing specified: “good news”; “a good report card”; “a good joke”; “a good exterior paint”; “a good secretary”)
=> bang-up, bully, cool, corking, cracking, dandy, great, keen, neat, nifty, not bad(predicate), peachy, swell, smashing -- ((informal) very good)
=> fine -- (very good of its kind or for its purpose: “a fine gentleman”; “a fine mind”; “a fine speech”; “a fine day”)
=> redeeming(prenominal), saving(prenominal) -- (offsetting some fault or defect: “redeeming feature”;
“saving grace”)
=> safe, sound -- (“a good investment”)
=> satisfactory -- (meeting requirements: “good qualifications for the job”)
=> suitable -- (serving the desired purpose: “Is this a good dress for the office?”)
=> unspoiled -- (“the meat is still good”)
=> well-behaved -- (“when she was good she was very good”)


Also See-> best, better, favorable, genuine, good, obedient, respectable, sound, well(predicate)


Sense 3: benevolent (vs. malevolent), good -- (having, showing, or arising from a desire to promote the welfare or happiness of others)
=> beneficent, charitable, generous, kind -- (“a benevolent contributor”)
=> good-hearted, kindly, openhearted -- (“a benevolent smile”; “take a kindly interest”)


Also See-> beneficent, benefic, charitable, kind


Sense 4: good, upright, virtuous -- (of moral excellence: “a genuinely good person”; “an upright and respectable man”; “the life of the nation is secure only while the nation is honest, truthful, and virtuous”- Frederick Douglass;
“the...prayer of a righteous man availeth much”- James 5:16)
=> righteous (vs. unrighteous)


Sense 5: estimable, good, honorable, respectable -- (“all reputable companies give guarantees”; “ruined the family's good name”)
=> reputable (vs. disreputable)


Sense 6: good, right, seasonable, timely, well-timed -- (occurring at a fitting time: “opportune moment”; “a good time to plant tomatoes”; “the right time to act”; “seasonable summer storms”; “timely warning”; “the book's publication was well-timed”)
=> opportune (vs. inopportune)


Sense 7: good, pleasing -- (agreeable or pleasant: “we had a nice time”; “a nice day”; “nice manners”)
=> nice (vs. nasty)


Sense 8: good, intact -- (not impaired in any way: “I still have one good leg”)
=> unimpaired (vs. impaired) -- (not damaged or diminished)


Sense 9: good -- (not forged: “a good dollar bill”)
=> genuine (vs. counterfeit)


Sense 10: good -- (“good taste”)
=> discriminating (vs. undiscriminating)


Sense 11: good, Sunday, Sunday-go-to-meeting(prenominal) -- (used of clothing: “my good clothes”; “his best suit”;
“her Sunday-go-to-meeting clothes”)
=> best (vs. worst) -- (superlative of “good”: “the best film of the year”)


Sense 12: full, good -- (“gives full (good) measure”; “a good mile from here”)
=> ample (vs. meager) -- (more than enough in size or scope or capacity)


The first thing one notices about the 12 senses is that the noun classes which they modify vary a great deal in size. Sense 2 dwarfs all the other senses in this respect. Senses 1 and 3-5 all pertain to humans and their actions and are very similar to each other: the association of one of these senses with a noun strongly entails or presupposes the association of the others with the same noun. The meaning of _good_ in the examples below can be in any of the WordNet senses 1 or 3-5, as it seems difficult for speakers to tell them apart:


Fred is a good man.
Fred’s behavior in that difficult situation was very good.
Mom & Pop, Inc. is a good company


This intuition is the basis for a procedure that Weinreich sought for determining the required levels of polysemy. A group of individuals, if defined as _good_, is indeed more likely to be understood in WordNet Sense 5, but none of the other three can be excluded either. In fact, other than in the context of at least several sentences, if not paragraphs, it is very hard to use _good_ specifically in one of these similar senses and not simultaneously in the others. This observation can serve as an operational criterion for limiting polysemy: if it is hard to pinpoint a sense within a one-sentence example, the status of the meaning as a separate sense in the lexical entry should be questioned.
One cannot understand that the sense of _good_ in _Fred is a good man_ signifies ‘of good moral character’ unless the text also says something like _he lives by the Bible_ .


One observes that if there are different shades of meaning in the above examples, they are due not the meaning of _good_ as such but rather to the differences in the meanings of the noun it modifies, for instance, when the latter is not an individual but a group. The influence of the syntactic head on the meaning of _good_ is even more obvious in the other WordNet senses for the adjective. Starting with Sense 6, the noun classes to which these senses apply shrink in size, and with Senses 812 come dangerously close to phrasals consisting of _good_ and the corresponding nouns. That these senses are listed at all is probably because, in these near-phrasals, the meaning of _good_ varies significantly. In ontological semantics, such a situation—when the classes of phenomena are very narrow—always calls for treatment of a construction as a separate phrasal lexical entry instead of adding more small senses to those already existing for the components of the construction.


WordNet itself recognizes some of the observations above by reducing, in one version of the resource, the 12 senses of _good_ to the following three senses in response to a different set of parameter settings:


Sense 1: good (vs. evil) -- (morally admirable)
=> good, virtue, goodness -- (the quality of being morally excellent or admirable)


Sense 2: good (vs. bad) -- (having positive qualities, esp. those desirable in a thing specified: “good news”; “a good report card”; “a good joke”; “a good exterior paint”; “a good secretary”)
=> goodness -- (being of positive value)


Sense 3: benevolent (vs. malevolent), good -- (having, showing, or arising from a desire to promote the welfare or happiness of others)
=> benevolence -- (an inclination to do kind or charitable acts)


This “short list” of the main senses of _good_ is still rather unbalanced with respect to the size of noun classes they modify, and the distinction between Senses 1 and 3 remains perhaps only slightly less problematic than the distinction among Senses 1 and 3-5 of the longer list. It is the long WordNet list rather than the short one that is closer to typical dictionary fare: compare the entries for _good_ from the online Webster’s (1963) and the American Heritage Dictionary
(1992)—we list only meaning-related information from each entry.


(Webster’s)


1. good...
1a1: of a favorable character or tendency {~ news}


1a2: BOUNTIFUL, FERTILE {~ land}
1a3: COMELY, ATTRACTIVE {~ looks}
1b1: SUITABLE, FIT {~ to eat}
1b2: SOUND, WHOLE {one ~ arm}
1b3: not depreciated {bad money drives out ~}
1b4: commercially reliable {~ risk}
1b5: certain to last or live {~ for another year}
1b6: certain to pay or contribute {~ for a hundred dollars}
1b7: certain to elicit a specified result {always ~ for a laugh}
1c1: AGREEABLE, PLEASANT
1c2: SALUTARY, WHOLESOME {~ for a cold}
1d1: CONSIDERABLE, AMPLE {~ margin}
1d2: FULL {~ measure}
1e1: WELL-FOUNDED, COGENT {~ reasons}
1e2: TRUE {holds ~ for society at large}
1e3: ACTUALIZED, REAL {made ~ his promises}
1e4: RECOGNIZED, HONORED {in ~ standing}
1e5: legally valid or effectual {~ title}
1f1: ADEQUATE, SATISFACTORY {~ care}
1f2: conforming to a standard {~ English}
1f3: DISCRIMINATING, CHOICE {~ taste}
1f4: containing less fat and being less tender than higher grades - used of meat and esp. of beef
2a1: COMMENDIBLE (sic!), VIRTUOUS, JUST {~ man}
2a2: RIGHT {~ conduct}
2a3: KIND, BENEVOLENT {~ intentions}
2b: UPPER-CLASS {~ family}
2c: COMPETENT, SKILLFUL {~ doctor}
2d: LOYAL {~ party man} {~ Catholic}: in effect: VIRTUALLY {as good as dead}: VERY, ENTIRELY {was good and mad}


(American Heritage)


good


1. Being positive or desirable in nature; not bad or poor: a good experience; good news from the hospital.
2.a. Having the qualities that are desirable or distinguishing in a particular thing: a good exterior paint; a good joke. b.
Serving the desired purpose or end; suitable: Is this a good dress for the party?
3.a. Not spoiled or ruined: The milk is still good. b. In excellent condition; sound: a good tooth.
4.a. Superior to the average; satisfactory: a good student. b. Used formerly to refer to the U.S. Government grade of meat higher than standard and lower than choice.
5.a. Of high quality: good books. b. Discriminating: good taste.
6. Worthy of respect; honorable: ruined the family's good name.
7. Attractive; handsome: good looks.
8. Beneficial to health; salutary: a good night's rest.
9. Competent; skilled: a good machinist.
10. Complete; thorough: a good workout.
11.a. Reliable; sure: a good investment. b. Valid or true: a good reason. c. Genuine; real: a good dollar bill.
12.a. In effect; operative: a warranty good for two years; a driver's license that is still good. b. Able to continue in a specified activity: I'm good for another round of golf.
13.a. Able to pay or contribute: Is she good for the money that you lent her? b. Able to elicit a specified reaction: He is always good for a laugh.
14.a. Ample; substantial: a good income. b. Bountiful: a good table.
15. Full: It is a good mile from here.


16.a. Pleasant; enjoyable: had a good time at the party. b. Propitious; favorable: good weather; a good omen.
17.a. Of moral excellence; upright: a good person. b. Benevolent; kind: a good soul; a good heart. c. Loyal; staunch: a good Republican.
18.a. Well-behaved; obedient: a good child. b. Socially correct; proper: good manners.
19. Sports. Having landed within bounds or within a particular area of a court: The first serve was wide, but the second was good.
20. Used to form exclamatory phrases expressing surprise or dismay: Good heavens! Good grief!


Ontological semantics promulgates both content- and computation-related guidelines for justifying the inclusion of a word sense for a lexeme. From the point of view of content, we are solidly with Weinreich in his concern about unlimited polysemy that would make any semantic theory indefensible and the semantic description determined by such a theory infeasible. Disambiguation at runtime will be greatly facilitated by the small number of senses for a lexeme. We cannot make a symmetrical claim that a small number of senses is easier to acquire, because the task of “bunching” senses is not simple. Thus, the guidelines for adding another sense to an adjective lexeme in ontological semantics are:


- that the candidate sense be clearly distinct from those already in the entry, and

- that set of nouns that the adjective in this sense can modify not be small.


The first of these guidelines calls for a significant difference in the properties and their fillers in the SEM-STRUC zone of the lexical entries. This guideline applies equally to all types of lexemes.
The second guideline, to be applicable to the other types of lexemes, should watch for dependency of a candidate sense on the meanings of its syntactic arguments. It would be unwise, for instance, to say that _join_ in _join the Army_ and _join the country club_ belong to different senses, on the tenuous ground that the former event involves relocation, while the latter does not. In other words, whatever difference in the shade of meaning exists, it depends on the meaning of the direct object of _join_ rather than on the meaning of the verb itself.


The rules of thumb to be used by lexicon acquirers for reducing polysemy can then be summarized as follows:


- check whether the candidate sense requires further disambiguation if used in a short text example; if you need to provide additional context to recognize what sense is used, this sense should be rejected and subsumed by one of the existing senses in the entry;

- check whether there is a property of the candidate sense that can be filled only with a member of a small set of fillers; if so, reject this sense: its meaning will be either subsumed by one of the existing senses in the entry or will become a part of the meaning of a phrasal.


With respect to the first of the above rules, if, as we showed above, _he is good_ cannot, without further detail, be understood in the moral sense without additional lexical material, or _he likes to join_
cannot be understood exclusively in the sense of involving relocation; the argument that _I went to_
_the bank_ cannot be disambiguated without further detail between the topographic and the repository senses is not relevant here because both senses of _bank_ are present in the example. In other words, we accept the views of Firth (1957) and Zvegincev (1968) that words, as a matter of rule, change their meanings when appearing in collocation with other words. What we do not do is declare that each such shade of meaning warrants a separate sense in a lexicon. On this issue,


ontological semantics differs from human-oriented lexicography, as exemplified above by WordNet and the two MRDs. In ontological semantics, the shades of lexical meaning yielding unique interpretations of collocations are reflected in the equally unique combinations of properties and their values in the results of the semantic analysis of text, namely, in TMRs. There is no doubting
Firth’s claim that the meaning of _dark,_ for instance, in _dark ale_ is different from that in _dark coat_, and this is how that difference is reflected in the corresponding portions of the TMRs for inputs in which these expression may occur (64). The relevant parts of the lexicon entries for _ale, coat_ and
_dark_ are as follows:


(64)

ale-n1
...
beer
...
color value OR yellow pale-yellow reddish-brown black dark-brown
...


coat-n1
...
coat
...
color value OR white yellow red green blue navy-blue dark-grey black dark-brown
...
...


dark-adj1
...
1 ^$var1
color value OR black navy-blue dark-grey dark-brown brown dark-green
...
2 ...
...


The following are fragments of the TMR for _dark ale_ and _dark coat_ .


beer
...
color value OR black dark-brown
...


coat
...
color value OR black navy-blue dark-brown dark-green dark-grey
...


The above clearly shows the difference in the meaning of _dark_ in the two collocations: while both senses of _dark_ have the effect of restricting the choice of fillers for the COLOR property, the resulting ranges are different. There is no need to add senses to the superentry for _dark_ in the lexicon to reflect this difference.


The above is a manifestation of a general linguistic principle of complementary distribution, or commutation, widely used for establishing variance and invariance of entities in phonology and


morphology: if two different senses of the same word can only be realized when used in collocation with different words, they should be seen as variants of the same sense. In a way, some dictionaries try to capture this in their entries by grouping all senses into a small number of “main” ones which are further divided, often recursively. Thus, as shown above, Webster’s has only two main senses for _good_ and two levels of specification under them, but American Heritage prefers putting
20 senses at the top level, with minimum further subdivision. Both from the point of view of theoretical linguistics and of natural language processing, entries like that in American Heritage are the least helpful.


The objections to the entry in American Heritage push us in an obvious direction: we see _good_ as having one sense, which takes different shades, depending on the meaning of the modified nouns.
This sense of _good_ is something like ‘assigning a high positive value range’ to a selected property of the noun. Our entry for _good_ (20) captures this meaning but refuses to specify the noun property, and we have a good reason for doing that. _Good_ is, of course, an adjective with a very broadly applicable meaning, but the same objections to excessive polysemy hold for other adjectives as well. The same principle of polysemy reduction pertains to other lexical categories: thus, in Nirenburg _et al_ (1995), we reduced 52 listed senses for the Spanish verb _dejar_ to a manageable set of just 7.


### 9.3.6 Grain Size and Practical Effability
Reducing the number of senses in a polysemous lexical item affects the grain size of its semantic representation: the fewer the number, the larger the grain size. It would be beneficial for ontological semantics, both in acquisition and in processing, to keep the number of entries in a superentry as low as possible. Particular applications, however, may dictate a finer grain size for some superentries. Thus, the corporate sense of _acquire,_ repeated here as (65), differs from the general sense of _acquire_ only in the meaning of the filler for the THEME property of BUY, namely, ORGANIZATION as opposed to OBJECT. According to the principle of reducing polysemy in the lexicon, this sense of _acquire_ should not have been defined as a separate entry. The reason it was defined in the
Mikrokosmos implementation of ontological semantics is that the implementation supported the application of processing texts about mergers and acquisitions, where this special sense of _acquire_
was very prominent. Similarly, in the CAMBIO/CREST lexicon, the sports and the currency exchange domains were represented in much greater detail than in the Mikrokosmos lexicon.


(65)

acquire-v2
cat v


anno def “when company A buys company, division, subsidiary, etc. of company
T from the latter”
ex “Alpha Inc acquired from Gamma Inc the latter’s candle division”


syn-struc root acquire subj root $var1
cat n obj root $var2
cat n


oblique root from cat prep opt +
obj root $var3
cat n

sem-struc buy agent value ^$var1
sem corporation theme value ^$var2
sem organization source value ^$var3
sem corporation


In the application of ontological semantics to machine translation, such as Mikrokosmos, meaning analysis and text generation at a certain grain size presuppose lexicons for the source and target languages which represent enough different word and phrase senses to give serious credence to a hope that a meaning expressed in one language will be largely expressible in another language, and at the same grain size. There are, however, cases when this presupposition will fail, and it is those cases that require a finer grain size of semantic analysis than the others. As a result, ontological semantics has variable grain-size meaning descriptions in its various implementations
(see Nirenburg and Raskin 1986 for an early discussion of variable depth semantics).


One such case would be a situation when one word in a source language can be translated into a target language as either one of two words, and the decision as to which word to use requires additional information that the source text may not contain at all or at least not in an easily extractable way. For example, the English _corner_ can be rendered in Spanish as either _rincón_ ‘(inside) corner, nook’ or as _esquina_ ‘(outside) corner, street corner’; the English _blue_ can be rendered in Russian as either _siniy_ ‘dark blue, navy blue’ or _goluboy_ ‘light blue, baby (sky) blue’. As a result, it is difficult to translate the sentences: _He could see the corner clearly_ and _She wore a blue dress_ into
Spanish and Russian, respectively.


Refining the grain size for _corner_ and _blue_ in their lexical entries—by adding to their lexicon definitions appropriate distinguishing properties in order to accommodate Spanish and Russian—is possible, though often practically useless. This is because the data on which lexical constraints can be checked may not be present in either the text or extralinguistic context. The decision to maintain a grain size of certain coarseness will result in failing many of such cross-language mismatches when no additional lexical clues are available to help disambiguation. Such situations are notably also difficult for human translators, who often have to resort to guesses or arbitrary rules or conventions, such as the common practice of using a form of _goluboy_ to translate _blue dress_
when worn in the daytime and a form of _siniy_ otherwise. The lack of specificity in language is a normal state of affairs because language always underdetermines reality (cf. Barwise and Perry
1983: 30): any sentence leaves out numerous details of the situation described in it, and in the case of the above examples, English underdetermines it more than Spanish or Russian.


In general, when one considers the entire gamut of applications requiring treatment of meaning, it becomes clear that no preset level of detail, or grain size in semantic description will be fail proof.


In fact, it is not reasonable even to pursue setting _a priori_ grain size as an R&D goal. What is essential is to anticipate what information an application will require and be able to utilize and adjust the grain size of description accordingly, while fully realizing that there is much more that can be said that, occasionally, the system may require more information than is available. For example, in the CAMBIO/CREST implementation of ontological semantics, the grain size of describing the sports domain is certainly not even the finest that the knowledge acquisition procedure could manage on the basis of the available inputs. It is rather coarser than that of sports page reports, especially box scores, in a newspaper. Thus, the CAMBIO/CREST Fact DB contains information on goal scorers in soccer but not the individual statistics of, say, players on a basketball team. So, the system, as it stands, will not be able to answer directly the question who scored the most points in the Lithuania - USA basketball game at the Sydney Olympics. The best the system would be able to do is to refer the questioner to an online report about the game.


Whether or not it was reasonable to have established the cut-off in acquisition at that particular level, it is important to understand that some such cut-off will be necessary in any application, no matter how fine the grain size of description actually is. There can always be an expectation of a question that refers to a data item that was not recorded in the static knowledge sources of an ontological semantic application. What makes systems with natural language input and output, such as MT, different is that, apparently, the linguistic universals make all natural languages in some sense “self-regulating” in maintaining roughly similar levels of grain size in deciding on what becomes a lexeme, and this is reflected in the principle of effability.


We use the principle of effability, or mutual intertranslatability of natural languages, in Katz’s
(1978: 209) formulation: “[e]ach proposition can be expressed by some sentence in any natural language” (see also Katz 1972/1974: 18-24, Frege 1963: 1, Tarski 1956: 19-21, and Searle 1969:
19-21). This is, of course, a view which is opposite to that famously formulated by Quine (1960:
26-30) in his _gavagai_ discourse. In our work, we have to assume a stronger form of this principle.
The generic formulation of this stronger form, expressed in the terms of the philosophical debate on effability, is as follows:


_Hypothesis of Practical Effability_ : Each sentence can be translated into another natural language on the basis of a lexicon compiled at the same level of granularity, which is made manifest by the roughly comparable ratio of entries per superentry.


A version more attuned to the environment of computational applications can be formulated as follows:


_Hypothesis of Practical Effability for Computational Applications_ : Any text in the source language can be translated into the target language in an acceptable way on the basis of a lexicon for the source language and a lexicon for the target language with a comparable ratio of entries per superentry.


We have consistently been able to use fewer than 10, very often, fewer than 5 senses per lexeme.
The limitation does not, of course, affect the scope of the word meaning: all the possible senses of a lexical item are captured in the superentry. The small number of these senses simply means a larger grain size. In a limited domain, however, some senses of the same word can be ignored


because they denote concepts that are not used in the domain, are not part of the sublanguage that serves the domain, and thus are unlikely to occur in the corresponding corpora (see Nirenburg and
Raskin 1987b; Raskin 1971, 1987a,b, 1990).


The practical effability hypothesis was successfully tested on a corpus of English with 1,506
adjective senses. Let us see how exactly it is reflected in the choices forming the lexical entries.
The adjective _good_ is, again, a good example. We will show how, for this adjective, we settled on a grain size of description coarser than the most detailed semantic analysis possible. We will then see how the principle of not specifying in detail the specific noun property modified by an adjective applies to all the other adjectives as well. And we will briefly discuss the conceptual and computational status of those properties which are introduced by the scales we need to postulate for adjective entries.


We interpret _good_ in a sentence like _This is a good book_ as, essentially, _The speaker evaluates this_
_book highly_ . We realize that in this sentence _good_ may have a large variety of senses, some of which are illustrated in the possible continuations of the sentence (cf. Example (23) in Section
7.2):


- ...because it is very informative.

- ...because it is very entertaining.

- ...because the style is great.

- ...because it looks great on the coffee table.

- ...because it is made very sturdy and will last for centuries.


In each case, _good_ selects a property of a noun and assigns it a high value on the evaluation scale associated with that property. The property changes not only from noun to noun but also within the same noun, depending on the context. The finest grain-size analysis requires that a certain property of the modified noun is contextually selected as the one on which the meaning of the noun and that of the adjective is connected. This is what many psychologists call a “salient” property. In our approach, the representation solution for _good_ would be to introduce an evaluation modality, with a high value and scoped over this property.


Now, it is difficult to identify salient properties formally, as is well known, for instance, in the scholarship on metaphor, where salience is the determining factor for the similarity dimension on which metaphors, and similes, are based (see, for instance, Black 1954-55, 1979; Davidson 1978;
Lakoff and Johnson 1980, Lakoff 1987; Searle 1979; on salience, specifically, see Tversky and
Kahnemann 1983). It is, therefore, wise to avoid having to search for the salient property, and the hypothesis of practical effability offers a justification for this. What this means, in plainer terms, is that if we treat the meaning of _good_ as unspecified with regard to the nominal property it modifies, there is a solid chance that there will be an adjective with a matching generalized, unspecified meaning like that in the target language as well.


In fact, however, we go one step further with the lexical entry of _good_ and other adjectives from the same scale and remove their meaning from the nouns they modify, making them contribute instead to an evaluative modality pertaining to the whole sentence. It can be argued, of course, that since the scope of the modality remains the modified noun, all that changes is the formalism


and not the essence of the matter. We do not wish to insist, therefore, that this additional step constitutes a step towards an even larger grain size.


Non-modality-based scalars are treated in a standard fashion: their lexicon entries effectively execute the following, informally defined, procedure: insert the scale name and scale value for an adjective as a property-value pair in the frame describing the meaning of the noun the adjective modifies.


If _house,_ in one of its senses, has the following lexicon entry:


house-n2
cat n syn-struc root house cat n sem-struc private-home


then the meanings of the phrases _big house_ and _red house_ will be represented in TMRs as follows:


private-home
...


size-attribute value      - 0.75
...


private-home
...


color-attribute value red
...


In the former example, the attribute is selected rather high in the hierarchy of attributes— in the ontology SIZE-ATTRIBUTE is the parent of such properties as LENGTH-ATTRIBUTE, WIDTHATTRIBUTE, AREA-ATTRIBUTE, WEIGHT-ATTRIBUTE, etc. If the context does not allow the analyzer to select one of those, a coarser-grain solution is preferred. In other words, we represent the meaning of _big house_ without specifying whether _big_ pertains to the length, width, height or area of a house. Such decisions, affecting all types of lexemes not only adjectives, are made throughout the ontological lexicon acquisition.


### 9.3.7 Ontological Matching and Lexical Constraints
Leaving the syntactic description step in lexical acquisition to the end, in order to discuss it together with linking, we will focus here on the basic question, What does this word mean? In some sense, this is the most important question in lexical acquisition. It is remarkable, therefore, how relatively little is written in semantic literature about it. Most authors prefer to discuss details of representation formalisms for meaning specifications, with no apparent interest in showing how one arrives at the content of meaning specification that is stated in examples. This is true


with respect not only to lexical semantics but also to compositional semantics, the study of deriving meaning representations of texts on the basis, among other factors, of lexical meaning (see
Sections 3.5.2-3 and 3.7 above).


In this section, we discuss, then, two related but distinct issues in lexical acquisition, namely, how a lexicon acquirer can discover what a lexeme means and how the choice is made of the way to represent this meaning. The commitment to using the ontology in lexical meaning specification helps to determine the actual representation of a lexical entry but it does not make it a deterministic process: there are further choices to make that require a theoretical underpinning. These choices form the basis of a procedure that a human acquirer follows for lexical acquisition. The first step of this procedure, polysemy reduction, was discussed in Section 9.3.5 above. The following steps relate to determination of meaning of one particular sense of a lexeme.


The next step is checking whether the meaning of a word can be fully, or almost fully, reduced to that of another. We showed above that a word may be a member of a class, such as that of adjectives of size, for which a single meaning template can be used. That was grouping by meaning.
Orthogonally, the acquirer must check whether a morphological cognate of the word being acquired is already in the lexicon, to establish whether the new meaning can be derived from that of the cognate, either with the help of a lexical rule (see Section 9.3.3 above) or directly. Thus, the acquirer will correctly determine that the meaning of the adjective _abhorrent_ (63) is the same as that of the verb _abhor_ (62).


If the candidate sense does not belong to a semantic class some of whose members have already been given lexical descriptions or when there are no useful morphological cognates with lexicon entries, a new lexicon entry must be created from scratch. In that case, the next step must be to determine whether there is an element in the ontology or the TMR specification that should be used in the representation of the entry being acquired. We describe at length elsewhere (see Section 7.2 above) what factors determine the decision to relate a lexical entry directly to an existing ontological concept or property in another concept or to describe it in parametric terms. Here we will focus on the former option, that is, finding a suitable ontological concept.


Remember that at this stage, the acquirer already has the name of the lexical entry and its lexicographical definition, borrowed and/or adapted from an MRD or another source. Looking for the most appropriate ontological concept, the acquirer attempts to match the name and/or the most information-laden, in his opinion, part(s) of the word meaning definition with a concept name or the fillers of the DEFINITION property. Let us consider the word _shirt_ in its ‘garment’ sense. The
CAMBIO/CREST implementation of ontological semantics supplies a tool to support this search
(Figure 48).


**Figure 48. The main window of the search tool in the CAMBIO/CREST**
**implementation of ontological semantics.**


The search will, actually, yield two concepts, SHIRT and SHIRT-NUMBER (Figure 49), because, as shown in Figure (48), the search mode was ‘prefix’ and asked, thus, for all concept names that begin with the string ‘shirt.” While the ‘exact’ search mode would yield only SHIRT, the consideration that the name of the concept may not exactly match the word, might make the ‘prefix’ or even ‘substring’ modes of the search preferable. The definition of the concept SHIRT will correspond to the acquirer’s lexicographic definition and the SEM-STRUC zone of the entry will contain only a reference to an instance of the concept SHIRT, with no further constraints. This is, of course, the simplest case.


**Figure 49. A sample screen from the acquisition and browsing tool from the CAMBIO/**
**CREST implementation of ontological semantics. The concept SHIRT.**


What if the concept SHIRT had not been found? The next option is to use the search tool to look for a string in definitions of ontological concepts. It is reasonable to suppose that the word _garment_ is used in the lexicographic definitions available to the acquirer, and the acquirer will choose this as a search string (Figure 50). The search yields all the concepts that contain _garment_ in their defini


tions (Figure 51).


**Figure 50. Search on** _**garment**_ **in the definition field.**


If, counterfactually, SHIRT were not among them, the acquirer would look these concepts up and check whether they are appropriate siblings for the meaning of _shirt_ . If this is so, which would be the case, the next step would be to add SHIRT as a sibling and make it the meaning of _shirt_ . To determine their common parent, the acquirer will click on any sibling, and discover that it is
CLOTHING-ARTIFACT (Figure 52). The new ontological concept SHIRT will, then become a child of the latter.


**Figure 51. Results of the search on** _**garment**_ **in the definition field.**


**Figure 52. The concept dress in the CAMBIO/CREST browser.**


If the above or similar heuristics for quickly finding the ontological concept on which to base the meaning of a lexical entry fail, the fall-back procedure is to perform a descending traversal of the ontological hierarchy, the way it is done in ontology acquisition (see Section 9.2 above). Unfortunately, there is no guarantee that this procedure will yield an appropriate ontological concept, either for direct specification of meaning or as a possible parent for a new concept that would serve as the basis of the lexical meaning. Such an eventuality can be a clue that the meaning should be formulated in ways other than ontological, that is, parametrically (or, as in the case of
_comfortable_, as a hybrid of ontological and parametric representation means).


Thus, reopening the case of _abhor_, its parametric representation in (62) actually historically emerged in the Mikrokosmos implementation of ontological semantics after an earlier attempt to place it in the EVENT branch of the ontology failed: there were no concepts in it that were similar to it, due to the strategic decision not to represent states as EVENTs. As a result of that decision, the lexical entry for _like_ was represented parametrically, and the acquirer applied the semantic class membership rule to modify the meaning of _like_ to yield that of _abhor_ .


Recall that we deliberately referred to the ontological concepts for which we looked in the above step as “the basis of the specification of the lexical meaning.” The reason for this pedantic formulation is that, except in the simplest cases, such as that of _shirt_, the accurate specification of lexical meaning will require modifications to the fillers of properties in the concept, such as changing the filler of the THEME of BUY to accommodate the corporate sense of _acquire_ from OBJECT to


ORGANIZATION. Sometimes, support for such modifications comes from the lexicographic definitions available to the acquirer.


So far in this section, we have been discussing what amounts to elements of the microtheory of lexical semantics for most open-class lexical entities. There are many other words in the language that must be given a lexical description but whose meanings are not based on an ontological concept or property. Some of these words contribute grammatical information (see Sections 6.3 and
7.2) and often serve as triggers for such text analysis procedures as reference resolution (see Section 8.6.1). The format of the lexicon in ontological semantics licenses the specification of such items, as it does for phrasals and idioms. While in any practical application of ontological semantics, the coverage of such lexical elements is required (as is the capability to support any morphological and syntactic processing), it is not appropriate to describe here in detail the microtheories that deal with phenomena such as the above. The interested reader will find detailed instructions for the acquisition of all the static resources in ontological semantics, including all the types of lexical entities, in the tutorial part of the knowledge base acquisition editor component of the
CAMBIO/CREST application at http://messene.nmsu.edu:9009/.


The information on the acquisition of syntactic description and syntax-semantics linking in the lexical entries can also be found in the resource cited above. The example of _abhor_ (17) illustrates a typical distinction between the SYN and SYN-STRUC zones of the lexicon. In the former, _abhor_ is characterized just as a transitive verb, from which it follows that it takes a subject and a direct object. In the SYN-STRUC zone, however, the former does not need to be mentioned because it is not bound in the SEM-STRUC zone. In other words, the meaning of the subject of _abhor_ plays no role in the specification of its meaning.


## 9.4 Acquisition of Fact DB
Facts in the Fact DB are acquired to support a particular application, and the nature of the acquired facts is dictated by the application’s needs. Many of the facts provide the semantics for entries in the onomasticon (see Section 7.4 above). These are **named facts** . Facts that do not have a name property ( **unnamed facts** ) include those automatically derivable from TMRs as a side effect of the operation of an ontological semantic application. This capability has not yet been implemented in ontological semantics.


Acquisition of both named and unnamed facts can be carried out manually, by people taking specific concepts from the ontology and, on reading a text or several texts, filling an instance of this concept with information, in the metalanguage of ontological semantics and storing it in the Fact
DB. For example, a movie star’s career can be presented this way, as would be reports about company earnings. What is interesting about acquiring facts is the potential for automating a significant portion of the fact acquisition task. In the CAMBIO/CREST implementation of ontological semantics, the acquisition of Fact DB for the domain of sports has enjoyed significant levels of automation, while the acquisition of the ontology and the lexicon, though considerably automated, still, at the time of writing, contains an irreducible human component.


In the above implementation of ontological semantics, acquisition of facts was partially automated using automatic information extraction. The process has been as follows. First, the ontol


ogy was used to generate a set of extraction templates. In the sports domain, these included the templates based on the ontological concepts ATHLETE, NATION, SPORTS-RESULT and some others.
A large subset of properties from these concepts were selected for inclusion in the facts. Second, an information extraction program was used on the content of many Web pages devoted to the
Sydney Olympics to fill these templates with snippets of text. Third, people converted the text to expressions in the ontological metalanguage, as a result of which candidate facts were produced.
Finally, a combined automatic/human step of validation of the syntax (automatically) and content
(manually) of the newly acquired facts was carried out. The Fact DB in this implementation was used to support a question answering application.
