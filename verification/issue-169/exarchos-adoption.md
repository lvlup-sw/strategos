Tracking issue: [lvlup-sw/exarchos#1895](https://github.com/lvlup-sw/exarchos/issues/1895)

Upstream issue: [lvlup-sw/strategos#169](https://github.com/lvlup-sw/strategos/issues/169)

Program context:
[lvlup-sw/strategos#153](https://github.com/lvlup-sw/strategos/issues/153) and
[lvlup-sw/strategos#172](https://github.com/lvlup-sw/strategos/issues/172)

Evidence subject: Strategos product revision
`263cc5720818b13268214c5df84d7575fd74a6d7`, tree/fingerprint
`24be1d97dfaedd88bebc7f33dda831d9b36b1ba1`.

Dossier date: `2026-09-08`.

Current evidence status: `Unproven`.

The open adoption issue establishes cross-repository coordination and satisfies
the requirement to name the Exarchos consumer. Issue existence is not evidence
that Exarchos implemented, built, tested, or released the contract. No Exarchos
implementation PR, consumer-test result, released package digest, or protected
check is bound here to the Strategos product revision above.

Requested consumer work:

- upgrade the extracted Strategos Contracts schemas/models to 0.12.0;
- mirror the optional `compensation.inverseAction` object with required
  `domainName`, `objectTypeName`, and `actionName` when present;
- update the Strategos public-builder mirror for
  `IStepConfiguration<TState>.Compensate<TCompensation>(WorkflowActionReference)`
  and `CompensationConfiguration.InverseAction`;
- preserve the no-argument `Compensate<T>()` legacy surface and omitted wire
  field for runtime-only workflows;
- consume `AGWF044` and `AGWF045` in the closed diagnostic vocabulary;
- preserve ordinal ontology identities exactly and reject partial inverse
  objects.

Requested acceptance evidence:

- Zod/schema validation accepts omitted and fully populated inverse-action
  metadata but rejects partial or blank identities;
- API-mirror tests cover both `Compensate` overloads and the immutable
  compensation descriptor slot;
- a fixture round-trips `inverseAction` without rewriting ontology names;
- compatibility tests continue to accept legacy compensation with no
  `inverseAction`;
- a consumer fixture pinned to Strategos #169 covers one proved inverse, one
  `AGWF044` disagreement, and one `AGWF045` non-compensable scope;
- the implementing Exarchos PR links back to Strategos #169.

Until those artifacts exist and are bound to the shipped Strategos Contracts
0.12.0 package, downstream Exarchos adoption remains `Unproven`.
