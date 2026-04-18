import { z } from "zod"

export const ReviewCompletedDataSchema = z.object({ "stage": z.union([z.literal("spec-review"), z.literal("quality-review"), z.literal("security-review")]), "verdict": z.any(), "findingsCount": z.number().int().gte(-2147483648).lte(2147483647), "summary": z.string() }).describe("Data payload for `review.completed` events.")
export type ReviewCompletedDataSchema = z.infer<typeof ReviewCompletedDataSchema>
