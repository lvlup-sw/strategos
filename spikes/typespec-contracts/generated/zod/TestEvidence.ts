import { z } from "zod"

export const TestEvidenceSchema = z.object({ "type": z.union([z.literal("test"), z.literal("build"), z.literal("typecheck"), z.literal("manual")]), "output": z.string(), "passed": z.boolean() }).describe("Test evidence attached to task completion.")
export type TestEvidenceSchema = z.infer<typeof TestEvidenceSchema>
