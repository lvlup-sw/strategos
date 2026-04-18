import { z } from "zod"

export const CheckpointDataSchema = z.object({ "counter": z.number().int().gte(-2147483648).lte(2147483647), "phase": z.string(), "featureId": z.string() }).describe("Data payload for `workflow.checkpoint` events.")
export type CheckpointDataSchema = z.infer<typeof CheckpointDataSchema>
