import { z } from "zod"

export const ShepherdIterationDataSchema = z.object({ "iteration": z.number().int().gte(-2147483648).lte(2147483647), "prsAssessed": z.number().int().gte(-2147483648).lte(2147483647), "fixesApplied": z.number().int().gte(-2147483648).lte(2147483647), "status": z.string() }).describe("Data payload for `shepherd.iteration` events.")
export type ShepherdIterationDataSchema = z.infer<typeof ShepherdIterationDataSchema>
