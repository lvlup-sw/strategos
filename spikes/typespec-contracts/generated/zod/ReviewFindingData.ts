import { z } from "zod"

export const ReviewFindingDataSchema = z.object({ "pr": z.number().int().gte(-2147483648).lte(2147483647), "source": z.union([z.literal("coderabbit"), z.literal("self-hosted")]), "severity": z.any(), "filePath": z.string(), "lineRange": z.array(z.number().int().gte(-2147483648).lte(2147483647)).optional(), "message": z.string(), "rule": z.string().optional() }).describe("Data payload for `review.finding` events.")
export type ReviewFindingDataSchema = z.infer<typeof ReviewFindingDataSchema>
