Tracking issue: [lvlup-sw/basileus#495](https://github.com/lvlup-sw/basileus/issues/495)

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
the requirement to name the Basileus consumer. Issue existence is not evidence
that Basileus implemented, built, tested, or released the contract. No Basileus
implementation PR, consumer-test result, released package digest, or protected
check is bound here to the Strategos product revision above.

Requested consumer work:

- upgrade to the Strategos Contracts 0.12.0 schema/model package;
- project the optional `compensation.inverseAction` field with required
  `domainName`, `objectTypeName`, and `actionName` when typed compensation is
  authored;
- preserve all three ontology names exactly and never substitute CLR types;
- omit `inverseAction` for legacy runtime-only compensation;
- consume `AGWF044` and `AGWF045` wherever Basileus exposes Strategos
  diagnostics;
- treat a retained failed saga or unknown inverse outcome as reconciliation
  work rather than successful terminal rollback.

Requested acceptance evidence:

- exported workflow JSON round-trips a populated `ActionReferenceV1` without
  changing any ordinal name;
- legacy compensation JSON remains compatible and omits `inverseAction`;
- schema tests reject partial or blank inverse-action identities;
- closed-enum tests recognize `AGWF044` and `AGWF045` while rejecting unknown
  diagnostic tokens;
- a consumer fixture pinned to Strategos #169 proves one valid inverse pair and
  rejects one deliberately contradictory authored inverse;
- the implementing Basileus PR links back to Strategos #169.

Until those artifacts exist and are bound to the shipped Strategos Contracts
0.12.0 package, downstream Basileus adoption remains `Unproven`.
