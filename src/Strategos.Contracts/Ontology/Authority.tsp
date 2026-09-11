import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Wire projection of the authority product lattice (#165, #193 kernel).
 *
 * The lattice itself is CLR-only today — the `AuthorityLattice` type under
 * `Strategos.Ontology.Descriptors`, with its axes, descriptors and
 * requirement. Only the `x-strategos-authority` NAME string crosses the wire
 * today, so a consumer that
 * receives an action contract cannot tell whether one authority dominates
 * another. These models carry the lattice itself, so the dominance test is
 * decidable on the wire.
 *
 * Declarative-only (LB-1) and moniker-only (LB-2, INV-8): every member is a
 * string or a string map. No CLR `Type` handle is projected.
 */

/**
 * One axis-to-level pair.
 *
 * A coordinate is an ORDERED ARRAY of these pairs rather than a map, for one
 * reason: `contentHash` must be deterministic. A JSON object has no canonical
 * member order, so two producers serializing the same coordinate can emit two
 * byte sequences and therefore two digests. An array sorted ordinally by `axis`
 * has exactly one serialization. `OntologyGraphHasher` already sorts every
 * collection before hashing for the same reason.
 */
model AuthorityCoordinateV1 {
  /** The axis name. */
  @minLength(1)
  @pattern(".*\\S.*")
  axis: string;

  /** The required level on that axis. */
  @minLength(1)
  @pattern(".*\\S.*")
  level: string;
}

/**
 * One independent, totally ordered axis of the product lattice. Mirrors
 * `AuthorityAxisDescriptor(Name, Levels)`.
 */
model AuthorityAxisV1 {
  /** The axis name. */
  @minLength(1)
  @pattern(".*\\S.*")
  name: string;

  /**
   * The levels of this axis, ordered from WEAKEST to STRONGEST. The order is
   * significant: it is the total order the dominance test reads. A consumer
   * must not re-sort this list.
   */
  levels: string[];
}

/**
 * One named authority literal and its coordinate on each axis. Mirrors
 * `AuthorityDescriptor(Name, Coordinates, ExplicitImplications)`.
 */
model AuthorityDescriptorV1 {
  /** The authority literal (for example `position.reader`). */
  @minLength(1)
  @pattern(".*\\S.*")
  name: string;

  /**
   * The authority's level on each axis, ordered ordinally by `axis`. An axis
   * absent from this list means the authority places no requirement on it.
   */
  coordinates: AuthorityCoordinateV1[];

  /**
   * Authority literals this one implies outside the coordinate order. Empty
   * when the product order alone decides dominance.
   */
  explicitImplications: string[];
}

/** The product lattice for one ontology domain. Mirrors `AuthorityLattice`. */
model AuthorityLatticeV1 {
  /** The independent axes of the product order. */
  axes: AuthorityAxisV1[];

  /** Every authority literal declared against those axes. */
  authorities: AuthorityDescriptorV1[];
}

/**
 * The pointwise join of one or more named authorities — what an action or a
 * workflow step REQUIRES. Mirrors `AuthorityRequirement`.
 *
 * This is the wire form of the kernel's `tasks[].capabilities[]`. It is a
 * coordinate, not a name list, so a consumer can compare two requirements
 * without resolving either against a lattice.
 */
model AuthorityRequirementV1 {
  /**
   * The strongest required level on each axis, ordered ordinally by `axis`.
   *
   * At least one pair. A requirement that demands nothing is expressed by
   * OMITTING the optional `authority` field, so an empty array would be a second
   * spelling of the same fact. `AuthorityDescriptorV1.coordinates` is deliberately
   * NOT bounded this way: an authority literal that sits at the bottom of every
   * axis is meaningful, and its coordinate list is legitimately empty.
   */
  @minItems(1)
  coordinates: AuthorityCoordinateV1[];

  /**
   * The authority literals whose join produced this coordinate. Provenance
   * only — the coordinate above is the semantic identity.
   */
  sourceAuthorities?: string[];
}

/** One domain's lattice, named. The catalog carries several. */
model DomainAuthorityLatticeV1 {
  /** The ontology domain the lattice belongs to. */
  @minLength(1)
  @pattern(".*\\S.*")
  domainName: string;

  /** The lattice itself. */
  lattice: AuthorityLatticeV1;
}
