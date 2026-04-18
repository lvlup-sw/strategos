import { z } from "zod"

export const ReviewVerdictSchema = z.enum(["pass","fail","blocked"]).describe("Review verdict outcomes.")
export type ReviewVerdictSchema = z.infer<typeof ReviewVerdictSchema>
