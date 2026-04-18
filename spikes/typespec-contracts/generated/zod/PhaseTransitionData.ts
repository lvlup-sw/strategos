import { z } from "zod"

export const PhaseTransitionDataSchema = z.object({ "from": z.string(), "to": z.string(), "trigger": z.string(), "featureId": z.string() }).describe("Data payload for `workflow.transition` events.")
export type PhaseTransitionDataSchema = z.infer<typeof PhaseTransitionDataSchema>
