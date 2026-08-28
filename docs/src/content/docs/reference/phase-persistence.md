---
title: "Phase Enum Persistence"
description: "The generated Phase enum persists by name under System.Text.Json and by ordinal under Newtonsoft. A member reorder is a data migration if your Marten store uses Newtonsoft."
---

# Phase Enum Persistence

Every workflow definition compiles to a `Phase` enum that the generated saga stores on the Marten document. How that value is written depends on the serializer **your** `StoreOptions` use — Strategos does not configure the store and cannot detect which one you picked.

## What each serializer stores

| Serializer | Stored representation | Member reorder |
|------------|----------------------|----------------|
| System.Text.Json (Marten default) | Member **name** (`"ShipApprovedOrder"`) | Safe. Existing documents still load as the same phase. |
| Newtonsoft.Json (`UseNewtonsoftJson()`) | Member **ordinal** (`4`) by default | **Unsafe** unless the store opted into name storage. Existing ordinal documents silently load as whichever member now occupies that position. |

Under System.Text.Json the generator also emits `[JsonConverter(typeof(JsonStringEnumConverter))]` on the phase enum. Newtonsoft does not honor that attribute. A Newtonsoft store therefore writes the integer position **by default**. A store that configured Marten's `EnumStorage.AsString` (or added a Newtonsoft `StringEnumConverter`) writes names instead and is not the ordinal case.

## When this becomes a migration

2.11.0 restores document order to the generated step list. For fork and branch workflows that changes the **order of members** on the emitted `Phase` enum. Name-based documents are unaffected. Ordinal-based documents written before the upgrade denote a different phase after it.

If your Marten store uses Newtonsoft, treat any release that reorders the generated `Phase` enum — starting with 2.11.0 — as a data migration. Rewrite stored `Phase` values (or migrate the documents onto name-based serialization) before deploying the new build.

Strategos cannot warn you at startup: it never sees your `StoreOptions`.

## How to tell which representation you have

Inspect the raw saga document, not a deserialized object:

```sql
select jsonb_typeof(data->'Phase') as json_type,
       data->>'Phase'              as stored_phase
  from mt_doc_<yoursaga>
 limit 5;
```

- `json_type = 'string'` — stored by name. A reorder is not a migration.
- `json_type = 'number'` — stored by ordinal. Plan a rewrite before you upgrade.

A store that called `opts.UseNewtonsoftJson()` (or otherwise installed Newtonsoft as Marten's serializer) **and left enum storage at the default** is the ordinal case. Name storage under Newtonsoft is an explicit `EnumStorage.AsString` / `StringEnumConverter` opt-in, not the default.

## What Strategos does not do

- It does not set `StoreOptions.Serializer`.
- It does not emit a Newtonsoft `[JsonConverter]` on the phase enum.
- It does not detect or migrate existing documents.
